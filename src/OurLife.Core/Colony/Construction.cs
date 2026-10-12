using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// Places blueprints of a buildable, of one of its materials, on the valid cells of a box, corners
/// included; only on its border if the buildable is drawn as an outline. Unknown defs, or a
/// material the buildable does not allow, are ignored.
/// </summary>
public sealed record PlaceBlueprintCommand(string Buildable, string Material, TileCoord From, TileCoord To) : ISimCommand
{
    public void Apply(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (!simulation.Content.TryGet<BuildableDef>(Buildable, out var buildable) || !buildable.Materials.Contains(Material)
            || !Designations.TryClampBox(simulation.Map, From, To, out var min, out var max))
        {
            return;
        }

        var plantCells = Designations.PlantCells(simulation);
        var occupied = Construction.OccupiedCells(simulation);
        for (var z = min.Z; z <= max.Z; z++)
        {
            for (var y = min.Y; y <= max.Y; y++)
            {
                for (var x = min.X; x <= max.X; x++)
                {
                    var onBorder = x == min.X || x == max.X || y == min.Y || y == max.Y;
                    var cell = new TileCoord(x, y, z);
                    if ((buildable.Outline && !onBorder) || !Construction.CanPlace(simulation.Map, buildable, cell, plantCells, occupied))
                    {
                        continue;
                    }

                    var blueprint = simulation.World.CreateEntity();
                    simulation.World.Add(blueprint, new Position { X = x, Y = y, Z = z });
                    simulation.World.Add(blueprint, new Blueprint { Buildable = buildable.Id, Material = Material });
                    simulation.Events.Emit(new EntitySpawnedEvent(blueprint));
                    foreach (var taken in Construction.CellsOf(buildable.Shape, cell))
                    {
                        occupied.Add(taken);
                    }
                }
            }
        }
    }
}

/// <summary>The rules of building, taking down and of blueprints (see <c>docs/gdd/construccion.md</c>).</summary>
public static class Construction
{
    /// <summary>The cell was built, not found: its material is a construction material.</summary>
    public static bool IsBuilt(GameMap map, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(map);
        return map.MaterialAt(cell)?.IsBuilt == true;
    }

    /// <summary>The buildable that leaves this shape on the map, the first by id; null if none does.</summary>
    public static BuildableDef? BuildableFor(DefDatabase content, TileShape shape)
    {
        ArgumentNullException.ThrowIfNull(content);
        BuildShape? wanted = shape switch
        {
            TileShape.Wall => BuildShape.Wall,
            TileShape.Floor => BuildShape.Floor,
            TileShape.Door => BuildShape.Door,
            TileShape.Ramp => BuildShape.Ramp,
            TileShape.StairUp or TileShape.StairDown or TileShape.StairUpDown => BuildShape.Stairs,
            _ => null,
        };
        return wanted is null ? null : content.All<BuildableDef>().FirstOrDefault(buildable => buildable.Shape == wanted);
    }

    /// <summary>The buildables in the order of the build bar: by <see cref="BuildableDef.Order"/>, then id.</summary>
    public static IReadOnlyList<BuildableDef> BuildablesInOrder(DefDatabase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return [.. content.All<BuildableDef>().OrderBy(buildable => buildable.Order).ThenBy(buildable => buildable.Id, StringComparer.Ordinal)];
    }

    /// <summary>The cells a blueprint takes: its own, and the one above for stairs.</summary>
    public static IEnumerable<TileCoord> CellsOf(BuildShape shape, TileCoord cell)
    {
        yield return cell;
        if (shape == BuildShape.Stairs)
        {
            yield return cell with { Z = cell.Z + 1 };
        }
    }

    /// <summary>The cells the blueprints take, so no other blueprint goes on them.</summary>
    public static HashSet<TileCoord> OccupiedCells(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var blueprints = new List<EntityId>();
        world.Query<Blueprint, Position>(blueprints);
        var cells = new HashSet<TileCoord>();
        foreach (var blueprint in blueprints)
        {
            var shape = simulation.Content.Get<BuildableDef>(world.Get<Blueprint>(blueprint).Buildable).Shape;
            cells.UnionWith(CellsOf(shape, world.Get<Position>(blueprint).Cell));
        }

        return cells;
    }

    /// <summary>
    /// A blueprint of <paramref name="buildable"/> makes sense on the cell: nothing else is planned
    /// or ordered there, no plant grows there and the shape fits the place.
    /// </summary>
    public static bool CanPlace(GameMap map, BuildableDef buildable, TileCoord cell, IReadOnlySet<TileCoord> plantCells, IReadOnlySet<TileCoord> occupied)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(buildable);
        ArgumentNullException.ThrowIfNull(plantCells);
        ArgumentNullException.ThrowIfNull(occupied);
        foreach (var taken in CellsOf(buildable.Shape, cell))
        {
            if (!map.Size.Contains(taken.X, taken.Y, taken.Z) || occupied.Contains(taken) || plantCells.Contains(taken)
                || (map.FlagsAt(taken) & (GameMap.OrderFlags | TileFlags.Blocked)) != 0)
            {
                return false;
            }
        }

        return FitsShape(map, buildable.Shape, cell);
    }

    /// <summary>The place suits the shape, whatever else is on it: the part of <see cref="CanPlace"/> that the terrain decides.</summary>
    internal static bool FitsShape(GameMap map, BuildShape shape, TileCoord cell)
    {
        var here = map.ShapeAt(cell);
        return shape switch
        {
            BuildShape.Wall or BuildShape.Door => here == TileShape.Floor,
            BuildShape.Floor => here == TileShape.Empty && HasStandableNeighbour(map, cell),
            BuildShape.Ramp => here == TileShape.Floor && map.ShapeOrEmpty(cell.X, cell.Y, cell.Z + 1) != TileShape.Wall && RampDirection(map, cell),
            BuildShape.Stairs => here == TileShape.Floor && map.ShapeOrEmpty(cell.X, cell.Y, cell.Z + 1) == TileShape.Empty
                                 && cell.Z + 1 < map.Size.Depth,
            _ => false,
        };
    }

    /// <summary>
    /// What was built on the cell can be taken down: it is not air, its material is a construction
    /// material and some buildable makes its shape.
    /// </summary>
    public static bool CanDeconstruct(DefDatabase content, GameMap map, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(map);
        return map.Size.Contains(cell.X, cell.Y, cell.Z) && map.ShapeAt(cell) != TileShape.Empty && IsBuilt(map, cell)
               && BuildableFor(content, map.ShapeAt(cell)) is not null;
    }

    /// <summary>
    /// A channel can be dug here: a natural flat floor above the deepest level, that nothing blocks,
    /// over a wall or a floor. A wall below has to be natural too, since it turns into a ramp.
    /// </summary>
    public static bool CanChannel(GameMap map, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (cell.Z == 0 || !map.Size.Contains(cell.X, cell.Y, cell.Z) || map.ShapeAt(cell) != TileShape.Floor
            || IsBuilt(map, cell) || (map.FlagsAt(cell) & TileFlags.Blocked) != 0)
        {
            return false;
        }

        var below = cell with { Z = cell.Z - 1 };
        return map.ShapeAt(below) switch
        {
            TileShape.Wall => !IsBuilt(map, below),
            TileShape.Floor => true,
            _ => false,
        };
    }

    /// <summary>
    /// Finishes a blueprint: the cell takes its shape and material, loses its grass, and piles left
    /// on a cell nobody can stand on any more fall on the builder's.
    /// </summary>
    internal static void Finish(Simulation simulation, EntityId blueprint, TileCoord builder)
    {
        var world = simulation.World;
        var map = simulation.Map;
        var plan = world.Get<Blueprint>(blueprint);
        var shape = simulation.Content.Get<BuildableDef>(plan.Buildable).Shape;
        var material = (ushort)simulation.Content.IndexOf<MaterialDef>(plan.Material);
        var cell = world.Get<Position>(blueprint).Cell;
        Remove(simulation, blueprint, dropDelivered: false);

        var result = shape switch
        {
            BuildShape.Wall => TileShape.Wall,
            BuildShape.Floor => TileShape.Floor,
            BuildShape.Door => TileShape.Door,
            BuildShape.Ramp => TileShape.Ramp,
            BuildShape.Stairs => TileShape.StairUp,
            _ => throw new InvalidOperationException($"Unknown shape {shape}."),
        };
        Set(simulation, cell, result, material);
        if (shape == BuildShape.Stairs)
        {
            Set(simulation, cell with { Z = cell.Z + 1 }, TileShape.StairDown, material);
        }

        if (!map.IsStandable(cell))
        {
            MovePiles(simulation, cell, builder);
        }
    }

    /// <summary>
    /// Takes down what was built on the cell and gives back part of its cost (see
    /// <see cref="AiConfigDef.DeconstructRefundPercent"/>), on the cell or, if nobody can stand
    /// there afterwards, on the worker's.
    /// </summary>
    internal static void TakeDown(Simulation simulation, TileCoord cell, TileCoord worker, int refundPercent)
    {
        var map = simulation.Map;
        var material = map.MaterialAt(cell)!;
        var buildable = BuildableFor(simulation.Content, map.ShapeAt(cell))!;
        switch (map.ShapeAt(cell))
        {
            case TileShape.Wall:
            case TileShape.Door:
            case TileShape.Ramp:
                SetGround(simulation, cell);
                break;
            case TileShape.Floor:
                Set(simulation, cell, TileShape.Empty, GameMap.NoMaterial);
                break;
            default:
                // Stairs come in pairs: the way up from the lower cell and the way down from the upper one.
                var lower = map.ShapeAt(cell).GoesUp() ? cell : cell with { Z = cell.Z - 1 };
                var upper = lower with { Z = lower.Z + 1 };
                if (map.Size.Contains(lower.X, lower.Y, lower.Z) && IsBuilt(map, lower) && map.ShapeAt(lower).GoesUp())
                {
                    if (map.ShapeAt(lower) == TileShape.StairUp)
                    {
                        SetGround(simulation, lower);
                    }
                    else
                    {
                        Set(simulation, lower, TileShape.StairDown, map.MaterialIndexAt(lower.X, lower.Y, lower.Z));
                    }
                }

                if (map.Size.Contains(upper.X, upper.Y, upper.Z) && IsBuilt(map, upper) && map.ShapeAt(upper).GoesDown())
                {
                    var keepsWayUp = map.ShapeAt(upper) == TileShape.StairUpDown;
                    Set(simulation, upper, keepsWayUp ? TileShape.StairUp : TileShape.Empty,
                        keepsWayUp ? map.MaterialIndexAt(upper.X, upper.Y, upper.Z) : GameMap.NoMaterial);
                }

                break;
        }

        var refund = ((buildable.Cost * refundPercent) + 99) / 100;
        if (refund > 0)
        {
            ItemPiles.Drop(simulation, material.BuiltFrom!, refund, map.IsStandable(cell) ? cell : worker);
        }

        if (!map.IsStandable(cell))
        {
            MovePiles(simulation, cell, worker);
        }
    }

    /// <summary>Takes away the blueprints in a box, corners included; what they had delivered stays on their cells.</summary>
    internal static void Cancel(Simulation simulation, TileCoord min, TileCoord max)
    {
        var world = simulation.World;
        var blueprints = new List<EntityId>();
        world.Query<Blueprint, Position>(blueprints);
        foreach (var blueprint in blueprints)
        {
            var cell = world.Get<Position>(blueprint).Cell;
            if (cell.X >= min.X && cell.X <= max.X && cell.Y >= min.Y && cell.Y <= max.Y && cell.Z >= min.Z && cell.Z <= max.Z)
            {
                Remove(simulation, blueprint, dropDelivered: true);
            }
        }
    }

    /// <summary>
    /// Takes away the blueprints that stopped making sense because the terrain changed, dropping
    /// what they had. Only the terrain counts here: orders and plants are kept off planned cells.
    /// </summary>
    internal static void RemoveSenseless(Simulation simulation, List<EntityId> buffer)
    {
        var world = simulation.World;
        world.Query<Blueprint, Position>(buffer);
        foreach (var blueprint in buffer)
        {
            var plan = world.Get<Blueprint>(blueprint);
            var cell = world.Get<Position>(blueprint).Cell;
            var shape = simulation.Content.Get<BuildableDef>(plan.Buildable).Shape;
            if (!FitsShape(simulation.Map, shape, cell))
            {
                Remove(simulation, blueprint, dropDelivered: true);
            }
        }
    }

    /// <summary>The item a blueprint is built from, and how many units it still lacks.</summary>
    internal static (string Item, int Missing) Needs(Simulation simulation, Blueprint plan)
    {
        var item = simulation.Content.Get<MaterialDef>(plan.Material).BuiltFrom!;
        return (item, Math.Max(0, simulation.Content.Get<BuildableDef>(plan.Buildable).Cost - plan.Delivered));
    }

    private static void Remove(Simulation simulation, EntityId blueprint, bool dropDelivered)
    {
        var world = simulation.World;
        var plan = world.Get<Blueprint>(blueprint);
        var cell = world.Get<Position>(blueprint).Cell;
        simulation.Events.Emit(new EntityRemovedEvent(blueprint));
        world.DestroyEntity(blueprint);
        if (dropDelivered && plan.Delivered > 0)
        {
            ItemPiles.Drop(simulation, simulation.Content.Get<MaterialDef>(plan.Material).BuiltFrom!, plan.Delivered, StandableNear(simulation.Map, cell));
        }
    }

    /// <summary>The cell if it can be stood on, or else its first standable neighbour on the level; the cell itself if there is none.</summary>
    private static TileCoord StandableNear(GameMap map, TileCoord cell)
    {
        if (map.IsStandable(cell))
        {
            return cell;
        }

        foreach (var (dx, dy) in MapNavigation.Directions)
        {
            var near = new TileCoord(cell.X + dx, cell.Y + dy, cell.Z);
            if (map.IsStandable(near))
            {
                return near;
            }
        }

        return cell;
    }

    /// <summary>
    /// Turns the cell back into a floor: of the wall below, like the valley's floors, or of its
    /// own material if there is no wall below.
    /// </summary>
    private static void SetGround(Simulation simulation, TileCoord cell)
    {
        var map = simulation.Map;
        var below = cell with { Z = cell.Z - 1 };
        var material = cell.Z > 0 && map.ShapeAt(below) == TileShape.Wall
            ? map.MaterialIndexAt(below.X, below.Y, below.Z)
            : map.MaterialIndexAt(cell.X, cell.Y, cell.Z);
        Set(simulation, cell, TileShape.Floor, material);
    }

    private static void Set(Simulation simulation, TileCoord cell, TileShape shape, ushort material)
    {
        var map = simulation.Map;
        const TileFlags cleared = TileFlags.Grass | TileFlags.DeconstructDesignation;
        var flags = map.FlagsAt(cell);
        map.SetTile(cell.X, cell.Y, cell.Z, shape, material, flags & ~cleared);
        if ((flags & TileFlags.DeconstructDesignation) != 0)
        {
            simulation.Events.Emit(new DesignationsChangedEvent(cell, cell));
        }
    }

    /// <summary>Moves every pile on a cell to another, as a load that falls there.</summary>
    internal static void MovePiles(Simulation simulation, TileCoord from, TileCoord to)
    {
        var world = simulation.World;
        var piles = new List<EntityId>();
        world.Query<ItemStack, Position>(piles);
        foreach (var pile in piles)
        {
            if (world.Get<Position>(pile).Cell != from)
            {
                continue;
            }

            var stack = world.Get<ItemStack>(pile);
            var (item, count) = (stack.Def, stack.Count);
            ItemPiles.Take(simulation, pile, count);
            ItemPiles.Drop(simulation, item, count, to);
        }
    }

    private static bool HasStandableNeighbour(GameMap map, TileCoord cell)
    {
        for (var i = 0; i < 4; i++)
        {
            var (dx, dy) = MapNavigation.Directions[i];
            if (map.IsStandable(cell.X + dx, cell.Y + dy, cell.Z))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Some side of the cell (north, east, south or west) is a wall with open space above: the ramp leads up there.</summary>
    private static bool RampDirection(GameMap map, TileCoord cell)
    {
        for (var i = 0; i < 4; i++)
        {
            var (dx, dy) = MapNavigation.Directions[i];
            if (map.ShapeOrEmpty(cell.X + dx, cell.Y + dy, cell.Z) == TileShape.Wall
                && map.ShapeOrEmpty(cell.X + dx, cell.Y + dy, cell.Z + 1) != TileShape.Wall)
            {
                return true;
            }
        }

        return false;
    }
}
