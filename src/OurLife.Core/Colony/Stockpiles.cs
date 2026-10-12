using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>Some stockpile changed: cells added or removed, or a filter. The view draws them again.</summary>
public sealed record StockpilesChangedEvent : ISimEvent;

/// <summary>Allows or forbids an item category in a stockpile zone. A zone that no longer exists is ignored.</summary>
public sealed record SetStockpileFilterCommand(EntityId Zone, string Category, bool Allowed) : ISimCommand
{
    public void Apply(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (!simulation.World.TryGet<Stockpile>(Zone, out var zone) || !simulation.Content.TryGet<ItemCategoryDef>(Category, out _))
        {
            return;
        }

        var index = zone.Categories.BinarySearch(Category, StringComparer.Ordinal);
        if (Allowed && index < 0)
        {
            zone.Categories.Insert(~index, Category);
        }
        else if (!Allowed && index >= 0)
        {
            zone.Categories.RemoveAt(index);
        }
        else
        {
            return;
        }

        simulation.Events.Emit(new StockpilesChangedEvent());
    }
}

/// <summary>Creating and shrinking stockpile zones, for <see cref="DesignateCommand"/> (see <c>docs/gdd/almacenes.md</c>).</summary>
public static class Stockpiles
{
    /// <summary>The zone that holds a cell, or <see cref="EntityId.None"/>. Looks at every zone: for the presentation and commands.</summary>
    public static EntityId ZoneAt(Simulation simulation, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var zones = new List<EntityId>();
        simulation.World.Query<Stockpile>(zones);
        foreach (var zone in zones)
        {
            var cells = simulation.World.Get<Stockpile>(zone).Cells;
            if (cells.BinarySearch(cell, CellOrder.For(simulation.Map)) >= 0)
            {
                return zone;
            }
        }

        return EntityId.None;
    }

    /// <summary>A new zone, accepting every category, with the standable cells of the box that no zone holds.</summary>
    internal static void Mark(Simulation simulation, TileCoord min, TileCoord max)
    {
        var map = simulation.Map;
        var taken = TakenCells(simulation);
        var cells = new List<TileCoord>();
        for (var z = min.Z; z <= max.Z; z++)
        {
            for (var y = min.Y; y <= max.Y; y++)
            {
                for (var x = min.X; x <= max.X; x++)
                {
                    if (map.IsStandable(x, y, z) && !taken.Contains(map.Size.IndexOf(x, y, z)))
                    {
                        cells.Add(new TileCoord(x, y, z));
                    }
                }
            }
        }

        if (cells.Count == 0)
        {
            return;
        }

        cells.Sort(CellOrder.For(map));
        var zone = simulation.World.CreateEntity();
        simulation.World.Add(zone, new Stockpile
        {
            Cells = cells,
            Categories = [.. simulation.Content.All<ItemCategoryDef>().Select(category => category.Id)],
        });
        simulation.Events.Emit(new StockpilesChangedEvent());
    }

    /// <summary>Takes the cells of the box out of every zone; a zone left without cells is removed.</summary>
    internal static void Unmark(Simulation simulation, TileCoord min, TileCoord max)
    {
        var world = simulation.World;
        var zones = new List<EntityId>();
        world.Query<Stockpile>(zones);
        var changed = false;
        foreach (var zone in zones)
        {
            var stockpile = world.Get<Stockpile>(zone);
            var removed = stockpile.Cells.RemoveAll(cell =>
                cell.X >= min.X && cell.X <= max.X && cell.Y >= min.Y && cell.Y <= max.Y && cell.Z >= min.Z && cell.Z <= max.Z);
            if (removed == 0)
            {
                continue;
            }

            changed = true;
            if (stockpile.Cells.Count == 0)
            {
                world.DestroyEntity(zone);
            }
        }

        if (changed)
        {
            simulation.Events.Emit(new StockpilesChangedEvent());
        }
    }

    private static HashSet<int> TakenCells(Simulation simulation)
    {
        var zones = new List<EntityId>();
        simulation.World.Query<Stockpile>(zones);
        var taken = new HashSet<int>();
        foreach (var zone in zones)
        {
            foreach (var cell in simulation.World.Get<Stockpile>(zone).Cells)
            {
                taken.Add(simulation.Map.Size.IndexOf(cell.X, cell.Y, cell.Z));
            }
        }

        return taken;
    }

    /// <summary>Orders cells by their index in the map, the order zones keep their cells in.</summary>
    internal sealed class CellOrder(MapSize size) : IComparer<TileCoord>
    {
        public static CellOrder For(GameMap map) => new(map.Size);

        public int Compare(TileCoord a, TileCoord b) => size.IndexOf(a.X, a.Y, a.Z).CompareTo(size.IndexOf(b.X, b.Y, b.Z));
    }
}

/// <summary>
/// What the stockpiles hold, for the colony systems: which zone each cell belongs to and what lies
/// on each zone cell. Derived from the state (ADR-011): rebuilt when a system needs it in a tick,
/// only looked up, never iterated.
/// </summary>
internal sealed class StorageIndex
{
    private readonly Dictionary<int, EntityId> _zoneByCell = [];
    private readonly Dictionary<int, (string Item, int Count, bool Mixed)> _loadByCell = [];
    private readonly Dictionary<string, bool> _roomByItem = new(StringComparer.Ordinal);
    private readonly List<EntityId> _entities = [];

    /// <summary>Zones by ascending id, as they were when rebuilt.</summary>
    public List<EntityId> Zones { get; } = [];

    public void Rebuild(Simulation simulation)
    {
        var world = simulation.World;
        var size = simulation.Map.Size;
        _zoneByCell.Clear();
        _loadByCell.Clear();
        _roomByItem.Clear();
        world.Query<Stockpile>(Zones);
        foreach (var zone in Zones)
        {
            foreach (var cell in world.Get<Stockpile>(zone).Cells)
            {
                _zoneByCell[size.IndexOf(cell.X, cell.Y, cell.Z)] = zone;
            }
        }

        if (Zones.Count == 0)
        {
            return;
        }

        world.Query<ItemStack, Position>(_entities);
        foreach (var pile in _entities)
        {
            var cell = world.Get<Position>(pile).Cell;
            var index = size.IndexOf(cell.X, cell.Y, cell.Z);
            if (!_zoneByCell.ContainsKey(index))
            {
                continue;
            }

            var stack = world.Get<ItemStack>(pile);
            _loadByCell[index] = _loadByCell.TryGetValue(index, out var load)
                ? (load.Item, load.Count + stack.Count, load.Mixed || load.Item != stack.Def)
                : (stack.Def, stack.Count, false);
        }
    }

    /// <summary>The pile lies on a standable cell of a zone that accepts its item: hauling it makes no sense.</summary>
    public bool IsStored(Simulation simulation, EntityId pile)
    {
        var cell = simulation.World.Get<Position>(pile).Cell;
        return Accepts(simulation, cell, simulation.World.Get<ItemStack>(pile).Def) && simulation.Map.IsStandable(cell);
    }

    /// <summary>Units of the item that still fit on a zone cell; 0 off the zones, where the item is not accepted, or on a cell that cannot be stood on.</summary>
    public int SpaceFor(Simulation simulation, string item, TileCoord cell)
    {
        if (!Accepts(simulation, cell, item) || !simulation.Map.IsStandable(cell))
        {
            return 0;
        }

        var limit = simulation.Content.Get<ItemDef>(item).StackLimit;
        if (!_loadByCell.TryGetValue(simulation.Map.Size.IndexOf(cell.X, cell.Y, cell.Z), out var load))
        {
            return limit;
        }

        return load.Item == item && !load.Mixed ? Math.Max(0, limit - load.Count) : 0;
    }

    /// <summary>Some zone cell has room for the item, reserved or not: a quick way to skip piles with nowhere to go.</summary>
    public bool AnyRoomFor(Simulation simulation, string item)
    {
        if (_roomByItem.TryGetValue(item, out var room))
        {
            return room;
        }

        room = false;
        foreach (var zone in Zones)
        {
            foreach (var cell in simulation.World.Get<Stockpile>(zone).Cells)
            {
                if (SpaceFor(simulation, item, cell) > 0)
                {
                    room = true;
                    break;
                }
            }

            if (room)
            {
                break;
            }
        }

        _roomByItem[item] = room;
        return room;
    }

    private bool Accepts(Simulation simulation, TileCoord cell, string item)
    {
        if (!_zoneByCell.TryGetValue(simulation.Map.Size.IndexOf(cell.X, cell.Y, cell.Z), out var zone))
        {
            return false;
        }

        var category = simulation.Content.Get<ItemDef>(item).Category;
        return simulation.World.Get<Stockpile>(zone).Categories.BinarySearch(category, StringComparer.Ordinal) >= 0;
    }
}
