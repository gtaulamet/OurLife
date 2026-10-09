using OurLife.Core.Sim;

namespace OurLife.Core.WorldGen;

/// <summary>
/// 2D gradient noise (Perlin's improved noise) with a permutation shuffled from a simulation
/// random stream. It only adds, multiplies and floors doubles, which IEEE 754 makes exact on every
/// machine, so the same stream always gives the same values. Written here instead of using
/// FastNoiseLite to keep Core free of third-party code.
/// </summary>
internal sealed class GradientNoise
{
    // Measured on this noise with the 3 octaves the generator uses: fractal values have a
    // standard deviation near 0.12. This factor brings it to about 0.55, close to a uniform spread.
    private const double SpreadFactor = 4.5;

    private readonly byte[] _permutation = new byte[512];

    public GradientNoise(Pcg32 random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var values = new byte[256];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (byte)i;
        }

        for (var i = values.Length - 1; i > 0; i--)
        {
            var j = random.NextInt(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }

        for (var i = 0; i < _permutation.Length; i++)
        {
            _permutation[i] = values[i & 255];
        }
    }

    /// <summary>One octave, roughly in [-1, 1]; 0 at every integer coordinate.</summary>
    public double Sample(double x, double y)
    {
        var floorX = Math.Floor(x);
        var floorY = Math.Floor(y);
        var xi = (int)floorX & 255;
        var yi = (int)floorY & 255;
        var dx = x - floorX;
        var dy = y - floorY;
        var u = Fade(dx);
        var v = Fade(dy);

        var a = _permutation[xi] + yi;
        var b = _permutation[xi + 1] + yi;
        var top = Lerp(Gradient(_permutation[a], dx, dy), Gradient(_permutation[b], dx - 1, dy), u);
        var bottom = Lerp(Gradient(_permutation[a + 1], dx, dy - 1), Gradient(_permutation[b + 1], dx - 1, dy - 1), u);
        return Math.Clamp(Lerp(top, bottom, v), -1.0, 1.0);
    }

    /// <summary>Sum of <paramref name="octaves"/> octaves, each twice as fine and half as strong, in [-1, 1].</summary>
    public double Fractal(double x, double y, int octaves)
    {
        double sum = 0, amplitude = 1, total = 0, frequency = 1;
        for (var octave = 0; octave < octaves; octave++)
        {
            sum += Sample(x * frequency, y * frequency) * amplitude;
            total += amplitude;
            amplitude *= 0.5;
            frequency *= 2;
        }

        return sum / total;
    }

    /// <summary>
    /// <see cref="Fractal"/> stretched so its values cover [-1, 1] fairly evenly: gradient noise
    /// rarely gets near its extremes, and thresholds such as a coverage would otherwise be off.
    /// </summary>
    public double Spread(double x, double y, int octaves) => Math.Clamp(Fractal(x, y, octaves) * SpreadFactor, -1.0, 1.0);

    /// <summary><see cref="Spread"/> mapped to [0, 1].</summary>
    public double Spread01(double x, double y, int octaves) => (Spread(x, y, octaves) + 1) * 0.5;

    private static double Fade(double t) => t * t * t * ((t * ((t * 6) - 15)) + 10);

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    private static double Gradient(int hash, double x, double y) => (hash & 7) switch
    {
        0 => x,
        1 => -x,
        2 => y,
        3 => -y,
        4 => (x + y) * 0.5,
        5 => (-x + y) * 0.5,
        6 => (x - y) * 0.5,
        _ => (-x - y) * 0.5,
    };
}
