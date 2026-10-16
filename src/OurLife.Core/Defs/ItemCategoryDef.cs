namespace OurLife.Core.Defs;

/// <summary>
/// A group of items for the stockpiles' filters, like wood or food (see
/// <c>docs/gdd/almacenes.md</c>), and for the colony's stock in the HUD
/// (<c>docs/gdd/indicadores.md</c>). It only sorts storage: whether an item is eaten is its
/// <see cref="ItemDef.Kind"/>.
/// </summary>
public sealed class ItemCategoryDef : Def
{
    /// <summary>Where the category goes in the lists of the interface: lowest first, then by id.</summary>
    public int Order { get; init; }

    /// <summary>The categories of the content in the order of the interface (see <see cref="Order"/>).</summary>
    public static IReadOnlyList<ItemCategoryDef> InOrder(DefDatabase content)
    {
        ArgumentNullException.ThrowIfNull(content);

        // The database gives them by id, and the sort keeps that order among equals.
        return [.. content.All<ItemCategoryDef>().OrderBy(category => category.Order)];
    }
}
