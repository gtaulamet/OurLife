using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>Some farm zone changed: cells added or removed, or a crop. The view draws them again.</summary>
public sealed record FarmZonesChangedEvent : ISimEvent;

/// <summary>
/// Draws a farm zone with the valid cells of a box, corners included, in any order (see "Zonas de
/// cultivo" in <c>docs/gdd/agricultura.md</c>). The cells of one box form one new zone. A crop that
/// does not exist or that the colony does not know yet, and a box without valid cells, are ignored.
/// </summary>
public sealed record PlaceFarmZoneCommand(TileCoord From, TileCoord To, [property: DefRef(typeof(CropDef))] string Crop) : ISimCommand
{
    public void Apply(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (!simulation.Content.TryGet<CropDef>(Crop, out var crop) || !simulation.Unlocks.IsUnlocked(crop, simulation.Progress))
        {
            return;
        }

        var cells = FarmRules.CellsToZone(simulation, From, To);
        if (cells.Count == 0)
        {
            return;
        }

        var zone = simulation.World.CreateEntity();
        simulation.World.Add(zone, new FarmZone { Crop = Crop, Cells = [.. cells] });
        simulation.Events.Emit(new FarmZonesChangedEvent());
    }
}

/// <summary>
/// Changes the crop sown in a zone from now on; what is already sown goes on and is reaped. A zone
/// that no longer exists, a crop that does not exist or is not known yet, and the crop it has
/// already, are ignored.
/// </summary>
public sealed record SetFarmZoneCropCommand(EntityId Zone, [property: DefRef(typeof(CropDef))] string Crop) : ISimCommand
{
    public void Apply(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (!simulation.World.IsAlive(Zone) || !simulation.World.TryGet<FarmZone>(Zone, out var zone) || zone.Crop == Crop
            || !simulation.Content.TryGet<CropDef>(Crop, out var crop) || !simulation.Unlocks.IsUnlocked(crop, simulation.Progress))
        {
            return;
        }

        zone.Crop = Crop;
        simulation.Events.Emit(new FarmZonesChangedEvent());
    }
}

/// <summary>How a cell of a farm zone is.</summary>
public enum FarmCellState
{
    /// <summary>Nothing is sown: it waits for a colonist.</summary>
    Empty,

    /// <summary>A crop grows on it.</summary>
    Growing,

    /// <summary>The crop is ripe and waits to be reaped.</summary>
    Ripe,
}

/// <summary>A cell of a farm zone: its zone, the crop of the zone, what is sown on it and how far it grew.</summary>
/// <param name="Zone">The zone that holds the cell.</param>
/// <param name="ZoneCrop">Id of the crop sown in the zone from now on.</param>
/// <param name="State">How the cell is.</param>
/// <param name="Crop">The crop entity on the cell, or none.</param>
/// <param name="CropDef">Id of the crop def sown on it; null if empty.</param>
/// <param name="PercentGrown">From 0 to 100; 0 for an empty cell.</param>
public sealed record FarmCellInfo(EntityId Zone, string ZoneCrop, FarmCellState State, EntityId Crop, string? CropDef, int PercentGrown);

/// <summary>A farm zone with its counts, for the inspector.</summary>
public sealed record FarmZoneInfo(EntityId Zone, string Crop, int Cells, int Growing, int Ripe);

/// <summary>
/// The rules of the farm zones and of what grows in them (see <c>docs/gdd/agricultura.md</c>),
/// shared by the commands, the decisions and the jobs. The queries only read.
/// </summary>
public static class FarmRules
{
    /// <summary>The random stream of the fractional part of a yield.</summary>
    public const string YieldStream = "farm.yield";

    /// <summary>Every farm zone by ascending id, with how many of its cells are growing and ripe.</summary>
    public static IReadOnlyList<FarmZoneInfo> Zones(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var zones = new List<EntityId>();
        world.Query<FarmZone>(zones);
        var crops = CropsByCell(simulation);
        var result = new List<FarmZoneInfo>(zones.Count);
        foreach (var zone in zones)
        {
            var state = world.Get<FarmZone>(zone);
            var growing = 0;
            var ripe = 0;
            foreach (var cell in state.Cells)
            {
                if (crops.TryGetValue(simulation.Map.Size.IndexOf(cell.X, cell.Y, cell.Z), out var crop))
                {
                    if (IsRipe(simulation, world.Get<Crop>(crop)))
                    {
                        ripe++;
                    }
                    else
                    {
                        growing++;
                    }
                }
            }

            result.Add(new FarmZoneInfo(zone, state.Crop, state.Cells.Count, growing, ripe));
        }

        return result;
    }

    /// <summary>The state of a cell of a farm zone, or null if no zone holds it.</summary>
    public static FarmCellInfo? State(Simulation simulation, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var zone = ZoneAt(simulation, cell);
        if (zone.IsNone)
        {
            return null;
        }

        var world = simulation.World;
        var crop = CropAt(simulation, cell);
        var zoneCrop = world.Get<FarmZone>(zone).Crop;
        if (crop.IsNone)
        {
            return new FarmCellInfo(zone, zoneCrop, FarmCellState.Empty, EntityId.None, null, 0);
        }

        var state = world.Get<Crop>(crop);
        var def = simulation.Content.Get<CropDef>(state.Def);
        return new FarmCellInfo(
            zone,
            zoneCrop,
            IsRipe(simulation, state) ? FarmCellState.Ripe : FarmCellState.Growing,
            crop,
            state.Def,
            Math.Min(100, state.Days * 100 / Math.Max(1, def.GrowDays)));
    }

    /// <summary>The zone that holds a cell, or none. Looks at every zone: for the presentation and the commands.</summary>
    public static EntityId ZoneAt(Simulation simulation, TileCoord cell, List<EntityId>? buffer = null)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var zones = buffer ?? [];
        simulation.World.Query<FarmZone>(zones);
        var order = Stockpiles.CellOrder.For(simulation.Map);
        foreach (var zone in zones)
        {
            if (simulation.World.Get<FarmZone>(zone).Cells.BinarySearch(cell, order) >= 0)
            {
                return zone;
            }
        }

        return EntityId.None;
    }

    /// <summary>The crop sown on a cell, or none.</summary>
    public static EntityId CropAt(Simulation simulation, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var crops = new List<EntityId>();
        world.Query<Crop, Position>(crops);
        return crops.Find(crop => world.Get<Position>(crop).Cell == cell);
    }

    /// <summary>The crop is ripe: it has all the days of growth its def asks for.</summary>
    public static bool IsRipe(Simulation simulation, Crop crop)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(crop);
        return crop.Days >= simulation.Content.Get<CropDef>(crop.Def).GrowDays;
    }

    /// <summary>
    /// The cells of the box between two corners where <see cref="PlaceFarmZoneCommand"/> would
    /// draw a zone, sorted by map index, without changing anything: the presentation shows them
    /// before the drag ends. A cell is valid if it is a flat floor of natural soil with open air
    /// above, that nothing takes: no plant, no order, no furniture or blueprint, no stockpile and
    /// no other farm zone.
    /// </summary>
    public static IReadOnlyList<TileCoord> CellsToZone(Simulation simulation, TileCoord from, TileCoord to)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var cells = new List<TileCoord>();
        if (!Designations.TryClampBox(simulation.Map, from, to, out var min, out var max))
        {
            return cells;
        }

        var map = simulation.Map;
        var taken = TakenByOthers(simulation);
        for (var z = min.Z; z <= max.Z; z++)
        {
            for (var y = min.Y; y <= max.Y; y++)
            {
                for (var x = min.X; x <= max.X; x++)
                {
                    if (IsNaturalSoilFloor(map, x, y, z) && !taken.Contains(map.Size.IndexOf(x, y, z)))
                    {
                        cells.Add(new TileCoord(x, y, z));
                    }
                }
            }
        }

        cells.Sort(Stockpiles.CellOrder.For(map));
        return cells;
    }

    /// <summary>
    /// A flat floor of natural soil, free to walk on, with nothing ordered on it and open air
    /// above. What the terrain decides; the things on the cell are looked at apart.
    /// </summary>
    public static bool IsNaturalSoilFloor(GameMap map, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!map.Size.Contains(x, y, z) || map.ShapeAt(x, y, z) != TileShape.Floor || !map.IsStandable(x, y, z)
            || (map.FlagsAt(x, y, z) & (GameMap.OrderFlags | TileFlags.Furniture)) != 0 || map.ShapeOrEmpty(x, y, z + 1) != TileShape.Empty)
        {
            return false;
        }

        return map.MaterialAt(x, y, z) is { Kind: MaterialKind.Soil, IsBuilt: false };
    }

    /// <summary>The map indexes of the cells another thing takes: stockpiles, farm zones, blueprints, furniture and plants.</summary>
    internal static HashSet<int> TakenByOthers(Simulation simulation)
    {
        var size = simulation.Map.Size;
        var taken = Stockpiles.TakenCells(simulation);
        taken.UnionWith(FarmZones.TakenCells(simulation));
        foreach (var cell in Construction.OccupiedCells(simulation))
        {
            taken.Add(size.IndexOf(cell.X, cell.Y, cell.Z));
        }

        foreach (var cell in Designations.PlantCells(simulation))
        {
            taken.Add(size.IndexOf(cell.X, cell.Y, cell.Z));
        }

        return taken;
    }

    /// <summary>
    /// Whether a crop sown today is ripe before its growing seasons end: the days of growth that
    /// remain after today, in a row, must be at least its <see cref="CropDef.GrowDays"/>. Nothing is
    /// sown in a season the crop does not grow in, and a tree has no such limit (see "Sembrar" in
    /// <c>docs/gdd/agricultura.md</c>).
    /// </summary>
    public static bool HasTimeToMature(Simulation simulation, CropDef crop)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(crop);
        if (crop.IsTree)
        {
            return true;
        }

        var time = simulation.Time;
        var date = simulation.CurrentDate;
        if (!crop.GrowSeasons.Contains(date.SeasonId))
        {
            return false;
        }

        var today = (date.SeasonIndex * time.DaysPerSeason) + date.Day - 1;
        var left = 0;
        for (var ahead = 1; ahead <= time.DaysPerYear && left < crop.GrowDays; ahead++)
        {
            if (!crop.GrowSeasons.Contains(time.Seasons[(today + ahead) % time.DaysPerYear / time.DaysPerSeason]))
            {
                break;
            }

            left++;
        }

        return left >= crop.GrowDays;
    }

    /// <summary>
    /// Units a reaper gets from a ripe crop: <c>count × (base + per level × level) / 100</c>, with
    /// the fractional part drawn from the stream <see cref="YieldStream"/>.
    /// </summary>
    public static int Yield(Simulation simulation, CropDef crop, int skillLevel)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(crop);
        var percent = crop.YieldBasePercent + (crop.YieldPercentPerLevel * Math.Max(0, skillLevel));
        var total = (long)crop.Count * percent;
        var units = (int)(total / 100);
        var rest = (int)(total % 100);
        if (rest > 0 && simulation.Random.Get(YieldStream).NextInt(100) < rest)
        {
            units++;
        }

        return units;
    }

    /// <summary>The number of cells of the farm zones whose crop gives food, sown or not.</summary>
    public static int FoodCropCells(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var zones = new List<EntityId>();
        world.Query<FarmZone>(zones);
        var cells = 0;
        foreach (var zone in zones)
        {
            var state = world.Get<FarmZone>(zone);
            if (simulation.Content.TryGet<CropDef>(state.Crop, out var crop) && crop.Item is not null)
            {
                cells += state.Cells.Count;
            }
        }

        return cells;
    }

    /// <summary>The crop entities by the map index of their cell, found once.</summary>
    internal static Dictionary<int, EntityId> CropsByCell(Simulation simulation)
    {
        var world = simulation.World;
        var crops = new List<EntityId>();
        world.Query<Crop, Position>(crops);
        var byCell = new Dictionary<int, EntityId>(crops.Count);
        foreach (var crop in crops)
        {
            var cell = world.Get<Position>(crop).Cell;
            byCell[simulation.Map.Size.IndexOf(cell.X, cell.Y, cell.Z)] = crop;
        }

        return byCell;
    }

    /// <summary>Sows a cell of a zone: a crop that grows, or a sapling for a tree.</summary>
    internal static void Sow(Simulation simulation, CropDef crop, TileCoord cell)
    {
        var world = simulation.World;
        var entity = world.CreateEntity();
        world.Add(entity, new Position { X = cell.X, Y = cell.Y, Z = cell.Z });
        if (crop.Tree is { } tree)
        {
            var days = simulation.Content.Get<PlantDef>(tree).Regrowth?.GrowDays ?? 1;
            world.Add(entity, new Plant { Def = tree });
            world.Add(entity, new Sapling { GrowsAtTick = simulation.CurrentTick + ((long)days * simulation.Time.TicksPerDay) });
        }
        else
        {
            world.Add(entity, new Crop { Def = crop.Id });
        }

        simulation.Events.Emit(new EntitySpawnedEvent(entity));
    }
}

/// <summary>Shrinking and measuring the farm zones, for <see cref="DesignateCommand"/> and the rules of what goes where.</summary>
internal static class FarmZones
{
    /// <summary>Every cell of a farm zone.</summary>
    public static HashSet<TileCoord> Cells(Simulation simulation)
    {
        var zones = new List<EntityId>();
        simulation.World.Query<FarmZone>(zones);
        var cells = new HashSet<TileCoord>();
        foreach (var zone in zones)
        {
            cells.UnionWith(simulation.World.Get<FarmZone>(zone).Cells);
        }

        return cells;
    }

    /// <summary>The map indexes of every cell of a farm zone.</summary>
    public static HashSet<int> TakenCells(Simulation simulation)
    {
        var zones = new List<EntityId>();
        simulation.World.Query<FarmZone>(zones);
        var taken = new HashSet<int>();
        foreach (var zone in zones)
        {
            foreach (var cell in simulation.World.Get<FarmZone>(zone).Cells)
            {
                taken.Add(simulation.Map.Size.IndexOf(cell.X, cell.Y, cell.Z));
            }
        }

        return taken;
    }

    /// <summary>
    /// Takes the cells of the box out of every zone, with the crops sown on them; a zone left
    /// without cells is removed.
    /// </summary>
    public static void Unmark(Simulation simulation, TileCoord min, TileCoord max)
    {
        var world = simulation.World;
        var zones = new List<EntityId>();
        world.Query<FarmZone>(zones);
        var changed = false;
        foreach (var zone in zones)
        {
            var state = world.Get<FarmZone>(zone);
            var removed = state.Cells.FindAll(cell => Inside(cell, min, max));
            if (removed.Count == 0)
            {
                continue;
            }

            changed = true;
            state.Cells.RemoveAll(cell => Inside(cell, min, max));
            RemoveCrops(simulation, removed);
            if (state.Cells.Count == 0)
            {
                world.DestroyEntity(zone);
            }
        }

        if (changed)
        {
            simulation.Events.Emit(new FarmZonesChangedEvent());
        }
    }

    /// <summary>Drops the cells that stopped being a floor from every zone, and their crops; true if any went.</summary>
    public static bool Prune(Simulation simulation)
    {
        var world = simulation.World;
        var map = simulation.Map;
        var zones = new List<EntityId>();
        world.Query<FarmZone>(zones);
        var changed = false;
        foreach (var zone in zones)
        {
            var state = world.Get<FarmZone>(zone);
            var lost = state.Cells.FindAll(cell => map.ShapeAt(cell) != TileShape.Floor);
            if (lost.Count == 0)
            {
                continue;
            }

            changed = true;
            state.Cells.RemoveAll(cell => map.ShapeAt(cell) != TileShape.Floor);
            RemoveCrops(simulation, lost);
            if (state.Cells.Count == 0)
            {
                world.DestroyEntity(zone);
            }
        }

        return changed;
    }

    private static void RemoveCrops(Simulation simulation, List<TileCoord> cells)
    {
        var world = simulation.World;
        var crops = new List<EntityId>();
        var gone = new HashSet<TileCoord>(cells);
        world.Query<Crop, Position>(crops);
        foreach (var crop in crops)
        {
            if (gone.Contains(world.Get<Position>(crop).Cell))
            {
                simulation.Events.Emit(new EntityRemovedEvent(crop));
                world.DestroyEntity(crop);
            }
        }

        // The saplings of a grove go with the zone; a tree that has grown already is the village's.
        var saplings = new List<EntityId>();
        world.Query<Sapling, Position>(saplings);
        foreach (var sapling in saplings)
        {
            if (gone.Contains(world.Get<Position>(sapling).Cell))
            {
                simulation.Events.Emit(new EntityRemovedEvent(sapling));
                world.DestroyEntity(sapling);
            }
        }
    }

    private static bool Inside(TileCoord cell, TileCoord min, TileCoord max) =>
        cell.X >= min.X && cell.X <= max.X && cell.Y >= min.Y && cell.Y <= max.Y && cell.Z >= min.Z && cell.Z <= max.Z;
}
