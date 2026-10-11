using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Pathfinding;
using OurLife.Core.Sim;

namespace OurLife.Game.Rendering;

/// <summary>
/// Draws the entities of the simulation with the model of their def's visual: plants, piles of
/// items and colonists (see "Dibujo del mundo" in <c>docs/gdd/interfaz-ip1.md</c>). It follows
/// the entity events: what appears, changes or disappears. Entities above the current level or
/// too deep are hidden, the rest darken with depth like the terrain. A gathered plant uses the
/// visual <c>&lt;visual&gt;.harvested</c> when the catalog has one. Colonists glide between cells
/// with the progress of their <see cref="Movement"/> and face where they walk.
/// </summary>
public partial class EntityRenderer : Node3D
{
    // How fast a drawn colonist catches up with where the simulation says it is, per second.
    private const float GlideRate = 14f;

    private readonly Dictionary<EntityId, EntityView> _views = [];

    // The views that move, a few among thousands of plants: the only ones updated every frame.
    private readonly Dictionary<EntityId, EntityView> _walkers = [];
    private Simulation _simulation = null!;
    private ModelLibrary _models = null!;
    private int _viewLevel;

    /// <summary>Draws the entities of a simulation. The renderer owns the library from this call on.</summary>
    public static EntityRenderer Create(Simulation simulation, ModelLibrary models, int viewLevel)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(models);
        var renderer = new EntityRenderer { Name = nameof(EntityRenderer), _simulation = simulation, _models = models, _viewLevel = viewLevel };

        try
        {
            var entities = new List<EntityId>();
            simulation.World.Query<Position>(entities);
            foreach (var entity in entities)
            {
                renderer.Spawn(entity);
            }

            return renderer;
        }
        catch
        {
            // Frees the views built so far and, on deletion, the library.
            renderer.Free();
            throw;
        }
    }

    public override void _Notification(int what)
    {
        // On deletion, not on leaving the tree (see MapRenderer).
        if (what == NotificationPredelete)
        {
            _models?.Dispose();
        }
    }

    /// <summary>Follows what the simulation reports about its entities.</summary>
    public void Apply(ISimEvent simEvent)
    {
        switch (simEvent)
        {
            case EntitySpawnedEvent spawned:
                Spawn(spawned.Entity);
                break;
            case EntityChangedEvent changed:
                Refresh(changed.Entity);
                break;
            case EntityRemovedEvent removed:
                Remove(removed.Entity);
                break;
        }
    }

    /// <summary>The box a drawn entity fills in the world, or null if it is not drawn or hidden: what a click can hit.</summary>
    public Aabb? DrawnBounds(EntityId entity)
    {
        if (!_views.TryGetValue(entity, out var view) || !view.Node.Visible)
        {
            return null;
        }

        Aabb? bounds = null;
        foreach (var mesh in view.Meshes)
        {
            var box = mesh.GlobalTransform * mesh.GetAabb();
            bounds = bounds is { } merged ? merged.Merge(box) : box;
        }

        return bounds;
    }

    public void SetViewLevel(int viewLevel)
    {
        _viewLevel = viewLevel;
        foreach (var view in _views.Values)
        {
            UpdateVisibility(view);
        }
    }

    public override void _Process(double delta)
    {
        var world = _simulation.World;
        var glide = Math.Min(1f, (float)delta * GlideRate);
        foreach (var (entity, view) in _walkers)
        {
            if (!world.IsAlive(entity))
            {
                continue;
            }

            var target = WalkingPosition(entity, out var facing);
            view.Node.Position = view.Node.Position.Lerp(target, glide);
            if (facing is { } yaw)
            {
                view.Node.Rotation = new Vector3(0, yaw, 0);
            }

            var level = world.Get<Position>(entity).Z;
            if (level != view.Level)
            {
                view.Level = level;
                UpdateVisibility(view);
            }
        }
    }

    private void Spawn(EntityId entity)
    {
        if (_views.ContainsKey(entity) || !_simulation.World.IsAlive(entity) || ModelOf(entity) is not { } model)
        {
            return;
        }

        var node = _models.Instantiate(model);
        var position = _simulation.World.Get<Position>(entity);
        var moves = _simulation.World.Has<Movement>(entity);
        node.Position = moves ? WalkingPosition(entity, out _) : CellBase(position.X, position.Y, position.Z);
        if (!moves)
        {
            node.RotationDegrees = new Vector3(0, CellAngle(position.X, position.Y), 0);
        }

        AddChild(node);
        var view = new EntityView(node, model, position.Z, ModelLibrary.MeshesOf(node));
        _views.Add(entity, view);
        if (moves)
        {
            _walkers.Add(entity, view);
        }

        UpdateVisibility(view);
    }

    /// <summary>A changed entity keeps its node unless its model changed, like a bush that was gathered.</summary>
    private void Refresh(EntityId entity)
    {
        if (!_views.TryGetValue(entity, out var view))
        {
            Spawn(entity);
            return;
        }

        if (ModelOf(entity) is { } model && model != view.Model)
        {
            Remove(entity);
            Spawn(entity);
        }
    }

    private void Remove(EntityId entity)
    {
        if (_views.Remove(entity, out var view))
        {
            _walkers.Remove(entity);
            view.Node.QueueFree();
        }
    }

    /// <summary>The model of a plant, a pile or a creature, or null for an entity that is not drawn.</summary>
    private VisualModel? ModelOf(EntityId entity)
    {
        var world = _simulation.World;
        var content = _simulation.Content;
        string visual;
        string? harvestedVisual = null;
        if (world.TryGet<Plant>(entity, out var plant))
        {
            visual = content.Get<PlantDef>(plant.Def).Visual;
            if (world.Has<Harvested>(entity))
            {
                harvestedVisual = $"{visual}.harvested";
            }
        }
        else if (world.TryGet<ItemStack>(entity, out var stack))
        {
            visual = content.Get<ItemDef>(stack.Def).Visual;
        }
        else if (world.TryGet<Colonist>(entity, out var colonist))
        {
            visual = content.Get<CreatureDef>(colonist.Creature).Visual;
        }
        else
        {
            return null;
        }

        var era = _simulation.Progress.CurrentEra;
        if (harvestedVisual is not null && _models.Catalog.TryResolve(harvestedVisual, era, out var harvested))
        {
            return harvested;
        }

        return _models.Catalog.TryResolve(visual, era, out var model)
            ? model
            : throw new InvalidOperationException($"The visual catalog has no model for '{visual}'.");
    }

    /// <summary>Where a walker is drawn: between its cell and the next one of its route, by its progress.</summary>
    private Vector3 WalkingPosition(EntityId entity, out float? facing)
    {
        var world = _simulation.World;
        var position = world.Get<Position>(entity);
        var here = CellBase(position.X, position.Y, position.Z);
        facing = null;
        if (!world.TryGet<Movement>(entity, out var movement) || !movement.IsMoving)
        {
            return here;
        }

        var next = movement.Path[movement.Next];
        var ticksPerTile = world.TryGet<Colonist>(entity, out var colonist)
            ? _simulation.Content.Get<CreatureDef>(colonist.Creature).MoveTicksPerTile
            : 1;
        var cost = ticksPerTile * PathFinder.StepCost(position.Cell, next);
        var fraction = Math.Clamp((float)movement.Progress / cost, 0f, 1f);
        var there = CellBase(next.X, next.Y, next.Z);

        // A step straight up or down the stairs has no heading: the walker keeps the last one.
        if (next.X != position.X || next.Y != position.Y)
        {
            facing = Mathf.Atan2(there.X - here.X, there.Z - here.Z);
        }

        return here.Lerp(there, fraction);
    }

    private void UpdateVisibility(EntityView view)
    {
        view.Node.Visible = LevelShading.IsVisible(view.Level, _viewLevel);
        var depth = _viewLevel - view.Level;
        if (view.Node.Visible && view.ShadedDepth != depth)
        {
            _models.Shade(view.Model, view.Meshes, depth);
            view.ShadedDepth = depth;
        }
    }

    /// <summary>The centre of a cell's floor, where entities stand.</summary>
    private static Vector3 CellBase(int x, int y, int z) => new(x + 0.5f, z + TerrainMeshes.FloorThickness, y + 0.5f);

    /// <summary>A fixed angle in [0, 360) for a cell, from a small integer hash, so a grove does not look copied.</summary>
    private static float CellAngle(int x, int y)
    {
        var hash = unchecked((uint)((x * 73856093) ^ (y * 19349663)));
        hash ^= hash >> 13;
        hash = unchecked(hash * 0x5bd1e995);
        return hash % 360;
    }

    private sealed class EntityView(Node3D node, VisualModel model, int level, IReadOnlyList<MeshInstance3D> meshes)
    {
        public Node3D Node { get; } = node;

        public VisualModel Model { get; } = model;

        public int Level { get; set; } = level;

        public IReadOnlyList<MeshInstance3D> Meshes { get; } = meshes;

        /// <summary>Depth its materials are shaded for; -1 before the first time.</summary>
        public int ShadedDepth { get; set; } = -1;
    }
}
