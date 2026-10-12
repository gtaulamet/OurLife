namespace OurLife.Core.Defs;

/// <summary>What a buildable leaves on the map (see <c>docs/gdd/construccion.md</c>).</summary>
public enum BuildShape
{
    Wall,
    Floor,
    Door,
    Ramp,

    /// <summary>Stairs up on the cell and stairs down on the one above.</summary>
    Stairs,
}

/// <summary>Something colonists build from a blueprint: a wall, a floor, a door, a ramp or stairs.</summary>
public sealed class BuildableDef : Def
{
    public required BuildShape Shape { get; init; }

    /// <summary>Ids of the construction materials it can be built of; the first one is the default.</summary>
    public required IReadOnlyList<string> Materials { get; init; }

    /// <summary>Units of the material's item to bring before building.</summary>
    public required int Cost { get; init; }

    /// <summary>Work ticks to build it once every unit is there.</summary>
    public required int WorkTicks { get; init; }

    /// <summary>A dragged box gets blueprints only on its border, as walls want.</summary>
    public bool Outline { get; init; }

    /// <summary>Place in the build bar: lowest first, then by id.</summary>
    public int Order { get; init; }

    internal override IEnumerable<DefReference> References() =>
        Materials.Select(material => new DefReference(typeof(MaterialDef), material, "materials"));

    internal override IEnumerable<string> Validate()
    {
        if (Materials.Count == 0)
        {
            yield return "materials cannot be empty.";
        }

        if (Materials.Distinct(StringComparer.Ordinal).Count() != Materials.Count)
        {
            yield return "materials cannot repeat.";
        }

        if (Cost < 1)
        {
            yield return "cost must be at least 1.";
        }

        if (WorkTicks < 1)
        {
            yield return "workTicks must be at least 1.";
        }
    }
}
