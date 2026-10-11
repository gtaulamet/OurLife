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

    /// <summary>Number of simulation ticks to run in smoke mode (milestone H1.2), or at once at the start with <c>--play</c> (milestone H1.7).</summary>
    public int? Ticks { get; init; }

    /// <summary>Level Z the world view starts at, instead of the highest surface (milestone H1.5).</summary>
    public int? Level { get; init; }

    /// <summary>Show the language test screen of H1.3 instead of the world (milestone H1.5).</summary>
    public bool Probe { get; init; }

    /// <summary>Distance from the camera to what it looks at, to look closer in screenshots (milestone H1.7).</summary>
    public int? Zoom { get; init; }

    /// <summary>Start a game at once instead of showing the main menu (milestone H1.7).</summary>
    public bool Play { get; init; }

    /// <summary>Open the options: over the game with <c>--play</c>, else instead of the main menu (milestone H1.7).</summary>
    public bool Options { get; init; }

    /// <summary>Select the first colonist at the start, to see the inspector in screenshots (milestone H1.7).</summary>
    public bool Inspect { get; init; }

    /// <summary>Give the smoke's orders around the arrival at the start, to see them in screenshots (milestone H1.7).</summary>
    public bool Orders { get; init; }

    /// <summary>
    /// Play a game for this many seconds at the fastest speed, with the smoke's orders, then report
    /// frame times and ticks and quit (milestone H1.8). It needs a window.
    /// </summary>
    public int? BenchmarkSeconds { get; init; }

    /// <summary>
    /// Write the licenses of the engine and the runtime that ship with an exported game to this
    /// path and quit (milestone H1.8). The packaging script puts the file next to the game.
    /// </summary>
    public string? LicensesPath { get; init; }

    /// <summary>
    /// Folder of the saved games, instead of <c>user://saves</c> (milestone H2.1). Captures and
    /// benchmarks only save, and autosave, when it is given, so they never touch the player's games.
    /// </summary>
    public string? SavesPath { get; init; }

    /// <summary>Load the game of this slot instead of showing the main menu (milestone H2.1).</summary>
    public string? LoadSlot { get; init; }

    /// <summary>
    /// Open a screen for captures (milestone H2.1): <c>load</c> instead of the main menu,
    /// <c>save</c> over the game with <c>--play</c> or <c>--load</c>, or <c>error</c> with a damaged
    /// saved game.
    /// </summary>
    public string? Show { get; init; }

    /// <summary>The screens <c>--show</c> knows.</summary>
    public static readonly IReadOnlyList<string> Screens = ["load", "save", "error"];

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
                case "--level":
                    var level = NumberAfter<int>(args, ref i);
                    if (level < 0)
                    {
                        throw new ArgumentException("--level must be zero or positive.");
                    }

                    options = options with { Level = level };
                    break;
                case "--probe":
                    options = options with { Probe = true };
                    break;
                case "--play":
                    options = options with { Play = true };
                    break;
                case "--options":
                    options = options with { Options = true };
                    break;
                case "--inspect":
                    options = options with { Inspect = true };
                    break;
                case "--orders":
                    options = options with { Orders = true };
                    break;
                case "--zoom":
                    var zoom = NumberAfter<int>(args, ref i);
                    if (zoom <= 0)
                    {
                        throw new ArgumentException("--zoom must be positive.");
                    }

                    options = options with { Zoom = zoom };
                    break;
                case "--benchmark":
                    var seconds = NumberAfter<int>(args, ref i);
                    if (seconds <= 0)
                    {
                        throw new ArgumentException("--benchmark must be positive.");
                    }

                    options = options with { BenchmarkSeconds = seconds };
                    break;
                case "--licenses":
                    options = options with { LicensesPath = ValueAfter(args, ref i) };
                    break;
                case "--saves":
                    options = options with { SavesPath = ValueAfter(args, ref i) };
                    break;
                case "--load":
                    options = options with { LoadSlot = ValueAfter(args, ref i) };
                    break;
                case "--show":
                    var screen = ValueAfter(args, ref i);
                    if (!Screens.Contains(screen))
                    {
                        throw new ArgumentException($"--show must be one of: {string.Join(", ", Screens)}.");
                    }

                    options = options with { Show = screen };
                    break;
                default:
                    unknown.Add(args[i]);
                    break;
            }
        }

        // Each of these modes quits on its own: together, one would end before the other measured anything.
        if (options.BenchmarkSeconds is not null && (options.Smoke || options.ScreenshotPath is not null))
        {
            throw new ArgumentException("--benchmark cannot be combined with --smoke or --screenshot.");
        }

        return options with { UnknownArguments = unknown };
    }

    /// <summary>Short single-line description for logs.</summary>
    public string Describe() =>
        $"smoke={Smoke}, screenshot={ScreenshotPath ?? "-"}, locale={Locale ?? "-"}, " +
        $"seed={Seed?.ToString(CultureInfo.InvariantCulture) ?? "-"}, " +
        $"ticks={Ticks?.ToString(CultureInfo.InvariantCulture) ?? "-"}, " +
        $"level={Level?.ToString(CultureInfo.InvariantCulture) ?? "-"}, probe={Probe}, " +
        $"zoom={Zoom?.ToString(CultureInfo.InvariantCulture) ?? "-"}, inspect={Inspect}, orders={Orders}, play={Play}, options={Options}, " +
        $"benchmark={BenchmarkSeconds?.ToString(CultureInfo.InvariantCulture) ?? "-"}, licenses={LicensesPath ?? "-"}, " +
        $"saves={SavesPath ?? "-"}, load={LoadSlot ?? "-"}, show={Show ?? "-"}";

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
