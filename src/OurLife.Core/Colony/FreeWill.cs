using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
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
            .Select(category => new StockTarget(category.Id, TargetOf(simulation, category.Id)!.Value))];
    }

    /// <summary>The stock target of a category: the player's, or the config's default; null if it has none.</summary>
    public static int? TargetOf(Simulation simulation, string category)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (ConfigOf(simulation.Content) is not { } config || !config.DefaultTargets.TryGetValue(category, out var fallback))
        {
            return null;
        }

        foreach (var target in simulation.StockTargets)
        {
            if (target.Category == category)
            {
                return target.Count;
            }
        }

        return fallback;
    }

    /// <summary>
    /// The chance, in basis points (10 000 is certain), that a colonist works on its own when it
    /// decides: the config's, by its trait for initiative and by its mood. Zero without free will
    /// in the content.
    /// </summary>
    public static int InitiativeChance(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return ConfigOf(simulation.Content) is { } config
            ? Chance(simulation, colonist, config, config.InitiativeChancePercent, config.InitiativeTrait)
            : 0;
    }

    /// <summary>
    /// The chance, in basis points, that a colonist seeks company when it decides: the config's,
    /// by its trait for seeking and by its mood. Zero without free will in the content.
    /// </summary>
    public static int SeekChance(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return ConfigOf(simulation.Content) is { } config
            ? Chance(simulation, colonist, config, config.SeekChancePercent, config.SeekTrait)
            : 0;
    }

    /// <summary>
    /// A chance of the config, in basis points, scaled by a trait, from
    /// <see cref="FreeWillConfigDef.TraitPercent"/> at its lowest to its highest, and by the mood:
    /// less when it is low, more when it is high. Basis points keep the fractions of a percent.
    /// </summary>
    internal static int Chance(Simulation simulation, EntityId colonist, FreeWillConfigDef config, int basePercent, string trait)
    {
        var mood = MoodRules.Value(simulation, colonist);
        var moodPercent = mood < config.LowMoodBelow ? config.LowMoodPercent
            : mood > config.HighMoodAbove ? config.HighMoodPercent
            : 100;
        var traitPercent = config.TraitPercent.At(PersonalityRules.Value(simulation, colonist, trait));
        return (int)((long)basePercent * traitPercent * moodPercent / 100);
    }

    /// <summary>
    /// Whether a visit to <paramref name="other"/> still makes sense (see "Vida social con
    /// intención" in <c>docs/gdd/libre-albedrio.md</c>): free will is on, both can interact, the
    /// visitor is in no crisis and they are not rivals.
    /// </summary>
    internal static bool StillWorthVisiting(Simulation simulation, EntityId colonist, EntityId other) =>
        simulation.FreeWill && Relationships.IsLivingColonist(simulation.World, other) && SocialSystem.CanInteract(simulation, colonist)
        && MoodRules.CrisisOf(simulation, colonist) is null && SocialSystem.CanInteract(simulation, other)
        && !Relationships.IsRival(simulation, colonist, other);

    /// <summary>
    /// Where to find a colonist to keep it company: where it is going if it is on its way to work
    /// or to wait a while, or carrying a load to a stockpile or a blueprint; else where it is.
    /// </summary>
    internal static TileCoord AimOf(World world, EntityId colonist)
    {
        if (world.TryGet<Job>(colonist, out var job))
        {
            if (job.Stage == JobStage.Moving)
            {
                return job.Cell;
            }

            // Only these carry to a fixed cell: a baby fed moves.
            if (job.Stage == JobStage.Carrying && job.Kind is JobKind.Haul or JobKind.Deliver)
            {
                return job.Destination;
            }
        }

        return world.Get<Position>(colonist).Cell;
    }

    /// <summary>Whether a cell is near the village: within the config's radius of its centre on the plane, at any level.</summary>
    public static bool IsNearVillage(Simulation simulation, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return simulation.VillageCenter is { } center && ConfigOf(simulation.Content) is { } config && IsNear(center, config.VillageRadius, cell);
    }

    /// <summary>Whether a cell is within <paramref name="radius"/> of <paramref name="center"/> on the plane, at any level: the rule of <see cref="IsNearVillage"/>.</summary>
    public static bool IsNear(TileCoord center, int radius, TileCoord cell)
    {
        long dx = cell.X - center.X;
        long dy = cell.Y - center.Y;
        return (dx * dx) + (dy * dy) <= (long)radius * radius;
    }
}
