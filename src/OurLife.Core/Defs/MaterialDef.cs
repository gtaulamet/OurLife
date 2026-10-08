using System.Text.RegularExpressions;

namespace OurLife.Core.Defs;

public enum MaterialKind
{
    Soil,
    Stone,
}

/// <summary>What a tile of the map is made of: soil or a kind of stone.</summary>
public sealed partial class MaterialDef : Def
{
    public required MaterialKind Kind { get; init; }

    /// <summary>Tint applied to grayscale sprites, as #RRGGBB.</summary>
    public required string Color { get; init; }

    /// <summary>Work ticks needed to dig out one tile.</summary>
    public required int MineWorkTicks { get; init; }

    /// <summary>Item that may appear when a tile is dug out.</summary>
    public string? MinedItem { get; init; }

    /// <summary>Chance of leaving <see cref="MinedItem"/>, in basis points: 10 000 means always.</summary>
    public int MinedItemChance { get; init; }

    internal override IEnumerable<DefReference> References()
    {
        yield return new DefReference(typeof(ItemDef), MinedItem, "minedItem");
    }

    internal override IEnumerable<string> Validate()
    {
        if (!ColorPattern().IsMatch(Color))
        {
            yield return $"color '{Color}' must look like #RRGGBB.";
        }

        if (MineWorkTicks <= 0)
        {
            yield return "mineWorkTicks must be positive.";
        }

        if (MinedItemChance is < 0 or > 10_000)
        {
            yield return "minedItemChance must be between 0 and 10000.";
        }

        if (MinedItem is null && MinedItemChance > 0)
        {
            yield return "minedItemChance needs a minedItem.";
        }
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorPattern();
}
