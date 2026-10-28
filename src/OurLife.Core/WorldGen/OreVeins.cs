using OurLife.Core.Defs;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.WorldGen;

/// <summary>
/// Hides veins of ore in the rock of a map (<c>docs/gdd/subsuelo.md</c>): when the valley is
/// generated, and when a game saved before the veins existed is migrated. Veins are drawn from the
/// stream <see cref="RandomStream"/>, in the order of the <see cref="OreVeinDef"/>s (by id) and of
/// the veins; nothing here iterates a hash set or a dictionary, so the same seed and content
/// always give the same veins.
/// </summary>
public static class OreVeins
{
    /// <summary>Name of the random stream of the veins.</summary>
    public const string RandomStream = "world.ore";

    // Veins grow wide, not deep: no further than this many levels above or below the cell they start in.
    private const int Thickness = 1;

    /// <summary>The middle of the valley on the plane, which <see cref="OutcropSpec.Radius"/> is measured from.</summary>
    public static (int X, int Y) ValleyCenter(MapSize size) => (size.Width / 2, size.Height / 2);

    /// <summary>What is wrong with the vein defs against the content, for the generator to refuse before it draws anything.</summary>
    internal static IEnumerable<string> Problems(DefDatabase content)
    {
        foreach (var def in content.All<OreVeinDef>())
        {
            if (content.Get<MaterialDef>(def.Material).Kind != MaterialKind.Ore)
            {
                yield return $"OreVein '{def.Id}': material '{def.Material}' is not of kind ore.";
            }

            foreach (var host in def.Hosts)
            {
                if (content.Get<MaterialDef>(host) is { IsBuilt: true } or { Kind: MaterialKind.Ore })
                {
                    yield return $"OreVein '{def.Id}': host '{host}' is not a natural rock.";
                }
            }
        }
    }

    /// <summary>
    /// Generates the veins of a new valley, once the strata are laid and the discovered cells are known.
    /// A vein starts at least two levels under the surface, and if fewer veins than the def asks for
    /// can be seen from the middle of the valley, the first ones that cannot are redrawn from the rock that can.
    /// </summary>
    /// <exception cref="InvalidOperationException">There is no rock in sight within the radius: the player could not find the ore.</exception>
    internal static void Generate(Simulation simulation, GameMap map, int[] surface)
    {
        foreach (var def in simulation.Content.All<OreVeinDef>())
        {
            var placer = new Placer(simulation, map, def, surface, hiddenOnly: false);
            placer.PlaceVeins();
            placer.EnsureOutcrop();
        }
    }

    /// <summary>
    /// Gives a game saved before the veins existed (format 16 or before) veins in the rock that
    /// stayed hidden, with the same numbers as a new valley and no outcrop: the discovered cells are
    /// already worked out. Fewer veins are placed if there is no room for them.
    /// </summary>
    internal static void Migrate(Simulation simulation)
    {
        foreach (var def in simulation.Content.All<OreVeinDef>())
        {
            new Placer(simulation, simulation.Map, def, surface: null, hiddenOnly: true).PlaceVeins();
        }
    }

    /// <summary>A placed vein: its cells, the rock each one replaced and the size it was drawn with.</summary>
    private sealed record Vein(int[] Cells, ushort[] Rock, int Target);

    private sealed class Placer
    {
        private readonly GameMap _map;
        private readonly OreVeinDef _def;
        private readonly Pcg32 _random;
        private readonly int[]? _surface;
        private readonly bool _hiddenOnly;
        private readonly ushort[] _hosts;
        private readonly ushort _material;
        private readonly List<Vein> _veins = [];
        private readonly List<int> _cells = [];
        private readonly List<int> _frontier = [];

        public Placer(Simulation simulation, GameMap map, OreVeinDef def, int[]? surface, bool hiddenOnly)
        {
            _map = map;
            _def = def;
            _random = simulation.Random.Get(RandomStream);
            _surface = surface;
            _hiddenOnly = hiddenOnly;
            _hosts = [.. def.Hosts.Select(host => (ushort)simulation.Content.IndexOf<MaterialDef>(host))];
            _material = (ushort)simulation.Content.IndexOf<MaterialDef>(def.Material);
        }

        /// <summary>Draws how many veins there are and places them, each at a random start; one that finds no room is skipped.</summary>
        public void PlaceVeins()
        {
            var count = _random.NextInt(_def.Veins.Min, _def.Veins.Max + 1);
            for (var i = 0; i < count; i++)
            {
                var target = _random.NextInt(_def.Cells.Min, _def.Cells.Max + 1);
                for (var attempt = 0; attempt < _def.Attempts; attempt++)
                {
                    var size = _map.Size;
                    int x = _random.NextInt(size.Width), y = _random.NextInt(size.Height), z = _random.NextInt(size.Depth);
                    if (CanStart(x, y, z) && Grow(size.IndexOf(x, y, z), target) >= _def.MinFit)
                    {
                        _veins.Add(Commit(target));
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// While fewer veins than the def asks for have a discovered cell near the middle of the
        /// valley, redraws the first one that has none from the rock that is already in sight there.
        /// </summary>
        public void EnsureOutcrop()
        {
            while (_veins.Count(InSight) < _def.Outcrop.Veins)
            {
                var index = _veins.FindIndex(vein => !InSight(vein));
                if (index < 0)
                {
                    throw NoOutcrop();
                }

                // Give the rock back, then start the vein again where the player can see it.
                var old = _veins[index];
                Restore(old);
                var starts = RockInSight();
                Vein? replaced = null;
                for (var attempt = 0; attempt < _def.Attempts && starts.Count > 0 && replaced is null; attempt++)
                {
                    var start = starts[_random.NextInt(starts.Count)];
                    if (Grow(start, old.Target) >= _def.MinFit)
                    {
                        replaced = Commit(old.Target);
                    }
                }

                if (replaced is null)
                {
                    throw NoOutcrop();
                }

                _veins[index] = replaced;
            }
        }

        private InvalidOperationException NoOutcrop() => new(
            $"OreVein '{_def.Id}': no rock of {string.Join(", ", _def.Hosts)} is in sight within {_def.Outcrop.Radius} cells of the middle of the valley.");

        private bool IsHost(ushort material) => Array.IndexOf(_hosts, material) >= 0;

        private bool CanGrow(int x, int y, int z) =>
            _map.Size.Contains(x, y, z)
            && _map.ShapeAt(x, y, z) == TileShape.Wall
            && IsHost(_map.MaterialIndexAt(x, y, z))
            && (!_hiddenOnly || !_map.IsDiscovered(x, y, z));

        private bool CanStart(int x, int y, int z) =>
            CanGrow(x, y, z) && (_surface is null || z <= _surface[x + (y * _map.Size.Width)] - 2);

        /// <summary>
        /// Fills <c>_cells</c> with up to <paramref name="target"/> cells of rock that touch each other,
        /// starting at one and adding a random cell next to those already taken, no more than
        /// <see cref="Thickness"/> levels away from the start. Returns how many it got.
        /// </summary>
        private int Grow(int start, int target)
        {
            var size = _map.Size;
            var origin = size.CoordOf(start);
            _cells.Clear();
            _frontier.Clear();
            _cells.Add(start);
            AddNeighbours(origin, origin.Z);
            while (_cells.Count < target && _frontier.Count > 0)
            {
                var pick = _random.NextInt(_frontier.Count);
                var index = _frontier[pick];
                _frontier[pick] = _frontier[^1];
                _frontier.RemoveAt(_frontier.Count - 1);
                _cells.Add(index);
                AddNeighbours(size.CoordOf(index), origin.Z);
            }

            return _cells.Count;
        }

        private void AddNeighbours(TileCoord cell, int originZ)
        {
            var size = _map.Size;
            for (var side = 0; side < 6; side++)
            {
                var (dx, dy, dz) = side switch
                {
                    0 => (-1, 0, 0),
                    1 => (1, 0, 0),
                    2 => (0, -1, 0),
                    3 => (0, 1, 0),
                    4 => (0, 0, -1),
                    _ => (0, 0, 1),
                };
                int x = cell.X + dx, y = cell.Y + dy, z = cell.Z + dz;
                if (Math.Abs(z - originZ) > Thickness || !CanGrow(x, y, z))
                {
                    continue;
                }

                var index = size.IndexOf(x, y, z);
                if (!_cells.Contains(index) && !_frontier.Contains(index))
                {
                    _frontier.Add(index);
                }
            }
        }

        /// <summary>Turns the cells of <c>_cells</c> into ore, remembering the rock.</summary>
        private Vein Commit(int target)
        {
            var size = _map.Size;
            var cells = _cells.ToArray();
            var rock = new ushort[cells.Length];
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = size.CoordOf(cells[i]);
                rock[i] = _map.MaterialIndexAt(cell.X, cell.Y, cell.Z);
                _map.SetTileQuietly(cell.X, cell.Y, cell.Z, TileShape.Wall, _material, _map.FlagsAt(cell));
            }

            return new Vein(cells, rock, target);
        }

        private void Restore(Vein vein)
        {
            var size = _map.Size;
            for (var i = 0; i < vein.Cells.Length; i++)
            {
                var cell = size.CoordOf(vein.Cells[i]);
                _map.SetTileQuietly(cell.X, cell.Y, cell.Z, TileShape.Wall, vein.Rock[i], _map.FlagsAt(cell));
            }
        }

        /// <summary>True if some cell of the vein is discovered and within the radius of the middle of the valley.</summary>
        private bool InSight(Vein vein)
        {
            var size = _map.Size;
            var (cx, cy) = ValleyCenter(size);
            var radius = (long)_def.Outcrop.Radius;
            foreach (var index in vein.Cells)
            {
                var cell = size.CoordOf(index);
                long dx = cell.X - cx, dy = cell.Y - cy;
                if (_map.IsDiscovered(cell) && (dx * dx) + (dy * dy) <= radius * radius)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The cells of rock the veins grow in that are discovered within the radius, in index order.</summary>
        private List<int> RockInSight()
        {
            var size = _map.Size;
            var (cx, cy) = ValleyCenter(size);
            var radius = _def.Outcrop.Radius;
            var cells = new List<int>();
            for (var z = 0; z < size.Depth; z++)
            {
                for (var y = Math.Max(0, cy - radius); y <= Math.Min(size.Height - 1, cy + radius); y++)
                {
                    for (var x = Math.Max(0, cx - radius); x <= Math.Min(size.Width - 1, cx + radius); x++)
                    {
                        long dx = x - cx, dy = y - cy;
                        if ((dx * dx) + (dy * dy) <= (long)radius * radius && _map.IsDiscovered(x, y, z) && CanGrow(x, y, z))
                        {
                            cells.Add(size.IndexOf(x, y, z));
                        }
                    }
                }
            }

            return cells;
        }
    }
}
