using OurLife.Core.Defs;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>How many units of a category of items the colony has.</summary>
public sealed record StockLine(ItemCategoryDef Category, int Count);

/// <summary>
/// The colony's stock, for the HUD (see <c>docs/gdd/indicadores.md</c>): the units of each item
/// category lying anywhere on the map, stored or not, plus what the colonists carry. A read-only
/// query.
/// </summary>
public static class ColonyStock
{
    /// <summary>One line per item category, by <see cref="ItemCategoryDef.Order"/> and then id, including the empty ones.</summary>
    public static IReadOnlyList<StockLine> ByCategory(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var categories = ItemCategoryDef.InOrder(simulation.Content);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (item, _, count, _) in Stocks.Everything(simulation))
        {
            counts[item.Category] = counts.GetValueOrDefault(item.Category) + count;
        }

        return [.. categories.Select(category => new StockLine(category, counts.GetValueOrDefault(category.Id)))];
    }

    /// <summary>
    /// What the colony has to eat in the winter, for the food reserve a couple needs to conceive: the
    /// nutrition of its food plus the <see cref="ItemDef.ReserveValue"/> of grain and flour (see
    /// "Comida y concepción" in <c>docs/gdd/agricultura.md</c>).
    /// </summary>
    public static long FoodReserve(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        long total = 0;
        foreach (var (item, _, count, _) in Stocks.Everything(simulation))
        {
            total += item.Kind == ItemKind.Food ? (long)item.Nutrition * count : (long)item.ReserveValue * count;
        }

        return total;
    }
}
