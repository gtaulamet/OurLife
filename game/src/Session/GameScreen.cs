using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
using OurLife.Core.WorldGen;
using OurLife.Game.Bootstrap;
using OurLife.Game.Input;
using OurLife.Game.Rendering;
using OurLife.Game.UI;

namespace OurLife.Game.Session;

/// <summary>
/// The screen of a game being played: the <see cref="GameSession"/> that runs the simulation and
/// the <see cref="WorldView"/> that draws it, joined by the events of each frame, the player's
/// <see cref="GameInput"/>, the <see cref="Hud"/>, and the menus over them: pause, options and the
/// end of the game (see <c>docs/gdd/interfaz-ip1.md</c>). While a menu is open, time stops and the
/// game's input sleeps. With slots, the game can be saved from the pause menu and autosaves when
/// each day of the game starts (see <c>docs/gdd/guardado.md</c>).
/// </summary>
public partial class GameScreen : Node
{
    // Above the HUD.
    private const int OverlayLayer = 10;

    private GameServices _services = null!;
    private CanvasLayer _overlays = null!;
    private Control? _overlay;
    private bool _over;
    private SaveSlots? _slots;
    private (int Year, int Season, int Day) _today;

    public GameSession Session { get; private set; } = null!;

    public WorldView View { get; private set; } = null!;

    public GameInput Input { get; private set; } = null!;

    public Hud Hud { get; private set; } = null!;

    /// <summary>Identifies the game across its saves, so its autosave slot follows it.</summary>
    public string GameId { get; private set; } = string.Empty;

    /// <summary>Raised when the player leaves the game for the main menu.</summary>
    public event Action? MainMenuRequested;

    /// <summary>
    /// Generates the only world of the content with the seed, brings the colony of the only
    /// scenario, named <paramref name="colonyName"/>, and builds the screen. <paramref name="level"/> is where the view starts, or the
    /// highest surface; <paramref name="distance"/>, how far the camera starts from it.
    /// </summary>
    public static GameScreen Create(
        GameServices services, VisualCatalog catalog, SaveSlots? slots, string colonyName, ulong seed, int? level, int? distance = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        return Create(services, catalog, slots, SaveSlots.NewGameId(), NewGame(services.Defs, seed, colonyName), level, distance);
    }

    /// <summary>
    /// Builds the screen for a simulation already started with <see cref="NewGame"/>, which may
    /// have run, or loaded from a saved game with its systems added. Without
    /// <paramref name="slots"/> the game neither saves nor autosaves.
    /// </summary>
    public static GameScreen Create(
        GameServices services, VisualCatalog catalog, SaveSlots? slots, string gameId, Simulation simulation, int? level, int? distance = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentException.ThrowIfNullOrEmpty(gameId);
        var screen = new GameScreen { Name = nameof(GameScreen), _services = services, _slots = slots, GameId = gameId };
        screen._today = Today(simulation);
        try
        {
            screen.Session = GameSession.Create(simulation);
            screen.AddChild(screen.Session);
            screen.View = WorldView.Create(simulation, catalog, level, distance);
            screen.AddChild(screen.View);
            screen.Session.EventsRead += screen.View.Apply;
            screen.Input = GameInput.Create(screen.Session, screen.View);
            screen.AddChild(screen.Input);
            screen.Hud = Hud.Create(services, screen.Session, screen.Input, screen.View, simulation.ColonyName);
            screen.AddChild(screen.Hud);
            screen.Session.EventsRead += screen.Hud.Apply;
            screen.Session.EventsRead += screen.WatchForTheEnd;
            screen.Input.PauseMenuRequested += screen.OpenPauseMenu;

            screen._overlays = new CanvasLayer { Name = "Overlays", Layer = OverlayLayer };
            screen.AddChild(screen._overlays);

            // The arrival emitted events before anyone listened: the view already drew what they
            // announce and ignores them, and the log shows the welcome message.
            screen.Session.Advance(0);

            // A simulation already lost, like a long smoke, emitted its ColonyLostEvent before
            // anyone listened: the end shows from the state.
            var colonists = new List<EntityId>();
            simulation.World.Query<Colonist>(colonists);
            if (colonists.Count == 0)
            {
                screen.ShowTheEnd();
            }

            return screen;
        }
        catch
        {
            screen.Free();
            throw;
        }
    }

    /// <summary>Runs ticks at once, for screenshots and tests, and shows what happened in them.</summary>
    public void Advance(int ticks) => Session.Advance(ticks);

    /// <summary>Autosaves when a new day of the game has started since the last frame.</summary>
    public override void _Process(double delta)
    {
        var today = Today(Session.Simulation);
        if (today == _today)
        {
            return;
        }

        _today = today;
        if (_slots is not null && !_over)
        {
            Save(SaveSlots.AutosaveId(GameId), autosave: true);
        }
    }

    /// <summary>
    /// Writes the game in a slot; false if it could not be written, which is logged. A component
    /// missing from <c>SaveTypes</c> also fails here instead of throwing at every autosave.
    /// </summary>
    public bool Save(string slotId, bool autosave = false)
    {
        if (_slots is null)
        {
            throw new InvalidOperationException("This game has no slots to save in.");
        }

        try
        {
            _slots.Write(slotId, Session.Simulation, GameId, autosave);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            GD.PushError($"The game could not be saved in slot '{slotId}': {ex.Message}");
            return false;
        }
    }

    private static (int Year, int Season, int Day) Today(Simulation simulation)
    {
        var date = simulation.CurrentDate;
        return (date.Year, date.SeasonIndex, date.Day);
    }

    /// <summary>A simulation with its world generated and its colonists arrived in the village <paramref name="colonyName"/>.</summary>
    public static Simulation NewGame(DefDatabase content, ulong seed, string colonyName)
    {
        ArgumentNullException.ThrowIfNull(content);
        var simulation = new Simulation(seed, content);
        GenerateTheOnlyWorld(simulation);
        StartColony(simulation, colonyName);
        return simulation;
    }

    /// <summary>
    /// The first half of <see cref="NewGame"/>: generates the only WorldGen def of the content,
    /// until IP7 brings the choice of site. Public so the smoke can time it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The content has no WorldGen def, or more than one.</exception>
    public static WorldGenDef GenerateTheOnlyWorld(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var worlds = simulation.Content.All<WorldGenDef>();
        if (worlds.Count != 1)
        {
            throw new InvalidOperationException($"The content must have exactly one WorldGen def until IP7, found {worlds.Count}.");
        }

        WorldGenerator.Generate(simulation, worlds[0]);
        return worlds[0];
    }

    /// <summary>The second half of <see cref="NewGame"/>: brings the colonists of the only scenario and adds the colony systems.</summary>
    public static IReadOnlyList<EntityId> StartColony(Simulation simulation, string colonyName)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var colonists = ColonyStart.Settle(simulation, ColonyStart.TheOnlyScenario(simulation.Content), colonyName);
        ColonySystems.AddTo(simulation);
        return colonists;
    }

    /// <summary>Opens the options over the game, as the pause menu's button does; for screenshots.</summary>
    public void OpenOptions()
    {
        // Over the end screen there is no pause menu to go back to.
        if (_over)
        {
            return;
        }

        var options = OptionsScreen.Create(_services, dimmed: true);
        options.Closed += OpenPauseMenu;
        ShowOverlay(options);
    }

    public void OpenPauseMenu() => OpenPauseMenu(statusKey: null);

    /// <summary>Opens the save screen over the game, as the pause menu's button does; for screenshots.</summary>
    public void OpenSaveScreen()
    {
        if (_over || _slots is null)
        {
            return;
        }

        var screen = SaveGameScreen.Create(_services, _slots);
        screen.SaveRequested += slotId => OpenPauseMenu(Save(slotId) ? "ui.save.done" : "ui.save.failed");
        screen.BackRequested += OpenPauseMenu;
        ShowOverlay(screen);
    }

    private void OpenPauseMenu(string? statusKey)
    {
        if (_over)
        {
            return;
        }

        var menu = PauseMenu.Create(canSave: _slots is not null, statusKey);
        menu.ResumeRequested += CloseOverlay;
        menu.SaveRequested += OpenSaveScreen;
        menu.OptionsRequested += OpenOptions;
        menu.MainMenuRequested += () => MainMenuRequested?.Invoke();
        ShowOverlay(menu);
    }

    private void WatchForTheEnd(IReadOnlyList<ISimEvent> events)
    {
        if (events.Any(simEvent => simEvent is ColonyLostEvent))
        {
            ShowTheEnd();
        }
    }

    private void ShowTheEnd()
    {
        if (_over)
        {
            return;
        }

        _over = true;
        var end = GameOverScreen.Create();
        end.MainMenuRequested += () => MainMenuRequested?.Invoke();
        ShowOverlay(end);
    }

    /// <summary>Replaces the open menu, if any, and stops time and the game's input under it.</summary>
    private void ShowOverlay(Control overlay)
    {
        _overlay?.QueueFree();
        _overlay = overlay;
        _overlays.AddChild(overlay);
        Session.SetHeld(true);
        Input.ProcessMode = ProcessModeEnum.Disabled;
        View.Camera.ProcessMode = ProcessModeEnum.Disabled;
    }

    private void CloseOverlay()
    {
        _overlay?.QueueFree();
        _overlay = null;
        Session.SetHeld(false);
        Input.ProcessMode = ProcessModeEnum.Inherit;
        View.Camera.ProcessMode = ProcessModeEnum.Inherit;
    }
}
