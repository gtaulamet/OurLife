using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Save;
using OurLife.Core.Sim;
using OurLife.Core.Text;
using OurLife.Game.Content;
using OurLife.Game.Input;
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

    // The settings of the startup error screen, shown before there are services.
    private Settings? _errorSettings;

    // Whether the interface size follows the window: a start that fails early never subscribes.
    private bool _followsWindow;
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
            _errorSettings = settings;
            ApplyUiScale();
            FollowWindow();
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

        Rendering.SeasonCycle.Forced = options.Season;

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
        if (options.UiScale is { } uiScale)
        {
            _services.Settings.SetUiScale(uiScale, save: false);
        }

        if (options.Decoration is { } decoration)
        {
            _services.Settings.SetDecoration(decoration, save: false);
        }

        if (options.Fps)
        {
            _services.Settings.SetShowFps(true, save: false);
        }

        // The interface size follows the setting and the size of the window (see docs/gdd/tamano-de-la-interfaz.md).
        _services.Settings.ApplyUiScale(GetTree().Root);
        _services.Settings.UiScaleChanged += ApplyUiScale;
        FollowWindow();

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
        else if (options.Show == "newgame")
        {
            ShowNewGame();
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
        if (_services is not null)
        {
            _services.Settings.UiScaleChanged -= ApplyUiScale;
        }

        if (_followsWindow)
        {
            GetTree().Root.SizeChanged -= ApplyUiScale;
            _followsWindow = false;
        }

        _bridge?.Dispose();
        _bridge = null;
    }

    /// <summary>Applies the interface size again whenever the window changes size; once.</summary>
    private void FollowWindow()
    {
        if (!_followsWindow)
        {
            GetTree().Root.SizeChanged += ApplyUiScale;
            _followsWindow = true;
        }
    }

    /// <summary>Draws the interface at the size of the settings, or of the startup error's settings before there are services.</summary>
    private void ApplyUiScale() => (_services?.Settings ?? _errorSettings)?.ApplyUiScale(GetTree().Root);

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
        screen.StartRequested += (name, seed, lifePace) => StartGame(name, seed, lifePace, level: null, distance: null);
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

    private GameScreen StartGame(string colonyName, ulong seed, string? lifePace, int? level, int? distance)
    {
        _catalog ??= LoadVisualCatalog();
        var screen = GameScreen.Create(_services!, _catalog, _slots, colonyName, seed, lifePace, level, distance);
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
    /// <c>--hour</c> runs on until that hour, <c>--inspect</c> selects the first colonist,
    /// <c>--options</c> opens the options over the game and <c>--show save</c> the save screen. <c>--benchmark</c> plays the game with the
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
            : StartGame(_services!.Text.Format("ui.newgame.default_name"), options.Seed ?? DefaultSeed, lifePace: null, options.Level, options.Zoom);
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

        // A bedroom and a dining room, and a bed for the first colonist's family, before time runs so the colonists use them.
        RoomScene.Built? rooms = null;
        if (options.Rooms && colonists.Count > 0 && simulation.World.IsAlive(colonists[0]))
        {
            rooms = RoomScene.Build(simulation, simulation.World.Get<Position>(colonists[0]).Cell);
            screen.Session.Enqueue(new SetFreeWillCommand(true));
            simulation.ApplyCommandsNow();
            if (Rooms.At(simulation, rooms.Value.BedroomCell) is { } bedroom)
            {
                screen.Hud.Rooms.AssignFamily(bedroom.Id, colonists[0]);
                simulation.ApplyCommandsNow();
            }
        }

        screen.Advance(options.Ticks ?? 0);
        if (options.Hour is { } hour)
        {
            // The first tick at or after that hour; within the next day.
            var date = simulation.CurrentDate;
            var minutes = ((hour * 60) - ((date.Hour * 60) + date.Minute) + TimeSettings.MinutesPerDay) % TimeSettings.MinutesPerDay;
            var perTick = simulation.Time.MinutesPerTick;
            screen.Advance((minutes + perTick - 1) / perTick);
        }

        if (options.Metals && colonists.Count > 0 && simulation.World.IsAlive(colonists[0]))
        {
            PlaceMetals(screen, colonists[0]);
        }

        if (rooms is { } built)
        {
            // The list opens with the layer on, and choosing a room moves the camera, selects it and shows its beds.
            foreach (var colonist in colonists.Where(simulation.World.IsAlive))
            {
                var at = simulation.World.Get<Position>(colonist).Cell;
                var doing = simulation.World.TryGet<Job>(colonist, out var job) ? job.Kind.ToString() : "-";
                GD.Print($"[rooms] {simulation.World.Get<Colonist>(colonist).Name} at ({at.X}, {at.Y}, {at.Z}): {doing}");
            }

            if (options.RoomsDining)
            {
                var seated = RoomScene.Seat(simulation, colonists.Where(simulation.World.IsAlive).ToList(), built.DiningCell, ticks: 600);
                GD.Print($"[rooms] {seated} colonists sit in the dining room");
                screen.Advance(0);
            }

            screen.Hud.Rooms.Toggle();
            var chosen = Rooms.At(simulation, options.RoomsDining ? built.DiningCell : built.BedroomCell);
            if (chosen is not null)
            {
                screen.Hud.Rooms.Choose(chosen);
            }
        }

        if ((options.Inspect || options.Sheet) && colonists.Count > 0 && simulation.World.IsAlive(colonists[0]))
        {
            screen.Input.SelectColonist(colonists[0]);
        }

        if (options.Sheet && colonists.Count > 0 && simulation.World.IsAlive(colonists[0]))
        {
            screen.Hud.ToggleSheet(colonists[0]);
        }

        var zones = new List<EntityId>();
        simulation.World.Query<Stockpile>(zones);
        if (options.InspectStockpile && zones.Count > 0)
        {
            screen.Input.SelectCell(simulation.World.Get<Stockpile>(zones[0]).Cells[0]);
        }

        var blueprints = new List<EntityId>();
        simulation.World.Query<Blueprint, Position>(blueprints);
        if (options.InspectBlueprint && blueprints.Count > 0)
        {
            screen.Input.SelectCell(simulation.World.Get<Position>(blueprints[0]).Cell);
        }

        var pieces = new List<EntityId>();
        simulation.World.Query<Furniture, Position>(pieces);
        if (options.InspectFurniture && pieces.Count > 0)
        {
            // A workshop first, to see its recipes and orders (milestone O3).
            var chosen = pieces.FirstOrDefault(piece => simulation.World.Has<Workshop>(piece) && (!options.Metals || simulation.World.Get<Furniture>(piece).Buildable == "forge"), pieces[0]);
            var cell = simulation.World.Get<Position>(chosen).Cell;
            screen.Input.SelectCell(cell);
            screen.View.Camera.Focus(new Vector2(cell.X + 0.5f, cell.Y + 0.5f));
        }

        // A ripe crop of the first farm zone, to see the zone, the crop and the pile on it (milestone O6).
        if (options.InspectFarm)
        {
            var farmZones = FarmRules.Zones(simulation);
            TileCoord? ripe = null;
            if (farmZones.Count > 0)
            {
                foreach (var cell in simulation.World.Get<FarmZone>(farmZones[0].Zone).Cells)
                {
                    if (FarmRules.State(simulation, cell) is { State: FarmCellState.Ripe })
                    {
                        ripe = cell;
                        break;
                    }
                }
            }

            if (ripe is { } ripeCell)
            {
                screen.Input.SelectCell(ripeCell);
                screen.View.Camera.Focus(new Vector2(ripeCell.X + 0.5f, ripeCell.Y + 0.5f));
            }
        }

        // A patch of every crop in its three stages in front of the first colonist, to look at the models (milestone O6).
        if (options.Crops && colonists.Count > 0 && simulation.World.IsAlive(colonists[0]))
        {
            var origin = simulation.World.Get<Position>(colonists[0]).Cell;
            var cropIds = new[] { "wheat", "peas", "cabbage" };
            for (var lineIndex = 0; lineIndex < cropIds.Length; lineIndex++)
            {
                var cropDef = simulation.Content.Get<CropDef>(cropIds[lineIndex]);
                for (var stageIndex = 0; stageIndex < 3; stageIndex++)
                {
                    var cell = new TileCoord(origin.X + 2 + (stageIndex * 2), origin.Y + 2 + (lineIndex * 2), 0);
                    cell = cell with { Z = simulation.Map.SurfaceZ(cell.X, cell.Y) + 1 };
                    var entity = simulation.World.CreateEntity();
                    simulation.World.Add(entity, new Position { X = cell.X, Y = cell.Y, Z = cell.Z });
                    simulation.World.Add(entity, new Crop { Def = cropDef.Id, Days = stageIndex switch { 0 => 0, 1 => cropDef.GrowDays / 2, _ => cropDef.GrowDays } });
                    simulation.Events.Emit(new EntitySpawnedEvent(entity));
                }
            }

            if (options.ReapOrder)
            {
                simulation.Commands.Enqueue(new DesignateCommand(DesignationKind.Reap, new TileCoord(origin.X + 2, origin.Y + 2, 0), new TileCoord(origin.X + 8, origin.Y + 8, simulation.Map.Size.Depth - 1)));
                simulation.ApplyCommandsNow();
                screen.Input.Order = DesignationKind.Reap;
            }

            screen.View.Camera.Focus(new Vector2(origin.X + 4.5f, origin.Y + (options.ReapOrder ? 9f : 4.5f)));
        }

        if (options.Farm)
        {
            screen.Input.ToggleFarm();
        }

        if (options.Knowledge)
        {
            screen.Session.Enqueue(new SetResearchQueueCommand(["stonecutting", "agriculture"]));
            screen.Advance(1);
            screen.Hud.RefreshResearch();
            screen.Hud.Knowledge.Toggle();
        }

        if (options.Production)
        {
            screen.Hud.Production.Toggle();
        }

        if (options.Colony)
        {
            screen.Hud.Colony.Toggle();
        }

        if (options.Inventory)
        {
            screen.Hud.Inventory.Toggle();
        }

        if (options.Build)
        {
            screen.Input.ToggleBuild();
        }

        // The preview of a piece of furniture, for screenshots (docs/gdd/vista-previa-de-muebles.md).
        if (options.Furniture is { } furniture)
        {
            if (!simulation.Content.TryGet<BuildableDef>(furniture, out var piece) || !piece.IsFurniture)
            {
                Fail($"--furniture: '{furniture}' is not a piece of furniture.", ExitBadArguments);
                return;
            }

            screen.Input.Build = new BuildChoice(piece.Id, piece.Materials[0], options.Rotation ?? 0);
        }

        if (options.Point is { } column)
        {
            var map = simulation.Map;
            if (!map.Size.Contains(column.X, column.Y, 0) || (options.DragTo is { } end && !map.Size.Contains(end.X, end.Y, 0)))
            {
                Fail("--point and --drag-to must be columns of the map.", ExitBadArguments);
                return;
            }

            // The camera looks at the middle of what is pointed at.
            var cell = new TileCoord(column.X, column.Y, map.SurfaceZ(column.X, column.Y));
            var far = options.DragTo ?? column;
            screen.View.Camera.Focus(new Vector2((column.X + far.X + 1) / 2f, (column.Y + far.Y + 1) / 2f));
            if (options.DragTo is { } to)
            {
                screen.Input.PointAtDrag(cell, new TileCoord(to.X, to.Y, cell.Z));
            }
            else
            {
                screen.Input.PointAt(cell);
            }
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
            var ages = colonists.Select(colonist => LifeRules.AgeYears(simulation, colonist) ?? -1).ToArray();
            var traits = simulation.Content.All<TraitDef>().Count;
            if (simulation.LifePace.Length == 0 || ages.Any(years => years < 0)
                || colonists.Any(colonist => !simulation.World.TryGet<Personality>(colonist, out var personality) || personality.Traits.Count != traits)
                || colonists.Any(colonist => !simulation.World.TryGet<Skills>(colonist, out var skills)
                    || skills.Values.Count != simulation.Content.All<WorkTypeDef>().Count(type => type.Skill))
                || colonists.Any(colonist => !simulation.World.TryGet<Relations>(colonist, out var relations) || relations.Others.Count != colonists.Count - 1)
                || Relationships.PartnerOf(simulation, colonists[0]) != colonists[1])
            {
                throw new InvalidOperationException("the colonists arrived without a pace of life, an age, a personality, skills, relations or the couple");
            }

            GD.Print($"[smoke] life: pace {simulation.LifePace}, ages {string.Join(", ", ages)}, {traits} traits and {simulation.World.Get<Skills>(colonists[0]).Values.Count} skills each");
            var (stairSite, house) = OrderWorkAround(simulation, arrival);

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
            if (DifferenceOf(loaded, simulation) is { } difference)
            {
                Fail($"[smoke] saved at tick {half} and loaded, the game differs {ticks - half} ticks later from the one that never stopped: {difference}", ExitFailure);
                return;
            }

            GD.Print($"[smoke] saved at tick {half} and loaded: same state, and the same {ticks - half} ticks later");

            // Nobody shows the events in smoke mode.
            simulation.Events.Clear();
            ReportColony(simulation, colonists, stairSite);
            ReportHouse(simulation, house);

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

        if (DifferenceOf(loaded, simulation) is { } difference)
        {
            GD.PushError($"[smoke] the loaded game differs from the saved one: {difference}");
            return null;
        }

        return loaded;
    }

    /// <summary>
    /// How a game differs from the one it should be, or null if it is the same: its fingerprint,
    /// and free will with the village and its targets, which the fingerprint leaves out while free
    /// will is off (docs/gdd/libre-albedrio.md).
    /// </summary>
    private static string? DifferenceOf(Simulation game, Simulation expected)
    {
        var (hash, expectedHash) = (game.ComputeStateHash(), expected.ComputeStateHash());
        if (hash != expectedHash)
        {
            return $"fingerprint {hash:X16} instead of {expectedHash:X16}";
        }

        if (game.FreeWill != expected.FreeWill)
        {
            return $"free will {game.FreeWill} instead of {expected.FreeWill}";
        }

        if (game.VillageCenter != expected.VillageCenter)
        {
            return $"village centre {game.VillageCenter} instead of {expected.VillageCenter}";
        }

        return game.StockTargets.SequenceEqual(expected.StockTargets)
            ? null
            : $"targets [{string.Join(", ", game.StockTargets)}] instead of [{string.Join(", ", expected.StockTargets)}]";
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

        if (DifferenceOf(slots.Load(SmokeSlot, services.Defs).Simulation, simulation) is { } difference)
        {
            GD.PushError($"[smoke] the slot written loads a different game: {difference}");
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
    /// With the interface at 200 %, a click sent to the window where a colonist is drawn still
    /// selects it, as the player's mouse would (<c>docs/gdd/tamano-de-la-interfaz.md</c>). The 3D
    /// view ignores the interface size, so the colonist's pixel is taken at 100 %, and the window
    /// turns the click into the coordinates of the scaled interface. A colonist under a panel of
    /// the larger HUD cannot be clicked, so any drawn one will do. The size goes through the
    /// settings, as the options set it: setting the window's factor alone would be undone at once
    /// by the handler of SizeChanged.
    /// </summary>
    private void ClickColonistWithLargeInterface(GameScreen screen, IReadOnlyList<EntityId> drawn)
    {
        // Without a window the root is 64×64 pixels, all of it under the HUD. At 1920×1080, 200 %
        // leaves the smallest canvas a fixed size may leave.
        var root = GetTree().Root;
        var settings = _services!.Settings;
        var (setting, size) = (settings.UiScale, root.Size);
        root.Size = new Vector2I(1920, 1080);
        settings.SetUiScale(100, save: false);
        var camera = screen.View.Camera.Camera;
        var pixels = drawn.Select(colonist => root.GetFinalTransform() * camera.UnprojectPosition(screen.View.DrawnBounds(colonist)!.Value.GetCenter())).ToList();
        settings.SetUiScale(200, save: false);
        try
        {
            if (!Mathf.IsEqualApprox(root.ContentScaleFactor, 2f))
            {
                throw new InvalidOperationException($"The interface should be at 200 %, and its factor is {root.ContentScaleFactor}.");
            }

            foreach (var pixel in pixels)
            {
                screen.Input.ClearSelection();
                root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = pixel, GlobalPosition = pixel });
                root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = pixel, GlobalPosition = pixel });
                if (screen.Input.Selected is { IsColonist: true })
                {
                    GD.Print("[smoke] with the interface at 200 %, a click on a colonist selects it");

                    // The click reached the world there, so a gesture there reaches the camera too.
                    ZoomWithTrackpadAndKeys(root, screen.View.Camera, pixel);
                    return;
                }
            }

            throw new InvalidOperationException($"With the interface at 200 %, a click on each of the {pixels.Count} drawn colonists selected none.");
        }
        finally
        {
            settings.SetUiScale(setting, save: false);
            root.Size = size;
        }
    }

    /// <summary>
    /// A Mac trackpad sends no wheel: pinching spread out comes closer, and a two-finger slide
    /// that scrolls down, as the wheel would, goes away, as the window gets them
    /// (<c>docs/gdd/interfaz-ip1.md</c>). A sideways slide does not zoom. Each gesture is then
    /// undone, so the view stays as it was. + and - zoom whatever the keyboard layout: + is a key
    /// of its own on a Spanish one and Shift and = on a US one.
    /// </summary>
    private static void ZoomWithTrackpadAndKeys(Window root, CameraRig rig, Vector2 pixel)
    {
        var start = rig.Distance;
        using var pinch = new InputEventMagnifyGesture { Factor = 1.5f, Position = pixel };
        using var unpinch = new InputEventMagnifyGesture { Factor = 1f / 1.5f, Position = pixel };
        using var slide = new InputEventPanGesture { Delta = new Vector2(0f, 0.5f), Position = pixel };
        using var unslide = new InputEventPanGesture { Delta = new Vector2(0f, -0.5f), Position = pixel };
        using var sideways = new InputEventPanGesture { Delta = new Vector2(2f, 0.5f), Position = pixel };
        root.PushInput(pinch);
        var pinched = rig.Distance;
        root.PushInput(unpinch);
        root.PushInput(slide);
        var slid = rig.Distance;
        root.PushInput(unslide);
        var beforeSideways = rig.Distance;
        root.PushInput(sideways);
        var sideSlid = rig.Distance;
        if (!(pinched < start && slid > start && sideSlid == beforeSideways))
        {
            throw new InvalidOperationException($"From {start}, a pinch left the camera at {pinched}, a slide down at {slid} and a sideways slide changed {beforeSideways} to {sideSlid}.");
        }

        using var plus = new InputEventKey { Keycode = Key.Plus };
        using var equal = new InputEventKey { Keycode = Key.Equal, ShiftPressed = true };
        using var minus = new InputEventKey { Keycode = Key.Minus };
        if (!InputMap.EventIsAction(plus, InputActions.ZoomIn) || !InputMap.EventIsAction(equal, InputActions.ZoomIn) || !InputMap.EventIsAction(minus, InputActions.ZoomOut))
        {
            throw new InvalidOperationException("+, Shift and = or - is not bound to the zoom.");
        }

        GD.Print("[smoke] a pinch and a two-finger slide on the trackpad zoom, and so do + and -");
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
            ClickColonistWithLargeInterface(screen, drawn);
        }

        // The preview of the first piece of furniture of the build bar, turned, where a colonist
        // stands: shown, or red if the cell does not take it (docs/gdd/vista-previa-de-muebles.md).
        var piece = Construction.BuildablesInOrder(screen.Session.Simulation.Content).FirstOrDefault(buildable => buildable.IsFurniture);
        if (piece is not null && colonists.Count > 0 && screen.Session.Simulation.World.IsAlive(colonists[0]))
        {
            var lastBuild = screen.Input.LastBuild;
            screen.Input.Build = new BuildChoice(piece.Id, piece.Materials[0], 1);
            screen.Input.PointAt(screen.Session.Simulation.World.Get<Position>(colonists[0]).Cell);
            screen.Input.UpdatePreviews();
            if (screen.View.Previews.Shown != 1)
            {
                throw new InvalidOperationException($"The preview of a {piece.Id} on one cell showed {screen.View.Previews.Shown} pieces.");
            }

            screen.Input.StopPointing();
            screen.Input.Build = null;
            screen.Input.LastBuild = lastBuild;
            screen.Input.UpdatePreviews();
            GD.Print($"[smoke] the preview of a {piece.Id} shows where it would go");
        }

        // The decoration option over the game: the grass is dressed again with each level, and
        // back to where it was (docs/gdd/decoracion-del-pasto.md).
        var decoration = _services!.Settings.Decoration;
        foreach (var level in Enum.GetValues<DecorationLevel>())
        {
            _services.Settings.SetDecoration(level, save: false);
        }

        _services.Settings.SetDecoration(decoration, save: false);
        GD.Print("[smoke] the grass is dressed again for every decoration level");

        // The colony's stock and the frames per second in the HUD (docs/gdd/indicadores.md).
        var stock = ColonyStock.ByCategory(screen.Session.Simulation);
        var stockTexts = screen.Hud.StockTexts.ToList();
        if (stockTexts.Count != stock.Count || stockTexts.Any(line => line.Length == 0 || line.Contains('⟦')))
        {
            throw new InvalidOperationException($"the HUD shows the stock as [{string.Join(" | ", stockTexts)}]");
        }

        var shownStock = screen.Hud.ShownStockTexts.Count();
        var withStock = stock.Count(line => line.Count > 0);
        var neededLines = Math.Max(3, withStock);
        if (shownStock < neededLines || shownStock > stockTexts.Count)
        {
            var failed = shownStock < neededLines
                ? $"fewer than the {neededLines} it needs (at least 3, and one per category with stock: {withStock})"
                : "more than the categories there are";
            throw new InvalidOperationException($"the HUD shows {shownStock} of {stockTexts.Count} stock lines, {failed}");
        }

        GD.Print($"[smoke] stock in the HUD: {string.Join(", ", screen.Hud.ShownStockTexts)} ({shownStock} of {stockTexts.Count} categories shown)");
        var showFps = _services.Settings.ShowFps;
        _services.Settings.SetShowFps(true, save: false);
        var shown = screen.Hud.ShowsFps;
        _services.Settings.SetShowFps(false, save: false);
        var hidden = !screen.Hud.ShowsFps;
        _services.Settings.SetShowFps(showFps, save: false);
        if (!shown || !hidden)
        {
            throw new InvalidOperationException($"the frames per second did not follow the option: shown {shown}, hidden {hidden}");
        }

        GD.Print("[smoke] the frames per second show and hide with the option");

        // The sheet of a colonist, with every part in words (docs/gdd/vida-y-generaciones.md).
        var people = new List<EntityId>();
        screen.Session.Simulation.World.Query<Colonist>(people);
        var someone = people.FirstOrDefault();
        if (!someone.IsNone)
        {
            screen.Hud.ToggleSheet(someone);
            var lines = screen.Hud.Sheet.Texts.ToList();
            // A missing text shows as its key, or as ⟦name⟧ for a missing argument.
            if (!screen.Hud.Sheet.Visible || lines.Any(line => line.Length == 0 || line.Contains('⟦') || line.StartsWith("ui.", StringComparison.Ordinal)
                                                                  || line.Contains("def.", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"the sheet shows [{string.Join(" | ", lines)}]");
            }

            screen.Hud.ToggleSheet(someone);
            GD.Print($"[smoke] the sheet of a colonist opens with {lines.Sum(line => line.Split('\n').Length)} lines and closes");
        }

        // The founding couple has the choice of children, and pressing it twice gives it back.
        var game = screen.Session.Simulation;
        var founder = people.FirstOrDefault(colonist => !Relationships.PartnerOf(game, colonist).IsNone);
        if (!founder.IsNone)
        {
            screen.Hud.ToggleSheet(founder);
            var button = screen.Hud.Sheet.ChildrenButton;
            var allowed = LifeRules.ChildrenAllowed(game, founder);
            button.EmitSignal(BaseButton.SignalName.Pressed);
            screen.Advance(1);
            var toggled = LifeRules.ChildrenAllowed(game, founder);
            screen.Hud.Sheet.ShowColonist(founder);
            var label = button.Tr(button.Text).ToString();
            button.EmitSignal(BaseButton.SignalName.Pressed);
            screen.Advance(1);
            if (!button.Visible || toggled == allowed || LifeRules.ChildrenAllowed(game, founder) != allowed
                || label.StartsWith("ui.", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"the choice of children did not work: shown {button.Visible}, '{label}', {allowed} -> {toggled}");
            }

            screen.Hud.ToggleSheet(founder);
            GD.Print($"[smoke] the sheet of a founder in a couple offers the choice of children: '{label}'");
        }

        // The colony panel, with a row per colonist and its priorities.
        screen.Hud.Colony.Toggle();
        screen.Advance(10);
        var workTypes = ColonyRules.From(screen.Session.Simulation.Content).WorkTypes.Count;
        if (screen.Hud.Colony.WorkColumns != workTypes || workTypes < 13)
        {
            throw new InvalidOperationException($"the colony panel has {screen.Hud.Colony.WorkColumns} work columns for {workTypes} work types");
        }

        // Without a window the root is 64x64 pixels: measure the table on the smallest canvas the
        // game supports (960x540 at 100 %) and on a larger one, then put the window back.
        var window = GetTree().Root;
        var (uiScale, windowSize) = (_services!.Settings.UiScale, window.Size);
        try
        {
            foreach (var canvas in new[] { new Vector2I(960, 540), new Vector2I(1920, 1080) })
            {
                window.Size = canvas;
                _services.Settings.SetUiScale(100, save: false);
                var okFit = screen.Hud.Colony.TableFits();
                if (!okFit)
                {
                    throw new InvalidOperationException($"the colony panel does not fit a canvas of {canvas.X}x{canvas.Y}: {screen.Hud.Colony.FitDetail}");
                }
            }
        }
        finally
        {
            _services.Settings.SetUiScale(uiScale, save: false);
            window.Size = windowSize;
        }

        GD.Print($"[smoke] the colony panel has {workTypes} work columns and scrolls within the canvas");
        CheckFreeWill(screen);
        CheckProduction(screen);
        CheckKnowledge(screen);
        CheckRooms(screen);
        CheckInventory(screen);
        CheckFarm(screen);
        screen.OpenPauseMenu();
        screen.OpenOptions();
        screen.OpenSaveScreen();

        ShowNewGame();
        ShowOptions();
        ShowLoadGame();
        ShowLoadError(new SaveLoadException(SaveLoadError.MissingContent, "smoke_def"));
        ShowMainMenu();
        GD.Print("[smoke] menus built: main, new game, options, pause, save, load, error, colony");
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
            if (DefVisuals.Of(def) is { } visual && !models.Catalog.TryResolve(visual, era: null, out _))
            {
                problems.Add($"'{visual}' is not in the visual catalog");
            }
        }

        // Every file of a model: its own, its variants and its parts.
        foreach (var file in models.Catalog.Models.SelectMany(model => model.Files).Distinct(StringComparer.Ordinal))
        {
            if (models.Load(file) is null)
            {
                problems.Add($"{file} does not load; run godot --headless --path game --import");
            }
        }

        foreach (var decoration in models.Catalog.Decorations)
        {
            if (models.Load(decoration.ModelPath) is null)
            {
                problems.Add($"{decoration.ModelPath} does not load; run godot --headless --path game --import");
            }
        }

        GD.Print($"[smoke] visuals: {models.Catalog.Models.Count()} models and {models.Catalog.Decorations.Count} decorations in the catalog");
        foreach (var problem in problems)
        {
            GD.PushError($"[smoke] {problem}");
        }

        return problems.Count == 0;
    }

    /// <summary>
    /// Gives the colonists every kind of order around their arrival, so the smoke exercises every
    /// job and measures the ticks with work going on: chop and gather nearby, dig the hills, stairs
    /// down where the ground allows them, and a house of timber walls with a door.
    /// </summary>
    private static (TileCoord? Stairs, TileCoord? House) OrderWorkAround(Simulation simulation, TileCoord arrival)
    {
        TileCoord Offset(int dx, int dy, int dz) => new(arrival.X + dx, arrival.Y + dy, arrival.Z + dz);
        simulation.Commands.Enqueue(new DesignateCommand(DesignationKind.Chop, Offset(-SmokeOrderRadius, -SmokeOrderRadius, -3), Offset(SmokeOrderRadius, SmokeOrderRadius, 3)));
        simulation.Commands.Enqueue(new DesignateCommand(DesignationKind.Harvest, Offset(-SmokeOrderRadius, -SmokeOrderRadius, -3), Offset(SmokeOrderRadius, SmokeOrderRadius, 3)));
        simulation.Commands.Enqueue(new DesignateCommand(DesignationKind.Mine, Offset(-SmokeOrderRadius, -SmokeOrderRadius, 0), Offset(SmokeOrderRadius, SmokeOrderRadius, 3)));

        // A stockpile beside the arrival, for what the colonists chop, dig and gather.
        var (stockpileFrom, stockpileTo) = StockpileBox(arrival);
        simulation.Commands.Enqueue(new DesignateCommand(DesignationKind.Stockpile, stockpileFrom, stockpileTo));

        // A house on flat open ground near the arrival, with the door in the middle of its south wall.
        var buildables = Construction.BuildablesInOrder(simulation.Content);
        var wall = buildables.FirstOrDefault(buildable => buildable.Shape == BuildShape.Wall);
        var doorway = buildables.FirstOrDefault(buildable => buildable.Shape == BuildShape.Door);
        var site = FindStairSite(simulation, arrival);
        var house = FindHouseSite(simulation, arrival, site);
        if (wall is not null && doorway is not null && wall.Materials.FirstOrDefault(doorway.Materials.Contains) is { } material && house is { } corner)
        {
            var far = HouseFar(corner);
            simulation.Commands.Enqueue(new PlaceBlueprintCommand(wall.Id, material, corner, far));
            var door = HouseDoor(corner);
            simulation.Commands.Enqueue(new DesignateCommand(DesignationKind.Cancel, door, door));
            simulation.Commands.Enqueue(new PlaceBlueprintCommand(doorway.Id, material, door, door));
            FurnishHouse(simulation, corner);
        }
        else
        {
            GD.PushWarning("[smoke] No wall and door of one material, or no flat ground for them; the smoke builds no house.");
        }
        if (site is { } stairs)
        {
            simulation.Commands.Enqueue(new DesignateCommand(DesignationKind.Stairs, stairs, stairs));
        }
        else
        {
            GD.PushWarning("[smoke] No cell near the arrival can take stairs; the smoke does not dig any.");
        }

        return (site, house);
    }

    private const int HouseSize = 5;

    /// <summary>
    /// Every piece of furniture of the content, in the order of the build bar, in the corners
    /// inside the smoke's house, each turned once more than the one before.
    /// </summary>
    private static void FurnishHouse(Simulation simulation, TileCoord corner)
    {
        TileCoord[] spots =
        [
            new(corner.X + 1, corner.Y + 1, corner.Z), new(corner.X + 3, corner.Y + 1, corner.Z),
            new(corner.X + 3, corner.Y + 3, corner.Z), new(corner.X + 1, corner.Y + 3, corner.Z),
        ];
        var furniture = Construction.BuildablesInOrder(simulation.Content).Where(buildable => buildable.IsFurniture).ToList();
        for (var i = 0; i < Math.Min(spots.Length, furniture.Count); i++)
        {
            simulation.Commands.Enqueue(new PlaceBlueprintCommand(furniture[i].Id, furniture[i].Materials[0], spots[i], spots[i], i));
        }
    }

    // Far enough to find flat open ground in the valley, near enough to walk there.
    private const int HouseSearchRadius = 16;

    /// <summary>The stockpile of the smoke's orders, beside the arrival.</summary>
    private static (TileCoord From, TileCoord To) StockpileBox(TileCoord arrival) =>
        (new TileCoord(arrival.X + 2, arrival.Y - 6, arrival.Z - 2), new TileCoord(arrival.X + 6, arrival.Y - 3, arrival.Z + 2));

    private static TileCoord HouseFar(TileCoord corner) => new(corner.X + HouseSize - 1, corner.Y + HouseSize - 1, corner.Z);

    private static TileCoord HouseDoor(TileCoord corner) => new(corner.X + (HouseSize / 2), corner.Y + HouseSize - 1, corner.Z);

    /// <summary>
    /// The north-west corner of the box of the smoke's house: the nearest to the arrival, on its
    /// level, where every cell is a flat floor and those of the border are open and without a plant,
    /// clear of the stockpile, of the arrival itself and of the stairs. Ties go to the lower cell index.
    /// </summary>
    private static TileCoord? FindHouseSite(Simulation simulation, TileCoord arrival, TileCoord? stairs)
    {
        var map = simulation.Map;
        var plantCells = Designations.PlantCells(simulation);
        var (stockpileFrom, stockpileTo) = StockpileBox(arrival);
        // Walls go on the border, which needs open floors; inside, a bush or a tree may stay.
        bool Open(int x, int y, bool border) =>
            map.Size.Contains(x, y, arrival.Z) && map.ShapeAt(x, y, arrival.Z) == TileShape.Floor
            && (!border || (map.IsStandable(x, y, arrival.Z) && !plantCells.Contains(new TileCoord(x, y, arrival.Z))))
            && new TileCoord(x, y, arrival.Z) != stairs && Math.Max(Math.Abs(x - arrival.X), Math.Abs(y - arrival.Y)) > 1
            && !(x >= stockpileFrom.X && x <= stockpileTo.X && y >= stockpileFrom.Y && y <= stockpileTo.Y);

        TileCoord? best = null;
        var bestDistance = int.MaxValue;
        for (var dy = -HouseSearchRadius; dy <= HouseSearchRadius; dy++)
        {
            for (var dx = -HouseSearchRadius; dx <= HouseSearchRadius; dx++)
            {
                var corner = new TileCoord(arrival.X + dx, arrival.Y + dy, arrival.Z);
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                if (distance >= bestDistance)
                {
                    continue;
                }

                var open = true;
                for (var y = corner.Y; y < corner.Y + HouseSize && open; y++)
                {
                    for (var x = corner.X; x < corner.X + HouseSize && open; x++)
                    {
                        open = Open(x, y, x == corner.X || y == corner.Y || x == corner.X + HouseSize - 1 || y == corner.Y + HouseSize - 1);
                    }
                }

                if (open)
                {
                    best = corner;
                    bestDistance = distance;
                }
            }
        }

        return best;
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

    /// <summary>How the house of the smoke's orders goes: blueprints left and cells built in its box.</summary>
    private static void ReportHouse(Simulation simulation, TileCoord? house)
    {
        if (house is not { } from)
        {
            return;
        }

        var map = simulation.Map;
        var to = HouseFar(from);
        int built = 0, doors = 0;
        for (var y = from.Y; y <= to.Y; y++)
        {
            for (var x = from.X; x <= to.X; x++)
            {
                var cell = new TileCoord(x, y, from.Z);
                if (map.Size.Contains(x, y, from.Z) && Construction.IsBuilt(map, cell))
                {
                    built++;
                    doors += map.ShapeAt(cell) == TileShape.Door ? 1 : 0;
                }
            }
        }

        var beds = new List<EntityId>();
        simulation.World.Query<Bed>(beds);
        var taken = beds.Count(bed => Beds.IsTaken(simulation, bed));
        GD.Print($"[smoke] construction: {simulation.World.Store<Blueprint>().Count} blueprints left, {built} cells built in the house, {doors} doors, "
                 + $"{simulation.World.Store<Furniture>().Count} pieces of furniture, {taken} beds taken");
    }

    /// <summary>
    /// For <c>--metals</c>: a forge two cells from the colonist with an order for an iron axe, and the
    /// colonist carrying a masterwork axe and a normal pickaxe, so the screenshots show the fire
    /// workshop and the tools without playing for days.
    /// </summary>
    private static void PlaceMetals(GameScreen screen, EntityId colonist)
    {
        var simulation = screen.Session.Simulation;
        var forge = simulation.Content.Get<BuildableDef>("forge");
        var origin = simulation.World.Get<Position>(colonist).Cell;
        var plants = new List<EntityId>();
        simulation.World.Query<Plant, Position>(plants);
        var plantCells = plants.Select(plant => simulation.World.Get<Position>(plant).Cell).ToHashSet();
        var occupied = Construction.FurnitureCells(simulation);

        // The nearest free cell around the colonist, ring by ring, in a fixed order.
        TileCoord? site = null;
        for (var ring = 2; ring <= 6 && site is null; ring++)
        {
            for (var dx = -ring; dx <= ring && site is null; dx++)
            {
                for (var dy = -ring; dy <= ring && site is null; dy++)
                {
                    var column = (X: origin.X + dx, Y: origin.Y + dy);
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring || !simulation.Map.Size.Contains(column.X, column.Y, 0))
                    {
                        continue;
                    }

                    var candidate = new TileCoord(column.X, column.Y, simulation.Map.SurfaceZ(column.X, column.Y) + 1);
                    if (simulation.Map.Size.Contains(candidate.X, candidate.Y, candidate.Z)
                        && Construction.CanPlace(simulation.Map, forge, candidate, plantCells, occupied))
                    {
                        site = candidate;
                    }
                }
            }
        }

        if (site is { } cell)
        {
            var piece = simulation.World.CreateEntity();
            simulation.World.Add(piece, new Position { X = cell.X, Y = cell.Y, Z = cell.Z });
            simulation.World.Add(piece, new Furniture { Buildable = "forge", Material = forge.Materials[0] });
            simulation.World.Add(piece, new Workshop());
            simulation.Events.Emit(new EntitySpawnedEvent(piece));
        }

        Tools.Grant(simulation, colonist, "iron_axe", "masterwork");
        Tools.Grant(simulation, colonist, "iron_pickaxe", null);
        screen.Session.Enqueue(new CreateWorkOrderCommand("iron_axe", WorkOrderMode.Count, 2));
        screen.Session.Enqueue(new CreateWorkOrderCommand("charcoal", WorkOrderMode.Count, 5));
        simulation.ApplyCommandsNow();
    }

    /// <summary>
    /// The list of rooms and the layer: with a bedroom and a dining room built (as the capture mode does), the layer
    /// draws both, the list has a row for each in plain text, a click on a row moves the selection, and the button of
    /// the family gives beds with commands.
    /// </summary>
    private static void CheckRooms(GameScreen screen)
    {
        var simulation = screen.Session.Simulation;
        var colonists = new List<EntityId>();
        simulation.World.Query<Colonist>(colonists);
        var scene = RoomScene.Build(simulation, simulation.World.Get<Position>(colonists[0]).Cell);
        screen.Advance(1);
        var panel = screen.Hud.Rooms;
        panel.Toggle();
        panel.Refresh();
        var rows = panel.RowTexts.ToList();
        if (rows.Count != 2 || rows.Any(row => row.StartsWith("ui.", StringComparison.Ordinal) || row.Contains('⟦')))
        {
            throw new InvalidOperationException($"the list of rooms shows [{string.Join(" | ", rows)}] for a bedroom and a dining room");
        }

        screen.View.Camera.GoTo(new Vector2(scene.BedroomCell.X, scene.BedroomCell.Y), scene.BedroomCell.Z + 1);
        if (!screen.View.RoomLayer.Shown || screen.View.RoomLayer.RoomsShown != 2)
        {
            throw new InvalidOperationException($"the layer of rooms shows {screen.View.RoomLayer.RoomsShown} rooms, on {screen.View.RoomLayer.Shown}");
        }

        var bedroom = Rooms.At(simulation, scene.BedroomCell) ?? throw new InvalidOperationException("the bedroom of the scene is not a room");
        panel.Choose(bedroom);
        if (panel.BedRows != bedroom.Beds.Count || screen.Hud.Inspector.ShownText.Contains('⟦'))
        {
            throw new InvalidOperationException($"the list shows {panel.BedRows} beds for {bedroom.Beds.Count}: '{screen.Hud.Inspector.ShownText}'");
        }

        if (!panel.PressAssignFamily(0))
        {
            throw new InvalidOperationException("the list of rooms has no button to assign a family");
        }

        simulation.ApplyCommandsNow();
        var owners = RoomRules.Owners(simulation, Rooms.At(simulation, scene.BedroomCell)!);
        if (owners.Count == 0 || owners[0] != colonists[0])
        {
            throw new InvalidOperationException($"assigning the family gave beds to {owners.Count} colonists");
        }

        panel.Toggle();
        if (screen.View.RoomLayer.Shown)
        {
            throw new InvalidOperationException("the layer of rooms stays on when the list closes");
        }

        GD.Print($"[smoke] the list shows {rows.Count} rooms, the layer draws them, and assigning a family gave beds to {owners.Count} colonists");
    }

    /// <summary>The knowledge panel shows the whole tree, lets a click queue a knowledge and a click again take it out.</summary>
    private static void CheckKnowledge(GameScreen screen)
    {
        var simulation = screen.Session.Simulation;
        var knowledge = screen.Hud.Knowledge;
        knowledge.Toggle();
        var cards = knowledge.CardTexts.ToList();
        var defs = simulation.Content.All<KnowledgeDef>().Count;
        if (cards.Count != defs || cards.Any(card => card.StartsWith("ui.", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"the knowledge panel shows {cards.Count} cards for {defs} knowledge");
        }

        screen.Session.Enqueue(new SetResearchQueueCommand(["stonecutting"]));
        screen.Advance(2);
        knowledge.Refresh();
        if (!knowledge.CardTexts.Any(card => card.Contains("(1)", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("the knowledge panel does not show the queued knowledge in position 1");
        }

        screen.Session.Enqueue(new SetResearchQueueCommand([]));
        screen.Advance(2);
        knowledge.Refresh();
        knowledge.Toggle();
        GD.Print($"[smoke] the knowledge panel shows {cards.Count} cards and follows the queue");
    }

    /// <summary>
    /// The farm tool: without a known crop it stays off; once the colony knows agriculture and
    /// cereals its bar offers exactly their crops; a drag previews the cells the command takes; a
    /// zone made by command is marked on the map, shown by the inspector with its crop and a
    /// button per crop, changes its crop by command and goes with Cancel. Also the notice of food
    /// that spoiled reads as text, with its plural and the name of the item.
    /// </summary>
    private void CheckFarm(GameScreen screen)
    {
        var simulation = screen.Session.Simulation;
        var input = screen.Input;
        var text = _services!.Text;
        input.ToggleFarm();
        if (input.FarmCrop is not null || screen.Hud.FarmButtonEnabled)
        {
            throw new InvalidOperationException("the farm tool can be turned on without a crop the colony knows");
        }

        // The smoke has no scriptorium: it learns as a test would, by the method only Core may call.
        var learn = typeof(ProgressState).GetMethod("Learn", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ProgressState.Learn is gone: the smoke of the farm tool needs another way to teach.");
        foreach (var knowledge in new[] { "agriculture", "cereals" })
        {
            learn.Invoke(simulation.Progress, [knowledge]);
        }

        screen.Hud.RefreshFarmTool();
        input.ToggleFarm();
        var bar = screen.Hud.FarmBarTexts.ToList();
        var known = input.UnlockedCrops().Select(crop => crop.Id).ToList();
        if (input.FarmCrop is null || !screen.Hud.FarmButtonEnabled || bar.Count != 3 || known.Count != 3 || known.Contains("oak_grove", StringComparer.Ordinal)
            || bar.Any(button => button.StartsWith("ui.", StringComparison.Ordinal) || button.StartsWith("Bosquecillo", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"the farm bar offers [{string.Join(" | ", bar)}] for the crops [{string.Join(", ", known)}] and chose '{input.FarmCrop}'");
        }

        // The crop is chosen with a click on its button.
        screen.Hud.PressFarmBarButton(known.IndexOf("wheat"));
        if (input.FarmCrop != "wheat")
        {
            throw new InvalidOperationException($"a click on the wheat button chose '{input.FarmCrop}'");
        }

        // A box of flat soil near the colonists, found by the same query the command uses.
        var colonists = new List<EntityId>();
        simulation.World.Query<Colonist, Position>(colonists);
        var arrival = simulation.World.Get<Position>(colonists[0]).Cell;
        (TileCoord From, TileCoord To)? box = null;
        var most = 0;
        for (var dy = -24; dy <= 24; dy += 2)
        {
            for (var dx = -24; dx <= 24; dx += 2)
            {
                var near = new TileCoord(arrival.X + dx, arrival.Y + dy, arrival.Z);
                var far = near with { X = near.X + 3, Y = near.Y + 3 };
                var cells = FarmRules.CellsToZone(simulation, near, far).Count(cell => LevelShading.IsVisible(cell.Z, screen.View.ViewLevel));
                if (cells > most)
                {
                    box = (near, far);
                    most = cells;
                }
            }
        }

        if (box is not { } found)
        {
            GD.PushWarning("[smoke] No flat soil to draw a farm zone on; the smoke skips the zone checks.");
            input.FarmCrop = null;
            return;
        }

        var expected = FarmRules.CellsToZone(simulation, found.From, found.To);
        input.PointAtDrag(found.From, found.To);
        input.UpdateFarmPreview();
        var previewed = screen.View.FarmCells.ShownCells;
        input.StopPointing();
        input.FarmCrop = null;
        if (previewed != expected.Count(cell => LevelShading.IsVisible(cell.Z, screen.View.ViewLevel)) || previewed == 0)
        {
            throw new InvalidOperationException($"the drag previews {previewed} cells and the command would take {expected.Count}");
        }

        screen.Session.Enqueue(new PlaceFarmZoneCommand(found.From, found.To, "wheat"));
        screen.Advance(2);
        var zones = FarmRules.Zones(simulation);
        var marked = screen.View.FarmCellsMarked;
        if (zones.Count != 1 || zones[0].Cells != expected.Count || zones[0].Crop != "wheat" || marked != previewed)
        {
            throw new InvalidOperationException($"the field has {zones.Count} zones, {zones.FirstOrDefault()?.Cells} cells and {marked} marked, against {expected.Count} previewed");
        }

        // The inspector: the zone, its crop and a button per known crop; the crop changes by command.
        screen.Input.SelectCell(expected[0]);
        var shown = screen.Hud.Inspector.ShownText;
        var wheatName = text.Format("def.crop.wheat.name");
        if (!shown.Contains(wheatName, StringComparison.Ordinal) || shown.Contains("ui.", StringComparison.Ordinal) || screen.Hud.Inspector.FarmButtonCount != 3)
        {
            throw new InvalidOperationException($"the inspector of a farm cell says '{shown}' with {screen.Hud.Inspector.FarmButtonCount} crop buttons");
        }

        screen.Session.Enqueue(new SetFarmZoneCropCommand(zones[0].Zone, "peas"));
        screen.Advance(1);
        screen.Input.SelectCell(expected[0]);
        if (!screen.Hud.Inspector.ShownText.Contains(text.Format("def.crop.peas.name"), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"the inspector does not follow the new crop: '{screen.Hud.Inspector.ShownText}'");
        }

        // A crop sown on the cell shows its percent, and a pile of food its freshness.
        var crop = simulation.World.CreateEntity();
        simulation.World.Add(crop, new Position { X = expected[0].X, Y = expected[0].Y, Z = expected[0].Z });
        simulation.World.Add(crop, new Crop { Def = "peas", Days = 4 });
        ItemPiles.Drop(simulation, "berries", 12, expected[0], freshness: 4_000);
        screen.Input.SelectCell(expected[0]);
        var sown = screen.Hud.Inspector.ShownText;
        if (!sown.Contains("50", StringComparison.Ordinal) || !sown.Contains("40", StringComparison.Ordinal) || sown.Contains("ui.", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"the inspector of a sown cell with a pile of berries says '{sown}'");
        }

        screen.Input.ClearSelection();

        // Cancel takes the zone out of the map.
        screen.Session.Enqueue(new DesignateCommand(DesignationKind.Cancel, found.From, found.To));
        screen.Advance(2);
        if (FarmRules.Zones(simulation).Count != 0 || screen.View.FarmCellsMarked != 0)
        {
            throw new InvalidOperationException("Cancel left the farm zone drawn");
        }

        // The notice of food that spoiled.
        var notice = text.Resolve(
            LocalizedText.Of(SpoilageSystem.SpoiledKey).With("count", 20).With("item", LocalizedText.Of("def.item.berries.name").With("count", 20)),
            simulation.World);
        if (!notice.Contains("20", StringComparison.Ordinal) || notice.Contains('{', StringComparison.Ordinal) || notice.StartsWith("event.", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"the notice of spoiled food reads '{notice}'");
        }

        GD.Print($"[smoke] the farm tool offers {bar.Count} crops, previews {previewed} cells, draws, shows and cancels a field: '{notice}'");
    }

    /// <summary>
    /// The inventory lists what the colony has by category, and the panels of the left column open one at a time
    /// (docs/gdd/oficios.md, O10). Leaves every panel closed.
    /// </summary>
    private static void CheckInventory(GameScreen screen)
    {
        var hud = screen.Hud;
        hud.TogglePanel(hud.Inventory);
        hud.Inventory.Refresh();
        var rows = hud.Inventory.RowTexts.ToList();
        var food = ColonyStock.ByCategory(screen.Session.Simulation).FirstOrDefault(line => line.Count > 0);
        if (!hud.Inventory.Visible || rows.Count == 0 || rows.Any(row => row.Length == 0 || row.StartsWith("ui.", StringComparison.Ordinal) || row.Contains('{', StringComparison.Ordinal))
            || (food is not null && !rows.Any(row => row.Contains(food.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal))))
        {
            throw new InvalidOperationException($"the inventory shows '{string.Join(" | ", rows)}'");
        }

        // One at a time: the next one replaces it, and opening the same one again closes it.
        Control[] panels = [hud.Colony, hud.Production, hud.Knowledge, hud.Rooms, hud.Inventory];
        foreach (var panel in panels)
        {
            hud.TogglePanel(panel);
            if (!panel.Visible || panels.Count(other => other.Visible) != 1)
            {
                throw new InvalidOperationException($"{panel.Name} did not replace the open panel");
            }
        }

        hud.TogglePanel(hud.Inventory);
        if (panels.Any(panel => panel.Visible))
        {
            throw new InvalidOperationException("a panel stayed open after its key");
        }

        GD.Print($"[smoke] the inventory lists {rows.Count} rows and the panels open one at a time: '{rows[0]}'");
    }

    /// <summary>The production panel lists an order made by command, with its mode and state, and forgets it when it is removed.</summary>
    private static void CheckProduction(GameScreen screen)
    {
        var simulation = screen.Session.Simulation;
        screen.Hud.Production.Toggle();
        screen.Session.Enqueue(new CreateWorkOrderCommand("plank", WorkOrderMode.Keep, 6));
        screen.Advance(10);
        screen.Hud.Production.Refresh();
        var texts = screen.Hud.Production.OrderTexts.ToList();
        if (texts.Count != 1 || texts[0].StartsWith("ui.", StringComparison.Ordinal) || !texts[0].Contains('6', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"the production panel shows {texts.Count} orders: '{string.Join(" | ", texts)}'");
        }

        var orders = WorkOrderRules.InOrder(simulation);
        screen.Session.Enqueue(new RemoveWorkOrderCommand(orders[0]));
        screen.Advance(30);
        screen.Hud.Production.Refresh();
        if (screen.Hud.Production.OrderTexts.Any())
        {
            throw new InvalidOperationException("the production panel still shows an order that was removed");
        }

        screen.Hud.Production.Toggle();
        GD.Print($"[smoke] the production panel lists and forgets an order: '{texts[0]}'");
    }

    /// <summary>
    /// Free will follows the option over the game, and the stock targets the fields of the colony
    /// panel, which say them in words (docs/gdd/libre-albedrio.md). Leaves the option as it was, and
    /// the first target at its value, now chosen by the player.
    /// </summary>
    private void CheckFreeWill(GameScreen screen)
    {
        var game = screen.Session.Simulation;
        var settings = _services!.Settings;
        var option = settings.FreeWill;
        var sent = game.FreeWill;
        settings.SetFreeWill(!option, save: false);
        screen.Advance(1);
        var followed = game.FreeWill;
        settings.SetFreeWill(option, save: false);
        screen.Advance(1);
        if (sent != option || followed == option || game.FreeWill != option)
        {
            throw new InvalidOperationException($"free will did not follow the option {option}: sent {sent}, then {followed}, then {game.FreeWill}");
        }

        var targets = FreeWillRules.Targets(game);
        var lines = screen.Hud.Colony.TargetTexts.ToList();
        if (targets.Count == 0 || lines.Count != targets.Count || lines.Any(line => line.Length == 0 || line.Contains('⟦')))
        {
            throw new InvalidOperationException($"the colony panel shows the targets as [{string.Join(" | ", lines)}]");
        }

        // A number written in the field, one above the highest that the field keeps at the highest,
        // and the first one written back; the field shows each.
        var first = targets[0];
        var highest = FreeWillRules.ConfigOf(game.Content)!.TargetMax;
        var written = new List<(int Target, int Shown)>();
        foreach (var target in new[] { 123, highest + 1, first.Count })
        {
            screen.Hud.Colony.WriteTarget(first.Category, target);
            screen.Advance(1);
            written.Add((FreeWillRules.Targets(game)[0].Count, screen.Hud.Colony.TargetShown(first.Category) ?? -1));
        }

        if (written[0] != (123, 123) || written[1] != (highest, highest) || written[2] != (first.Count, first.Count))
        {
            throw new InvalidOperationException($"the target of {first.Category} written as 123, {highest + 1} and {first.Count} became [{string.Join(", ", written)}]");
        }

        // Written as the player does: the field takes the keys from the game, keeps only digits,
        // Enter applies the number and lets go, and Esc puts the target back and lets go. The keys
        // of the frame the field lets go of went into it, so the game still waits for that frame.
        var line = screen.Hud.Colony.TargetLine(first.Category)!;
        bool Focused() => screen.GetViewport().GuiGetFocusOwner() == line;
        line.GrabFocus();
        var typing = TextFocus.IsTyping(screen);
        // What the player types, not text the game shows.
        const string Typed = "7.770";
        const string HalfTyped = "15";
        // Enter in the same frame as the last key: the text still has the dot when the field lets go.
        line.Text = Typed;
        line.EmitSignal(LineEdit.SignalName.TextSubmitted, line.Text);
        screen.Advance(1);
        var submitted = (FreeWillRules.Targets(game)[0].Count, Focused(), TextFocus.IsTyping(screen));
        line.GrabFocus();
        line.Text = HalfTyped;
        using (var escape = new InputEventAction { Action = "ui_cancel", Pressed = true })
        {
            line.EmitSignal(Control.SignalName.GuiInput, escape);
        }

        screen.Advance(1);
        var cancelled = (FreeWillRules.Targets(game)[0].Count, Focused());
        screen.Hud.Colony.WriteTarget(first.Category, first.Count);
        screen.Advance(1);
        if (!typing || submitted != (7770, false, true) || cancelled != (7770, false) || FreeWillRules.Targets(game)[0].Count != first.Count)
        {
            throw new InvalidOperationException($"writing the target of {first.Category}: typing {typing}, Enter gave {submitted}, Esc gave {cancelled}");
        }

        GD.Print($"[smoke] free will follows the option, and the targets the panel: {string.Join(", ", lines)}");
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

        var zones = new List<EntityId>();
        world.Query<Stockpile>(zones);
        var stockpileCells = zones.SelectMany(zone => world.Get<Stockpile>(zone).Cells).ToHashSet();
        var stored = Stockpiles.StoredItems(simulation);
        GD.Print($"[smoke] colony: {zones.Count} stockpiles, {stockpileCells.Count} cells, {stored} items stored, {world.Store<Carried>().Count} colonists carrying");

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
            GD.Print($"[smoke]   {world.Get<Colonist>(colonist).Name}: {job} | {needs} | mood {MoodRules.Value(simulation, colonist)}");
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
