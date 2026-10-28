namespace OurLife.Core.Defs;

/// <summary>How many veins must show above ground, and how near the middle of the valley (see "Generación" in <c>docs/gdd/subsuelo.md</c>).</summary>
public sealed class OutcropSpec
{
    /// <summary>Veins that need a discovered cell within <see cref="Radius"/>.</summary>
    public required int Veins { get; init; }

    /// <summary>Cells from the middle of the valley, measured on the plane.</summary>
    public required int Radius { get; init; }
}

/// <summary>
/// Veins of ore the generation hides in the rock of the valley (see "Vetas de mineral" in
/// <c>docs/gdd/subsuelo.md</c>). A vein replaces wall cells of the <see cref="Hosts"/> only.
/// </summary>
public sealed class OreVeinDef : Def
{
    /// <summary>Id of the <see cref="MaterialDef"/> (kind ore) of the cells of a vein.</summary>
    public required string Material { get; init; }

    /// <summary>Ids of the rocks (<see cref="MaterialDef"/>) in which a vein may grow.</summary>
    public required IReadOnlyList<string> Hosts { get; init; }

    /// <summary>How many veins are generated.</summary>
    public required IntRange Veins { get; init; }

    /// <summary>The size of each vein, in cells.</summary>
    public required IntRange Cells { get; init; }

    /// <summary>The fewest cells of a vein that does not fit where it started: a smaller one is tried again elsewhere.</summary>
    public required int MinFit { get; init; }

    /// <summary>How many times a vein, or an outcrop, is tried before giving up.</summary>
    public required int Attempts { get; init; }

    public required OutcropSpec Outcrop { get; init; }

    internal override IEnumerable<DefReference> References()
    {
        yield return new DefReference(typeof(MaterialDef), Material, "material");
        for (var i = 0; i < Hosts.Count; i++)
        {
            yield return new DefReference(typeof(MaterialDef), Hosts[i], $"hosts[{i}]");
        }
    }

    internal override IEnumerable<string> Validate()
    {
        if (Hosts.Count == 0 || Hosts.Distinct(StringComparer.Ordinal).Count() != Hosts.Count || Hosts.Contains(Material))
        {
            yield return "hosts needs at least one rock, without repeats and without the material of the vein.";
        }

        if (Veins.Min < 0 || Veins.Max < Veins.Min)
        {
            yield return "veins must have a minimum of zero or more and a maximum not below it.";
        }

        if (Cells.Min < 1 || Cells.Max < Cells.Min)
        {
            yield return "cells must have a minimum of one or more and a maximum not below it.";
        }

        if (MinFit < 1 || MinFit > Cells.Min)
        {
            yield return "minFit must be at least one and not above cells.min.";
        }

        if (Attempts < 1)
        {
            yield return "attempts must be positive.";
        }

        if (Outcrop.Veins < 0 || Outcrop.Veins > Veins.Min || Outcrop.Radius < 0)
        {
            yield return "outcrop.veins must be between zero and veins.min, and outcrop.radius zero or more.";
        }
    }
}
