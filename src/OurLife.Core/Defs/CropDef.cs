namespace OurLife.Core.Defs;

/// <summary>
/// What a farm zone grows (see "Cultivos" in <c>docs/gdd/agricultura.md</c>): either a plant that
/// is sown, grows by the days of the growing seasons and is reaped for <see cref="Count"/> units of
/// <see cref="Item"/>, or a tree (<see cref="Tree"/>) whose sapling is planted every
/// <see cref="Spacing"/> cells and then grows as the forest does.
/// </summary>
public sealed class CropDef : Def
{
    /// <summary>Id of the <see cref="ItemDef"/> reaping gives; null for a tree.</summary>
    public string? Item { get; init; }

    /// <summary>Units reaping gives, before the skill of the reaper.</summary>
    public int Count { get; init; }

    /// <summary>Days of growth, counted at midnight in <see cref="GrowSeasons"/>, until it is ripe.</summary>
    public int GrowDays { get; init; }

    /// <summary>Seasons, by the ids of the calendar, in which a crop grows; in the others it waits.</summary>
    public IReadOnlyList<string> GrowSeasons { get; init; } = [];

    /// <summary>Work ticks of sowing, at a speed of 100 %.</summary>
    public required int SowTicks { get; init; }

    /// <summary>Work ticks of reaping, at a speed of 100 %.</summary>
    public int ReapTicks { get; init; }

    /// <summary>Percent of <see cref="Count"/> that a reaper of skill level 0 gets (see "Rendimiento" in <c>docs/gdd/agricultura.md</c>).</summary>
    public int YieldBasePercent { get; init; } = 100;

    /// <summary>Percent of <see cref="Count"/> that each level of the skill of farming adds.</summary>
    public int YieldPercentPerLevel { get; init; }

    /// <summary>Id of its model in the visual catalog; null for a tree, which is drawn as a plant.</summary>
    public string? Visual { get; init; }

    /// <summary>Id of the <see cref="PlantDef"/> that a tree crop plants as a sapling; null for a plant that is reaped.</summary>
    public string? Tree { get; init; }

    /// <summary>For a tree: a sapling goes on the cells whose <c>x</c> and <c>y</c> are multiples of this.</summary>
    public int Spacing { get; init; }

    public bool IsTree => Tree is not null;

    internal override IEnumerable<DefReference> References()
    {
        foreach (var reference in base.References())
        {
            yield return reference;
        }

        yield return new DefReference(typeof(ItemDef), Item, "item");
        yield return new DefReference(typeof(PlantDef), Tree, "tree");
    }

    internal override IEnumerable<string> Validate()
    {
        foreach (var error in base.Validate())
        {
            yield return error;
        }

        if (SowTicks <= 0)
        {
            yield return "sowTicks must be positive.";
        }

        if ((Item is null) == (Tree is null))
        {
            yield return "a crop gives an item or plants a tree: give item or tree, and only one.";
        }
        else if (Tree is null)
        {
            if (YieldBasePercent <= 0 || YieldPercentPerLevel < 0)
            {
                yield return "yieldBasePercent must be positive and yieldPercentPerLevel zero or more.";
            }

            if (Count <= 0 || GrowDays <= 0 || ReapTicks <= 0 || GrowSeasons.Count == 0 || Spacing != 0)
            {
                yield return "a crop that is reaped needs count, growDays, reapTicks and growSeasons, and no spacing.";
            }

            if (GrowSeasons.Distinct(StringComparer.Ordinal).Count() != GrowSeasons.Count)
            {
                yield return "growSeasons repeats a season.";
            }
        }
        else if (Spacing < 1 || Count != 0 || GrowDays != 0 || ReapTicks != 0 || GrowSeasons.Count != 0)
        {
            yield return "a tree crop needs a spacing of at least 1, and no count, growDays, reapTicks or growSeasons: the sapling grows as the forest does.";
        }
    }
}
