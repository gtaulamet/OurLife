namespace OurLife.Game.Rendering;

/// <summary>
/// How much decoration dresses the grass, an option of the player (<c>docs/gdd/decoracion-del-pasto.md</c>):
/// it scales the chances of the catalog. Less decoration draws fewer triangles, for slower
/// graphics cards.
/// </summary>
public enum DecorationLevel
{
    None = 0,
    Low = 1,
    Normal = 2,
    High = 3,
}

public static class DecorationLevels
{
    /// <summary>The levels in the order Options shows them, with the key of their name.</summary>
    public static readonly IReadOnlyList<(DecorationLevel Level, string Key)> InOrder =
    [
        (DecorationLevel.None, "ui.options.decoration_none"),
        (DecorationLevel.Low, "ui.options.decoration_low"),
        (DecorationLevel.Normal, "ui.options.decoration_normal"),
        (DecorationLevel.High, "ui.options.decoration_high"),
    ];

    /// <summary>What the chances of the catalog are multiplied by. Presentation, not balance.</summary>
    public static float Multiplier(DecorationLevel level) => level switch
    {
        DecorationLevel.None => 0f,
        DecorationLevel.Low => 0.4f,
        DecorationLevel.High => 1.8f,
        _ => 1f,
    };

    /// <summary>A level by its lower-case name, as <c>--decoration</c> takes it, or null.</summary>
    public static DecorationLevel? Parse(string name) =>
        Enum.GetValues<DecorationLevel>().Cast<DecorationLevel?>().FirstOrDefault(level => level.ToString()!.Equals(name, StringComparison.OrdinalIgnoreCase));
}
