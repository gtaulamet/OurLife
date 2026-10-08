using System.Numerics;

namespace OurLife.Core.Sim;

/// <summary>
/// PCG32 (XSH-RR) pseudo-random generator by Melissa O'Neill (pcg-random.org). It is small,
/// fast and statistically solid, and its whole state is two 64-bit words, which makes it
/// easy to save, restore and hash. Get instances from <see cref="RngStreams"/>.
/// </summary>
public sealed class Pcg32
{
    private const ulong Multiplier = 6364136223846793005UL;

    private ulong _state;
    private ulong _increment;

    /// <summary>Seeds the generator like the reference <c>pcg32_srandom_r(seed, sequence)</c>.</summary>
    public Pcg32(ulong seed, ulong sequence)
    {
        _state = 0;
        _increment = (sequence << 1) | 1UL;
        NextUInt();
        _state = unchecked(_state + seed);
        NextUInt();
    }

    private Pcg32()
    {
    }

    public ulong State => _state;

    /// <summary>Stream selector; always odd.</summary>
    public ulong Increment => _increment;

    /// <summary>Recreates a generator from a saved <see cref="State"/> and <see cref="Increment"/>.</summary>
    public static Pcg32 FromState(ulong state, ulong increment)
    {
        if ((increment & 1UL) == 0)
        {
            throw new ArgumentException("The increment of a PCG32 generator is always odd.", nameof(increment));
        }

        return new Pcg32 { _state = state, _increment = increment };
    }

    public uint NextUInt()
    {
        var old = _state;
        _state = unchecked((old * Multiplier) + _increment);
        var xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        var rotation = (int)(old >> 59);
        return BitOperations.RotateRight(xorShifted, rotation);
    }

    /// <summary>Uniform integer in [0, <paramref name="bound"/>), without modulo bias.</summary>
    public uint NextUInt(uint bound)
    {
        if (bound == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bound), "The bound must be positive.");
        }

        var threshold = unchecked(0u - bound) % bound;
        while (true)
        {
            var value = NextUInt();
            if (value >= threshold)
            {
                return value % bound;
            }
        }
    }

    /// <summary>Uniform integer in [0, <paramref name="maxExclusive"/>).</summary>
    public int NextInt(int maxExclusive) => NextInt(0, maxExclusive);

    /// <summary>Uniform integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive), $"The range [{minInclusive}, {maxExclusive}) is empty.");
        }

        var range = (uint)((long)maxExclusive - minInclusive);
        return (int)(minInclusive + (long)NextUInt(range));
    }

    public bool NextBool() => (NextUInt() >> 31) != 0;

    /// <summary>
    /// True with a probability of <paramref name="basisPoints"/> in 10 000. Always consumes one
    /// number, so tuning a probability does not shift the rest of the stream.
    /// </summary>
    public bool Chance(int basisPoints) => NextUInt(10_000) < (uint)Math.Clamp(basisPoints, 0, 10_000);

    /// <summary>Uniform value in [0, 1) with 32 bits of resolution. Meant for world generation.</summary>
    public double NextDouble() => NextUInt() * (1.0 / 4_294_967_296.0);
}
