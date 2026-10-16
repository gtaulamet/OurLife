using OurLife.Core.Entities;

namespace OurLife.Core.Defs;

/// <summary>
/// A kind of work colonists do for the player's orders, like mining or woodcutting, and the jobs
/// it groups. Colonists look for work of the type with the lowest <see cref="Order"/> first.
/// </summary>
public sealed class WorkTypeDef : Def
{
    /// <summary>Jobs done as work, for the player's orders or for the stockpiles, which work types group.</summary>
    public static readonly IReadOnlyList<JobKind> OrderJobs =
    [
        JobKind.Mine, JobKind.DigStairs, JobKind.Channel, JobKind.Chop, JobKind.Harvest,
        JobKind.Deliver, JobKind.Build, JobKind.Deconstruct, JobKind.Haul,
    ];

    public required int Order { get; init; }

    /// <summary>Ids of the jobs of this type, like <c>mine</c> or <c>chop</c>.</summary>
    public required IReadOnlyList<string> Jobs { get; init; }

    /// <summary>Light work, which colonists at a stage of life with <see cref="StageWork.Light"/> also do.</summary>
    public bool Light { get; init; }

    /// <summary>The work has a skill of its own, with the name of the work type (see <see cref="SkillConfigDef"/>).</summary>
    public bool Skill { get; init; }

    /// <summary>The jobs of this type, in the order listed.</summary>
    public IEnumerable<JobKind> JobKindsOf() => Jobs.Select(id => OrderJobs.First(kind => JobKinds.Id(kind) == id));

    internal override IEnumerable<string> Validate()
    {
        if (Order < 0)
        {
            yield return "order must be zero or positive.";
        }

        if (Jobs.Count == 0)
        {
            yield return "jobs cannot be empty.";
        }

        foreach (var job in Jobs.Where(job => !OrderJobs.Any(kind => JobKinds.Id(kind) == job)))
        {
            yield return $"'{job}' is not a job for the player's orders; use {string.Join(", ", OrderJobs.Select(JobKinds.Id))}.";
        }

        if (Jobs.Distinct(StringComparer.Ordinal).Count() != Jobs.Count)
        {
            yield return "jobs cannot repeat.";
        }
    }
}

/// <summary>The range of the work priorities of a colonist (see <c>docs/gdd/gestion.md</c>).</summary>
public static class WorkPriorityLevels
{
    /// <summary>The colonist never does this work.</summary>
    public const int Never = 0;

    public const int Highest = 1;

    public const int Lowest = 4;
}
