namespace OurLife.Core.Defs;

public enum ItemKind
{
    Raw,
    Food,
}

/// <summary>Something that can lie on the ground, be carried, stored or used.</summary>
public sealed class ItemDef : Def
{
    public required ItemKind Kind { get; init; }

    public required string Sprite { get; init; }

    /// <summary>Most units of this item that fit in one stack.</summary>
    public required int StackLimit { get; init; }

    /// <summary>Hunger points restored by eating one unit. Only for food.</summary>
    public int Nutrition { get; init; }

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
