using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// Places blueprints of a buildable, of one of its materials, on the valid cells of a box, corners
/// included; only on its border if the buildable is drawn as an outline. Unknown defs, or a
/// material the buildable does not allow, are ignored. <paramref name="Rotation"/> turns furniture
/// by quarters (see <c>docs/gdd/muebles-y-camas.md</c>).
/// </summary>
public sealed record PlaceBlueprintCommand(
    [property: DefRef(typeof(BuildableDef))] string Buildable,
    [property: DefRef(typeof(MaterialDef))] string Material,
    TileCoord From,
    TileCoord To,
    int Rotation = 0) : ISimCommand
{
    public void Apply(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (!simulation.Content.TryGet<BuildableDef>(Buildable, out var buildable) || !buildable.Materials.Contains(Material))
        {
            return;
        }

        var rotation = ((Rotation % 4) + 4) % 4;
        foreach (var cell in Construction.CellsToPlan(simulation, buildable, From, To))
        {
            var blueprint = simulation.World.CreateEntity();
            simulation.World.Add(blueprint, new Position { X = cell.X, Y = cell.Y, Z = cell.Z });
            simulation.World.Add(blueprint, new Blueprint { Buildable = buildable.Id, Material = Material, Rotation = rotation });
            simulation.Events.Emit(new EntitySpawnedEvent(blueprint));
        }
    }
}

/// <summary>
/// The rules of building, taking down and of blueprints (see <c>docs/gdd/construccion.md</c>), and
/// of furniture (<c>docs/gdd/muebles-y-camas.md</c>).
/// </summary>
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

    /// <summary>
    /// The cells of the box between two corners where <see cref="PlaceBlueprintCommand"/> would put
    /// a blueprint of <paramref name="buildable"/>, in the order it would, without changing
    /// anything: the presentation shows them before the drag ends (<c>docs/gdd/vista-previa-de-muebles.md</c>).
    /// An outline takes only the border of the box; a cell a blueprint of the box already takes,
    /// like the top of stairs, is skipped; furniture stays off the stockpiles.
    /// </summary>
    public static IReadOnlyList<TileCoord> CellsToPlan(Simulation simulation, BuildableDef buildable, TileCoord from, TileCoord to)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(buildable);
        var cells = new List<TileCoord>();
        if (!Designations.TryClampBox(simulation.Map, from, to, out var min, out var max))
        {
            return cells;
        }

        var plantCells = Designations.PlantCells(simulation);
        var occupied = OccupiedCells(simulation);

        // Furniture stays off the stockpiles: piles and furniture would share the cell.
        var zoneCells = buildable.IsFurniture ? Stockpiles.TakenCells(simulation) : [];
        for (var z = min.Z; z <= max.Z; z++)
        {
            for (var y = min.Y; y <= max.Y; y++)
            {
                for (var x = min.X; x <= max.X; x++)
                {
                    var onBorder = x == min.X || x == max.X || y == min.Y || y == max.Y;
                    var cell = new TileCoord(x, y, z);
                    if ((buildable.Outline && !onBorder) || !CanPlace(simulation.Map, buildable, cell, plantCells, occupied)
                        || zoneCells.Contains(simulation.Map.Size.IndexOf(x, y, z)))
                    {
                        continue;
                    }

                    cells.Add(cell);
                    foreach (var taken in CellsOf(buildable.Shape, cell))
                    {
                        occupied.Add(taken);
                    }
                }
            }
        }

        return cells;
    }

    /// <summary>The cells the blueprints and the furniture take, so no other blueprint goes on them.</summary>
    public static HashSet<TileCoord> OccupiedCells(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var entities = new List<EntityId>();
        world.Query<Blueprint, Position>(entities);
        var cells = new HashSet<TileCoord>();
        foreach (var blueprint in entities)
        {
            var shape = simulation.Content.Get<BuildableDef>(world.Get<Blueprint>(blueprint).Buildable).Shape;
            cells.UnionWith(CellsOf(shape, world.Get<Position>(blueprint).Cell));
        }

        world.Query<Furniture, Position>(entities);
        foreach (var furniture in entities)
        {
            cells.Add(world.Get<Position>(furniture).Cell);
        }

        return cells;
    }

    /// <summary>
    /// The piece of furniture on the cell, or none. The map flags the cells with furniture, so
    /// other cells answer at once; only a flagged cell looks for its piece.
    /// </summary>
    public static EntityId FurnitureAt(Simulation simulation, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var map = simulation.Map;
        if (!map.Size.Contains(cell.X, cell.Y, cell.Z) || (map.FlagsAt(cell) & TileFlags.Furniture) == 0)
        {
            return EntityId.None;
        }

        var world = simulation.World;
        var pieces = new List<EntityId>();
        world.Query<Furniture, Position>(pieces);
        return pieces.Find(piece => world.Get<Position>(piece).Cell == cell);
    }

    /// <summary>The cells with furniture or a blueprint of furniture: no stockpile takes them.</summary>
    public static HashSet<TileCoord> FurnitureCells(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var entities = new List<EntityId>();
        var cells = new HashSet<TileCoord>();
        world.Query<Furniture, Position>(entities);
        cells.UnionWith(entities.Select(piece => world.Get<Position>(piece).Cell));
        world.Query<Blueprint, Position>(entities);
        cells.UnionWith(entities
            .Where(blueprint => simulation.Content.Get<BuildableDef>(world.Get<Blueprint>(blueprint).Buildable).IsFurniture)
            .Select(blueprint => world.Get<Position>(blueprint).Cell));
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
            BuildShape.Wall or BuildShape.Door => IsGround(map, cell),
            BuildShape.Floor => here == TileShape.Empty && HasStandableNeighbour(map, cell),
            BuildShape.Ramp => IsGround(map, cell) && map.ShapeOrEmpty(cell.X, cell.Y, cell.Z + 1) != TileShape.Wall && RampDirection(map, cell),
            BuildShape.Stairs => IsGround(map, cell) && map.ShapeOrEmpty(cell.X, cell.Y, cell.Z + 1) == TileShape.Empty
                                 && cell.Z + 1 < map.Size.Depth,
            BuildShape.Furniture => IsGround(map, cell),
            _ => false,
        };
    }

    /// <summary>
    /// A flat floor that taking down what is built on it gives back as it was: over a wall, whose
    /// material it takes again, or built itself. A natural floor left over air, as digging out the
    /// wall below leaves it, would come back as a built floor nobody paid for.
    /// </summary>
    private static bool IsGround(GameMap map, TileCoord cell) =>
        map.ShapeAt(cell) == TileShape.Floor && (IsBuilt(map, cell) || (cell.Z > 0 && map.ShapeAt(cell with { Z = cell.Z - 1 }) == TileShape.Wall));

    /// <summary>The other half of built stairs on the cell: above a way up, below a way down; null off built stairs.</summary>
    public static TileCoord? StairPartner(GameMap map, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!map.Size.Contains(cell.X, cell.Y, cell.Z) || !IsBuilt(map, cell))
        {
            return null;
        }

        var shape = map.ShapeAt(cell);
        var other = shape.GoesUp() ? cell with { Z = cell.Z + 1 } : shape.GoesDown() ? cell with { Z = cell.Z - 1 } : (TileCoord?)null;
        return other is { } partner && map.Size.Contains(partner.X, partner.Y, partner.Z) && IsBuilt(map, partner) ? partner : null;
    }

    /// <summary>
    /// The cells that taking down what was built on the cell turns into air: a floor, or the upper
    /// half of stairs. None when a piece of furniture is what goes.
    /// </summary>
    public static IEnumerable<TileCoord> CellsLeftOpen(Simulation simulation, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return FurnitureAt(simulation, cell).IsNone ? TerrainLeftOpen(simulation.Map, cell) : [];
    }

    private static IEnumerable<TileCoord> TerrainLeftOpen(GameMap map, TileCoord cell)
    {
        var shape = map.ShapeAt(cell);
        if (shape == TileShape.Floor)
        {
            yield return cell;
        }
        else if (shape is TileShape.StairDown && IsBuilt(map, cell))
        {
            yield return cell;
        }
        else if (shape.GoesUp() && StairPartner(map, cell) is { } upper && map.ShapeAt(upper) == TileShape.StairDown)
        {
            yield return upper;
        }
    }

    /// <summary>
    /// What was built on the cell can be taken down: a piece of furniture, or else the cell if it
    /// is not air, its material is a construction material and some buildable makes its shape.
    /// </summary>
    public static bool CanDeconstruct(Simulation simulation, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var map = simulation.Map;
        return map.Size.Contains(cell.X, cell.Y, cell.Z)
               && (!FurnitureAt(simulation, cell).IsNone
                   || (map.ShapeAt(cell) != TileShape.Empty && IsBuilt(map, cell) && BuildableFor(simulation.Content, map.ShapeAt(cell)) is not null));
    }

    /// <summary>What taking down the cell's furniture, or else the cell, takes: the buildable that made it.</summary>
    internal static BuildableDef BuildableToTakeDown(Simulation simulation, TileCoord cell)
    {
        var furniture = FurnitureAt(simulation, cell);
        return furniture.IsNone
            ? BuildableFor(simulation.Content, simulation.Map.ShapeAt(cell))!
            : simulation.Content.Get<BuildableDef>(simulation.World.Get<Furniture>(furniture).Buildable);
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
        var buildable = simulation.Content.Get<BuildableDef>(plan.Buildable);
        var shape = buildable.Shape;
        var material = (ushort)simulation.Content.IndexOf<MaterialDef>(plan.Material);
        var cell = world.Get<Position>(blueprint).Cell;
        Remove(simulation, blueprint, dropDelivered: false);
        if (buildable.IsFurniture)
        {
            AddFurniture(simulation, buildable, plan, cell);
            return;
        }

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
        if (FurnitureAt(simulation, cell) is { IsNone: false } furniture)
        {
            RemoveFurniture(simulation, furniture, refundPercent);
            return;
        }

        var material = map.MaterialAt(cell)!;
        var buildable = BuildableFor(simulation.Content, map.ShapeAt(cell))!;
        var leftOpen = TerrainLeftOpen(map, cell).ToList();
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

        var refund = Refund(buildable, refundPercent);
        if (refund > 0)
        {
            ItemPiles.Drop(simulation, material.BuiltFrom!, refund, map.IsStandable(cell) ? cell : worker);
        }

        foreach (var open in leftOpen)
        {
            MovePiles(simulation, open, worker);
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
    /// what they had, and the furniture left without firm ground, dropping what taking it down
    /// would give. Only the terrain counts here: orders and plants are kept off planned cells.
    /// </summary>
    internal static void RemoveSenseless(Simulation simulation, List<EntityId> buffer, int refundPercent)
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

        world.Query<Furniture, Position>(buffer);
        foreach (var furniture in buffer)
        {
            if (!IsGround(simulation.Map, world.Get<Position>(furniture).Cell))
            {
                RemoveFurniture(simulation, furniture, refundPercent);
            }
        }
    }

    /// <summary>Units taking down a buildable gives back: its cost at the percent, rounded up.</summary>
    private static int Refund(BuildableDef buildable, int refundPercent) => ((buildable.Cost * refundPercent) + 99) / 100;

    /// <summary>
    /// A finished piece of furniture on the cell: a bed starts free, and a chest is a stockpile of
    /// its cell that accepts every category.
    /// </summary>
    private static void AddFurniture(Simulation simulation, BuildableDef buildable, Blueprint plan, TileCoord cell)
    {
        var world = simulation.World;
        var map = simulation.Map;
        map.SetFlags(cell.X, cell.Y, cell.Z, map.FlagsAt(cell) | TileFlags.Furniture);
        var piece = world.CreateEntity();
        world.Add(piece, new Position { X = cell.X, Y = cell.Y, Z = cell.Z });
        world.Add(piece, new Furniture { Buildable = buildable.Id, Material = plan.Material, Rotation = plan.Rotation });
        if (buildable.RestRecoveryPercent is not null)
        {
            world.Add(piece, new Bed());
        }

        if (buildable.Storage)
        {
            world.Add(piece, new Stockpile
            {
                Cells = [cell],
                Categories = [.. simulation.Content.All<ItemCategoryDef>().Select(category => category.Id)],
            });
            simulation.Events.Emit(new StockpilesChangedEvent());
        }

        simulation.Events.Emit(new EntitySpawnedEvent(piece));
    }

    /// <summary>
    /// Takes a piece of furniture away, with any order to take it down, and gives back part of its
    /// cost on its cell, or by it if nobody can stand there. What a chest held stays on the cell, loose.
    /// </summary>
    private static void RemoveFurniture(Simulation simulation, EntityId piece, int refundPercent)
    {
        var world = simulation.World;
        var map = simulation.Map;
        var furniture = world.Get<Furniture>(piece);
        var buildable = simulation.Content.Get<BuildableDef>(furniture.Buildable);
        var cell = world.Get<Position>(piece).Cell;
        var flags = map.FlagsAt(cell);
        map.SetFlags(cell.X, cell.Y, cell.Z, flags & ~(TileFlags.Furniture | TileFlags.DeconstructDesignation));
        if ((flags & TileFlags.DeconstructDesignation) != 0)
        {
            simulation.Events.Emit(new DesignationsChangedEvent(cell, cell));
        }
        var item = simulation.Content.Get<MaterialDef>(furniture.Material).BuiltFrom!;
        var wasChest = world.Has<Stockpile>(piece);
        simulation.Events.Emit(new EntityRemovedEvent(piece));
        world.DestroyEntity(piece);
        if (wasChest)
        {
            simulation.Events.Emit(new StockpilesChangedEvent());
        }

        var refund = Refund(buildable, refundPercent);
        if (refund > 0)
        {
            ItemPiles.Drop(simulation, item, refund, StandableNear(simulation.Map, cell));
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
            var (item, count, quality, freshness) = (stack.Def, stack.Count, stack.Quality, stack.Freshness);
            ItemPiles.Take(simulation, pile, count);
            ItemPiles.Drop(simulation, item, count, to, quality, freshness);
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
