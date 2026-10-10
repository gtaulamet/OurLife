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

        if (Items.Any(item => item.Count <= 0))
        {
            yield return "items.count must be positive.";
        }
    }
}
