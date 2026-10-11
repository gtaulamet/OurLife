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
/// game's input sleeps.
/// </summary>
public partial class GameScreen : Node
{
    // Above the HUD.
    private const int OverlayLayer = 10;

    private GameServices _services = null!;
    private CanvasLayer _overlays = null!;
    private Control? _overlay;
    private bool _over;

    public GameSession Session { get; private set; } = null!;

    public WorldView View { get; private set; } = null!;

    public GameInput Input { get; private set; } = null!;

    public Hud Hud { get; private set; } = null!;

    /// <summary>Raised when the player leaves the game for the main menu.</summary>
    public event Action? MainMenuRequested;

    /// <summary>
    /// Generates the only world of the content with the seed, brings the colony of the only
    /// scenario and builds the screen. <paramref name="level"/> is where the view starts, or the
    /// highest surface; <paramref name="distance"/>, how far the camera starts from it.
    /// </summary>
    public static GameScreen Create(GameServices services, VisualCatalog catalog, string colonyName, ulong seed, int? level, int? distance = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        return Create(services, catalog, colonyName, NewGame(services.Defs, seed), level, distance);
    }

    /// <summary>Builds the screen for a simulation already started with <see cref="NewGame"/>, which may have run.</summary>
    public static GameScreen Create(GameServices services, VisualCatalog catalog, string colonyName, Simulation simulation, int? level, int? distance = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(simulation);
        var screen = new GameScreen { Name = nameof(GameScreen), _services = services };
        try
        {
            screen.Session = GameSession.Create(simulation);
            screen.AddChild(screen.Session);
            screen.View = WorldView.Create(simulation, catalog, level, distance);
            screen.AddChild(screen.View);
            screen.Session.EventsRead += screen.View.Apply;
            screen.Input = GameInput.Create(screen.Session, screen.View);
            screen.AddChild(screen.Input);
            screen.Hud = Hud.Create(services, screen.Session, screen.Input, screen.View, colonyName);
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

    /// <summary>A simulation with its world generated and its colonists arrived.</summary>
    public static Simulation NewGame(DefDatabase content, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(content);
        var simulation = new Simulation(seed, content);
        GenerateTheOnlyWorld(simulation);
        StartColony(simulation);
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
    public static IReadOnlyList<EntityId> StartColony(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var colonists = ColonyStart.Settle(simulation, ColonyStart.TheOnlyScenario(simulation.Content));
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

    public void OpenPauseMenu()
    {
        if (_over)
        {
            return;
        }

        var menu = PauseMenu.Create();
        menu.ResumeRequested += CloseOverlay;
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
