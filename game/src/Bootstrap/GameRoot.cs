using System.Diagnostics;
using System.Globalization;
using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Save;
using OurLife.Core.Sim;
using OurLife.Game.Content;
using OurLife.Game.Localization;
using OurLife.Game.Rendering;
using OurLife.Game.Session;
using OurLife.Game.UI;
using OurLife.Localization;

namespace OurLife.Game.Bootstrap;

/// <summary>
/// Root node of the game. Loads content, texts and preferences, connects the localizer to
/// Godot and starts one of four modes: the normal game window, <c>--smoke</c> (headless checks
/// for agents and CI), <c>--screenshot</c> (saves an image of the first screen and quits) or
/// <c>--benchmark</c> (plays a while and checks frame and tick times).
/// The first screen is the main menu; <c>--play</c> starts a game at once, <c>--load</c> loads one,
/// <c>--options</c> opens the options, <c>--show</c> opens a screen of saved games and <c>--probe</c>
/// shows the language test screen. A game that cannot start shows why, instead of closing.
/// </summary>
public partial class GameRoot : Node
{
    private const int ExitOk = 0;
    private const int ExitFailure = 1;
    private const int ExitBadArguments = 2;

    private const ulong DefaultSeed = 1;
    private const int DefaultSmokeTicks = 1_000;
    private const int FramesBeforeScreenshot = 3;

    // Cells around the arrival where the smoke gives orders.
    private const int SmokeOrderRadius = 8;

    // Ticks the smoke runs after loading, to compare a loaded game with the one that never stopped.
    private const int SmokeTicksAfterLoad = 600;
    private const string SmokeSavesPath = "user://smoke-saves";
    private const string SmokeSlot = "smoke";

    private GameServices? _services;
    private GodotLocalizationBridge? _bridge;
    private VisualCatalog? _catalog;
    private Node? _screen;
    private SaveSlots? _slots;

    // A player is watching: failures show on screen instead of quitting with an exit code.
    private bool _interactive;

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
            FailToStart($"The game content has problems:{System.Environment.NewLine}{ex.Message}", ex.Message);
        }
        catch (Exception ex)
        {
            FailToStart($"The game could not start: {ex}", ex.Message);
        }
    }

    /// <summary>
    /// Logs the failure, then quits with an exit code, or, with a player watching, shows the error
    /// screen: in their language if the texts loaded, else as it is.
    /// </summary>
    private void FailToStart(string log, string detail)
    {
        if (!_interactive)
        {
            Fail(log, ExitFailure);
            return;
        }

        GD.PushError(log);
        ErrorScreen screen;
        if (_services is not null)
        {
            screen = ErrorScreen.Create(_services.Text.Format("ui.error.startup", ("detail", detail)), "ui.mainmenu.quit");
        }
        else
        {
            screen = TryStartupErrorInLanguage(detail) ?? ErrorScreen.CreateUntranslated(detail);
        }

        screen.Closed += () => GetTree().Quit(ExitFailure);
        ShowScreen(screen);
    }

    /// <summary>When only the defs failed, the texts may still load to say so in the player's language.</summary>
    private ErrorScreen? TryStartupErrorInLanguage(string detail)
    {
        try
        {
            var catalogs = LocaleCatalogs.Load(new GodotContentSource());
            var localizer = new Localizer(catalogs);
            var settings = Settings.Load();
            if (localizer.IsAvailable(settings.Locale))
            {
                localizer.SetLocale(settings.Locale);
            }

            _bridge = new GodotLocalizationBridge(localizer, catalogs);
            return ErrorScreen.Create(new TextResolver(localizer).Format("ui.error.startup", ("detail", detail)), "ui.mainmenu.quit");
        }
        catch (Exception ex)
        {
            // Already failing: anything that goes wrong here falls back to the untranslated screen.
            GD.PushError($"The texts could not load either: {ex.Message}");
            return null;
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

        if (options.LicensesPath is { } licensesPath)
        {
            var fullPath = Path.GetFullPath(licensesPath);
            File.WriteAllText(fullPath, LicenseNotices.Build());
            GD.Print($"[licenses] {fullPath}");
            GetTree().Quit(ExitOk);
            return;
        }

        // Without a window nothing is drawn, and the frame rate would mean nothing.
        if (options.BenchmarkSeconds is not null && DisplayServer.GetName() == "headless")
        {
            Fail("--benchmark needs a window: run it without --headless.", ExitBadArguments);
            return;
        }

        var automated = options.Smoke || options.ScreenshotPath is not null || options.BenchmarkSeconds is not null;
        _interactive = !automated;
        _services = GameServices.Load(new GodotContentSource(), automated ? Settings.InMemory() : Settings.Load());

        // Automated runs never touch the player's saved games, unless told where to save.
        _slots = options.SavesPath is { } savesPath
            ? new SaveSlots(Path.GetFullPath(ProjectSettings.GlobalizePath(savesPath)))
            : automated ? null : SaveSlots.ForPlayer();

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

        if (!automated)
        {
            _services.Settings.ApplyWindowMode();
        }

        if (options.Probe)
        {
            ShowScreen(LanguageProbeScreen.Create(_services));
        }
        else if (options.Play || options.LoadSlot is not null || options.BenchmarkSeconds is not null)
        {
            PlayFromCommandLine(options);
        }
        else if (options.Show == "load")
        {
            ShowLoadGame();
        }
        else if (options.Show == "error")
        {
            ShowLoadError(new SaveLoadException(SaveLoadError.Corrupt, "--show error"));
        }
        else if (options.Options)
        {
            ShowOptions();
        }
        else
        {
            ShowMainMenu();
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

    /// <summary>Replaces the screen on show: menus, the game, or the language test.</summary>
    private void ShowScreen(Node screen)
    {
        _screen?.QueueFree();
        _screen = screen;
        AddChild(screen);
    }

    private void ShowMainMenu()
    {
        var menu = MainMenu.Create(canLoad: _slots?.Any() ?? false);
        menu.NewGameRequested += ShowNewGame;
        menu.LoadGameRequested += ShowLoadGame;
        menu.OptionsRequested += ShowOptions;
        menu.QuitRequested += () => GetTree().Quit(ExitOk);
        ShowScreen(menu);
    }

    private void ShowNewGame()
    {
        var screen = NewGameScreen.Create(_services!);
        screen.StartRequested += (name, seed) => StartGame(name, seed, level: null, distance: null);
        screen.BackRequested += ShowMainMenu;
        ShowScreen(screen);
    }

    private void ShowLoadGame()
    {
        if (_slots is null)
        {
            Fail("--show load needs --saves in automated runs.", ExitBadArguments);
            return;
        }

        var screen = LoadGameScreen.Create(_services!, _slots);
        screen.LoadRequested += slotId => LoadGame(slotId);
        screen.BackRequested += ShowMainMenu;
        ShowScreen(screen);
    }

    /// <summary>Loads the game of a slot and shows it; if it cannot be loaded, shows why and returns null.</summary>
    private GameScreen? LoadGame(string slotId, int? level = null, int? distance = null)
    {
        SaveHeader header;
        Simulation simulation;
        try
        {
            (simulation, header) = _slots!.Load(slotId, _services!.Defs);
        }
        catch (SaveLoadException ex)
        {
            if (!_interactive)
            {
                Fail($"The saved game '{slotId}' cannot be loaded: {ex.Message}", ExitFailure);
                return null;
            }

            GD.PushWarning($"The saved game '{slotId}' cannot be loaded: {ex.Message}");
            ShowLoadError(ex);
            return null;
        }

        _catalog ??= LoadVisualCatalog();
        // The id names the autosave slot: one that cannot be a file name starts a new one.
        var gameId = SaveSlots.IsValidId(SaveSlots.AutosaveId(header.GameId)) && header.GameId.Length > 0
            ? header.GameId
            : SaveSlots.NewGameId();
        var screen = GameScreen.Create(_services, _catalog, _slots, gameId, simulation, level, distance);
        ShowGame(screen);
        return screen;
    }

    private void ShowLoadError(SaveLoadException problem)
    {
        var screen = ErrorScreen.Create(SlotTexts.Problem(_services!, problem), "ui.common.back");
        screen.Closed += () =>
        {
            if (_slots is not null)
            {
                ShowLoadGame();
            }
            else
            {
                ShowMainMenu();
            }
        };
        ShowScreen(screen);
    }

    private void ShowOptions()
    {
        var options = OptionsScreen.Create(_services!, dimmed: false);
        options.Closed += ShowMainMenu;
        ShowScreen(options);
    }

    private GameScreen StartGame(string colonyName, ulong seed, int? level, int? distance)
    {
        _catalog ??= LoadVisualCatalog();
        var screen = GameScreen.Create(_services!, _catalog, _slots, colonyName, seed, level, distance);
        ShowGame(screen);
        return screen;
    }

    private void ShowGame(GameScreen screen)
    {
        screen.MainMenuRequested += ShowMainMenu;
        ShowScreen(screen);
    }

    /// <summary>
    /// <c>--play</c>: a game straight away, for screenshots and tests, or <c>--load</c>: the game
    /// of a slot. <c>--orders</c> gives the smoke's orders, <c>--ticks</c> runs time at once,
    /// <c>--inspect</c> selects the first colonist, <c>--options</c> opens the options over the
    /// game and <c>--show save</c> the save screen. <c>--benchmark</c> plays the game with the
    /// orders and measures it.
    /// </summary>
    private void PlayFromCommandLine(CommandLineOptions options)
    {
        if (options.LoadSlot is not null && _slots is null)
        {
            Fail("--load needs --saves in automated runs.", ExitBadArguments);
            return;
        }

        var screen = options.LoadSlot is { } slotId
            ? LoadGame(slotId, options.Level, options.Zoom)
            : StartGame(_services!.Text.Format("ui.newgame.default_name"), options.Seed ?? DefaultSeed, options.Level, options.Zoom);
        if (screen is null)
        {
            return;
        }

        var simulation = screen.Session.Simulation;

        // A capture shows the same thing wherever the real mouse happens to be.
        if (options.ScreenshotPath is not null)
        {
            screen.Input.SetProcessUnhandledInput(false);
        }
        var colonists = new List<EntityId>();
        simulation.World.Query<Colonist>(colonists);
        // The benchmark measures a colony at work.
        if ((options.Orders || options.BenchmarkSeconds is not null) && colonists.Count > 0)
        {
            // Applied at once, like orders given while time is stopped, so their marks show even
            // with no --ticks or under --options.
            OrderWorkAround(simulation, simulation.World.Get<Position>(colonists[0]).Cell);
            simulation.ApplyCommandsNow();
        }

        screen.Advance(options.Ticks ?? 0);
        if (options.Inspect && colonists.Count > 0 && simulation.World.IsAlive(colonists[0]))
        {
            screen.Input.SelectColonist(colonists[0]);
        }

        if (options.Options)
        {
            screen.OpenOptions();
        }
        else if (options.Show == "save")
        {
            if (_slots is null)
            {
                Fail("--show save needs --saves in automated runs.", ExitBadArguments);
                return;
            }

            screen.OpenSaveScreen();
        }

        if (options.BenchmarkSeconds is { } seconds)
        {
            var benchmark = Benchmark.Create(screen, seconds);
            benchmark.Finished += code => GetTree().Quit(code);
            screen.AddChild(benchmark);
        }
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

            // The game screen of the smoke draws with the catalog just checked.
            _catalog = catalog;

            var ticks = options.Ticks ?? DefaultSmokeTicks;
            var simulation = new Simulation(options.Seed ?? DefaultSeed, services.Defs);
            if (!GenerateWorld(simulation))
            {
                GetTree().Quit(ExitFailure);
                return;
            }

            // The same two steps as GameScreen.NewGame, apart so the smoke can report each one.
            var colonists = GameScreen.StartColony(simulation, SmokeColonyName(services));
            var arrival = simulation.World.Get<Position>(colonists[0]).Cell;
            GD.Print($"[smoke] colony: {colonists.Count} colonists arrived at ({arrival.X}, {arrival.Y}, {arrival.Z})");
            var stairSite = OrderWorkAround(simulation, arrival);

            // Halfway, the game is saved and loaded, and the copy goes on beside it: both must
            // end alike. The game reported and drawn is the one that ran all its ticks.
            var half = ticks / 2;
            var stopwatch = Stopwatch.StartNew();
            simulation.Run(half);
            stopwatch.Stop();
            var loaded = SaveAndLoad(services, simulation);
            if (loaded is null)
            {
                GetTree().Quit(ExitFailure);
                return;
            }

            stopwatch.Start();
            simulation.Run(ticks - half);
            stopwatch.Stop();
            loaded.Run(ticks - half);
            if (loaded.ComputeStateHash() != simulation.ComputeStateHash())
            {
                Fail($"[smoke] saved at tick {half} and loaded, the game differs {ticks - half} ticks later from the one that never stopped", ExitFailure);
                return;
            }

            GD.Print($"[smoke] saved at tick {half} and loaded: same state, and the same {ticks - half} ticks later");

            // Nobody shows the events in smoke mode.
            simulation.Events.Clear();
            ReportColony(simulation, colonists, stairSite);

            var totalMs = stopwatch.Elapsed.TotalMilliseconds;
            var msPerTick = ticks == 0 ? 0 : totalMs / ticks;
            GD.Print(string.Create(
                CultureInfo.InvariantCulture,
                $"[smoke] simulated {ticks} ticks in {totalMs:F1} ms ({msPerTick:F4} ms/tick)"));
            GD.Print($"[smoke] date {simulation.CurrentDate} | state {simulation.ComputeStateHash():X16}");
            GD.Print($"[smoke] date shown to the player: {services.Text.Date(simulation.CurrentDate)}, {services.Text.Clock(simulation.CurrentDate)}");

            // Saved games go to a scratch folder, removed at the end.
            var slots = new SaveSlots(ProjectSettings.GlobalizePath(SmokeSavesPath));
            try
            {
                DeleteFolder(slots.Directory);
                _slots = slots;
                if (!CheckSlots(services, simulation, slots))
                {
                    GetTree().Quit(ExitFailure);
                    return;
                }

                BuildScreens(simulation, options, slots);
            }
            finally
            {
                _slots = null;
                DeleteFolder(slots.Directory);
            }

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
    /// The default village name in the default language: the name is part of the state, so the
    /// smoke's fingerprint must not depend on <c>--lang</c>.
    /// </summary>
    private static string SmokeColonyName(GameServices services)
    {
        var catalog = services.Localizer.Catalog(Localizer.DefaultLocale);
        return catalog.Format(catalog.Messages["ui.newgame.default_name"]);
    }

    /// <summary>
    /// The game saved and loaded in memory, with the colony systems, if it has the same
    /// fingerprint as the one saved (<c>docs/gdd/guardado.md</c>); else null, reported.
    /// </summary>
    private static Simulation? SaveAndLoad(GameServices services, Simulation simulation)
    {
        var stopwatch = Stopwatch.StartNew();
        var buffer = new MemoryStream();
        SaveGame.Write(buffer, simulation, new SaveInfo(SmokeSlot, IsAutosave: false, SaveSlots.GameVersion, DateTimeOffset.Now));
        var saved = stopwatch.Elapsed;
        var loaded = SaveGame.Load(new MemoryStream(buffer.ToArray()), services.Defs);
        ColonySystems.AddTo(loaded);
        stopwatch.Stop();
        GD.Print(string.Create(
            CultureInfo.InvariantCulture,
            $"[smoke] save: {buffer.Length / 1024} KB, saved in {saved.TotalMilliseconds:F0} ms, loaded in {(stopwatch.Elapsed - saved).TotalMilliseconds:F0} ms"));

        var expected = simulation.ComputeStateHash();
        if (loaded.ComputeStateHash() != expected)
        {
            GD.PushError($"[smoke] the loaded game differs from the saved one: {loaded.ComputeStateHash():X16} instead of {expected:X16}");
            return null;
        }

        return loaded;
    }

    /// <summary>A slot is written, listed and loaded with the same fingerprint.</summary>
    private static bool CheckSlots(GameServices services, Simulation simulation, SaveSlots slots)
    {
        slots.Write(SmokeSlot, simulation, SmokeSlot, isAutosave: false);
        var listed = slots.List();
        if (listed is not [{ Id: SmokeSlot, Header: { } header }] || header.ColonyName != simulation.ColonyName || header.Tick != simulation.CurrentTick)
        {
            GD.PushError($"[smoke] the slot written is not listed as it was saved: {string.Join(", ", listed.Select(slot => slot.Id))}");
            return false;
        }

        if (slots.Load(SmokeSlot, services.Defs).Simulation.ComputeStateHash() != simulation.ComputeStateHash())
        {
            GD.PushError("[smoke] the slot written loads a different game");
            return false;
        }

        GD.Print("[smoke] slot written, listed and loaded");
        return true;
    }

    private static void DeleteFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    /// <summary>
    /// Builds every screen without a window and frees them: the game screen (3D view, input and HUD,
    /// with a colonist picked by a click and shown in the inspector) for the simulation that ran, so the view draws a
    /// world with work done in it, the menus over it, and the main, new game and options screens.
    /// Texts formatted in code that do not exist are then in the localizer's missing keys, and the
    /// smoke fails. The pause menu has Save, and the save, load and error screens are built too.
    /// </summary>
    private void BuildScreens(Simulation simulation, CommandLineOptions options, SaveSlots slots)
    {
        var stopwatch = Stopwatch.StartNew();
        var screen = GameScreen.Create(_services!, _catalog!, slots, SmokeSlot, simulation, options.Level);
        ShowGame(screen);
        stopwatch.Stop();
        GD.Print(string.Create(
            CultureInfo.InvariantCulture,
            $"[smoke] game screen built in {stopwatch.Elapsed.TotalMilliseconds:F0} ms at level {screen.View.ViewLevel}"));

        // A click on a colonist's chest selects it, with the camera's real slant. A colonist that
        // the terrain hides from the camera cannot be clicked, so any drawn one will do. A long
        // smoke can end with the colony lost: then there is nobody to click.
        var colonists = new List<EntityId>();
        screen.Session.Simulation.World.Query<Colonist>(colonists);
        var drawn = colonists.Where(colonist => screen.View.DrawnBounds(colonist) is not null).ToList();
        foreach (var colonist in drawn)
        {
            screen.Input.ClickAt(screen.View.Camera.Camera.UnprojectPosition(screen.View.DrawnBounds(colonist)!.Value.GetCenter()));
            if (screen.Input.Selected is { IsColonist: true })
            {
                break;
            }
        }

        if (drawn.Count > 0)
        {
            if (screen.Input.Selected is not { IsColonist: true })
            {
                throw new InvalidOperationException($"A click on the chest of each of the {drawn.Count} drawn colonists selected none.");
            }

            GD.Print("[smoke] a click on a colonist selects it");
        }

        screen.Advance(10);
        screen.OpenPauseMenu();
        screen.OpenOptions();
        screen.OpenSaveScreen();

        ShowNewGame();
        ShowOptions();
        ShowLoadGame();
        ShowLoadError(new SaveLoadException(SaveLoadError.MissingContent, "smoke_def"));
        ShowMainMenu();
        GD.Print("[smoke] menus built: main, new game, options, pause, save, load, error");
        _screen?.QueueFree();
        _screen = null;
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

    /// <summary>
    /// Gives the colonists every kind of order around their arrival, so the smoke exercises every
    /// job and measures the ticks with work going on: chop and gather nearby, dig the hills, and
    /// stairs down where the ground allows them.
    /// </summary>
    private static TileCoord? OrderWorkAround(Simulation simulation, TileCoord arrival)
    {
        TileCoord Offset(int dx, int dy, int dz) => new(arrival.X + dx, arrival.Y + dy, arrival.Z + dz);
        simulation.Commands.Enqueue(new DesignateCommand(DesignationKind.Chop, Offset(-SmokeOrderRadius, -SmokeOrderRadius, -3), Offset(SmokeOrderRadius, SmokeOrderRadius, 3)));
        simulation.Commands.Enqueue(new DesignateCommand(DesignationKind.Harvest, Offset(-SmokeOrderRadius, -SmokeOrderRadius, -3), Offset(SmokeOrderRadius, SmokeOrderRadius, 3)));
        simulation.Commands.Enqueue(new DesignateCommand(DesignationKind.Mine, Offset(-SmokeOrderRadius, -SmokeOrderRadius, 0), Offset(SmokeOrderRadius, SmokeOrderRadius, 3)));
        var site = FindStairSite(simulation, arrival);
        if (site is { } stairs)
        {
            simulation.Commands.Enqueue(new DesignateCommand(DesignationKind.Stairs, stairs, stairs));
        }
        else
        {
            GD.PushWarning("[smoke] No cell near the arrival can take stairs; the smoke does not dig any.");
        }

        return site;
    }

    /// <summary>
    /// The cell nearest the arrival, on its level and outside its pile of provisions, where stairs
    /// down can be dug: a floor without a plant over rock or soil. Ties go to the lower cell index.
    /// </summary>
    private static TileCoord? FindStairSite(Simulation simulation, TileCoord arrival)
    {
        var plantCells = Designations.PlantCells(simulation);
        TileCoord? best = null;
        var bestDistance = int.MaxValue;
        for (var dy = -SmokeOrderRadius; dy <= SmokeOrderRadius; dy++)
        {
            for (var dx = -SmokeOrderRadius; dx <= SmokeOrderRadius; dx++)
            {
                var cell = new TileCoord(arrival.X + dx, arrival.Y + dy, arrival.Z);
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                if (distance == 0 || distance >= bestDistance || !simulation.Map.Size.Contains(cell.X, cell.Y, cell.Z)
                    || !Designations.CanOrderStairs(simulation.Map, cell, plantCells))
                {
                    continue;
                }

                best = cell;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>How many colonists live, what each one is doing and how hungry and rested they are.</summary>
    private static void ReportColony(Simulation simulation, IReadOnlyList<EntityId> colonists, TileCoord? stairSite)
    {
        var world = simulation.World;
        var alive = colonists.Where(world.IsAlive).ToList();
        GD.Print($"[smoke] colony: {alive.Count} of {colonists.Count} colonists alive");

        var piles = new List<EntityId>();
        world.Query<ItemStack>(piles);
        var items = piles
            .Select(world.Get<ItemStack>)
            .GroupBy(stack => stack.Def)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key} {group.Sum(stack => stack.Count)}");
        GD.Print($"[smoke] colony: items on the ground: {string.Join(", ", items)}");

        var map = simulation.Map;
        int mineOrders = 0, stairOrders = 0;
        foreach (var index in map.OrderedCells)
        {
            var flags = map.FlagsAt(map.Size.CoordOf(index));
            mineOrders += (flags & TileFlags.MineDesignation) != 0 ? 1 : 0;
            stairOrders += (flags & TileFlags.StairsDesignation) != 0 ? 1 : 0;
        }

        var stairs = stairSite is { } site
            ? $"stairs at ({site.X}, {site.Y}, {site.Z}): {map.ShapeAt(site)} over {map.ShapeAt(site.X, site.Y, site.Z - 1)}"
            : "no stairs ordered";
        GD.Print($"[smoke] colony: orders left: mine {mineOrders}, stairs {stairOrders} | {stairs}");
        foreach (var colonist in alive)
        {
            var job = world.TryGet<Job>(colonist, out var current) ? JobKinds.Id(current.Kind) : "idle";
            var needs = string.Join(", ", world.Get<Needs>(colonist).Values.Select(need => $"{need.Need} {need.Value}"));
            GD.Print($"[smoke]   {world.Get<Colonist>(colonist).Name}: {job} | {needs}");
        }
    }

    /// <summary>
    /// Generates the world and reports its size, the time it took, its plants and whether its
    /// whole surface is one region.
    /// </summary>
    private static bool GenerateWorld(Simulation simulation)
    {
        var stopwatch = Stopwatch.StartNew();
        var world = GameScreen.GenerateTheOnlyWorld(simulation);
        stopwatch.Stop();
        var map = simulation.Map;

        var plants = new List<EntityId>();
        simulation.World.Query<Plant>(plants);
        var plantCounts = plants
            .GroupBy(plant => simulation.World.Get<Plant>(plant).Def)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key} {group.Count()}");

        // What a full computation costs. The generator pays it once; later changes that only open
        // ways, like digging or chopping, merge regions in place, and only one that closes a way
        // computes everything again.
        var connectivityTime = Stopwatch.StartNew();
        var surfaceRegions = Connectivity.Compute(map).SurfaceRegions(map);
        connectivityTime.Stop();

        GD.Print(string.Create(
            CultureInfo.InvariantCulture,
            $"[smoke] world '{world.Id}' {map.Size.Width}x{map.Size.Height}x{map.Size.Depth} generated in {stopwatch.Elapsed.TotalMilliseconds:F0} ms"));
        GD.Print(string.Create(
            CultureInfo.InvariantCulture,
            $"[smoke] plants: {string.Join(", ", plantCounts)} | surface regions: {surfaceRegions.Count} in {connectivityTime.Elapsed.TotalMilliseconds:F1} ms"));
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
