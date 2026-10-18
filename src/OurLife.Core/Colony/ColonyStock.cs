using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
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
        var content = simulation.Content;
        var world = simulation.World;
        var categories = ItemCategoryDef.InOrder(content);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        var entities = new List<EntityId>();
        world.Query<ItemStack>(entities);
        foreach (var pile in entities)
        {
            var stack = world.Get<ItemStack>(pile);
            Add(counts, content.Get<ItemDef>(stack.Def).Category, stack.Count);
        }

        world.Query<Carried>(entities);
        foreach (var carrier in entities)
        {
            var carried = world.Get<Carried>(carrier);
            Add(counts, content.Get<ItemDef>(carried.Def).Category, carried.Count);
        }

        return [.. categories.Select(category => new StockLine(category, counts.GetValueOrDefault(category.Id)))];
    }

    /// <summary>The nutrition of all the food the colony has, lying anywhere or carried, as eating gives it.</summary>
    public static long FoodNutrition(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var content = simulation.Content;
        var world = simulation.World;
        long total = 0;
        var entities = new List<EntityId>();
        world.Query<ItemStack>(entities);
        foreach (var pile in entities)
        {
            var stack = world.Get<ItemStack>(pile);
            var item = content.Get<ItemDef>(stack.Def);
            total += item.Kind == ItemKind.Food ? (long)item.Nutrition * stack.Count : 0;
        }

        world.Query<Carried>(entities);
        foreach (var carrier in entities)
        {
            var carried = world.Get<Carried>(carrier);
            var item = content.Get<ItemDef>(carried.Def);
            total += item.Kind == ItemKind.Food ? (long)item.Nutrition * carried.Count : 0;
        }

        return total;
    }

    private static void Add(Dictionary<string, int> counts, string category, int count) =>
        counts[category] = counts.GetValueOrDefault(category) + count;
}
