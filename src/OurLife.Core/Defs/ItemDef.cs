namespace OurLife.Core.Defs;

public enum ItemKind
{
    Raw,
    Food,
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

    internal override IEnumerable<DefReference> References()
    {
        yield return new DefReference(typeof(ItemCategoryDef), Category, "category");
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
    }
}
