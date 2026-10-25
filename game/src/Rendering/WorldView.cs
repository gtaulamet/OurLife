using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Game.Input;

namespace OurLife.Game.Rendering;

/// <summary>
/// The 3D view of a game (ADR-009): sky colour and light, which follow the hour with the
/// <see cref="DayNightCycle"/>, terrain, entities, order marks and camera.
/// It starts at the highest surface of the map, so the whole valley shows, unless a level is
/// given. <see cref="Apply"/> follows the events of the simulation.
/// </summary>
public partial class WorldView : Node3D
{
    private const float StartDistance = 85f;

    private MapRenderer _map = null!;
    private Simulation _simulation = null!;

    // The cells with a plant or a farm zone, for the decoration; worked out again after entities come or go.
    private IReadOnlySet<TileCoord> _plantCells = new HashSet<TileCoord>();
    private HashSet<TileCoord> _farmCells = [];
    private bool _plantsChanged = true;
    private EntityRenderer _entities = null!;
    private OrderMarkers _orders = null!;

    /// <summary>The level the view cuts the map at.</summary>
    public int ViewLevel => _map.ViewLevel;

    public CameraRig Camera { get; private set; } = null!;

    /// <summary>Where the furniture being placed would go (<c>docs/gdd/vista-previa-de-muebles.md</c>).</summary>
    public FurnitureGhosts Previews { get; private set; } = null!;

    /// <summary>The cells a farm zone would take, while the farm tool is on (<c>docs/gdd/agricultura.md</c>).</summary>
    public FarmPreview FarmCells { get; private set; } = null!;

    /// <summary>How many farm zone cells the view marks now, for the smoke.</summary>
    internal int FarmCellsMarked => _orders.FarmCellsMarked;

    /// <summary>Raised when the player moves the view to another level.</summary>
    public event Action<int>? ViewLevelChanged;

    /// <summary>Builds the view of a simulation whose world is already generated.</summary>
    /// <param name="dayNight">Whether the day and night cycle is on; read every frame.</param>
    /// <param name="decoration">How much decoration dresses the grass at the start; <see cref="SetDecoration"/> changes it.</param>
    /// <param name="animationSpeed">How fast colonists move their bodies: the game's speed, 0 when it stands still. Read every frame.</param>
    public static WorldView Create(
        Simulation simulation,
        VisualCatalog catalog,
        Func<bool> dayNight,
        DecorationLevel decoration,
        Func<float> animationSpeed,
        int? level,
        int? distance = null)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(dayNight);
        ArgumentNullException.ThrowIfNull(animationSpeed);
        var map = simulation.Map;
        var viewLevel = Math.Clamp(level ?? HighestSurface(simulation), 0, map.Size.Depth - 1);

        var view = new WorldView { Name = nameof(WorldView), _simulation = simulation };
        try
        {
            var environment = CreateEnvironment();
            view.AddChild(environment);
            var sun = new DirectionalLight3D { Name = "Sun", ShadowEnabled = true };
            view.AddChild(sun);
            // The sun keeps the night of the colonists.
            var rules = ColonyRules.From(simulation.Content);
            var ai = rules.Ai;
            view.AddChild(DayNightCycle.Create(simulation, sun, environment.Environment, ai.NightEndHour, ai.NightStartHour, dayNight));

            var painted = SeasonCycle.CanPaint(simulation, catalog);
            var season = painted ? SeasonCycle.LookAt(simulation, catalog.Seasons!, SeasonCycle.Forced) : null;
            view._farmCells = view.FarmCellSet();
            view._map = MapRenderer.Create(map, catalog, viewLevel, view.PlantCells, DecorationLevels.Multiplier(decoration), season);
            view.AddChild(view._map);

            var models = new ModelLibrary(catalog);
            if (season is not null)
            {
                models.ApplySeason(season);
            }

            view._entities = EntityRenderer.Create(simulation, rules, models, viewLevel, animationSpeed);
            view.AddChild(view._entities);
            if (painted)
            {
                // The look was shown at the start; from here on, once per hour of the game.
                view.AddChild(SeasonCycle.Create(simulation, catalog.Seasons!, view.ShowSeason));
            }
            view._orders = OrderMarkers.Create(simulation, catalog, viewLevel);
            view.AddChild(view._orders);
            view.Previews = FurnitureGhosts.Create(catalog, "Previews");
            view.AddChild(view.Previews);
            view.FarmCells = FarmPreview.Create();
            view.AddChild(view.FarmCells);
            view.Camera = CameraRig.Create(new Vector2(map.Size.Width, map.Size.Height), viewLevel, map.Size.Depth - 1, distance ?? StartDistance);
            view.Camera.LevelChanged += view.OnLevelChanged;
            view.AddChild(view.Camera);
            return view;
        }
        catch
        {
            // Frees whatever was built, and with it the meshes, materials and models the
            // renderers hold, so a failed start does not leak at exit.
            view.Free();
            throw;
        }
    }

    /// <summary>The box a drawn entity fills, or null if it is not drawn (see <see cref="EntityRenderer.DrawnBounds"/>).</summary>
    public Aabb? DrawnBounds(EntityId entity) => _entities.DrawnBounds(entity);

    /// <summary>Follows the events of a frame: entities that appear, change or go, and orders.</summary>
    public void Apply(IReadOnlyList<ISimEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (var simEvent in events)
        {
            _entities.Apply(simEvent);
            _orders.Apply(simEvent);
            _plantsChanged |= simEvent is EntitySpawnedEvent or EntityRemovedEvent;
            if (simEvent is FarmZonesChangedEvent)
            {
                RedressFarmCells();
            }
        }
    }

    private void ShowSeason(SeasonLook look)
    {
        _map.ApplySeason(look);
        _entities.ApplySeason(look);
    }

    /// <summary>Dresses the grass with this much decoration, as the options over the game may ask.</summary>
    public void SetDecoration(DecorationLevel level) => _map.SetDecorationDensity(DecorationLevels.Multiplier(level));

    private IReadOnlySet<TileCoord> PlantCells()
    {
        if (_plantsChanged)
        {
            // No decoration grows in a farm zone, where the crops are what grows.
            var cells = new HashSet<TileCoord>(Designations.PlantCells(_simulation));
            cells.UnionWith(_farmCells);
            _plantCells = cells;
            _plantsChanged = false;
        }

        return _plantCells;
    }

    private HashSet<TileCoord> FarmCellSet()
    {
        var cells = new HashSet<TileCoord>();
        var zones = new List<EntityId>();
        _simulation.World.Query<FarmZone>(zones);
        foreach (var zone in zones)
        {
            cells.UnionWith(_simulation.World.Get<FarmZone>(zone).Cells);
        }

        return cells;
    }

    /// <summary>A farm zone was drawn, changed or removed: the chunks with the cells that came or went are dressed again.</summary>
    private void RedressFarmCells()
    {
        var now = FarmCellSet();
        var changed = new HashSet<TileCoord>(now);
        changed.SymmetricExceptWith(_farmCells);
        _farmCells = now;
        _plantsChanged = true;
        if (changed.Count > 0)
        {
            _map.Redress(changed);
        }
    }

    private static int HighestSurface(Simulation simulation)
    {
        var map = simulation.Map;
        var highest = 0;
        for (var y = 0; y < map.Size.Height; y++)
        {
            for (var x = 0; x < map.Size.Width; x++)
            {
                highest = Math.Max(highest, map.SurfaceZ(x, y));
            }
        }

        return highest;
    }

    private static WorldEnvironment CreateEnvironment()
    {
        // Colours and energies come from the DayNightCycle.
        using var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
        };

        // The node keeps its own reference; the C# one is released (see MapRenderer.Fill).
        return new WorldEnvironment { Name = "Environment", Environment = environment };
    }

    private void OnLevelChanged(int level)
    {
        _map.SetViewLevel(level);
        _entities.SetViewLevel(level);
        _orders.SetViewLevel(level);
        ViewLevelChanged?.Invoke(level);
    }
}
