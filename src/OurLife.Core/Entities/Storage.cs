using OurLife.Core.Defs;
using OurLife.Core.Map;

namespace OurLife.Core.Entities;

/// <summary>
/// A stockpile zone (see <c>docs/gdd/almacenes.md</c>): its cells, sorted by map index, and the
/// item categories it accepts, sorted by id. Each cell holds one pile.
/// </summary>
public sealed class Stockpile
{
    public List<TileCoord> Cells { get; set; } = [];

    /// <summary>Ids of the <see cref="ItemCategoryDef"/>s allowed in the zone.</summary>
    [DefRef(typeof(ItemCategoryDef))]
    public List<string> Categories { get; set; } = [];
}

/// <summary>What a colonist carries in its hands, on the way to a stockpile.</summary>
public sealed class Carried
{
    [DefRef(typeof(ItemDef))]
    public string Def { get; set; } = string.Empty;

    public int Count { get; set; }
}
