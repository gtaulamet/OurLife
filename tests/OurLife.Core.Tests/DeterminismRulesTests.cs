using System.Text.RegularExpressions;

namespace OurLife.Core.Tests;

/// <summary>
/// The simulation must be deterministic: same seed and same commands give the same result.
/// These source checks forbid APIs that bring hidden nondeterminism into OurLife.Core.
/// Iterating HashSet or Dictionary to make decisions is also forbidden, but that one is
/// enforced in code review because it cannot be detected reliably with a pattern.
/// </summary>
public class DeterminismRulesTests
{
    private static readonly (Regex Pattern, string Reason)[] ForbiddenApis =
    [
        (new Regex(@"\bnew\s+Random\s*\("), "use the simulation RNG streams"),
        (new Regex(@"\bSystem\.Random\b"), "use the simulation RNG streams"),
        (new Regex(@"\bRandom\.Shared\b"), "use the simulation RNG streams"),
        (new Regex(@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b"), "use the simulation clock"),
        (new Regex(@"\bGuid\.NewGuid\s*\("), "use deterministic ids"),
        (new Regex(@"\bEnvironment\.TickCount(64)?\b"), "use the simulation clock"),
        (new Regex(@"\bStopwatch\b"), "real time must not drive simulation decisions"),
        (new Regex(@"\bParallel\."), "parallel execution order is not deterministic"),
    ];

    [Fact]
    public void Core_source_does_not_use_nondeterministic_apis()
    {
        var files = EnumerateCoreSourceFiles().ToList();
        files.ShouldNotBeEmpty("the scan must find the Core source files");

        var violations = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var reason in FindViolations(lines[i]))
                {
                    var relative = Path.GetRelativePath(RepoPaths.Root, file);
                    violations.Add($"{relative}:{i + 1}: '{lines[i].Trim()}' ({reason})");
                }
            }
        }

        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [InlineData("var rng = new Random(42);")]
    [InlineData("var rng = new System.Random();")]
    [InlineData("var value = Random.Shared.Next();")]
    [InlineData("var now = DateTime.UtcNow;")]
    [InlineData("var id = Guid.NewGuid();")]
    [InlineData("var t = Environment.TickCount64;")]
    [InlineData("Parallel.For(0, 10, Work);")]
    public void Scanner_flags_forbidden_apis(string line)
    {
        FindViolations(line).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("var rng = new SimRandom(seed);")]
    [InlineData("// DateTime.Now is fine inside a comment")]
    [InlineData("var tick = clock.CurrentTick;")]
    public void Scanner_allows_deterministic_code(string line)
    {
        FindViolations(line).ShouldBeEmpty();
    }

    private static IEnumerable<string> FindViolations(string line)
    {
        var code = StripLineComment(line);
        return ForbiddenApis.Where(api => api.Pattern.IsMatch(code)).Select(api => api.Reason);
    }

    private static string StripLineComment(string line)
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index >= 0 ? line[..index] : line;
    }

    private static IEnumerable<string> EnumerateCoreSourceFiles()
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory
            .EnumerateFiles(RepoPaths.Combine("src", "OurLife.Core"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                        && !path.Contains($"{separator}bin{separator}", StringComparison.Ordinal));
    }
}
