using OurLife.Core.Entities;

namespace OurLife.Core.Defs;

/// <summary>Items obtained from a job, and how long the job takes.</summary>
public sealed class ItemYield
{
    public required string Item { get; init; }

    public required int Count { get; init; }

    public required int WorkTicks { get; init; }
}

/// <summary>
/// How a tree spreads on its own (see "Bosque que vuelve" in <c>docs/gdd/libre-albedrio.md</c>): each day an
/// adult may leave a sapling near it, until the species covers its share of the ground where it grows.
/// </summary>
public sealed class PlantRegrowth
{
    /// <summary>Chance, in basis points, that an adult leaves a sapling on a day.</summary>
    public required int SeedChanceBasisPoints { get; init; }

    /// <summary>The farthest, in cells on the plane, from the adult that a sapling appears.</summary>
    public required int SeedRadius { get; init; }

    /// <summary>Days until a sapling becomes an adult.</summary>
    public required int GrowDays { get; init; }

    /// <summary>
    /// The most cells, in thousandths of the cells where the species grows in the valley, that adults
    /// and saplings of the species take.
    /// </summary>
    public required int MaxCoverPerMille { get; init; }

    /// <summary>Seasons, by the ids of the calendar, in which the species leaves no saplings; the ones there keep growing.</summary>
    public IReadOnlyList<string> NoSeedSeasons { get; init; } = [];
}

/// <summary>A tree, bush or crop growing on the map.</summary>
public sealed class PlantDef : Def, IHasVisual
{
    public required string Visual { get; init; }

    /// <summary>True for trunks and other plants that colonists must walk around.</summary>
    public bool BlocksMovement { get; init; }

    /// <summary>What felling the plant gives; felling removes it.</summary>
    public ItemYield? Chop { get; init; }

    /// <summary>What gathering gives; the plant stays and regrows.</summary>
    public ItemYield? Harvest { get; init; }

    /// <summary>What a job on the plant gives: felling for <see cref="JobKind.Chop"/>, gathering for <see cref="JobKind.Harvest"/>, else nothing.</summary>
    public ItemYield? OutputOf(JobKind kind) => kind switch
    {
        JobKind.Chop => Chop,
        JobKind.Harvest => Harvest,
        _ => null,
    };

    /// <summary>How the plant spreads by itself; null for a plant that does not.</summary>
    public PlantRegrowth? Regrowth { get; init; }

    /// <summary>Days until a gathered plant can be gathered again.</summary>
    public int RegrowDays { get; init; }

    /// <summary>
    /// Seasons, by the ids of the calendar, in which a gathered plant does not bear fruit again:
    /// it waits for the first season that is not in the list. A plant that was not gathered keeps
    /// its fruit.
    /// </summary>
    public IReadOnlyList<string> DormantSeasons { get; init; } = [];

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

        if (Regrowth is { } regrowth)
        {
            if (regrowth.SeedChanceBasisPoints is < 0 or > 10_000 || regrowth.SeedRadius < 1 || regrowth.GrowDays < 1
                || regrowth.MaxCoverPerMille is < 0 or > 1000)
            {
                yield return "regrowth needs a seed chance up to 10000, a radius and grow days of at least 1 and a cover up to 1000.";
            }

            if (!BlocksMovement || Chop is null)
            {
                yield return "only a tree, which blocks the way and can be felled, spreads by itself.";
            }

            if (regrowth.NoSeedSeasons.Distinct(StringComparer.Ordinal).Count() != regrowth.NoSeedSeasons.Count)
            {
                yield return "regrowth.noSeedSeasons repeats a season.";
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

        if (DormantSeasons.Count > 0 && Harvest is null)
        {
            yield return "dormantSeasons only makes sense for a plant that can be gathered.";
        }

        if (DormantSeasons.Distinct(StringComparer.Ordinal).Count() != DormantSeasons.Count)
        {
            yield return "dormantSeasons repeats a season.";
        }
    }
}
