using OurLife.Core.Defs;
using OurLife.Core.Entities;

namespace OurLife.Core.Colony;

/// <summary>The content the colony systems work with, checked once when they are added.</summary>
public sealed class ColonyRules
{
    private ColonyRules(AiConfigDef ai, IReadOnlyList<WorkTypeDef> workTypes, IReadOnlySet<string> foodPlants)
    {
        Ai = ai;
        WorkTypes = workTypes;
        FoodPlants = foodPlants;
        WorkJobs = workTypes.Select(type => (IReadOnlyList<JobKind>)type.JobKindsOf().ToArray()).ToArray();
    }

    public AiConfigDef Ai { get; }

    /// <summary>Work types in the order colonists look for work: by <c>order</c>, then id.</summary>
    public IReadOnlyList<WorkTypeDef> WorkTypes { get; }

    /// <summary>The jobs of each work type, in the same order as <see cref="WorkTypes"/>.</summary>
    public IReadOnlyList<IReadOnlyList<JobKind>> WorkJobs { get; }

    /// <summary>Ids of the plant defs whose harvest is food, so a hungry colonist skips the rest at once.</summary>
    public IReadOnlySet<string> FoodPlants { get; }

    /// <exception cref="InvalidOperationException">
    /// The content has no AiConfig or several, or some job for the player's orders is in no work
    /// type or in more than one.
    /// </exception>
    public static ColonyRules From(DefDatabase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var configs = content.All<AiConfigDef>();
        if (configs.Count != 1)
        {
            throw new InvalidOperationException($"The colony needs exactly one AiConfig def, found {configs.Count}.");
        }

        var workTypes = content.All<WorkTypeDef>()
            .OrderBy(type => type.Order)
            .ThenBy(type => type.Id, StringComparer.Ordinal)
            .ToArray();
        foreach (var job in WorkTypeDef.OrderJobs)
        {
            var owners = workTypes.Count(type => type.JobKindsOf().Contains(job));
            if (owners != 1)
            {
                throw new InvalidOperationException(
                    $"The job '{JobKinds.Id(job)}' must belong to exactly one WorkType def, found {owners}.");
            }
        }

        var foodPlants = content.All<PlantDef>()
            .Where(plant => plant.Harvest is { } harvest && content.Get<ItemDef>(harvest.Item).Kind == ItemKind.Food)
            .Select(plant => plant.Id)
            .ToHashSet(StringComparer.Ordinal);

        return new ColonyRules(configs[0], workTypes, foodPlants);
    }
}
