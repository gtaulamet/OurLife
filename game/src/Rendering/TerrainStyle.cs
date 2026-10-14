namespace OurLife.Game.Rendering;

/// <summary>
/// Style test of IP2.5 (V1): how the terrain shader paints the cells. <c>--terrain-style</c>
/// chooses it for captures; the style the player picks stays, the rest goes in V4.
/// </summary>
public static class TerrainStyle
{
    /// <summary>Styles by name, with their value in the shader.</summary>
    public static readonly IReadOnlyDictionary<string, int> Names = new Dictionary<string, int>
    {
        ["flat"] = 0,
        ["a"] = 1,
        ["b"] = 2,
        ["c"] = 3,
    };

    // Pattern of each material in the shader; materials not listed are plain.
    private static readonly Dictionary<string, int> Patterns = new()
    {
        ["soil"] = 1,
        ["granite"] = 2,
        ["limestone"] = 3,
        ["sandstone"] = 3,
        ["timber"] = 5,
        ["masonry"] = 6,
    };

    /// <summary>The style of the terrain, set once at the start.</summary>
    public static int Current { get; set; } = Names["b"];

    public static int PatternOf(string material) => Patterns.GetValueOrDefault(material);
}
