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

    /// <summary>A piece of furniture: an entity with a model on the cell (see <c>docs/gdd/muebles-y-camas.md</c>).</summary>
    Furniture,
}

/// <summary>
/// Something colonists build from a blueprint: a wall, a floor, a door, a ramp, stairs, or a piece
/// of furniture.
/// </summary>
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

    /// <summary>The model of a piece of furniture; null for what is part of the terrain.</summary>
    public string? Visual { get; init; }

    /// <summary>
    /// The furniture is a bed: whoever sleeps on it recovers rest at this percent of the usual rate.
    /// </summary>
    public int? RestRecoveryPercent { get; init; }

    /// <summary>
    /// The stages of life whose colonists take the bed, like babies for a cradle; empty for every
    /// stage. Only for beds.
    /// </summary>
    public IReadOnlyList<string> ForStages { get; init; } = [];

    /// <summary>The furniture is a chest: a stockpile of one cell.</summary>
    public bool Storage { get; init; }

    public bool IsFurniture => Shape == BuildShape.Furniture;

    internal override IEnumerable<DefReference> References() =>
        Materials.Select(material => new DefReference(typeof(MaterialDef), material, "materials"))
            .Concat(ForStages.Select(stage => new DefReference(typeof(LifeStageDef), stage, "forStages")));

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

        if (IsFurniture != (Visual is not null))
        {
            yield return "furniture needs a visual, and only furniture has one.";
        }

        if (!IsFurniture && (RestRecoveryPercent is not null || Storage))
        {
            yield return "only furniture can be a bed (restRecoveryPercent) or a chest (storage).";
        }

        if (RestRecoveryPercent is <= 0)
        {
            yield return "restRecoveryPercent must be positive.";
        }

        if (ForStages.Count > 0 && RestRecoveryPercent is null)
        {
            yield return "only a bed (restRecoveryPercent) can be for some stages of life (forStages).";
        }

        if (RestRecoveryPercent is not null && Storage)
        {
            yield return "a piece of furniture is a bed or a chest, not both.";
        }
    }
}
