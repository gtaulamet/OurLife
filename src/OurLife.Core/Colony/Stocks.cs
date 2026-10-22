using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// The units of an item of one quality the colony has, and how fresh they are on average. The
/// quality is null for the default one, as in <see cref="ItemStack.Quality"/>.
/// </summary>
public sealed record StockEntry(ItemDef Item, string? Quality, int Count, int Freshness);

/// <summary>
/// What the colony has, by item and quality (see "Pilas con calidad y frescura" in
/// <c>docs/gdd/oficios.md</c>): the piles lying anywhere on the map, stored or not, plus what the
/// colonists carry. The HUD, the stock targets, the production orders and the inventory panel
/// share it. Read-only queries.
/// </summary>
public static class Stocks
{
    /// <summary>
    /// Units of an item of <paramref name="minQuality"/> or better; of any quality if it is null.
    /// A minimum of null is any quality, not "the default one".
    /// </summary>
    /// <exception cref="ArgumentException">The content has no such quality.</exception>
    public static int Count(Simulation simulation, string item, string? minQuality = null)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentException.ThrowIfNullOrEmpty(item);
        var rules = minQuality is null ? null : QualityRules.From(simulation.Content);
        var minimum = rules?.RankOf(minQuality);
        var total = 0;
        foreach (var load in Everything(simulation))
        {
            if (load.Item.Id == item && (rules is null || rules.RankOf(load.Quality) >= minimum))
            {
                total += load.Count;
            }
        }

        return total;
    }

    /// <summary>
    /// One entry per item and quality that has units, by item id and then from the lowest quality
    /// to the highest. The freshness of an entry is the average of its units, rounded down.
    /// </summary>
    public static IReadOnlyList<StockEntry> Totals(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var rank = Ranks(simulation.Content);
        var sums = new SortedDictionary<(string Item, int Rank), (ItemDef Def, string? Quality, long Count, long Freshness)>(Comparer<(string Item, int Rank)>.Create(
            (a, b) => a.Item == b.Item ? a.Rank.CompareTo(b.Rank) : string.CompareOrdinal(a.Item, b.Item)));
        foreach (var load in Everything(simulation))
        {
            var key = (load.Item.Id, rank(load.Quality));
            var (def, quality, count, freshness) = sums.TryGetValue(key, out var sum) ? sum : (load.Item, load.Quality, 0L, 0L);
            sums[key] = (def, quality, count + load.Count, freshness + ((long)load.Count * load.Freshness));
        }

        return [.. sums.Values.Select(sum => new StockEntry(sum.Def, sum.Quality, (int)sum.Count, (int)(sum.Freshness / sum.Count)))];
    }

    /// <summary>Every pile on the map, stored or not, and every load a colonist carries, by ascending id.</summary>
    internal static IEnumerable<(ItemDef Item, string? Quality, int Count, int Freshness)> Everything(Simulation simulation)
    {
        var content = simulation.Content;
        var world = simulation.World;
        var entities = new List<EntityId>();
        world.Query<ItemStack>(entities);
        foreach (var pile in entities)
        {
            var stack = world.Get<ItemStack>(pile);
            yield return (content.Get<ItemDef>(stack.Def), stack.Quality, stack.Count, stack.FreshnessValue);
        }

        world.Query<Carried>(entities);
        foreach (var carrier in entities)
        {
            var carried = world.Get<Carried>(carrier);
            yield return (content.Get<ItemDef>(carried.Def), carried.Quality, carried.Count, carried.Freshness ?? ItemStack.FullFreshness);
        }
    }

    /// <summary>Place of a quality in the ladder; 0 for all when the content has no qualities (then nothing has one).</summary>
    private static Func<string?, int> Ranks(DefDatabase content)
    {
        if (content.All<QualityConfigDef>().Count != 1)
        {
            return _ => 0;
        }

        var rules = QualityRules.From(content);
        return quality => rules.RankOf(quality);
    }
}
