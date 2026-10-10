namespace OurLife.Core.Map;

/// <summary>
/// Groups the standable cells into regions whose cells can reach each other (flood fill with
/// <see cref="MapNavigation"/>). Two cells in different regions are unreachable, which lets the
/// pathfinding discard a target at once. <see cref="Compute"/> numbers regions from 1 in the order
/// of the cell indexes, so the numbering is deterministic. After the map changes, compute again,
/// or call <see cref="OpenWays"/> when the changes only opened ways. Only whether two cells share
/// a region matters, never the number of the region.
/// </summary>
public sealed class Connectivity
{
    public const int NoRegion = 0;

    private readonly MapSize _size;
    private readonly int[] _regions;

    // Cells per region; a region merged into another keeps a size of 0.
    private readonly List<int> _sizes;
    private readonly List<int> _relabel = [];

    // Regions with cells. Merged regions keep their slot at size 0 until the next full computation.
    private int _liveRegions;

    private Connectivity(MapSize size, int[] regions, List<int> sizes)
    {
        _size = size;
        _regions = regions;
        _sizes = sizes;
        _liveRegions = sizes.Count - 1;
    }

    public int RegionCount => _liveRegions;

    public static Connectivity Compute(GameMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var size = map.Size;
        var regions = new int[size.CellCount];
        var sizes = new List<int> { 0 };

        // Grows with the largest region instead of holding a slot for every cell of the map.
        var queue = new List<int>();
        Span<TileCoord> neighbours = stackalloc TileCoord[MapNavigation.MaxNeighbours];

        for (var start = 0; start < regions.Length; start++)
        {
            if (regions[start] != NoRegion)
            {
                continue;
            }

            var origin = size.CoordOf(start);
            if (!map.IsStandable(origin.X, origin.Y, origin.Z))
            {
                continue;
            }

            var region = sizes.Count;
            regions[start] = region;
            queue.Clear();
            queue.Add(start);
            for (var head = 0; head < queue.Count; head++)
            {
                var cell = size.CoordOf(queue[head]);
                var count = MapNavigation.GetNeighbours(map, cell.X, cell.Y, cell.Z, neighbours);
                for (var i = 0; i < count; i++)
                {
                    var next = size.IndexOf(neighbours[i].X, neighbours[i].Y, neighbours[i].Z);
                    if (regions[next] == NoRegion)
                    {
                        regions[next] = region;
                        queue.Add(next);
                    }
                }
            }

            sizes.Add(queue.Count);
        }

        return new Connectivity(size, regions, sizes);
    }

    /// <summary>
    /// Updates the regions after changes that only opened ways (see <see cref="GameMap"/>): a cell
    /// that became standable gets a region, and two regions that now touch become one. The rules of
    /// <see cref="MapNavigation"/> are symmetric, and every way a change opens has an end on the
    /// level of the change, next to it: a dug wall above a ramp opens the ramp to the cells around
    /// the wall. So only the 3×3 cells around each change, on its level, are checked. Merging
    /// renumbers the smaller region, so its cost is the size of that region.
    /// </summary>
    internal void OpenWays(GameMap map, IReadOnlyList<int> changedCells)
    {
        foreach (var index in changedCells)
        {
            var cell = _size.CoordOf(index);
            if (_regions[index] == NoRegion && map.IsStandable(cell))
            {
                _regions[index] = _sizes.Count;
                _sizes.Add(1);
                _liveRegions++;
            }
        }

        Span<TileCoord> neighbours = stackalloc TileCoord[MapNavigation.MaxNeighbours];
        foreach (var index in changedCells)
        {
            var changed = _size.CoordOf(index);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var cell = new TileCoord(changed.X + dx, changed.Y + dy, changed.Z);
                    var count = MapNavigation.GetNeighbours(map, cell.X, cell.Y, cell.Z, neighbours);
                    for (var i = 0; i < count; i++)
                    {
                        Merge(map, cell, neighbours[i]);
                    }
                }
            }
        }
    }

    /// <summary>Region of a cell, or <see cref="NoRegion"/> if nobody can stand there or it is outside the map.</summary>
    public int RegionAt(int x, int y, int z) =>
        _size.Contains(x, y, z) ? _regions[_size.IndexOf(x, y, z)] : NoRegion;

    public int RegionAt(TileCoord cell) => RegionAt(cell.X, cell.Y, cell.Z);

    /// <summary>
    /// The distinct regions of the surface cell of every column, sorted. A connected surface gives
    /// exactly one region; <see cref="NoRegion"/> appears if some surface cell is not standable.
    /// Blocked cells, like those of tree trunks, are skipped: they belong to no region.
    /// </summary>
    public IReadOnlyList<int> SurfaceRegions(GameMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var regions = new SortedSet<int>();
        for (var y = 0; y < map.Size.Height; y++)
        {
            for (var x = 0; x < map.Size.Width; x++)
            {
                var z = map.SurfaceZ(x, y);
                if (z >= 0 && (map.FlagsAt(x, y, z) & TileFlags.Blocked) != 0)
                {
                    continue;
                }

                regions.Add(RegionAt(x, y, z));
            }
        }

        return regions.ToArray();
    }

    /// <summary>
    /// The largest region of the surface, or <see cref="NoRegion"/> if nobody can stand on it.
    /// Ties go to the region whose first surface cell, in index order, comes first. The tie never
    /// depends on region numbers, which differ between maintained and fresh connectivity (ADR-011).
    /// </summary>
    public int MainSurfaceRegion(GameMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        int best = NoRegion, bestSize = 0;
        var seen = new HashSet<int>();
        for (var y = 0; y < map.Size.Height; y++)
        {
            for (var x = 0; x < map.Size.Width; x++)
            {
                var region = RegionAt(x, y, map.SurfaceZ(x, y));
                if (region == NoRegion || !seen.Add(region))
                {
                    continue;
                }

                // Regions are met in index order, so a later region wins only if it is larger.
                if (SizeOf(region) > bestSize)
                {
                    best = region;
                    bestSize = SizeOf(region);
                }
            }
        }

        return best;
    }

    /// <summary>Number of cells in a region.</summary>
    public int SizeOf(int region) => _sizes[region];

    public bool AreConnected(TileCoord a, TileCoord b)
    {
        var region = RegionAt(a);
        return region != NoRegion && region == RegionAt(b);
    }

    /// <summary>Joins the regions of two neighbouring cells, renumbering the smaller one.</summary>
    private void Merge(GameMap map, TileCoord a, TileCoord b)
    {
        var regionA = RegionAt(a);
        var regionB = RegionAt(b);
        if (regionA == regionB)
        {
            return;
        }

        // The larger region keeps its number; on a tie, the lower number.
        var aKeeps = _sizes[regionA] > _sizes[regionB] || (_sizes[regionA] == _sizes[regionB] && regionA < regionB);
        var (keep, drop, start) = aKeeps ? (regionA, regionB, b) : (regionB, regionA, a);

        Span<TileCoord> neighbours = stackalloc TileCoord[MapNavigation.MaxNeighbours];
        _relabel.Clear();
        _relabel.Add(_size.IndexOf(start.X, start.Y, start.Z));
        _regions[_relabel[0]] = keep;
        for (var head = 0; head < _relabel.Count; head++)
        {
            var cell = _size.CoordOf(_relabel[head]);
            var count = MapNavigation.GetNeighbours(map, cell.X, cell.Y, cell.Z, neighbours);
            for (var i = 0; i < count; i++)
            {
                var next = _size.IndexOf(neighbours[i].X, neighbours[i].Y, neighbours[i].Z);
                if (_regions[next] == drop)
                {
                    _regions[next] = keep;
                    _relabel.Add(next);
                }
            }
        }

        _sizes[keep] += _sizes[drop];
        _sizes[drop] = 0;
        _liveRegions--;
    }
}
