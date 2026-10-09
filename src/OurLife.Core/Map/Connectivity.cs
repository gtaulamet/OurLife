namespace OurLife.Core.Map;

/// <summary>
/// Groups the standable cells into regions whose cells can reach each other (flood fill with
/// <see cref="MapNavigation"/>). Two cells in different regions are unreachable, which lets the
/// pathfinding discard a target at once. Regions are numbered from 1 in the order of the cell
/// indexes, so the numbering is deterministic. Recompute after the map changes.
/// </summary>
public sealed class Connectivity
{
    public const int NoRegion = 0;

    private readonly MapSize _size;
    private readonly int[] _regions;
    private readonly List<int> _sizes;

    private Connectivity(MapSize size, int[] regions, List<int> sizes)
    {
        _size = size;
        _regions = regions;
        _sizes = sizes;
    }

    public int RegionCount => _sizes.Count - 1;

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
            if (!map.ShapeAt(origin.X, origin.Y, origin.Z).IsStandable())
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

    /// <summary>Region of a cell, or <see cref="NoRegion"/> if nobody can stand there or it is outside the map.</summary>
    public int RegionAt(int x, int y, int z) =>
        _size.Contains(x, y, z) ? _regions[_size.IndexOf(x, y, z)] : NoRegion;

    public int RegionAt(TileCoord cell) => RegionAt(cell.X, cell.Y, cell.Z);

    /// <summary>
    /// The distinct regions of the surface cell of every column, sorted. A connected surface gives
    /// exactly one region; <see cref="NoRegion"/> appears if some surface cell is not standable.
    /// </summary>
    public IReadOnlyList<int> SurfaceRegions(GameMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var regions = new SortedSet<int>();
        for (var y = 0; y < map.Size.Height; y++)
        {
            for (var x = 0; x < map.Size.Width; x++)
            {
                regions.Add(RegionAt(x, y, map.SurfaceZ(x, y)));
            }
        }

        return regions.ToArray();
    }

    /// <summary>Number of cells in a region.</summary>
    public int SizeOf(int region) => _sizes[region];

    public bool AreConnected(TileCoord a, TileCoord b)
    {
        var region = RegionAt(a);
        return region != NoRegion && region == RegionAt(b);
    }
}
