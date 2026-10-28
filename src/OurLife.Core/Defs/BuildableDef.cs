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
/// What makes a piece of furniture a workshop (see "Talleres" in <c>docs/gdd/oficios.md</c>): the
/// work type of whoever works at it, which gives the skill and the priority of the work.
/// </summary>
public sealed class BuildableWorkstation
{
    /// <summary>Id of the <see cref="WorkTypeDef"/> of the work done here, one that is of a workshop.</summary>
    public required string WorkType { get; init; }
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

    /// <summary>
    /// Set for a workshop: furniture where colonists make what the <see cref="RecipeDef"/>s of
    /// this buildable say. Null for the rest.
    /// </summary>
    public BuildableWorkstation? Workstation { get; init; }

    /// <summary>What the furniture adds to the beauty of its room, before the multiplier of its quality; negative for a workshop.</summary>
    public int Beauty { get; init; }

    /// <summary>
    /// What kind of furniture it is, for the functions of a room (see <c>docs/gdd/habitaciones.md</c>):
    /// <c>bed</c>, <c>table</c>, <c>seat</c>, <c>chest</c>, <c>workshop</c>. Only furniture has them.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    public bool IsFurniture => Shape == BuildShape.Furniture;

    /// <summary>Whether the quality of the builder is rolled for what is built: furniture other than a workshop.</summary>
    public bool HasQuality => IsFurniture && Workstation is null;

    internal override IEnumerable<DefReference> References() =>
        Materials.Select(material => new DefReference(typeof(MaterialDef), material, "materials"))
            .Concat(ForStages.Select(stage => new DefReference(typeof(LifeStageDef), stage, "forStages")))
            .Append(new DefReference(typeof(WorkTypeDef), Workstation?.WorkType, "workstation.workType"));

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

        if (Workstation is not null && (!IsFurniture || RestRecoveryPercent is not null || Storage))
        {
            yield return "only furniture that is neither a bed nor a chest can be a workshop (workstation).";
        }

        if (Beauty != 0 && !IsFurniture)
        {
            yield return "only furniture has beauty.";
        }

        if (Tags.Count > 0 && !IsFurniture)
        {
            yield return "only furniture has tags.";
        }

        if (Tags.Any(string.IsNullOrWhiteSpace) || Tags.Distinct(StringComparer.Ordinal).Count() != Tags.Count)
        {
            yield return "tags cannot be empty or repeat.";
        }
    }
}
