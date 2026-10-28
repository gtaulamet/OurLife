using System.Text.RegularExpressions;

namespace OurLife.Core.Defs;

public enum MaterialKind
{
    Soil,
    Stone,
    Wood,

    /// <summary>Mineral in veins of rock; it is dug out, not built with (see <c>docs/gdd/subsuelo.md</c>).</summary>
    Ore,
}

/// <summary>
/// What a tile of the map is made of: soil or a kind of stone, or a construction material when it
/// has <see cref="BuiltFrom"/> (see <c>docs/gdd/construccion.md</c>).
/// </summary>
public sealed partial class MaterialDef : Def
{
    public required MaterialKind Kind { get; init; }

    /// <summary>Tint of the terrain made of this material, as #RRGGBB.</summary>
    public required string Color { get; init; }

    /// <summary>Work ticks needed to dig out one tile.</summary>
    public required int MineWorkTicks { get; init; }

    /// <summary>Item that may appear when a tile is dug out.</summary>
    public string? MinedItem { get; init; }

    /// <summary>Chance of leaving <see cref="MinedItem"/>, in basis points: 10 000 means always.</summary>
    public int MinedItemChance { get; init; }

    /// <summary>Item that tiles of this material are built from; null for the natural materials of the valley.</summary>
    public string? BuiltFrom { get; init; }

    /// <summary>Tiles of this material were built, not found: they are taken down, not dug.</summary>
    public bool IsBuilt => BuiltFrom is not null;

    /// <summary>What walls and floors of this material add to the score of their room (see <c>docs/gdd/habitaciones.md</c>).</summary>
    public MaterialBeauty Beauty { get; init; } = new();

    internal override IEnumerable<DefReference> References()
    {
        yield return new DefReference(typeof(ItemDef), MinedItem, "minedItem");
        yield return new DefReference(typeof(ItemDef), BuiltFrom, "builtFrom");
    }

    internal override IEnumerable<string> Validate()
    {
        if (!IsHexColor(Color))
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

        if (Kind == MaterialKind.Ore && (IsBuilt || MinedItem is null))
        {
            yield return "an ore material is dug out for a minedItem and has no builtFrom.";
        }

        if (IsBuilt && MinedItem is not null)
        {
            yield return "a built material is taken down, not dug: it cannot have a minedItem.";
        }
    }

    /// <summary>True for a colour written as #RRGGBB, the format of every colour in the content.</summary>
    public static bool IsHexColor(string value) => ColorPattern().IsMatch(value);

    // \z, not $: $ would also accept a line break at the end.
    [GeneratedRegex(@"^#[0-9a-fA-F]{6}\z")]
    private static partial Regex ColorPattern();
}

/// <summary>
/// The beauty of a material in a room: <see cref="Wall"/> for each piece of the border (a wall or
/// a door) built of it, <see cref="Floor"/> for each cell of the room with a floor of it.
/// </summary>
public sealed class MaterialBeauty
{
    public int Wall { get; init; }

    public int Floor { get; init; }
}
