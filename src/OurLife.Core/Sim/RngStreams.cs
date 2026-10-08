using System.Text;
using System.Text.RegularExpressions;

namespace OurLife.Core.Sim;

/// <summary>
/// Independent random streams derived from the world seed, one per system ("ai", "worldgen"…).
/// Each stream is seeded from the seed and a stable hash of its name, so adding a stream or
/// drawing from one never changes the numbers another stream produces.
/// </summary>
public sealed partial class RngStreams
{
    private readonly SortedDictionary<string, Pcg32> _streams = new(StringComparer.Ordinal);

    public RngStreams(ulong seed)
    {
        Seed = seed;
    }

    public ulong Seed { get; }

    /// <summary>Streams sorted by name, for state hashing and saving.</summary>
    internal IEnumerable<KeyValuePair<string, Pcg32>> Streams => _streams;

    /// <summary>Returns the stream with this name, creating it on first use.</summary>
    /// <param name="name">Lowercase letters, digits, dots and underscores, e.g. "ai" or "worldgen.trees".</param>
    public Pcg32 Get(string name)
    {
        if (!_streams.TryGetValue(name, out var stream))
        {
            if (name is null || !StreamNamePattern().IsMatch(name))
            {
                throw new ArgumentException(
                    $"Invalid random stream name '{name}'. Use lowercase letters, digits, dots and underscores.",
                    nameof(name));
            }

            stream = new Pcg32(Seed, StableHash(name));
            _streams.Add(name, stream);
        }

        return stream;
    }

    /// <summary>64-bit FNV-1a over the UTF-8 bytes: stable across runs, machines and .NET versions.</summary>
    private static ulong StableHash(string name)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        var hash = offsetBasis;
        foreach (var value in Encoding.UTF8.GetBytes(name))
        {
            hash ^= value;
            hash = unchecked(hash * prime);
        }

        return hash;
    }

    [GeneratedRegex("^[a-z0-9_.]+$")]
    private static partial Regex StreamNamePattern();
}
