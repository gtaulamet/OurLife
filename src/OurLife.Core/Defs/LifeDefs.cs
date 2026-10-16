namespace OurLife.Core.Defs;

/// <summary>
/// How fast colonists live (see <c>docs/gdd/vida-y-generaciones.md</c>): days of life that pass in a
/// game day. The player picks one for a new game, and it never changes afterwards.
/// </summary>
public sealed class LifePaceDef : Def
{
    public required int LifeDaysPerDay { get; init; }

    /// <summary>The pace of a new game unless the player picks another; exactly one pace has it.</summary>
    public bool Default { get; init; }

    internal override IEnumerable<string> Validate()
    {
        if (LifeDaysPerDay <= 0)
        {
            yield return "lifeDaysPerDay must be positive.";
        }
    }
}

/// <summary>What a colonist does as work at a stage of its life.</summary>
public enum StageWork
{
    /// <summary>No work at all.</summary>
    None,

    /// <summary>Only work types with <see cref="WorkTypeDef.Light"/>.</summary>
    Light,

    /// <summary>Every work type.</summary>
    All,
}

/// <summary>
/// A stage of life, like childhood or old age, from an age on. The code never names a stage: it
/// only reads these fields.
/// </summary>
public sealed class LifeStageDef : Def
{
    /// <summary>Age in years at which the stage starts; exactly one stage starts at 0.</summary>
    public required int FromYears { get; init; }

    public required StageWork Work { get; init; }

    /// <summary>Speed of work, in percent of an adult's.</summary>
    public int WorkSpeedPercent { get; init; } = 100;

    /// <summary>Speed of walking, in percent of the creature's.</summary>
    public int MoveSpeedPercent { get; init; } = 100;

    /// <summary>
    /// How fast each need drops, in percent of its <see cref="NeedDef.DecayPerHour"/>, by need id.
    /// A need left out drops at 100 %.
    /// </summary>
    public IReadOnlyDictionary<string, int> NeedsPercent { get; init; } = new Dictionary<string, int>();

    /// <summary>The percent at which a need drops at this stage.</summary>
    public int NeedPercent(string need) => NeedsPercent.TryGetValue(need, out var percent) ? percent : 100;

    internal override IEnumerable<DefReference> References()
    {
        // Sorted, so the messages come in the same order every time.
        foreach (var need in NeedsPercent.Keys.Order(StringComparer.Ordinal))
        {
            yield return new DefReference(typeof(NeedDef), need, "needsPercent");
        }
    }

    internal override IEnumerable<string> Validate()
    {
        if (FromYears < 0)
        {
            yield return "fromYears must be zero or positive.";
        }

        if (WorkSpeedPercent <= 0 || MoveSpeedPercent <= 0)
        {
            yield return "workSpeedPercent and moveSpeedPercent must be positive.";
        }

        if (NeedsPercent.Values.Any(percent => percent < 0))
        {
            yield return "needsPercent values must be zero or positive.";
        }
    }
}

/// <summary>The rules of life that are not tied to a stage or a pace. The content must define exactly one.</summary>
public sealed class LifeConfigDef : Def
{
    /// <summary>
    /// Chance of dying of old age in a year of life, by age, ascending: linear between two rows,
    /// none before the first, the last one's after it. At 100 % death is certain.
    /// </summary>
    public required IReadOnlyList<OldAgeChance> OldAge { get; init; }

    internal override IEnumerable<string> Validate()
    {
        if (OldAge.Count == 0)
        {
            yield return "oldAge cannot be empty.";
        }

        if (OldAge.Any(row => row.Years < 0 || row.PercentPerYear is < 0 or > 100))
        {
            yield return "oldAge rows need years >= 0 and percentPerYear between 0 and 100.";
        }

        for (var i = 1; i < OldAge.Count; i++)
        {
            if (OldAge[i].Years <= OldAge[i - 1].Years)
            {
                yield return "oldAge rows must go up in years.";
                break;
            }
        }
    }
}

/// <summary>A row of the old age table: from this age, this chance per year of life.</summary>
public sealed class OldAgeChance
{
    public required int Years { get; init; }

    public required int PercentPerYear { get; init; }
}
