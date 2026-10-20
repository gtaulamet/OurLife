using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// Turns the colonists' free will on or off (see "Interruptor" in <c>docs/gdd/libre-albedrio.md</c>):
/// the player's option, which the session sends when a game starts or loads and whenever it changes.
/// </summary>
public sealed record SetFreeWillCommand(bool On) : ISimCommand
{
    public void Apply(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        simulation.FreeWill = On;
    }
}

/// <summary>
/// Sets how many units of an item category the village wants to keep (see "Aldea y metas" in
/// <c>docs/gdd/libre-albedrio.md</c>), within 0 and the config's highest target. A category without
/// a target in the config, or content without free will, is ignored.
/// </summary>
public sealed record SetStockTargetCommand([property: DefRef(typeof(ItemCategoryDef))] string Category, int Count) : ISimCommand
{
    public void Apply(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        // A pending order read from a damaged save can lack its category.
        if (Category is null || FreeWillRules.ConfigOf(simulation.Content) is not { } config || !config.DefaultTargets.ContainsKey(Category))
        {
            return;
        }

        var targets = simulation.StockTargetList;
        var count = Math.Clamp(Count, 0, config.TargetMax);
        var index = targets.FindIndex(target => target.Category == Category);
        if (index >= 0)
        {
            targets[index] = new StockTarget(Category, count);
            return;
        }

        var at = targets.FindIndex(target => string.CompareOrdinal(target.Category, Category) > 0);
        targets.Insert(at < 0 ? targets.Count : at, new StockTarget(Category, count));
    }
}

/// <summary>Read-only queries about free will, for the colony systems and the presentation.</summary>
public static class FreeWillRules
{
    /// <summary>The free will config of the content, or null if it has none.</summary>
    /// <exception cref="InvalidOperationException">The content has more than one.</exception>
    public static FreeWillConfigDef? ConfigOf(DefDatabase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var configs = content.All<FreeWillConfigDef>();
        return configs.Count switch
        {
            0 => null,
            1 => configs[0],
            _ => throw new InvalidOperationException($"The content has {configs.Count} FreeWillConfig defs; at most one is allowed."),
        };
    }

    /// <summary>
    /// The stock target of each category that has one, in the order of the categories of the
    /// interface: the player's, or the config's default. Empty without free will in the content.
    /// </summary>
    public static IReadOnlyList<StockTarget> Targets(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (ConfigOf(simulation.Content) is not { } config)
        {
            return [];
        }

        return [.. ItemCategoryDef.InOrder(simulation.Content)
            .Where(category => config.DefaultTargets.ContainsKey(category.Id))
            .Select(category => simulation.StockTargets.FirstOrDefault(target => target.Category == category.Id)
                                ?? new StockTarget(category.Id, config.DefaultTargets[category.Id]))];
    }

    /// <summary>
    /// The chance, in percent, that a colonist works on its own when it decides: the config's, by
    /// its trait for initiative and by its mood. Zero without free will in the content.
    /// </summary>
    public static int InitiativeChancePercent(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return ConfigOf(simulation.Content) is { } config
            ? ChancePercent(simulation, colonist, config, config.InitiativeChancePercent, config.InitiativeTrait)
            : 0;
    }

    /// <summary>
    /// A chance of the config scaled by a trait, from <see cref="FreeWillConfigDef.TraitPercent"/>
    /// at its lowest to its highest, and by the mood: less when it is low, more when it is high.
    /// </summary>
    internal static int ChancePercent(Simulation simulation, EntityId colonist, FreeWillConfigDef config, int basePercent, string trait)
    {
        var mood = MoodRules.Value(simulation, colonist);
        var moodPercent = mood < config.LowMoodBelow ? config.LowMoodPercent
            : mood > config.HighMoodAbove ? config.HighMoodPercent
            : 100;
        var traitPercent = config.TraitPercent.At(PersonalityRules.Value(simulation, colonist, trait));
        return (int)((long)basePercent * traitPercent * moodPercent / 10_000);
    }

    /// <summary>Whether a cell is near the village: within the config's radius of its centre on the plane, at any level.</summary>
    public static bool IsNearVillage(Simulation simulation, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (simulation.VillageCenter is not { } center || ConfigOf(simulation.Content) is not { } config)
        {
            return false;
        }

        long dx = cell.X - center.X;
        long dy = cell.Y - center.Y;
        return (dx * dx) + (dy * dy) <= (long)config.VillageRadius * config.VillageRadius;
    }
}
