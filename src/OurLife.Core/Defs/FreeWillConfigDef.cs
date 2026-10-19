namespace OurLife.Core.Defs;

/// <summary>
/// The numbers of free will (see <c>docs/gdd/libre-albedrio.md</c>): how near the village the
/// colonists work on their own, how keen they are, whom they seek and avoid, and the stock targets
/// a new game starts with. The content defines at most one; without it there is no free will.
/// </summary>
public sealed class FreeWillConfigDef : Def
{
    /// <summary>Cells around the village centre, on the plane and at any level, that count as near the village.</summary>
    public required int VillageRadius { get; init; }

    /// <summary>The chance of working on its own at each free decision, in percent, for an average colonist.</summary>
    public required int InitiativeChancePercent { get; init; }

    /// <summary>The trait that scales the chance of initiative.</summary>
    public required string InitiativeTrait { get; init; }

    /// <summary>The chance of seeking company at each free decision, in percent, for an average colonist.</summary>
    public required int SeekChancePercent { get; init; }

    /// <summary>The trait that scales the chance of seeking company.</summary>
    public required string SeekTrait { get; init; }

    /// <summary>The farthest, in cells on the plane, someone it seeks can be.</summary>
    public required int SeekRadius { get; init; }

    /// <summary>Rivals within this many cells are avoided when wandering.</summary>
    public required int AvoidRadius { get; init; }

    /// <summary>What a trait does to a chance: from <see cref="PercentRange.Low"/> at 0 to <see cref="PercentRange.High"/> at 100.</summary>
    public required PercentRange TraitPercent { get; init; }

    /// <summary>Under this mood, the chances are times <see cref="LowMoodPercent"/>.</summary>
    public required int LowMoodBelow { get; init; }

    public required int LowMoodPercent { get; init; }

    /// <summary>Over this mood, the chances are times <see cref="HighMoodPercent"/>.</summary>
    public required int HighMoodAbove { get; init; }

    public required int HighMoodPercent { get; init; }

    /// <summary>The stock target of each item category a new game starts with, by category id; only these have targets.</summary>
    public required IReadOnlyDictionary<string, int> DefaultTargets { get; init; }

    /// <summary>How much a press of the panel's buttons changes a target.</summary>
    public required int TargetStep { get; init; }

    /// <summary>The highest target the player can choose.</summary>
    public required int TargetMax { get; init; }

    internal override IEnumerable<DefReference> References()
    {
        yield return new DefReference(typeof(TraitDef), InitiativeTrait, "initiativeTrait");
        yield return new DefReference(typeof(TraitDef), SeekTrait, "seekTrait");

        // Sorted, so the messages come in the same order every time.
        foreach (var category in DefaultTargets.Keys.Order(StringComparer.Ordinal))
        {
            yield return new DefReference(typeof(ItemCategoryDef), category, "defaultTargets");
        }
    }

    internal override IEnumerable<string> Validate()
    {
        if (VillageRadius <= 0 || SeekRadius <= 0 || AvoidRadius < 0)
        {
            yield return "villageRadius and seekRadius must be positive, and avoidRadius zero or more.";
        }

        if (InitiativeChancePercent is < 0 or > 100 || SeekChancePercent is < 0 or > 100)
        {
            yield return "initiativeChancePercent and seekChancePercent must be between 0 and 100.";
        }

        if (TraitPercent.Low < 0 || TraitPercent.High < 0 || LowMoodPercent < 0 || HighMoodPercent < 0 || LowMoodBelow > HighMoodAbove)
        {
            yield return "the trait and mood percents must be zero or more, and lowMoodBelow not above highMoodAbove.";
        }

        if (TargetStep <= 0 || TargetMax <= 0 || DefaultTargets.Values.Any(target => target < 0 || target > TargetMax))
        {
            yield return "targetStep and targetMax must be positive, and every default target between 0 and targetMax.";
        }
    }
}
