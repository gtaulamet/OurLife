namespace OurLife.Core.Defs;

public enum ItemKind
{
    Raw,
    Food,
}

/// <summary>
/// What makes an item a tool a colonist can carry (see "Herramientas" in <c>docs/gdd/oficios.md</c>):
/// the work type it speeds up and by how much, before the multiplier of its quality.
/// </summary>
public sealed class ItemTool
{
    /// <summary>Id of the <see cref="WorkTypeDef"/> the tool speeds up.</summary>
    public required string WorkType { get; init; }

    /// <summary>Speed it adds to the work, in percent, for a tool of the default quality.</summary>
    public required int SpeedPercent { get; init; }
}

/// <summary>Something that can lie on the ground, be carried, stored or used.</summary>
public sealed class ItemDef : Def, IHasVisual
{
    public required ItemKind Kind { get; init; }

    /// <summary>Id of the <see cref="ItemCategoryDef"/> the stockpiles filter it by.</summary>
    public required string Category { get; init; }

    public required string Visual { get; init; }

    /// <summary>Most units of this item that fit in one stack.</summary>
    public required int StackLimit { get; init; }

    /// <summary>Hunger points restored by eating one unit. Only for food.</summary>
    public int Nutrition { get; init; }

    /// <summary>Days until the item rots; 0 for what does not rot. The decay itself arrives with the farming hito of IP4.</summary>
    public int SpoilDays { get; init; }

    /// <summary>A prepared dish. Only for food.</summary>
    public bool Meal { get; init; }

    /// <summary>
    /// Nutrition that counts for the village's food reserve although the item is not eaten as it is,
    /// like grain (see <c>docs/gdd/agricultura.md</c>). Only for what is not food.
    /// </summary>
    public int ReserveValue { get; init; }

    /// <summary>Set for the items colonists equip as a tool; null for the rest.</summary>
    public ItemTool? Tool { get; init; }

    internal override IEnumerable<DefReference> References()
    {
        yield return new DefReference(typeof(ItemCategoryDef), Category, "category");
        yield return new DefReference(typeof(WorkTypeDef), Tool?.WorkType, "tool.workType");
    }

    internal override IEnumerable<string> Validate()
    {
        if (StackLimit < 1)
        {
            yield return "stackLimit must be at least 1.";
        }

        if (Kind == ItemKind.Food && Nutrition <= 0)
        {
            yield return "food needs a positive nutrition.";
        }

        if (Kind != ItemKind.Food && Nutrition != 0)
        {
            yield return "only food can have nutrition.";
        }

        if (SpoilDays < 0)
        {
            yield return "spoilDays must be zero or positive.";
        }

        if (Meal && Kind != ItemKind.Food)
        {
            yield return "only food can be a meal.";
        }

        if (ReserveValue < 0 || (ReserveValue > 0 && Kind == ItemKind.Food))
        {
            yield return "reserveValue must be zero or positive, and only for what is not food: food counts by its nutrition.";
        }

        if (Tool is not null && (Tool.SpeedPercent <= 0 || StackLimit != 1))
        {
            yield return "a tool needs a positive speedPercent and a stackLimit of 1: each tool has its own quality.";
        }
    }
}
