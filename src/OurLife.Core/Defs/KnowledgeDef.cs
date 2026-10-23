namespace OurLife.Core.Defs;

/// <summary>What practicing a work type gives a knowledge (see "Puntos" in <c>docs/gdd/conocimientos.md</c>).</summary>
public sealed class KnowledgePractice
{
    /// <summary>Id of the <see cref="WorkTypeDef"/> whose work teaches it.</summary>
    public required string WorkType { get; init; }

    /// <summary>Hundredths of a point for each tick of that work.</summary>
    public required int CentipointsPerTick { get; init; }
}

/// <summary>
/// Something the colony can learn, which unlocks content through <see cref="Def.Requires"/> (see
/// <c>docs/gdd/conocimientos.md</c>). Its own <see cref="Def.Requires"/> are what the colony must
/// know before it can learn it. It is learned by researching at a scriptorium and by practicing.
/// </summary>
public sealed class KnowledgeDef : Def
{
    /// <summary>The row of the panel: <c>crafts</c>, <c>field</c> or <c>fire_metal</c>.</summary>
    public string Branch { get; init; } = string.Empty;

    /// <summary>The column of the panel, from 1.</summary>
    public int Tier { get; init; } = 1;

    /// <summary>Points of research and practice to learn it; zero for what the colony knows at the start.</summary>
    public int Cost { get; init; }

    /// <summary>What practice gives it; none if it cannot be practiced.</summary>
    public KnowledgePractice? Practice { get; init; }

    /// <summary>Place within its branch.</summary>
    public int Order { get; init; }

    internal override IEnumerable<DefReference> References() =>
        base.References().Append(new DefReference(typeof(WorkTypeDef), Practice?.WorkType, "practice.workType"));

    internal override IEnumerable<string> Validate()
    {
        foreach (var error in base.Validate())
        {
            yield return error;
        }

        if (Cost < 0)
        {
            yield return "cost cannot be negative.";
        }

        if (Tier < 1)
        {
            yield return "tier must be 1 or more.";
        }

        if (Practice is { } practice)
        {
            if (practice.CentipointsPerTick <= 0)
            {
                yield return "practice.centipointsPerTick must be positive.";
            }

            if (Cost == 0)
            {
                yield return "a knowledge with no cost cannot be practiced.";
            }
        }
    }
}

/// <summary>
/// The numbers of research (see <c>docs/gdd/conocimientos.md</c>). The content defines at most
/// one; without it nothing is researched.
/// </summary>
public sealed class KnowledgeConfigDef : Def
{
    /// <summary>Share of the cost, in percent, that practice can give; the rest is only researched.</summary>
    public required int PracticeCapPercent { get; init; }

    /// <summary>Work ticks of one stretch of research, after which the researcher decides again.</summary>
    public required int ResearchChunkTicks { get; init; }

    /// <summary>Most knowledge in the queue.</summary>
    public required int QueueMax { get; init; }

    /// <summary>Id of the work type of research, that of the workshops where it is done.</summary>
    public required string WorkType { get; init; }

    internal override IEnumerable<DefReference> References() =>
        base.References().Append(new DefReference(typeof(WorkTypeDef), WorkType, "workType"));

    internal override IEnumerable<string> Validate()
    {
        foreach (var error in base.Validate())
        {
            yield return error;
        }

        if (PracticeCapPercent is < 0 or > 100)
        {
            yield return "practiceCapPercent must be from 0 to 100.";
        }

        if (ResearchChunkTicks <= 0)
        {
            yield return "researchChunkTicks must be positive.";
        }

        if (QueueMax <= 0)
        {
            yield return "queueMax must be positive.";
        }
    }
}
