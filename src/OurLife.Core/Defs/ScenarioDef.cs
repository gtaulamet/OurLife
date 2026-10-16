namespace OurLife.Core.Defs;

/// <summary>A number of units of an item.</summary>
public sealed class ItemCount
{
    public required string Item { get; init; }

    public required int Count { get; init; }
}

/// <summary>
/// How a game starts: who arrives in the valley and what they bring (see
/// <c>docs/gdd/colonos-e-ia.md</c>). The content has exactly one until IP7 brings the choice of site.
/// </summary>
public sealed class ScenarioDef : Def
{
    /// <summary>Creature of the colonists.</summary>
    public required string Creature { get; init; }

    public required int Colonists { get; init; }

    /// <summary>Each need of a colonist starts at random between this value and its maximum.</summary>
    public required int StartNeedsMin { get; init; }

    /// <summary>The youngest age of a colonist on arrival, in years (see <c>docs/gdd/vida-y-generaciones.md</c>).</summary>
    public required int FounderMinYears { get; init; }

    /// <summary>The oldest age of a colonist on arrival: any day of life up to the end of this year.</summary>
    public required int FounderMaxYears { get; init; }

    /// <summary>The lowest level of each skill of a colonist on arrival.</summary>
    public required int FounderSkillMin { get; init; }

    /// <summary>The highest level of each skill on arrival, before the bonus.</summary>
    public required int FounderSkillMax { get; init; }

    /// <summary>Levels added to one skill of each colonist, drawn, so each one has a strength.</summary>
    public required int FounderSkillBonus { get; init; }

    /// <summary>Items left at the arrival point.</summary>
    public IReadOnlyList<ItemCount> Items { get; init; } = [];

    internal override IEnumerable<DefReference> References()
    {
        yield return new DefReference(typeof(CreatureDef), Creature, "creature");
        foreach (var item in Items)
        {
            yield return new DefReference(typeof(ItemDef), item.Item, "items.item");
        }
    }

    internal override IEnumerable<string> Validate()
    {
        if (Colonists <= 0)
        {
            yield return "colonists must be positive.";
        }

        if (StartNeedsMin < 0)
        {
            yield return "startNeedsMin must be zero or positive.";
        }

        if (FounderMinYears < 0 || FounderMaxYears < FounderMinYears)
        {
            yield return "founder ages must satisfy 0 <= founderMinYears <= founderMaxYears.";
        }

        if (FounderSkillMin < 0 || FounderSkillMax < FounderSkillMin || FounderSkillBonus < 0)
        {
            yield return "founder skills must satisfy 0 <= founderSkillMin <= founderSkillMax, with founderSkillBonus zero or more.";
        }

        if (Items.Any(item => item.Count <= 0))
        {
            yield return "items.count must be positive.";
        }
    }
}
