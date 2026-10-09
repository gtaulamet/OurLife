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

    private readonly TileShape[] _shapes;
    private readonly ushort[] _materials;
    private readonly TileFlags[] _flags;

    private readonly bool[] _dirty;
    private readonly List<ChunkKey> _dirtyList = [];

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

    /// <summary>Shape at a cell, or <see cref="TileShape.Empty"/> outside the map.</summary>
    public TileShape ShapeOrEmpty(int x, int y, int z) =>
        Size.Contains(x, y, z) ? _shapes[Size.IndexOf(x, y, z)] : TileShape.Empty;

    public bool IsStandable(int x, int y, int z) => ShapeOrEmpty(x, y, z).IsStandable();

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
        SetTileQuietly(x, y, z, shape, material, flags);
        MarkDirty(x, y, z);
        MarkDirty(x - 1, y, z);
        MarkDirty(x + 1, y, z);
        MarkDirty(x, y - 1, z);
        MarkDirty(x, y + 1, z);
        MarkDirty(x, y, z - 1);
        MarkDirty(x, y, z + 1);
    }

    /// <summary>Changes a cell without marking chunks: for building a new map, which is all dirty.</summary>
    internal void SetTileQuietly(int x, int y, int z, TileShape shape, ushort material, TileFlags flags)
    {
        if (material != NoMaterial && material >= Materials.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(material), $"There is no material with index {material}.");
        }

        var index = Index(x, y, z);
        _shapes[index] = shape;
        _materials[index] = material;
        _flags[index] = flags;
    }

    internal ReadOnlySpan<byte> ShapeBytes => MemoryMarshal.AsBytes(_shapes.AsSpan());

    internal ReadOnlySpan<ushort> MaterialIndexes => _materials;

    internal ReadOnlySpan<byte> MaterialBytes => MemoryMarshal.AsBytes(_materials.AsSpan());

    internal ReadOnlySpan<byte> FlagBytes => MemoryMarshal.AsBytes(_flags.AsSpan());

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
