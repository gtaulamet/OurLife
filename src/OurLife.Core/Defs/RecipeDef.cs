namespace OurLife.Core.Defs;

/// <summary>
/// What a recipe takes: a number of units of one item, or of any item of a category, optionally of
/// a minimum quality (see "Recetas" in <c>docs/gdd/oficios.md</c>). Exactly one of
/// <see cref="Item"/> and <see cref="Category"/> is set.
/// </summary>
public sealed class RecipeInput
{
    /// <summary>Id of the <see cref="ItemDef"/> it takes.</summary>
    public string? Item { get; init; }

    /// <summary>Id of the <see cref="ItemCategoryDef"/>: any item of the category does.</summary>
    public string? Category { get; init; }

    public required int Count { get; init; }

    /// <summary>Id of the lowest <see cref="QualityDef"/> that does; any quality if not set.</summary>
    public string? MinQuality { get; init; }
}

/// <summary>
/// What colonists make at a workshop: it takes <see cref="Inputs"/> delivered to the workshop,
/// needs <see cref="WorkTicks"/> of work and leaves <see cref="Outputs"/>. It is available when
/// <see cref="UnlockService"/> says so, like any content (see "Recetas" in <c>docs/gdd/oficios.md</c>).
/// </summary>
public sealed class RecipeDef : Def
{
    /// <summary>Id of the <see cref="BuildableDef"/> where it is made, which must be a workshop.</summary>
    public required string Workshop { get; init; }

    public required IReadOnlyList<RecipeInput> Inputs { get; init; }

    public required IReadOnlyList<ItemCount> Outputs { get; init; }

    /// <summary>Work ticks at the speed of 100 %, with every input at the workshop.</summary>
    public required int WorkTicks { get; init; }

    /// <summary>The outputs get the quality of a roll with the skill of the worker, instead of the default one.</summary>
    public bool Quality { get; init; }

    /// <summary>Place in the list of the workshop: lowest first, then by id.</summary>
    public int Order { get; init; }

    /// <summary>The first output, which production orders count.</summary>
    public ItemCount MainOutput => Outputs[0];

    internal override IEnumerable<DefReference> References()
    {
        yield return new DefReference(typeof(BuildableDef), Workshop, "workshop");
        foreach (var input in Inputs)
        {
            yield return new DefReference(typeof(ItemDef), input.Item, "inputs.item");
            yield return new DefReference(typeof(ItemCategoryDef), input.Category, "inputs.category");
            yield return new DefReference(typeof(QualityDef), input.MinQuality, "inputs.minQuality");
        }

        foreach (var output in Outputs)
        {
            yield return new DefReference(typeof(ItemDef), output.Item, "outputs.item");
        }
    }

    internal override IEnumerable<string> Validate()
    {
        if (Inputs.Count == 0)
        {
            yield return "inputs cannot be empty.";
        }

        if (Outputs.Count == 0)
        {
            yield return "outputs cannot be empty.";
        }

        if (Inputs.Any(input => (input.Item is null) == (input.Category is null)))
        {
            yield return "each input needs an item or a category, and only one.";
        }

        if (Inputs.Any(input => input.Count <= 0) || Outputs.Any(output => output.Count <= 0))
        {
            yield return "the counts of inputs and outputs must be positive.";
        }

        if (WorkTicks < 1)
        {
            yield return "workTicks must be at least 1.";
        }

        if (Order < 0)
        {
            yield return "order must be zero or positive.";
        }
    }
}
