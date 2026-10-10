using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Sim;
using OurLife.Core.WorldGen;
using OurLife.Game.Rendering;

namespace OurLife.Game.Session;

/// <summary>
/// The screen of a game being played: the <see cref="GameSession"/> that runs the simulation and
/// the <see cref="WorldView"/> that draws it, joined by the events of each frame. Input and HUD
/// join it in later steps of H1.7 (see <c>docs/gdd/interfaz-ip1.md</c>).
/// </summary>
public partial class GameScreen : Node
{
    public GameSession Session { get; private set; } = null!;

    public WorldView View { get; private set; } = null!;

    /// <summary>
    /// Generates the only world of the content with the seed, brings the colony of the only
    /// scenario and builds the screen. <paramref name="level"/> is where the view starts, or the
    /// highest surface; <paramref name="distance"/>, how far the camera starts from it.
    /// </summary>
    public static GameScreen Create(DefDatabase content, VisualCatalog catalog, ulong seed, int? level, int? distance = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(catalog);
        var simulation = NewGame(content, seed);
        var screen = new GameScreen { Name = nameof(GameScreen) };
        try
        {
            screen.Session = GameSession.Create(simulation);
            screen.AddChild(screen.Session);
            screen.View = WorldView.Create(simulation, catalog, level, distance);
            screen.AddChild(screen.View);
            screen.Session.EventsRead += screen.View.Apply;

            // The arrival emitted events before anyone listened; the view already drew what they announce.
            simulation.Events.Clear();
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
        var worlds = content.All<WorldGenDef>();
        if (worlds.Count != 1)
        {
            throw new InvalidOperationException($"The content must have exactly one WorldGen def until IP7, found {worlds.Count}.");
        }

        WorldGenerator.Generate(simulation, worlds[0]);
        ColonyStart.Settle(simulation, ColonyStart.TheOnlyScenario(content));
        ColonySystems.AddTo(simulation);
        return simulation;
    }
}
