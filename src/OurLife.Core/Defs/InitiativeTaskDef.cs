using OurLife.Core.Entities;

namespace OurLife.Core.Defs;

/// <summary>
/// Work a colonist with free will does on its own while the village lacks something (see
/// "Trabajo por iniciativa" in <c>docs/gdd/libre-albedrio.md</c>): gathering or chopping the
/// plants near the village that give items of <see cref="Category"/>, while its stock is under
/// its target. It is work of the type that groups its job: that priority decides, and that skill
/// gains experience.
/// </summary>
public sealed class InitiativeTaskDef : Def
{
    /// <summary>The jobs a task can be, by their id.</summary>
    public static readonly IReadOnlyList<JobKind> Jobs = [JobKind.Harvest, JobKind.Chop];

    private JobKind? _kind;

    /// <summary>The job, <c>harvest</c> or <c>chop</c>.</summary>
    public required string Job { get; init; }

    /// <summary>The item category whose stock target the task looks at, and that the plants it works on give.</summary>
    public required string Category { get; init; }

    /// <summary>The job as a kind.</summary>
    public JobKind Kind => _kind ??= Jobs.First(kind => JobKinds.Id(kind) == Job);

    internal override IEnumerable<DefReference> References()
    {
        yield return new DefReference(typeof(ItemCategoryDef), Category, "category");
    }

    internal override IEnumerable<string> Validate()
    {
        if (!Jobs.Any(kind => JobKinds.Id(kind) == Job))
        {
            yield return $"job must be {string.Join(" or ", Jobs.Select(JobKinds.Id))}.";
        }
    }
}
