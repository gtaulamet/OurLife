namespace OurLife.Core.Defs;

/// <summary>Items obtained from a job, and how long the job takes.</summary>
public sealed class ItemYield
{
    public required string Item { get; init; }

    public required int Count { get; init; }

    public required int WorkTicks { get; init; }
}

/// <summary>A tree, bush or crop growing on the map.</summary>
public sealed class PlantDef : Def
{
    public required string Sprite { get; init; }

    /// <summary>True for trunks and other plants that colonists must walk around.</summary>
    public bool BlocksMovement { get; init; }

    /// <summary>What felling the plant gives; felling removes it.</summary>
    public ItemYield? Chop { get; init; }

    /// <summary>What gathering gives; the plant stays and regrows.</summary>
    public ItemYield? Harvest { get; init; }

    /// <summary>Days until a gathered plant can be gathered again.</summary>
    public int RegrowDays { get; init; }

    internal override IEnumerable<DefReference> References()
    {
        yield return new DefReference(typeof(ItemDef), Chop?.Item, "chop.item");
        yield return new DefReference(typeof(ItemDef), Harvest?.Item, "harvest.item");
    }

    internal override IEnumerable<string> Validate()
    {
        foreach (var (field, output) in new[] { ("chop", Chop), ("harvest", Harvest) })
        {
            if (output is not null && (output.Count <= 0 || output.WorkTicks <= 0))
            {
                yield return $"{field}.count and {field}.workTicks must be positive.";
            }
        }

        if (RegrowDays < 0)
        {
            yield return "regrowDays must be zero or positive.";
        }

        if (Harvest is not null && RegrowDays == 0)
        {
            yield return "a plant that can be gathered needs regrowDays.";
        }
    }
}
