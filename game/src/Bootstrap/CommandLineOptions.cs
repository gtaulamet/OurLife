using System.Globalization;
using System.Numerics;

namespace OurLife.Game.Bootstrap;

/// <summary>
/// Arguments given to the game after "--" on the Godot command line, for example
/// <c>godot --headless --path game -- --smoke --seed 42 --ticks 5000</c>.
/// Agents and CI use them to run and verify the game without human input.
/// </summary>
public sealed record CommandLineOptions
{
    /// <summary>Boot, validate and quit with exit code 0 (success) or non-zero (failure).</summary>
    public bool Smoke { get; init; }

    /// <summary>Save a screenshot to this path and quit (milestone H1.5).</summary>
    public string? ScreenshotPath { get; init; }

    /// <summary>Force the UI language, "es" or "en" (milestone H1.3).</summary>
    public string? Locale { get; init; }

    /// <summary>World seed (milestone H1.4).</summary>
    public ulong? Seed { get; init; }

    /// <summary>Number of simulation ticks to run in smoke mode (milestone H1.2).</summary>
    public int? Ticks { get; init; }

    /// <summary>Arguments that were not recognized; the game warns and ignores them.</summary>
    public IReadOnlyList<string> UnknownArguments { get; init; } = [];

    /// <exception cref="ArgumentException">A value is missing or is not a valid number.</exception>
    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        var options = new CommandLineOptions();
        var unknown = new List<string>();

        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--smoke":
                    options = options with { Smoke = true };
                    break;
                case "--screenshot":
                    options = options with { ScreenshotPath = ValueAfter(args, ref i) };
                    break;
                case "--locale":
                    options = options with { Locale = ValueAfter(args, ref i) };
                    break;
                case "--seed":
                    options = options with { Seed = NumberAfter<ulong>(args, ref i) };
                    break;
                case "--ticks":
                    var ticks = NumberAfter<int>(args, ref i);
                    if (ticks < 0)
                    {
                        throw new ArgumentException("--ticks must be zero or positive.");
                    }

                    options = options with { Ticks = ticks };
                    break;
                default:
                    unknown.Add(args[i]);
                    break;
            }
        }

        return options with { UnknownArguments = unknown };
    }

    /// <summary>Short single-line description for logs.</summary>
    public string Describe() =>
        $"smoke={Smoke}, screenshot={ScreenshotPath ?? "-"}, locale={Locale ?? "-"}, " +
        $"seed={Seed?.ToString(CultureInfo.InvariantCulture) ?? "-"}, " +
        $"ticks={Ticks?.ToString(CultureInfo.InvariantCulture) ?? "-"}";

    private static string ValueAfter(IReadOnlyList<string> args, ref int i)
    {
        if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Missing value after {args[i]}.");
        }

        i++;
        return args[i];
    }

    private static T NumberAfter<T>(IReadOnlyList<string> args, ref int i)
        where T : INumber<T>
    {
        var name = args[i];
        var raw = ValueAfter(args, ref i);
        if (!T.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new ArgumentException($"Invalid number for {name}: '{raw}'.");
        }

        return value;
    }
}
