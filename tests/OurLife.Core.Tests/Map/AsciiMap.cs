using OurLife.Core.Defs;
using OurLife.Core.Map;

namespace OurLife.Core.Tests.Map;

/// <summary>
/// Builds small maps for scenario tests from text, one string per level from the bottom up.
/// Legend: <c>#</c> wall, <c>.</c> floor, space or <c>_</c> air, <c>/</c> ramp,
/// <c>&lt;</c> stairs up, <c>&gt;</c> stairs down, <c>X</c> stairs both ways. Cells have no
/// material, unless the map is built with <see cref="ParseWith"/>.
/// </summary>
internal static class AsciiMap
{
    public static GameMap Parse(params string[] levels) => ParseWith([], levels);

    /// <summary>Same as <see cref="Parse"/>, but every cell that is not air is made of the first material.</summary>
    public static GameMap ParseWith(IReadOnlyList<MaterialDef> materials, params string[] levels)
    {
        var rows = levels.Select(level => level.Replace("\r", string.Empty).Split('\n')).ToArray();
        var height = rows.Max(level => level.Length);
        var width = rows.SelectMany(level => level).Max(row => row.Length);
        var map = new GameMap(new MapSize(width, height, levels.Length), materials);
        var material = materials.Count > 0 ? (ushort)0 : GameMap.NoMaterial;

        for (var z = 0; z < rows.Length; z++)
        {
            for (var y = 0; y < rows[z].Length; y++)
            {
                for (var x = 0; x < rows[z][y].Length; x++)
                {
                    var shape = ShapeOf(rows[z][y][x]);
                    map.SetTileQuietly(x, y, z, shape, shape == TileShape.Empty ? GameMap.NoMaterial : material, TileFlags.None);
                }
            }
        }

        return map;
    }

    private static TileShape ShapeOf(char symbol) => symbol switch
    {
        '#' => TileShape.Wall,
        '.' => TileShape.Floor,
        ' ' or '_' => TileShape.Empty,
        '/' => TileShape.Ramp,
        '<' => TileShape.StairUp,
        '>' => TileShape.StairDown,
        'X' => TileShape.StairUpDown,
        _ => throw new ArgumentException($"Unknown map symbol '{symbol}'."),
    };
}
