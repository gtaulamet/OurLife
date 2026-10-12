using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>An order the player gives over an area (see <c>docs/gdd/colonos-e-ia.md</c>).</summary>
public enum DesignationKind
{
    Mine,
    Stairs,
    Chop,
    Harvest,

    /// <summary>Marks a stockpile zone (see <c>docs/gdd/almacenes.md</c>).</summary>
    Stockpile,

    /// <summary>Takes down what was built (see <c>docs/gdd/construccion.md</c>).</summary>
    Deconstruct,

    /// <summary>Opens floors down to the level below.</summary>
    Channel,

    /// <summary>Removes every order and blueprint in the area, and its cells from the stockpiles.</summary>
    Cancel,
}

/// <summary>Ids of the order kinds, so the presentation keeps no list of its own.</summary>
public static class DesignationKinds
{
    /// <summary>The id of a kind in texts and input actions, like <c>mine</c> in <c>ui.orders.mine</c>.</summary>
    public static string Id(DesignationKind kind) => kind switch
    {
        DesignationKind.Mine => "mine",
        DesignationKind.Stairs => "stairs",
        DesignationKind.Chop => "chop",
        DesignationKind.Harvest => "harvest",
        DesignationKind.Stockpile => "stockpile",
        DesignationKind.Deconstruct => "deconstruct",
        DesignationKind.Channel => "channel",
        DesignationKind.Cancel => "cancel",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}

/// <summary>
/// The player's order over a box of cells, corners included, in any order. Cells outside the map
/// are ignored. Orders on cells are map flags; orders on plants are components of the plant.
/// </summary>
public sealed record DesignateCommand(DesignationKind Kind, TileCoord From, TileCoord To) : ISimCommand
{
    public void Apply(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (!Designations.TryClampBox(simulation.Map, From, To, out var min, out var max))
        {
            return;
        }

        if (Kind == DesignationKind.Stockpile)
        {
            Stockpiles.Mark(simulation, min, max);
            return;
        }

        var plants = PlantsInside(simulation, min, max);
        switch (Kind)
        {
            case DesignationKind.Mine:
            case DesignationKind.Stairs:
            case DesignationKind.Deconstruct:
            case DesignationKind.Channel:
                MarkCells(simulation, plants, min, max);
                break;
            case DesignationKind.Chop:
            case DesignationKind.Harvest:
                OrderPlants(simulation, plants);
                break;
            case DesignationKind.Cancel:
                Cancel(simulation, plants, min, max);
                Stockpiles.Unmark(simulation, min, max);
                Construction.Cancel(simulation, min, max);
                break;
            default:
                throw new InvalidOperationException($"Unknown designation {Kind}.");
        }

        simulation.Events.Emit(new DesignationsChangedEvent(min, max));
    }

    private static List<(EntityId Plant, TileCoord Cell)> PlantsInside(Simulation simulation, TileCoord min, TileCoord max)
    {
        var plants = new List<EntityId>();
        simulation.World.Query<Plant, Position>(plants);
        var inside = new List<(EntityId, TileCoord)>();
        foreach (var plant in plants)
        {
            var cell = simulation.World.Get<Position>(plant).Cell;
            if (cell.X >= min.X && cell.X <= max.X && cell.Y >= min.Y && cell.Y <= max.Y && cell.Z >= min.Z && cell.Z <= max.Z)
            {
                inside.Add((plant, cell));
            }
        }

        return inside;
    }

    private void MarkCells(Simulation simulation, List<(EntityId Plant, TileCoord Cell)> plants, TileCoord min, TileCoord max)
    {
        var map = simulation.Map;
        var plantCells = plants.Select(plant => plant.Cell).ToHashSet();
        var planned = Construction.OccupiedCells(simulation);
        for (var z = min.Z; z <= max.Z; z++)
        {
            for (var y = min.Y; y <= max.Y; y++)
            {
                for (var x = min.X; x <= max.X; x++)
                {
                    var cell = new TileCoord(x, y, z);
                    var order = Kind switch
                    {
                        DesignationKind.Mine when Designations.CanMine(map, cell) => TileFlags.MineDesignation,
                        DesignationKind.Stairs when Designations.CanOrderStairs(map, cell, plantCells) => TileFlags.StairsDesignation,
                        DesignationKind.Deconstruct when Construction.CanDeconstruct(simulation, cell) => TileFlags.DeconstructDesignation,
                        DesignationKind.Channel when !plantCells.Contains(cell) && Construction.CanChannel(map, cell) => TileFlags.ChannelDesignation,
                        _ => TileFlags.None,
                    };

                    // Planned cells take no orders to dig: the blueprint would lose its place.
                    if (order != TileFlags.None && (order == TileFlags.DeconstructDesignation || !planned.Contains(cell)))
                    {
                        map.SetFlags(x, y, z, map.FlagsAt(cell) | order);
                    }
                }
            }
        }
    }

    private void OrderPlants(Simulation simulation, List<(EntityId Plant, TileCoord Cell)> plants)
    {
        var world = simulation.World;
        foreach (var (plant, _) in plants)
        {
            var def = simulation.Content.Get<PlantDef>(world.Get<Plant>(plant).Def);
            if (Kind == DesignationKind.Chop && def.Chop is not null && !world.Has<ChopOrder>(plant))
            {
                world.Add(plant, new ChopOrder());
                simulation.Events.Emit(new EntityChangedEvent(plant));
            }
            else if (Kind == DesignationKind.Harvest && def.Harvest is not null && !world.Has<HarvestOrder>(plant))
            {
                world.Add(plant, new HarvestOrder());
                simulation.Events.Emit(new EntityChangedEvent(plant));
            }
        }
    }

    private static void Cancel(Simulation simulation, List<(EntityId Plant, TileCoord Cell)> plants, TileCoord min, TileCoord max)
    {
        var map = simulation.Map;
        for (var z = min.Z; z <= max.Z; z++)
        {
            for (var y = min.Y; y <= max.Y; y++)
            {
                for (var x = min.X; x <= max.X; x++)
                {
                    map.SetFlags(x, y, z, map.FlagsAt(x, y, z) & ~GameMap.OrderFlags);
                }
            }
        }

        var world = simulation.World;
        foreach (var (plant, _) in plants)
        {
            if (world.Remove<ChopOrder>(plant) | world.Remove<HarvestOrder>(plant))
            {
                simulation.Events.Emit(new EntityChangedEvent(plant));
            }
        }
    }
}

/// <summary>Rules about where orders make sense, shared by the command and the colonists' jobs.</summary>
public static class Designations
{
    /// <summary>
    /// The box between two corners, in any order, cut to the map. False if it lies wholly outside.
    /// </summary>
    public static bool TryClampBox(GameMap map, TileCoord from, TileCoord to, out TileCoord min, out TileCoord max)
    {
        ArgumentNullException.ThrowIfNull(map);
        var size = map.Size;
        min = new TileCoord(
            Math.Max(0, Math.Min(from.X, to.X)), Math.Max(0, Math.Min(from.Y, to.Y)), Math.Max(0, Math.Min(from.Z, to.Z)));
        max = new TileCoord(
            Math.Min(size.Width - 1, Math.Max(from.X, to.X)),
            Math.Min(size.Height - 1, Math.Max(from.Y, to.Y)),
            Math.Min(size.Depth - 1, Math.Max(from.Z, to.Z)));
        return min.X <= max.X && min.Y <= max.Y && min.Z <= max.Z;
    }

    /// <summary>A wall can be dug out: a natural one. What was built is taken down instead.</summary>
    public static bool CanMine(GameMap map, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(map);
        return map.ShapeAt(cell) == TileShape.Wall && !Construction.IsBuilt(map, cell);
    }

    /// <summary>The cells that hold a plant, for the rules that keep orders and arrivals off them.</summary>
    public static HashSet<TileCoord> PlantCells(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var plants = new List<EntityId>();
        simulation.World.Query<Plant, Position>(plants);
        return plants.Select(plant => simulation.World.Get<Position>(plant).Cell).ToHashSet();
    }

    /// <summary>
    /// Stairs can be ordered on this cell: they can be dug there (<see cref="CanDigStairs"/>) and no
    /// plant stands on it. What <see cref="DesignateCommand"/> accepts.
    /// </summary>
    public static bool CanOrderStairs(GameMap map, TileCoord cell, IReadOnlySet<TileCoord> plantCells)
    {
        ArgumentNullException.ThrowIfNull(plantCells);
        return !plantCells.Contains(cell) && CanDigStairs(map, cell);
    }

    /// <summary>
    /// Stairs down can start on a floor or on stairs up, above the deepest level, over a wall, a
    /// floor or stairs down. Neither the cell nor the one below can be blocked, by a tree for
    /// example, or built: what was built is taken down, not dug.
    /// </summary>
    public static bool CanDigStairs(GameMap map, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (cell.Z == 0 || !map.Size.Contains(cell.X, cell.Y, cell.Z)
            || ((map.FlagsAt(cell) | map.FlagsAt(cell.X, cell.Y, cell.Z - 1)) & TileFlags.Blocked) != 0)
        {
            return false;
        }

        var below = map.ShapeAt(cell.X, cell.Y, cell.Z - 1);
        return !Construction.IsBuilt(map, cell) && !Construction.IsBuilt(map, cell with { Z = cell.Z - 1 })
               && map.ShapeAt(cell) is TileShape.Floor or TileShape.StairUp
               && below is TileShape.Wall or TileShape.Floor or TileShape.StairDown;
    }
}
