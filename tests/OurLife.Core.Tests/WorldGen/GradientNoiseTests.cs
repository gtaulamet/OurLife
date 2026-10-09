using OurLife.Core.Sim;
using OurLife.Core.WorldGen;

namespace OurLife.Core.Tests.WorldGen;

public class GradientNoiseTests
{
    [Fact]
    public void The_same_stream_gives_the_same_noise()
    {
        var first = new GradientNoise(new Pcg32(42, 1));
        var second = new GradientNoise(new Pcg32(42, 1));

        for (var i = 0; i < 100; i++)
        {
            second.Fractal(i * 0.37, i * 0.21, 3).ShouldBe(first.Fractal(i * 0.37, i * 0.21, 3));
        }
    }

    [Fact]
    public void Spread_values_cover_their_range_fairly_evenly()
    {
        var noise = new GradientNoise(new Pcg32(7, 3));
        var buckets = new int[5];
        const int side = 200;

        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var value = noise.Spread01(x / 8.0, y / 8.0, 3);
                value.ShouldBeInRange(0.0, 1.0);
                buckets[Math.Min(4, (int)(value * 5))]++;
            }
        }

        // Each fifth of the range should hold a fair share of the samples.
        foreach (var bucket in buckets)
        {
            ((double)bucket / (side * side)).ShouldBeInRange(0.08, 0.35);
        }
    }
}
