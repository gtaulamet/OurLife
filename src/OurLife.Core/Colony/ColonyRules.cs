using OurLife.Core.Defs;
using OurLife.Core.Entities;

namespace OurLife.Core.Colony;

/// <summary>The content the colony systems work with, checked once when they are added.</summary>
public sealed class ColonyRules
{
    private ColonyRules(AiConfigDef ai, IReadOnlyList<WorkTypeDef> workTypes, IReadOnlySet<string> foodPlants, SkillRules skills, IReadOnlyDictionary<string, WorkTypeDef> recipeWorkTypes, KnowledgeConfigDef? knowledge, IReadOnlyList<KnowledgeDef> practiced)
    {
        Knowledge = knowledge;
        _practiced = practiced.GroupBy(def => def.Practice!.WorkType, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<KnowledgeDef>)[.. group], StringComparer.Ordinal);
        ResearchWorkType = knowledge is null ? null : workTypes.FirstOrDefault(type => type.Id == knowledge.WorkType && type.Workshop)
            ?? throw new InvalidOperationException($"The work type of research '{knowledge.WorkType}' must be a work type of workshops (workshop: true).");
        _recipeWorkTypes = recipeWorkTypes;
        Ai = ai;
        Skills = skills;
        WorkTypes = workTypes;
        FoodPlants = foodPlants;
        WorkJobs = workTypes.Select(type => (IReadOnlyList<JobKind>)type.JobKindsOf().ToArray()).ToArray();
    }

    private readonly IReadOnlyDictionary<string, WorkTypeDef> _recipeWorkTypes;
    private readonly Dictionary<string, IReadOnlyList<KnowledgeDef>> _practiced;

    /// <summary>The numbers of research, or null without them.</summary>
    public KnowledgeConfigDef? Knowledge { get; }

    /// <summary>The work type of research, or null without a config of knowledge.</summary>
    public WorkTypeDef? ResearchWorkType { get; }

    /// <summary>The knowledge that practicing a work type teaches, by the id of the work type; empty for others.</summary>
    public IReadOnlyList<KnowledgeDef> PracticedBy(string workType) => _practiced.TryGetValue(workType, out var list) ? list : [];

    public AiConfigDef Ai { get; }

    /// <summary>The skills of the work types (see <see cref="SkillRules"/>).</summary>
    public SkillRules Skills { get; }

    /// <summary>Work types in the order colonists look for work: by <c>order</c>, then id.</summary>
    public IReadOnlyList<WorkTypeDef> WorkTypes { get; }

    /// <summary>The jobs of each work type, in the same order as <see cref="WorkTypes"/>.</summary>
    public IReadOnlyList<IReadOnlyList<JobKind>> WorkJobs { get; }

    /// <summary>Ids of the plant defs whose harvest is food, so a hungry colonist skips the rest at once.</summary>
    public IReadOnlySet<string> FoodPlants { get; }

    /// <summary>The work type a job belongs to, or null for a job that is not work, like eating or gathering to eat.</summary>
    public WorkTypeDef? WorkTypeOf(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.ForFood)
        {
            return null;
        }

        if (job.Kind == JobKind.Research)
        {
            return ResearchWorkType;
        }

        // A craft is the work of the workshop of its recipe.
        if (job.Kind == JobKind.Craft)
        {
            return job.Recipe is not null && _recipeWorkTypes.TryGetValue(job.Recipe, out var crafted) ? crafted : null;
        }

        for (var type = 0; type < WorkTypes.Count; type++)
        {
            if (WorkJobs[type].Contains(job.Kind))
            {
                return WorkTypes[type];
            }
        }

        return null;
    }

    /// <exception cref="InvalidOperationException">
    /// The content has no AiConfig or several, no SkillConfig or several, or some job for the
    /// player's orders is in no work type or in more than one.
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

        var recipeWorkTypes = new Dictionary<string, WorkTypeDef>(StringComparer.Ordinal);
        foreach (var recipe in content.All<RecipeDef>())
        {
            if (content.Get<BuildableDef>(recipe.Workshop).Workstation is { } station)
            {
                recipeWorkTypes[recipe.Id] = content.Get<WorkTypeDef>(station.WorkType);
            }
        }

        var knowledgeConfigs = content.All<KnowledgeConfigDef>();
        if (knowledgeConfigs.Count > 1)
        {
            throw new InvalidOperationException($"The colony needs at most one KnowledgeConfig def, found {knowledgeConfigs.Count}.");
        }

        var practiced = content.All<KnowledgeDef>().Where(def => def.Practice is not null).OrderBy(def => def.Id, StringComparer.Ordinal).ToList();
        return new ColonyRules(configs[0], workTypes, foodPlants, SkillRules.From(content), recipeWorkTypes, knowledgeConfigs.Count == 1 ? knowledgeConfigs[0] : null, practiced);
    }
}
