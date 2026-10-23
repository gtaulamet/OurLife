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

    /// <summary>
    /// Hold this season's look (<c>spring</c>, <c>summer</c>, <c>autumn</c> or <c>winter</c>) all
    /// year, to capture the four (milestone L4c, <c>docs/gdd/estaciones-visibles.md</c>).
    /// </summary>
    public string? Season { get; init; }

    /// <summary>Start a game at once instead of showing the main menu (milestone H1.7).</summary>
    public bool Play { get; init; }

    /// <summary>Open the options: over the game with <c>--play</c>, else instead of the main menu (milestone H1.7).</summary>
    public bool Options { get; init; }

    /// <summary>Select the first colonist at the start, to see the inspector in screenshots (milestone H1.7).</summary>
    public bool Inspect { get; init; }

    /// <summary>Select the first cell of the first stockpile, to see its filters in screenshots (milestone H2.3).</summary>
    public bool InspectStockpile { get; init; }

    /// <summary>Select the cell of the first blueprint, to see it in the inspector in screenshots (milestone H2.4).</summary>
    public bool InspectBlueprint { get; init; }

    /// <summary>Select the cell of the first piece of furniture, to see it in the inspector in screenshots (milestone H2.5).</summary>
    public bool InspectFurniture { get; init; }

    /// <summary>Open the colony panel, to see it in screenshots (milestone H2.6).</summary>
    public bool Colony { get; init; }

    /// <summary>Open the production panel, to see it in screenshots (milestone O4).</summary>
    public bool Production { get; init; }

    /// <summary>Open the knowledge panel, with something in its queue, to see it in screenshots (milestone O5).</summary>
    public bool Knowledge { get; init; }

    /// <summary>Turn the build tool on, to see the build bar in screenshots (milestone H2.4).</summary>
    public bool Build { get; init; }

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
    /// <c>save</c> over the game with <c>--play</c> or <c>--load</c>, <c>error</c> with a damaged
    /// saved game, or <c>newgame</c> (milestone H3.2).
    /// </summary>
    public string? Show { get; init; }

    /// <summary>
    /// Run a game of <c>--play</c> or <c>--load</c>, after <c>--ticks</c>, until this hour, for
    /// captures of the day and night cycle (milestone H2.2).
    /// </summary>
    public int? Hour { get; init; }

    /// <summary>
    /// Size of the interface for this session, in percent or <c>auto</c> (0), without saving it
    /// (milestone V2, <c>docs/gdd/tamano-de-la-interfaz.md</c>).
    /// </summary>
    public int? UiScale { get; init; }

    /// <summary>
    /// How much decoration dresses the grass for this session, without saving it: none, low,
    /// normal or high (<c>docs/gdd/decoracion-del-pasto.md</c>), to compare captures and benchmarks.
    /// </summary>
    public Rendering.DecorationLevel? Decoration { get; init; }

    /// <summary>Show the frames per second over the game for this session, without saving it (<c>docs/gdd/indicadores.md</c>).</summary>
    public bool Fps { get; init; }

    /// <summary>With <c>--play</c> or <c>--load</c>, open the sheet of the first colonist (<c>docs/gdd/vida-y-generaciones.md</c>).</summary>
    public bool Sheet { get; init; }

    /// <summary>
    /// Turn the build tool on with this piece of furniture, of its first material, to see its
    /// preview in screenshots (milestone V3, <c>docs/gdd/vista-previa-de-muebles.md</c>).
    /// </summary>
    public string? Furniture { get; init; }

    /// <summary>Quarter turns of the furniture of <c>--furniture</c>, from 0 to 3 (milestone V3).</summary>
    public int? Rotation { get; init; }

    /// <summary>Point at the surface of this column, x and y, as the mouse would, for screenshots (milestone V3).</summary>
    public (int X, int Y)? Point { get; init; }

    /// <summary>With <c>--point</c>, show a drag from that cell to this column, on the same level, without placing anything (milestone V3).</summary>
    public (int X, int Y)? DragTo { get; init; }

    /// <summary>The screens <c>--show</c> knows.</summary>
    public static readonly IReadOnlyList<string> Screens = ["load", "save", "error", "newgame"];

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
                case "--fps":
                    options = options with { Fps = true };
                    break;
                case "--sheet":
                    options = options with { Sheet = true };
                    break;
                case "--inspect-stockpile":
                    options = options with { InspectStockpile = true };
                    break;
                case "--inspect-blueprint":
                    options = options with { InspectBlueprint = true };
                    break;
                case "--inspect-furniture":
                    options = options with { InspectFurniture = true };
                    break;
                case "--knowledge":
                    options = options with { Knowledge = true };
                    break;
                case "--production":
                    options = options with { Production = true };
                    break;
                case "--colony":
                    options = options with { Colony = true };
                    break;
                case "--build":
                    options = options with { Build = true };
                    break;
                case "--orders":
                    options = options with { Orders = true };
                    break;
                case "--season":
                    var season = ValueAfter(args, ref i);
                    if (season is not ("spring" or "summer" or "autumn" or "winter"))
                    {
                        throw new ArgumentException("--season must be spring, summer, autumn or winter.");
                    }

                    options = options with { Season = season };
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
                case "--hour":
                    var hour = NumberAfter<int>(args, ref i);
                    if (hour is < 0 or > 23)
                    {
                        throw new ArgumentException("--hour must be from 0 to 23.");
                    }

                    options = options with { Hour = hour };
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
                case "--ui-scale":
                    var uiScaleText = ValueAfter(args, ref i);
                    var uiScale = uiScaleText == "auto" ? UI.UiScaleRules.Auto
                        : int.TryParse(uiScaleText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent) ? percent : -1;
                    if (!UI.UiScaleRules.IsValid(uiScale))
                    {
                        throw new ArgumentException($"--ui-scale must be auto or one of: {string.Join(", ", UI.UiScaleRules.Steps)}.");
                    }

                    options = options with { UiScale = uiScale };
                    break;
                case "--decoration":
                    var decorationName = ValueAfter(args, ref i);
                    options = options with
                    {
                        Decoration = Rendering.DecorationLevels.Parse(decorationName)
                            ?? throw new ArgumentException("--decoration must be one of: none, low, normal, high."),
                    };
                    break;
                case "--furniture":
                    options = options with { Furniture = ValueAfter(args, ref i) };
                    break;
                case "--rotation":
                    var rotation = NumberAfter<int>(args, ref i);
                    if (rotation is < 0 or > 3)
                    {
                        throw new ArgumentException("--rotation must be from 0 to 3.");
                    }

                    options = options with { Rotation = rotation };
                    break;
                case "--point":
                    options = options with { Point = ColumnAfter(args, ref i) };
                    break;
                case "--drag-to":
                    options = options with { DragTo = ColumnAfter(args, ref i) };
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

        if (options.Hour is not null && !options.Play && options.LoadSlot is null)
        {
            throw new ArgumentException("--hour needs --play or --load: it runs a game until that hour.");
        }

        if (options.DragTo is not null && options.Point is null)
        {
            throw new ArgumentException("--drag-to needs --point: the drag starts there.");
        }

        if (options.Rotation is not null && options.Furniture is null)
        {
            throw new ArgumentException("--rotation needs --furniture: it turns that piece.");
        }

        if ((options.Furniture is not null || options.Point is not null) && !options.Play && options.LoadSlot is null)
        {
            throw new ArgumentException("--furniture and --point need --play or --load: they act on a game.");
        }

        if (options.Show == "save" && !options.Play && options.LoadSlot is null)
        {
            throw new ArgumentException("--show save needs --play or --load: it opens over a game.");
        }

        return options with { UnknownArguments = unknown };
    }

    /// <summary>Short single-line description for logs.</summary>
    public string Describe() =>
        $"smoke={Smoke}, screenshot={ScreenshotPath ?? "-"}, locale={Locale ?? "-"}, " +
        $"seed={Seed?.ToString(CultureInfo.InvariantCulture) ?? "-"}, " +
        $"ticks={Ticks?.ToString(CultureInfo.InvariantCulture) ?? "-"}, " +
        $"level={Level?.ToString(CultureInfo.InvariantCulture) ?? "-"}, probe={Probe}, " +
        $"zoom={Zoom?.ToString(CultureInfo.InvariantCulture) ?? "-"}, inspect={Inspect}, inspect-stockpile={InspectStockpile}, inspect-blueprint={InspectBlueprint}, inspect-furniture={InspectFurniture}, colony={Colony}, production={Production}, knowledge={Knowledge}, build={Build}, orders={Orders}, play={Play}, options={Options}, " +
        $"benchmark={BenchmarkSeconds?.ToString(CultureInfo.InvariantCulture) ?? "-"}, licenses={LicensesPath ?? "-"}, " +
        $"saves={SavesPath ?? "-"}, load={LoadSlot ?? "-"}, show={Show ?? "-"}, " +
        $"hour={Hour?.ToString(CultureInfo.InvariantCulture) ?? "-"}, season={Season ?? "-"}, " +
        $"ui-scale={(UiScale is null ? "-" : UiScale == UI.UiScaleRules.Auto ? "auto" : UiScale.Value.ToString(CultureInfo.InvariantCulture))}, " +
        $"decoration={Decoration?.ToString().ToLowerInvariant() ?? "-"}, fps={Fps}, sheet={Sheet}, furniture={Furniture ?? "-"}, rotation={Rotation?.ToString(CultureInfo.InvariantCulture) ?? "-"}, " +
        $"point={(Point is { } point ? $"{point.X},{point.Y}" : "-")}, drag-to={(DragTo is { } drag ? $"{drag.X},{drag.Y}" : "-")}";

    private static string ValueAfter(IReadOnlyList<string> args, ref int i)
    {
        if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Missing value after {args[i]}.");
        }

        i++;
        return args[i];
    }

    /// <summary>A column of the map written as "x,y".</summary>
    private static (int X, int Y) ColumnAfter(IReadOnlyList<string> args, ref int i)
    {
        var name = args[i];
        var raw = ValueAfter(args, ref i);
        var parts = raw.Split(',');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
            || x < 0 || y < 0)
        {
            throw new ArgumentException($"{name} must be a column of the map as x,y: '{raw}'.");
        }

        return (x, y);
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
