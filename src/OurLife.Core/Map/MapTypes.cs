namespace OurLife.Core.Map;

/// <summary>What occupies a cell of the map.</summary>
public enum TileShape : byte
{
    /// <summary>Open air, with nothing to stand on.</summary>
    Empty,

    /// <summary>An open cell with a floor; its material is the material of the floor.</summary>
    Floor,

    /// <summary>Solid soil or rock.</summary>
    Wall,

    /// <summary>A sloped floor that leads to the four neighbours one level up.</summary>
    Ramp,

    StairUp,
    StairDown,
    StairUpDown,

    /// <summary>A built door: walked through like a floor (see <c>docs/gdd/construccion.md</c>).</summary>
    Door,
}

/// <summary>Ids of the tile shapes, so the presentation keeps no list of its own.</summary>
public static class TileShapes
{
    /// <summary>The id of a shape in texts, like <c>stair_up</c> in <c>ui.inspector.shape.stair_up</c>.</summary>
    public static string Id(TileShape shape) => shape switch
    {
        TileShape.Empty => "empty",
        TileShape.Floor => "floor",
        TileShape.Wall => "wall",
        TileShape.Ramp => "ramp",
        TileShape.StairUp => "stair_up",
        TileShape.StairDown => "stair_down",
        TileShape.StairUpDown => "stair_updown",
        TileShape.Door => "door",
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
    };
}

/// <summary>Extra facts about a cell. Fog arrives in a later milestone.</summary>
[Flags]
public enum TileFlags : byte
{
    None = 0,

    /// <summary>The floor is covered with grass.</summary>
    Grass = 1,

    /// <summary>Something on the cell, like a tree trunk, keeps creatures out: the cell is not standable.</summary>
    Blocked = 2,

    /// <summary>The player ordered to dig out this wall.</summary>
    MineDesignation = 4,

    /// <summary>The player ordered to dig stairs down from this cell.</summary>
    StairsDesignation = 8,

    /// <summary>The player ordered to take down what was built on this cell.</summary>
    DeconstructDesignation = 16,

    /// <summary>The player ordered to dig a channel: open this floor down to the level below.</summary>
    ChannelDesignation = 32,
}

/// <summary>A cell of the map. <c>Z</c> grows upwards: 0 is the deepest level.</summary>
public readonly record struct TileCoord(int X, int Y, int Z);

/// <summary>A chunk: 16×16 cells of one level, the unit the renderer rebuilds.</summary>
public readonly record struct ChunkKey(int X, int Y, int Z);

/// <summary>Dimensions of a map. Cells are stored at <c>x + y·Width + z·Width·Height</c>.</summary>
public readonly record struct MapSize(int Width, int Height, int Depth)
{
    public int CellCount => Width * Height * Depth;

    public bool Contains(int x, int y, int z) =>
        (uint)x < (uint)Width && (uint)y < (uint)Height && (uint)z < (uint)Depth;

    public int IndexOf(int x, int y, int z) => x + (y * Width) + (z * Width * Height);

    public TileCoord CoordOf(int index)
    {
        var levelSize = Width * Height;
        var z = index / levelSize;
        var rest = index - (z * levelSize);
        return new TileCoord(rest % Width, rest / Width, z);
    }
}

public static class TileShapeExtensions
{
    /// <summary>A creature can stand here: a floor, a ramp or stairs.</summary>
    public static bool IsStandable(this TileShape shape) => shape is not (TileShape.Empty or TileShape.Wall);

    public static bool GoesUp(this TileShape shape) => shape is TileShape.StairUp or TileShape.StairUpDown;

    public static bool GoesDown(this TileShape shape) => shape is TileShape.StairDown or TileShape.StairUpDown;
}
