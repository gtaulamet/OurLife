namespace OurLife.Core.Defs;

/// <summary>
/// A group of items for the stockpiles' filters, like wood or food (see
/// <c>docs/gdd/almacenes.md</c>), and for the colony's stock in the HUD
/// (<c>docs/gdd/indicadores.md</c>). It only sorts storage: whether an item is eaten is its
/// <see cref="ItemDef.Kind"/>.
/// </summary>
public sealed class ItemCategoryDef : Def
{
    /// <summary>Where the category goes in the stock of the HUD: lowest first, then by id.</summary>
    public int Order { get; init; }
}
