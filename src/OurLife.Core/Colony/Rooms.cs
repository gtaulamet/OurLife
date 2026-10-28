using System.Runtime.CompilerServices;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// A closed space of the colony (see "Qué es una habitación" in <c>docs/gdd/habitaciones.md</c>). It is
/// derived from the map and the furniture (ADR-011): never saved, and recalculated on demand when they
/// change. A room is a snapshot: it does not change after it was handed out, but a change to the map or
/// the furniture makes <see cref="Rooms"/> give new ones.
/// </summary>
public sealed class Room
{
    internal Room(
        int id, int z, IReadOnlyList<TileCoord> cells, bool roofed, int sizePoints, int furniturePoints, int floorPoints, int wallPoints,
        RoomLevelDef level, IReadOnlyList<RoomRoleDef> roles, IReadOnlyList<EntityId> furniture, IReadOnlyList<EntityId> beds)
    {
        Id = id;
        Z = z;
        Cells = cells;
        Roofed = roofed;
        SizePoints = sizePoints;
        FurniturePoints = furniturePoints;
        FloorPoints = floorPoints;
        WallPoints = wallPoints;
        Level = level;
        Roles = roles;
        Furniture = furniture;
        Beds = beds;
    }

    /// <summary>The index in the map of its lowest cell: it identifies the room while the map does not change.</summary>
    public int Id { get; }

    /// <summary>The level of the map (z) the room is on: a room is flat.</summary>
    public int Z { get; }

    /// <summary>Its cells, by ascending index.</summary>
    public IReadOnlyList<TileCoord> Cells { get; }

    /// <summary>Whether every cell has something solid above it. Nothing needs it yet but the spoilage of food and, later, the weather.</summary>
    public bool Roofed { get; }

    public int SizePoints { get; }

    /// <summary>The beauty of its furniture, each by the value of its quality (workshops subtract).</summary>
    public int FurniturePoints { get; }

    public int FloorPoints { get; }

    /// <summary>The beauty of the pieces of its border: walls and doors.</summary>
    public int WallPoints { get; }

    public int Score => SizePoints + FurniturePoints + FloorPoints + WallPoints;

    /// <summary>Its level of quality by score: from miserable to luxurious in the medieval content.</summary>
    public RoomLevelDef Level { get; }

    /// <summary>Its functions, in the order of the role defs.</summary>
    public IReadOnlyList<RoomRoleDef> Roles { get; }

    /// <summary>The furniture inside, by ascending id.</summary>
    public IReadOnlyList<EntityId> Furniture { get; }

    /// <summary>The beds and cradles inside, by ascending id.</summary>
    public IReadOnlyList<EntityId> Beds { get; }

    /// <summary>The cell of the room nearest to its centroid (ties to the lowest index): where the camera goes and where the icons stand.</summary>
    public TileCoord Middle
    {
        get
        {
            long sumX = 0, sumY = 0;
            foreach (var cell in Cells)
            {
                sumX += cell.X;
                sumY += cell.Y;
            }

            long count = Cells.Count;
            var best = Cells[0];
            var bestDistance = long.MaxValue;
            foreach (var cell in Cells)
            {
                long dx = (count * cell.X) - sumX, dy = (count * cell.Y) - sumY;
                var distance = (dx * dx) + (dy * dy);
                if (distance < bestDistance)
                {
                    (best, bestDistance) = (cell, distance);
                }
            }

            return best;
        }
    }

    public bool Has(string role)
    {
        foreach (var each in Roles)
        {
            if (each.Id == role)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>A chair of a dining room with a table beside it, where a colonist can sit to eat or to gather.</summary>
internal readonly record struct Seat(EntityId Chair, TileCoord Cell, Room Room);

/// <summary>The rooms of the colony changed: a wall, a door, a floor or a piece of furniture made one appear, go or change, so the presentation redraws its layer.</summary>
public sealed record RoomsChangedEvent(long Tick) : ISimEvent;

/// <summary>
/// Read-only queries about the rooms of the colony (see <c>docs/gdd/habitaciones.md</c>). A room is
/// found by searching four ways from a floor cell on its level, stopping at walls and doors; one that
/// leaks (a cell without floor, a ramp, stairs or the edge of the map) or has more cells than the
/// config allows is outdoors and is not a room. The rooms are kept until the map or the furniture
/// change, and found again on the next question.
/// </summary>
public static class Rooms
{
    private static readonly ConditionalWeakTable<Simulation, RoomIndex> Indexes = new();

    /// <summary>Whether the content has rooms at all: a room config.</summary>
    public static bool IsEnabled(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return simulation.HasMap && RoomSetup.From(simulation.Content) is not null;
    }

    /// <summary>The room that has the cell, or null if it is outdoors, a wall, a door, or there are no rooms.</summary>
    public static Room? At(Simulation simulation, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return IsEnabled(simulation) ? IndexOf(simulation).At(simulation, cell) : null;
    }

    /// <summary>
    /// The rooms with furniture or stockpile zones in them, by ascending <see cref="Room.Id"/>. Empty rooms
    /// are found with <see cref="At"/>. The list belongs to the index: copy it to keep it past a change.
    /// </summary>
    public static IReadOnlyList<Room> All(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return IsEnabled(simulation) ? IndexOf(simulation).All(simulation) : [];
    }

    /// <summary>Forgets the rooms found, so the next question finds them again. Nothing in the game needs it (the key of the index sees the changes); the tests use it to measure finding them.</summary>
    internal static void Invalidate(Simulation simulation) => IndexOf(simulation).Invalidate();

    internal static RoomIndex IndexOf(Simulation simulation) => Indexes.GetValue(simulation, _ => new RoomIndex());
}

/// <summary>The rooms of a simulation, found on demand and kept while the map and the furniture do not change.</summary>
internal sealed class RoomIndex
{
    private static readonly (int Dx, int Dy)[] Orthogonal = [(0, -1), (1, 0), (0, 1), (-1, 0)];

    private readonly List<Room> _seeded = [];
    private readonly Dictionary<int, Room> _roomByCell = [];
    private readonly HashSet<int> _outdoors = [];
    private readonly Dictionary<int, EntityId> _furnitureAt = [];
    private readonly HashSet<int> _stockpileCells = [];
    private readonly List<Seat> _seats = [];
    private readonly List<EntityId> _entities = [];
    private readonly List<EntityId> _zones = [];

    // Scratch space of the search, reused.
    private readonly List<int> _queue = [];
    private readonly HashSet<int> _visited = [];
    private readonly HashSet<int> _border = [];
    private readonly List<EntityId> _inside = [];
    private readonly List<EntityId> _insideBeds = [];

    private (int Structure, int Furniture, int Zones, long Edits)? _key;
    private long _signature;

    /// <summary>A number that tells whether the rooms with furniture are the same as the last time they were found.</summary>
    public long Signature => _signature;

    /// <summary>The <see cref="Signature"/> the <see cref="RoomSystem"/> last told the presentation about; kept here because anyone may refresh the index first.</summary>
    public long NotifiedSignature { get; set; }

    public void Invalidate() => _key = null;

    /// <summary>Brings the rooms up to date; true if the rooms with furniture are not the ones of the last time.</summary>
    public bool Refresh(Simulation simulation)
    {
        var key = KeyOf(simulation);
        if (_key == key)
        {
            return false;
        }

        Rebuild(simulation);
        _key = key;
        return true;
    }

    public Room? At(Simulation simulation, TileCoord cell)
    {
        Refresh(simulation);
        var map = simulation.Map;
        if (!map.Size.Contains(cell.X, cell.Y, cell.Z))
        {
            return null;
        }

        var index = map.Size.IndexOf(cell.X, cell.Y, cell.Z);
        if (_roomByCell.TryGetValue(index, out var known))
        {
            return known;
        }

        if (_outdoors.Contains(index) || map.ShapeAt(cell) != TileShape.Floor)
        {
            return null;
        }

        var room = Flood(simulation, cell);
        if (room is not null)
        {
            Register(simulation.Map, room);
        }

        return room;
    }

    public IReadOnlyList<Room> All(Simulation simulation)
    {
        Refresh(simulation);
        return _seeded;
    }

    /// <summary>The chairs of the dining rooms that have a table beside them, by ascending id.</summary>
    public IReadOnlyList<Seat> Seats(Simulation simulation)
    {
        Refresh(simulation);
        return _seats;
    }

    /// <summary>
    /// What the rooms depend on besides the map, as counters that cost nothing to read: the structure of the map,
    /// the pieces of furniture and the zones added or removed (the versions of their stores) and the edits in place
    /// to their fields (<see cref="ChangeStamp"/>). A deletion and an addition of the same size both bump a version.
    /// </summary>
    private (int Structure, int Furniture, int Zones, long Edits) KeyOf(Simulation simulation) =>
        (simulation.Map.StructureVersion, simulation.World.Store<Furniture>().Version, simulation.World.Store<Stockpile>().Version, ChangeStamp.Value);

    private void Rebuild(Simulation simulation)
    {
        var world = simulation.World;
        var map = simulation.Map;
        _seeded.Clear();
        _roomByCell.Clear();
        _outdoors.Clear();
        _furnitureAt.Clear();
        _stockpileCells.Clear();
        _seats.Clear();

        world.Query<Furniture, Position>(_entities);
        foreach (var piece in _entities)
        {
            var cell = world.Get<Position>(piece).Cell;
            _furnitureAt[map.Size.IndexOf(cell.X, cell.Y, cell.Z)] = piece;
        }

        world.Query<Stockpile>(_zones);
        foreach (var zone in _zones)
        {
            foreach (var cell in world.Get<Stockpile>(zone).Cells)
            {
                _stockpileCells.Add(map.Size.IndexOf(cell.X, cell.Y, cell.Z));
            }
        }

        // Every piece of furniture and every zone cell asks which room it is in; a room without either is found by At.
        foreach (var piece in _entities)
        {
            Seed(simulation, world.Get<Position>(piece).Cell);
        }

        foreach (var zone in _zones)
        {
            foreach (var cell in world.Get<Stockpile>(zone).Cells)
            {
                Seed(simulation, cell);
            }
        }

        _seeded.Sort((a, b) => a.Id.CompareTo(b.Id));
        FindSeats(simulation);
        _signature = SignatureOf();
    }

    private void Seed(Simulation simulation, TileCoord cell)
    {
        var map = simulation.Map;
        if (!map.Size.Contains(cell.X, cell.Y, cell.Z))
        {
            return;
        }

        var index = map.Size.IndexOf(cell.X, cell.Y, cell.Z);
        if (_roomByCell.ContainsKey(index) || _outdoors.Contains(index) || map.ShapeAt(cell) != TileShape.Floor)
        {
            return;
        }

        if (Flood(simulation, cell) is { } room)
        {
            Register(map, room);
            _seeded.Add(room);
        }
    }

    private void Register(GameMap map, Room room)
    {
        foreach (var cell in room.Cells)
        {
            _roomByCell[map.Size.IndexOf(cell.X, cell.Y, cell.Z)] = room;
        }
    }

    /// <summary>
    /// Searches the room of a floor cell: a room if the search ends at walls and doors with no more than the most
    /// cells the config allows; else it is outdoors, and the cells visited are known to be so.
    /// </summary>
    private Room? Flood(Simulation simulation, TileCoord seed)
    {
        var setup = RoomSetup.From(simulation.Content)!;
        var map = simulation.Map;
        var size = map.Size;
        var z = seed.Z;
        var max = setup.Config.MaxCells;

        _queue.Clear();
        _visited.Clear();
        var start = size.IndexOf(seed.X, seed.Y, z);
        _queue.Add(start);
        _visited.Add(start);
        var leaks = false;
        for (var head = 0; head < _queue.Count && !leaks; head++)
        {
            var cell = size.CoordOf(_queue[head]);
            foreach (var (dx, dy) in Orthogonal)
            {
                int nx = cell.X + dx, ny = cell.Y + dy;
                if (!size.Contains(nx, ny, z))
                {
                    leaks = true;
                    break;
                }

                switch (map.ShapeAt(nx, ny, z))
                {
                    case TileShape.Wall:
                    case TileShape.Door:
                        break;
                    case TileShape.Floor:
                        var next = size.IndexOf(nx, ny, z);
                        if (_outdoors.Contains(next))
                        {
                            leaks = true;
                        }
                        else if (_visited.Add(next))
                        {
                            _queue.Add(next);
                            leaks = _queue.Count > max;
                        }

                        break;
                    default:
                        leaks = true;
                        break;
                }

                if (leaks)
                {
                    break;
                }
            }
        }

        if (leaks || _queue.Count < setup.Config.MinCells)
        {
            // A closed space too small is not a room either, but it is not outdoors: only what leaks or is too big is remembered as such.
            if (leaks)
            {
                foreach (var index in _queue)
                {
                    _outdoors.Add(index);
                }
            }

            return null;
        }

        return Describe(simulation, setup);
    }

    /// <summary>Works out the room whose cells are in the queue.</summary>
    private Room Describe(Simulation simulation, RoomSetup setup)
    {
        var map = simulation.Map;
        var size = map.Size;
        var world = simulation.World;
        _queue.Sort();
        var z = size.CoordOf(_queue[0]).Z;
        var cells = new List<TileCoord>(_queue.Count);
        var roofed = true;
        var floorPoints = 0;
        _border.Clear();
        _inside.Clear();
        _insideBeds.Clear();
        var inStock = 0;
        foreach (var index in _queue)
        {
            var cell = size.CoordOf(index);
            cells.Add(cell);
            floorPoints += map.MaterialAt(cell)?.Beauty.Floor ?? 0;
            roofed &= size.Contains(cell.X, cell.Y, z + 1) && map.ShapeAt(cell.X, cell.Y, z + 1) is TileShape.Wall or TileShape.Floor;
            if (_stockpileCells.Contains(index))
            {
                inStock++;
            }

            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    int nx = cell.X + dx, ny = cell.Y + dy;
                    if ((dx != 0 || dy != 0) && size.Contains(nx, ny, z) && map.ShapeAt(nx, ny, z) is TileShape.Wall or TileShape.Door)
                    {
                        _border.Add(size.IndexOf(nx, ny, z));
                    }
                }
            }

            if (_furnitureAt.TryGetValue(index, out var piece))
            {
                _inside.Add(piece);
            }
        }

        var wallPoints = 0;
        foreach (var index in _border)
        {
            wallPoints += map.MaterialAt(size.CoordOf(index))?.Beauty.Wall ?? 0;
        }

        // Furniture: the beauty of each piece by the value of its quality, in hundredths until the end so a negative piece does not round away.
        var hundredths = 0L;
        _inside.Sort();
        var tagged = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var piece in _inside)
        {
            var furniture = world.Get<Furniture>(piece);
            var buildable = simulation.Content.Get<BuildableDef>(furniture.Buildable);
            hundredths += (long)buildable.Beauty * QualityRules.ValuePercentOf(simulation.Content, furniture.Quality);
            foreach (var tag in buildable.Tags)
            {
                tagged[tag] = tagged.GetValueOrDefault(tag) + 1;
            }

            if (world.Has<Bed>(piece))
            {
                _insideBeds.Add(piece);
            }
        }

        var roles = new List<RoomRoleDef>();
        foreach (var role in setup.Roles)
        {
            var holds = role.Needs.Count > 0 && role.Needs.All(need => tagged.GetValueOrDefault(need.Tag) >= need.Count);
            holds |= role.StorageCellsPercent is { } percent && (long)inStock * 100 >= (long)percent * cells.Count;
            if (holds)
            {
                roles.Add(role);
            }
        }

        var sizePoints = Math.Min(cells.Count * setup.Config.SizePointsPerCell, setup.Config.SizeCap);
        var furniturePoints = (int)(hundredths / 100);
        var score = sizePoints + furniturePoints + floorPoints + wallPoints;
        return new Room(
            _queue[0], z, cells, roofed, sizePoints, furniturePoints, floorPoints, wallPoints,
            setup.LevelFor(score), roles, [.. _inside], [.. _insideBeds]);
    }

    /// <summary>The chairs of dining rooms with a table in one of the four cells beside them.</summary>
    private void FindSeats(Simulation simulation)
    {
        var world = simulation.World;
        var map = simulation.Map;
        foreach (var room in _seeded)
        {
            if (!room.Has(RoomRules.DiningRole))
            {
                continue;
            }

            foreach (var piece in room.Furniture)
            {
                var buildable = simulation.Content.Get<BuildableDef>(world.Get<Furniture>(piece).Buildable);
                if (!buildable.Tags.Contains(RoomRules.SeatTag, StringComparer.Ordinal))
                {
                    continue;
                }

                var cell = world.Get<Position>(piece).Cell;
                foreach (var (dx, dy) in Orthogonal)
                {
                    if (_furnitureAt.TryGetValue(map.Size.IndexOf(cell.X + dx, cell.Y + dy, cell.Z), out var beside)
                        && simulation.Content.Get<BuildableDef>(world.Get<Furniture>(beside).Buildable).Tags.Contains(RoomRules.TableTag, StringComparer.Ordinal))
                    {
                        _seats.Add(new Seat(piece, cell, room));
                        break;
                    }
                }
            }
        }

        _seats.Sort((a, b) => a.Chair.CompareTo(b.Chair));
    }

    // FNV-1a over the numbers and the characters of the ids: the same in every run.
    private long SignatureOf()
    {
        if (_seeded.Count == 0)
        {
            return 0;
        }

        var hash = 14695981039346656037UL;
        hash = Mix(hash, (ulong)_seeded.Count);
        foreach (var room in _seeded)
        {
            hash = Mix(hash, (ulong)room.Id);
            hash = Mix(hash, (ulong)room.Cells.Count);
            hash = Mix(hash, (ulong)(uint)room.Score);
            hash = Mix(hash, room.Roofed ? 1UL : 0UL);
            foreach (var role in room.Roles)
            {
                foreach (var letter in role.Id)
                {
                    hash = Mix(hash, letter);
                }

                hash = Mix(hash, 0xFFFFUL);
            }

            hash = Mix(hash, 0xFFFEUL);
        }

        return (long)hash;
    }

    private static ulong Mix(ulong hash, ulong value) => (hash ^ value) * 1099511628211UL;
}
