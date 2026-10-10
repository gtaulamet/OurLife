namespace OurLife.Game.Rendering;

/// <summary>
/// How the view shows levels below the current one: each level down is a little darker, so
/// depth reads at a glance, and levels too deep are not drawn (ADR-009). Presentation values,
/// not balance: they only change how the map looks.
/// </summary>
public static class LevelShading
{
    /// <summary>Deepest level drawn, counted from the current one.</summary>
    public const int MaxDepth = 24;

    /// <summary>Brightness of the top of a block cut by the view, relative to its material colour.</summary>
    public const float CutBrightness = 0.45f;

    private const float DarkeningPerLevel = 0.025f;
    private const float MinBrightness = 0.55f;

    public static bool IsVisible(int level, int viewLevel) => level <= viewLevel && viewLevel - level <= MaxDepth;

    /// <summary>Brightness of a level <paramref name="depth"/> levels below the current one, in (0, 1].</summary>
    public static float Brightness(int depth) => Math.Max(MinBrightness, 1f - (depth * DarkeningPerLevel));
}
