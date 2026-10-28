using System.Runtime.InteropServices;
using OurLife.Core.Defs;

namespace OurLife.Core.Map;

/// <summary>
/// The 3D map in dense arrays, one per property (ADR-003): shape, material and flags of every
/// cell. Only Core changes it (architecture rule 2); the presentation reads it and rebuilds the
/// chunks that <see cref="DrainDirtyChunks"/> reports. Material indexes come from
/// <see cref="DefDatabase.IndexOf{T}"/> and are only valid for the content the map was built with.
/// </summary>
public sealed class GameMap
{
    public const int ChunkSize = 16;
    public const ushort NoMaterial = ushort.MaxValue;

    /// <summary>The flags of the player's orders on cells.</summary>
    public const TileFlags OrderFlags =
        TileFlags.MineDesignation | TileFlags.StairsDesignation | TileFlags.DeconstructDesignation | TileFlags.ChannelDesignation;

    // Beyond this many changes between two reads, connectivity is cheaper to compute again.
    private const int MaxTrackedNavigationChanges = 1024;

    private readonly TileShape[] _shapes;
    private readonly ushort[] _materials;
    private readonly TileFlags[] _flags;

    private readonly bool[] _dirty;
    private readonly List<ChunkKey> _dirtyList = [];

    // Cells that changed where creatures walk, while every change only opened ways.
    private readonly List<int> _navigationChanges = [];
    private bool _navigationCut;

    // Derived from the flags, kept up to date so finding orders never scans the whole map.
    private readonly SortedSet<int> _orderedCells = [];

    public GameMap(MapSize size, IReadOnlyList<MaterialDef> materials)
    {
        ArgumentNullException.ThrowIfNull(materials);
        if (size.Width <= 0 || size.Height <= 0 || size.Depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Every dimension of the map must be positive.");
        }

        Size = size;
        Materials = materials;
        _shapes = new TileShape[size.CellCount];
        _materials = new ushort[size.CellCount];
        _flags = new TileFlags[size.CellCount];
        Array.Fill(_materials, NoMaterial);

        ChunksX = (size.Width + ChunkSize - 1) / ChunkSize;
        ChunksY = (size.Height + ChunkSize - 1) / ChunkSize;
        _dirty = new bool[ChunksX * ChunksY * size.Depth];
        MarkAllDirty();
    }

    public MapSize Size { get; }

    /// <summary>
    /// Grows when a change can alter where creatures walk: a shape or the <see cref="TileFlags.Blocked"/>
    /// flag. Connectivity is recomputed only then.
    /// </summary>
    public int NavigationVersion { get; private set; }

    /// <summary>Grows when an order of the player is added to or removed from a cell.</summary>
    public int OrdersVersion { get; private set; }

    /// <summary>Material defs by index, as stored in the cells.</summary>
    public IReadOnlyList<MaterialDef> Materials { get; }

    public int ChunksX { get; }

    public int ChunksY { get; }

    public TileShape ShapeAt(int x, int y, int z) => _shapes[Index(x, y, z)];

    public ushort MaterialIndexAt(int x, int y, int z) => _materials[Index(x, y, z)];

    /// <summary>The material of a wall or floor; null in the air.</summary>
    public MaterialDef? MaterialAt(int x, int y, int z)
    {
        var material = MaterialIndexAt(x, y, z);
        return material == NoMaterial ? null : Materials[material];
    }

    public TileFlags FlagsAt(int x, int y, int z) => _flags[Index(x, y, z)];

    /// <summary>
    /// True if the player knows the cell: it, or one of the 26 around it, is not a wall. Hidden cells
    /// are not drawn and the inspector does not name them. The simulation ignores it.
    /// </summary>
    public bool IsDiscovered(int x, int y, int z) => (_flags[Index(x, y, z)] & TileFlags.Discovered) != 0;

    public bool IsDiscovered(TileCoord cell) => IsDiscovered(cell.X, cell.Y, cell.Z);

    /// <summary>Shape at a cell, or <see cref="TileShape.Empty"/> outside the map.</summary>
    public TileShape ShapeOrEmpty(int x, int y, int z) =>
        Size.Contains(x, y, z) ? _shapes[Size.IndexOf(x, y, z)] : TileShape.Empty;

    /// <summary>A creature can stand here: a floor, ramp or stairs inside the map that nothing blocks.</summary>
    public bool IsStandable(int x, int y, int z)
    {
        if (!Size.Contains(x, y, z))
        {
            return false;
        }

        var index = Size.IndexOf(x, y, z);
        return IsStandable(_shapes[index], _flags[index]);
    }

    /// <summary>The rule of <see cref="IsStandable(int, int, int)"/> for a shape and its flags.</summary>
    public static bool IsStandable(TileShape shape, TileFlags flags) => shape.IsStandable() && (flags & TileFlags.Blocked) == 0;

    public bool IsStandable(TileCoord cell) => IsStandable(cell.X, cell.Y, cell.Z);

    public TileShape ShapeAt(TileCoord cell) => ShapeAt(cell.X, cell.Y, cell.Z);

    public TileFlags FlagsAt(TileCoord cell) => FlagsAt(cell.X, cell.Y, cell.Z);

    public MaterialDef? MaterialAt(TileCoord cell) => MaterialAt(cell.X, cell.Y, cell.Z);

    /// <summary>The highest level of a column that is not air, or -1 if the column is all air.</summary>
    public int SurfaceZ(int x, int y)
    {
        for (var z = Size.Depth - 1; z >= 0; z--)
        {
            if (ShapeAt(x, y, z) != TileShape.Empty)
            {
                return z;
            }
        }

        return -1;
    }

    /// <summary>
    /// Appends the chunks changed since the last call, in the order they changed, and forgets them.
    /// A new map reports every chunk.
    /// </summary>
    public void DrainDirtyChunks(List<ChunkKey> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.AddRange(_dirtyList);
        foreach (var chunk in _dirtyList)
        {
            _dirty[ChunkIndex(chunk)] = false;
        }

        _dirtyList.Clear();
    }

    /// <summary>
    /// Changes a cell and marks its chunk dirty, plus the chunks of its six face neighbours,
    /// whose visible faces may change too.
    /// </summary>
    internal void SetTile(int x, int y, int z, TileShape shape, ushort material, TileFlags flags)
    {
        var index = Index(x, y, z);
        var (oldShape, oldFlags) = (_shapes[index], _flags[index]);

        // Discovering is one way: a caller that rebuilds the flags cannot hide the cell again.
        flags = (flags & ~TileFlags.Discovered) | (oldFlags & TileFlags.Discovered);
        SetTileQuietly(x, y, z, shape, material, flags);
        RecordNavigationChange(index, oldShape, oldFlags, shape, flags);
        if (shape != TileShape.Wall)
        {
            DiscoverAround(x, y, z);
        }

        MarkDirty(x, y, z);
        MarkDirty(x - 1, y, z);
        MarkDirty(x + 1, y, z);
        MarkDirty(x, y - 1, z);
        MarkDirty(x, y + 1, z);
        MarkDirty(x, y, z - 1);
        MarkDirty(x, y, z + 1);
    }

    /// <summary>
    /// Changes only the flags of a cell, like a designation. Its chunk is marked dirty only if a
    /// flag other than an order changed: orders are drawn from <c>DesignationsChangedEvent</c>.
    /// </summary>
    internal void SetFlags(int x, int y, int z, TileFlags flags)
    {
        var index = Index(x, y, z);
        flags = (flags & ~TileFlags.Discovered) | (_flags[index] & TileFlags.Discovered);
        if (_flags[index] == flags)
        {
            return;
        }

        var oldFlags = _flags[index];
        _flags[index] = flags;
        TrackOrders(index, oldFlags, flags);
        RecordNavigationChange(index, _shapes[index], oldFlags, _shapes[index], flags);
        if (((oldFlags ^ flags) & ~OrderFlags) != 0)
        {
            MarkDirty(x, y, z);
        }
    }

    /// <summary>
    /// Discovers the cell and the 26 around it (inside the map) and marks the chunks of the ones that
    /// were hidden as dirty. Called when a cell stops being a wall.
    /// </summary>
    private void DiscoverAround(int x, int y, int z)
    {
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy, nz = z + dz;
                    if (!Size.Contains(nx, ny, nz))
                    {
                        continue;
                    }

                    var index = Size.IndexOf(nx, ny, nz);
                    if ((_flags[index] & TileFlags.Discovered) == 0)
                    {
                        _flags[index] |= TileFlags.Discovered;
                        MarkDirty(nx, ny, nz);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Works out the discovered cells of a map from its shapes alone, by the rule of
    /// <c>docs/gdd/subsuelo.md</c>: a cell is discovered if it or one of its 26 neighbours is not a
    /// wall. Used when a map is generated and when an old game is migrated; flags that were
    /// already set stay set. Does not mark chunks: those maps are all dirty anyway.
    /// </summary>
    internal void ComputeDiscovered()
    {
        var (width, height, depth) = (Size.Width, Size.Height, Size.Depth);
        var index = 0;
        for (var z = 0; z < depth; z++)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++, index++)
                {
                    if (_shapes[index] == TileShape.Wall)
                    {
                        continue;
                    }

                    int x0 = Math.Max(0, x - 1), x1 = Math.Min(width - 1, x + 1);
                    int y0 = Math.Max(0, y - 1), y1 = Math.Min(height - 1, y + 1);
                    int z0 = Math.Max(0, z - 1), z1 = Math.Min(depth - 1, z + 1);
                    for (var nz = z0; nz <= z1; nz++)
                    {
                        for (var ny = y0; ny <= y1; ny++)
                        {
                            var row = (nz * width * height) + (ny * width);
                            for (var nx = x0; nx <= x1; nx++)
                            {
                                _flags[row + nx] |= TileFlags.Discovered;
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>Indexes of the cells with an order of the player, ascending.</summary>
    public IReadOnlyCollection<int> OrderedCells => _orderedCells;

    /// <summary>
    /// Moves to <paramref name="output"/> the cells whose navigation changed since the last call.
    /// Returns false when some change may have cut a way, or there were too many to track: then
    /// connectivity has to be computed again from scratch.
    /// </summary>
    internal bool DrainNavigationChanges(List<int> output)
    {
        output.AddRange(_navigationChanges);
        var onlyAdded = !_navigationCut;
        _navigationChanges.Clear();
        _navigationCut = false;
        return onlyAdded;
    }

    /// <summary>
    /// True if a change can only open ways: a wall dug out, a blocked cell freed, a floor turned
    /// into stairs. Anything that may close a way returns false.
    /// </summary>
    private static bool OnlyOpensWays(TileShape oldShape, TileFlags oldFlags, TileShape newShape, TileFlags newFlags)
    {
        if ((newFlags & ~oldFlags & TileFlags.Blocked) != 0)
        {
            return false;
        }

        if (newShape == TileShape.Wall)
        {
            return oldShape == TileShape.Wall;
        }

        return !(IsStandable(oldShape, oldFlags) && !IsStandable(newShape, newFlags))
               && !(oldShape == TileShape.Ramp && newShape != TileShape.Ramp)
               && !(oldShape.GoesUp() && !newShape.GoesUp())
               && !(oldShape.GoesDown() && !newShape.GoesDown());
    }

    private void RecordNavigationChange(int index, TileShape oldShape, TileFlags oldFlags, TileShape newShape, TileFlags newFlags)
    {
        if (oldShape == newShape && ((oldFlags ^ newFlags) & TileFlags.Blocked) == 0)
        {
            return;
        }

        NavigationVersion++;
        if (_navigationCut)
        {
            return;
        }

        if (!OnlyOpensWays(oldShape, oldFlags, newShape, newFlags) || _navigationChanges.Count >= MaxTrackedNavigationChanges)
        {
            _navigationCut = true;
            _navigationChanges.Clear();
            return;
        }

        _navigationChanges.Add(index);
    }

    /// <summary>Changes a cell without marking chunks: for building a new map, which is all dirty.</summary>
    internal void SetTileQuietly(int x, int y, int z, TileShape shape, ushort material, TileFlags flags)
    {
        if (material != NoMaterial && material >= Materials.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(material), $"There is no material with index {material}.");
        }

        var index = Index(x, y, z);
        var oldFlags = _flags[index];
        _shapes[index] = shape;
        _materials[index] = material;
        _flags[index] = flags;

        TrackOrders(index, oldFlags, flags);
    }

    internal ReadOnlySpan<byte> ShapeBytes => MemoryMarshal.AsBytes(_shapes.AsSpan());

    internal ReadOnlySpan<ushort> MaterialIndexes => _materials;

    internal ReadOnlySpan<byte> MaterialBytes => MemoryMarshal.AsBytes(_materials.AsSpan());

    internal ReadOnlySpan<byte> FlagBytes => MemoryMarshal.AsBytes(_flags.AsSpan());

    /// <summary>
    /// Keeps <see cref="OrderedCells"/> and <see cref="OrdersVersion"/> up to date. Returns at once
    /// when no order changed, which is every cell while a world is built.
    /// </summary>
    private void TrackOrders(int index, TileFlags oldFlags, TileFlags flags)
    {
        if (((oldFlags ^ flags) & OrderFlags) == 0)
        {
            return;
        }

        OrdersVersion++;
        if ((flags & OrderFlags) != 0)
        {
            _orderedCells.Add(index);
        }
        else
        {
            _orderedCells.Remove(index);
        }
    }

    private int Index(int x, int y, int z)
    {
        if (!Size.Contains(x, y, z))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}, {z}) is outside the {Size.Width}×{Size.Height}×{Size.Depth} map.");
        }

        return Size.IndexOf(x, y, z);
    }

    private void MarkDirty(int x, int y, int z)
    {
        if (!Size.Contains(x, y, z))
        {
            return;
        }

        var chunk = new ChunkKey(x / ChunkSize, y / ChunkSize, z);
        var index = ChunkIndex(chunk);
        if (!_dirty[index])
        {
            _dirty[index] = true;
            _dirtyList.Add(chunk);
        }
    }

    private void MarkAllDirty()
    {
        for (var z = 0; z < Size.Depth; z++)
        {
            for (var cy = 0; cy < ChunksY; cy++)
            {
                for (var cx = 0; cx < ChunksX; cx++)
                {
                    var chunk = new ChunkKey(cx, cy, z);
                    _dirty[ChunkIndex(chunk)] = true;
                    _dirtyList.Add(chunk);
                }
            }
        }
    }

    private int ChunkIndex(ChunkKey chunk) => chunk.X + (chunk.Y * ChunksX) + (chunk.Z * ChunksX * ChunksY);
}
