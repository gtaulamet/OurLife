using System.Diagnostics;
using System.Globalization;
using Godot;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Core.WorldGen;
using OurLife.Game.Content;
using OurLife.Game.Localization;
using OurLife.Game.Rendering;
using OurLife.Game.UI;
using OurLife.Localization;

namespace OurLife.Game.Bootstrap;

/// <summary>
/// Root node of the game. Loads content, texts and preferences, connects the localizer to
/// Godot and starts one of three modes: the normal game window, <c>--smoke</c> (headless checks
/// for agents and CI) or <c>--screenshot</c> (saves an image of the first screen and quits).
/// The first screen is the 3D view of a valley, or the language test screen with <c>--probe</c>.
/// </summary>
public partial class GameRoot : Node
{
    private const int ExitOk = 0;
    private const int ExitFailure = 1;
    private const int ExitBadArguments = 2;

    private const ulong DefaultSeed = 1;
    private const int DefaultSmokeTicks = 1_000;
    private const int FramesBeforeScreenshot = 3;

    private GameServices? _services;
    private GodotLocalizationBridge? _bridge;

    public override void _Ready()
    {
        // Any failure must quit with an exit code: an exception escaping _Ready would leave
        // --smoke and --screenshot running forever.
        try
        {
            Start();
        }
        catch (Exception ex) when (ex is DefValidationException or LocalizationException)
        {
            Fail($"The game content has problems:{System.Environment.NewLine}{ex.Message}", ExitFailure);
        }
        catch (Exception ex)
        {
            Fail($"The game could not start: {ex}", ExitFailure);
        }
    }

    private void Start()
    {
        CommandLineOptions options;
        try
        {
            options = CommandLineOptions.Parse(OS.GetCmdlineUserArgs());
        }
        catch (ArgumentException ex)
        {
            Fail($"Invalid command line: {ex.Message}", ExitBadArguments);
            return;
        }

        foreach (var argument in options.UnknownArguments)
        {
            GD.PushWarning($"Unknown command line argument ignored: {argument}");
        }

        var automated = options.Smoke || options.ScreenshotPath is not null;
        _services = GameServices.Load(new GodotContentSource(), automated ? Settings.InMemory() : Settings.Load());

        if (UiTheme.FontImportProblem() is { } fontProblem)
        {
            if (automated)
            {
                Fail(fontProblem, ExitFailure);
                return;
            }

            GD.PushWarning(fontProblem);
        }

        _bridge = new GodotLocalizationBridge(_services.Localizer, _services.Catalogs);

        if (options.Locale is { } locale)
        {
            if (!_services.Localizer.IsAvailable(locale))
            {
                Fail($"Unknown locale '{locale}'. Available: {string.Join(", ", _services.Localizer.Locales)}.", ExitBadArguments);
                return;
            }

            // Only for this session: the saved preference does not change.
            _services.Localizer.SetLocale(locale);
        }

        if (options.Smoke)
        {
            RunSmoke(_services, options);
            return;
        }

        if (options.Probe)
        {
            AddChild(LanguageProbeScreen.Create(_services));
        }
        else
        {
            // Until the main menu of H1.7, the game opens straight onto a generated valley.
            var simulation = new Simulation(options.Seed ?? DefaultSeed, _services.Defs);
            GenerateTheOnlyWorld(simulation);
            AddChild(WorldView.Create(simulation, LoadVisualCatalog(), options.Level));
        }

        if (options.ScreenshotPath is { } screenshotPath)
        {
            _ = SaveScreenshotAndQuitAsync(screenshotPath);
        }
    }

    public override void _ExitTree()
    {
        _bridge?.Dispose();
        _bridge = null;
    }

    /// <summary>
    /// Headless checks: content and texts load, Godot follows language changes, the world is
    /// generated and the simulation runs. Real-time measurement lives here, outside the
    /// deterministic core.
    /// </summary>
    private void RunSmoke(GameServices services, CommandLineOptions options)
    {
        try
        {
            var godotVersion = Engine.GetVersionInfo()["string"];
            GD.Print($"[smoke] Godot {godotVersion} | .NET {System.Environment.Version} | {OS.GetName()}");
            GD.Print($"[smoke] options: {options.Describe()}");
            GD.Print($"[smoke] content: {services.Defs.Count} defs | locales {string.Join(", ", services.Localizer.Locales)} | language {services.Localizer.CurrentLocale}");

            if (!CheckLiveLanguageSwitch(services))
            {
                GetTree().Quit(ExitFailure);
                return;
            }

            var catalog = LoadVisualCatalog();
            if (!CheckVisuals(services.Defs, catalog))
            {
                GetTree().Quit(ExitFailure);
                return;
            }

            var ticks = options.Ticks ?? DefaultSmokeTicks;
            var simulation = new Simulation(options.Seed ?? DefaultSeed, services.Defs);
            if (!GenerateWorld(simulation))
            {
                GetTree().Quit(ExitFailure);
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            simulation.Run(ticks);
            stopwatch.Stop();

            var totalMs = stopwatch.Elapsed.TotalMilliseconds;
            var msPerTick = ticks == 0 ? 0 : totalMs / ticks;
            GD.Print(string.Create(
                CultureInfo.InvariantCulture,
                $"[smoke] simulated {ticks} ticks in {totalMs:F1} ms ({msPerTick:F4} ms/tick)"));
            GD.Print($"[smoke] date {simulation.CurrentDate} | state {simulation.ComputeStateHash():X16}");
            GD.Print($"[smoke] date shown to the player: {services.Text.Date(simulation.CurrentDate)}, {services.Text.Clock(simulation.CurrentDate)}");

            BuildWorldView(simulation, catalog, options.Level);

            if (services.Localizer.MissingKeys.Count > 0)
            {
                Fail($"[smoke] missing texts: {string.Join(", ", services.Localizer.MissingKeys)}", ExitFailure);
                return;
            }

            GD.Print("[smoke] OK");
            GetTree().Quit(ExitOk);
        }
        catch (Exception ex)
        {
            Fail($"[smoke] FAILED: {ex}", ExitFailure);
        }
    }

    /// <summary>
    /// Builds the 3D view of the simulated world at the level of <c>--level</c>, or the highest
    /// surface, and frees it: terrain, plants and camera are created with every model the world
    /// uses, in its current era.
    /// </summary>
    private void BuildWorldView(Simulation simulation, VisualCatalog catalog, int? level)
    {
        var stopwatch = Stopwatch.StartNew();
        var view = WorldView.Create(simulation, catalog, level);
        AddChild(view);
        stopwatch.Stop();
        GD.Print(string.Create(
            CultureInfo.InvariantCulture,
            $"[smoke] world view built in {stopwatch.Elapsed.TotalMilliseconds:F0} ms at level {view.ViewLevel}"));
        view.QueueFree();
    }

    private static VisualCatalog LoadVisualCatalog() =>
        VisualCatalog.Parse(Godot.FileAccess.GetFileAsString(VisualCatalog.ResourcePath));

    /// <summary>
    /// The visual catalog loads, every def with a visual has a model, and every model of the
    /// catalog, era variants included, loads. A model that does not load usually means the
    /// project needs <c>--import</c>.
    /// </summary>
    private static bool CheckVisuals(DefDatabase defs, VisualCatalog catalog)
    {
        using var models = new ModelLibrary(catalog);
        var problems = new List<string>();
        foreach (var def in defs.AllDefs())
        {
            // The plain id must exist: it is the fallback of every era variant.
            if (def is IHasVisual { Visual: var visual } && !models.Catalog.TryResolve(visual, era: null, out _))
            {
                problems.Add($"'{visual}' is not in the visual catalog");
            }
        }

        foreach (var model in models.Catalog.Models)
        {
            if (models.Load(model) is null)
            {
                problems.Add($"{model.ModelPath} does not load; run godot --headless --path game --import");
            }
        }

        GD.Print($"[smoke] visuals: {models.Catalog.Models.Count()} models in the catalog");
        foreach (var problem in problems)
        {
            GD.PushError($"[smoke] {problem}");
        }

        return problems.Count == 0;
    }

    /// <summary>Generates the only WorldGen def of the content, until IP7 brings the choice of site.</summary>
    /// <exception cref="InvalidOperationException">The content has no WorldGen def, or more than one.</exception>
    private static WorldGenDef GenerateTheOnlyWorld(Simulation simulation)
    {
        var worlds = simulation.Content.All<WorldGenDef>();
        if (worlds.Count != 1)
        {
            throw new InvalidOperationException($"The content must have exactly one WorldGen def until IP7, found {worlds.Count}.");
        }

        WorldGenerator.Generate(simulation, worlds[0]);
        return worlds[0];
    }

    /// <summary>
    /// Generates the world and reports its size, the time it took, its plants and whether its
    /// whole surface is one region.
    /// </summary>
    private static bool GenerateWorld(Simulation simulation)
    {
        var stopwatch = Stopwatch.StartNew();
        var world = GenerateTheOnlyWorld(simulation);
        stopwatch.Stop();
        var map = simulation.Map;

        var plants = new List<EntityId>();
        simulation.World.Query<Plant>(plants);
        var plantCounts = plants
            .GroupBy(plant => simulation.World.Get<Plant>(plant).Def)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key} {group.Count()}");

        var surfaceRegions = Connectivity.Compute(map).SurfaceRegions(map);

        GD.Print(string.Create(
            CultureInfo.InvariantCulture,
            $"[smoke] world '{world.Id}' {map.Size.Width}x{map.Size.Height}x{map.Size.Depth} generated in {stopwatch.Elapsed.TotalMilliseconds:F0} ms"));
        GD.Print($"[smoke] plants: {string.Join(", ", plantCounts)} | surface regions: {surfaceRegions.Count}");
        if (surfaceRegions.Count != 1 || surfaceRegions[0] == Connectivity.NoRegion)
        {
            GD.PushError("[smoke] The surface of the world is not one connected region.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Switches through every language and checks that a translated control would show the
    /// localizer's text for it, and that each language shows something different.
    /// </summary>
    private static bool CheckLiveLanguageSwitch(GameServices services)
    {
        const string key = "ui.mainmenu.new_game";
        var original = services.Localizer.CurrentLocale;
        var shownTexts = new List<string>();

        foreach (var locale in services.Localizer.Locales)
        {
            services.Localizer.SetLocale(locale);
            var shown = GodotLocalizationBridge.Translate(key);
            GD.Print($"[smoke] {locale}: {key} -> \"{shown}\"");
            if (shown != services.Localizer.Format(key))
            {
                GD.PushError($"[smoke] Godot shows \"{shown}\" for {key} in '{locale}', the localizer says \"{services.Localizer.Format(key)}\".");
                return false;
            }

            shownTexts.Add(shown);
        }

        services.Localizer.SetLocale(original);
        if (shownTexts.Distinct().Count() != shownTexts.Count)
        {
            GD.PushError($"[smoke] Two languages show the same text for {key}.");
            return false;
        }

        return true;
    }

    private async Task SaveScreenshotAndQuitAsync(string path)
    {
        try
        {
            for (var frame = 0; frame < FramesBeforeScreenshot; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

            var fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var error = GetViewport().GetTexture().GetImage().SavePng(fullPath);
            if (error != Error.Ok)
            {
                Fail($"[screenshot] could not save {fullPath}: {error}", ExitFailure);
                return;
            }

            GD.Print($"[screenshot] {fullPath}");
            GetTree().Quit(ExitOk);
        }
        catch (Exception ex)
        {
            Fail($"[screenshot] FAILED: {ex}", ExitFailure);
        }
    }

    private void Fail(string message, int exitCode)
    {
        GD.PushError(message);
        GetTree().Quit(exitCode);
    }
}
