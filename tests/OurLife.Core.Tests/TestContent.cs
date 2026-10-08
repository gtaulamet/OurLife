using OurLife.Core.Defs;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests;

/// <summary>
/// Content for tests, written as JSON and loaded through the real loader. The values mirror the
/// time settings of the game content, but tests never depend on the real content files.
/// </summary>
internal static class TestContent
{
    public const string TimeConfigJson = """
        {
          "type": "TimeConfig", "id": "test_time",
          "ticksPerSecond": 10, "minutesPerTick": 1, "daysPerSeason": 15,
          "seasons": ["spring", "summer", "autumn", "winter"],
          "startYear": 1200, "startHour": 6
        }
        """;

    public const string FirstEraJson = """{ "type": "Era", "id": "test_age", "order": 0 }""";

    private static readonly Lazy<DefDatabase> MinimalLazy =
        new(() => Load(("defs/base.json", $"[{TimeConfigJson}, {FirstEraJson}]")));

    /// <summary>Same time settings as <see cref="Minimal"/>, for tests that only need a calendar.</summary>
    public static TimeSettings Time => Minimal.Time;

    /// <summary>Just a time config and one era: enough to run a simulation.</summary>
    public static DefDatabase Minimal => MinimalLazy.Value;

    public static DefDatabase Load(params (string Path, string Json)[] files) =>
        DefLoader.Load(new InMemoryContentSource(files));

    /// <summary>Loads the minimal content plus extra defs given as a JSON array.</summary>
    public static DefDatabase WithDefs(string defsJsonArray) =>
        Load(("defs/base.json", $"[{TimeConfigJson}, {FirstEraJson}]"), ("defs/extra.json", defsJsonArray));
}

/// <summary>Content files held in memory, keyed by relative path.</summary>
internal sealed class InMemoryContentSource : IContentSource
{
    private readonly SortedDictionary<string, string> _files = new(StringComparer.Ordinal);

    public InMemoryContentSource(IEnumerable<(string Path, string Json)> files)
    {
        foreach (var (path, json) in files)
        {
            _files.Add(path, json);
        }
    }

    public IReadOnlyList<string> ListFiles(string directory, string extension) =>
        _files.Keys
            .Where(path => path.StartsWith(directory + "/", StringComparison.Ordinal)
                           && path.EndsWith(extension, StringComparison.Ordinal))
            .ToArray();

    public IReadOnlyList<string> ListDirectories(string directory) =>
        _files.Keys
            .Where(path => path.StartsWith(directory + "/", StringComparison.Ordinal))
            .Select(path => path[(directory.Length + 1)..])
            .Where(rest => rest.Contains('/'))
            .Select(rest => rest[..rest.IndexOf('/')])
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

    public string ReadText(string path) =>
        _files.TryGetValue(path, out var json) ? json : throw new FileNotFoundException(path);
}
