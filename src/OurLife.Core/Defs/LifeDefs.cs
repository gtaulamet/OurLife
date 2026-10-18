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

    /// <summary>
    /// How much each experience moves the traits of personality at this stage, in percent: the
    /// years when the character takes shape move them more.
    /// </summary>
    public int TraitDriftPercent { get; init; } = 100;

    /// <summary>Whether a colonist at this stage interacts with others; a baby does not.</summary>
    public bool Interacts { get; init; } = true;

    /// <summary>Whether a colonist at this stage eats on its own; one that does not is fed by others (H3.7).</summary>
    public bool EatsAlone { get; init; } = true;

    /// <summary>
    /// With nothing to do, a colonist at this stage stays within this many cells of its mother, or
    /// of where it is without her; null wanders as usual.
    /// </summary>
    public int? CrawlRadius { get; init; }

    /// <summary>
    /// Instead of work, a colonist at this stage goes to play with another of a stage that plays, or
    /// with family, within this many cells; null does not play.
    /// </summary>
    public int? PlayRadius { get; init; }

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

        if (WorkSpeedPercent <= 0 || MoveSpeedPercent <= 0 || TraitDriftPercent < 0)
        {
            yield return "workSpeedPercent and moveSpeedPercent must be positive, and traitDriftPercent zero or more.";
        }

        if (NeedsPercent.Values.Any(percent => percent < 0))
        {
            yield return "needsPercent values must be zero or positive.";
        }

        if (CrawlRadius is < 1 || PlayRadius is < 1)
        {
            yield return "crawlRadius and playRadius must be at least 1.";
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

    /// <summary>Who can have children and how (H3.7); without it, nobody is born.</summary>
    public BirthConfig? Birth { get; init; }

    internal override IEnumerable<string> Validate()
    {
        foreach (var error in Birth?.Validate() ?? [])
        {
            yield return error;
        }

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

/// <summary>
/// Fertility, conception, pregnancy and birth (see "Parejas, embarazo y nacimiento" in
/// <c>docs/gdd/vida-y-generaciones.md</c>). Ages and lengths are in years and days of life.
/// </summary>
public sealed class BirthConfig
{
    /// <summary>The years of life, both included, in which a woman can have children.</summary>
    public required IntRange FemaleFertileYears { get; init; }

    /// <summary>The same for a man.</summary>
    public required IntRange MaleFertileYears { get; init; }

    /// <summary>The chance that a fertile couple conceives in a year of life, in percent.</summary>
    public required int ConceptionChancePerYear { get; init; }

    public required int PregnancyLifeDays { get; init; }

    /// <summary>From this day of the pregnancy on, the mother works at <see cref="LateWorkSpeedPercent"/>.</summary>
    public required int LateFromLifeDays { get; init; }

    public required int LateWorkSpeedPercent { get; init; }

    /// <summary>A couple does not conceive while the mother has a child younger than this, in years of life.</summary>
    public required int YearsBetweenChildren { get; init; }

    /// <summary>What parents and their newborn child think of each other.</summary>
    public required int ParentOpinion { get; init; }

    /// <summary>What a newborn and its brothers and sisters think of each other.</summary>
    public required int SiblingOpinion { get; init; }

    /// <summary>
    /// A couple conceives only while the colony has food for this many days for each colonist, at
    /// an adult's appetite; 0 does not look at the colony's food.
    /// </summary>
    public int FoodReserveDays { get; init; }

    /// <summary>
    /// A couple conceives only while the map has this many plants that give food for each
    /// colonist, the child to be born included: what the land can feed caps the colony. 0 does not
    /// count them.
    /// </summary>
    public int FoodPlantsPerColonist { get; init; }

    internal IEnumerable<string> Validate()
    {
        if (FemaleFertileYears.Min > FemaleFertileYears.Max || MaleFertileYears.Min > MaleFertileYears.Max
            || FemaleFertileYears.Min < 0 || MaleFertileYears.Min < 0)
        {
            yield return "birth fertile years need 0 <= min <= max.";
        }

        if (ConceptionChancePerYear is < 0 or > 100 || LateWorkSpeedPercent <= 0)
        {
            yield return "birth.conceptionChancePerYear must be between 0 and 100, and lateWorkSpeedPercent positive.";
        }

        if (PregnancyLifeDays <= 0 || LateFromLifeDays < 0 || YearsBetweenChildren < 0 || FoodReserveDays < 0 || FoodPlantsPerColonist < 0)
        {
            yield return "birth.pregnancyLifeDays must be positive, and lateFromLifeDays, yearsBetweenChildren, foodReserveDays and foodPlantsPerColonist zero or more.";
        }

        if (ParentOpinion is < -100 or > 100 || SiblingOpinion is < -100 or > 100)
        {
            yield return "birth opinions must be between -100 and 100.";
        }
    }
}
