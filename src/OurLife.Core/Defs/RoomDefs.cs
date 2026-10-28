namespace OurLife.Core.Defs;

/// <summary>
/// The numbers of rooms, dormitories, dining and gathering (see <c>docs/gdd/habitaciones.md</c>).
/// The content defines at most one; without it there are no rooms.
/// </summary>
public sealed class RoomConfigDef : Def
{
    /// <summary>The fewest cells of a room.</summary>
    public required int MinCells { get; init; }

    /// <summary>The most cells of a room; a bigger space is outdoors.</summary>
    public required int MaxCells { get; init; }

    /// <summary>Points of score for each cell of a room.</summary>
    public required int SizePointsPerCell { get; init; }

    /// <summary>The most points the size gives.</summary>
    public required int SizeCap { get; init; }

    /// <summary>How far, in cells, a colonist looks for a chair to eat sitting.</summary>
    public required int DiningRadius { get; init; }

    /// <summary>How far, in cells, a colonist goes to gather in a dining room.</summary>
    public required int GatherRadius { get; init; }

    /// <summary>Chance, in percent before the scaling by sociability and mood, that a colonist with nothing to do gathers.</summary>
    public required int GatherChancePercent { get; init; }

    /// <summary>Hour of the day at which gathering starts, included.</summary>
    public required int GatherFromHour { get; init; }

    /// <summary>Hour of the day at which gathering ends, excluded.</summary>
    public required int GatherToHour { get; init; }

    /// <summary>Ticks a colonist stays seated at a gathering.</summary>
    public required int GatherTicks { get; init; }

    /// <summary>Beds with an owner from which a room is crowded if one of the owners has no relative among the others.</summary>
    public required int CrowdedBeds { get; init; }

    internal override IEnumerable<string> Validate()
    {
        if (MinCells < 1 || MaxCells < MinCells)
        {
            yield return "minCells must be at least 1 and maxCells at least minCells.";
        }

        if (SizePointsPerCell < 0 || SizeCap < 0)
        {
            yield return "sizePointsPerCell and sizeCap cannot be negative.";
        }

        if (DiningRadius < 1 || GatherRadius < 1)
        {
            yield return "diningRadius and gatherRadius must be at least 1.";
        }

        if (GatherChancePercent is < 0 or > 100)
        {
            yield return "gatherChancePercent must be between 0 and 100.";
        }

        if (GatherFromHour is < 0 or > 23 || GatherToHour is < 0 or > 24 || GatherFromHour >= GatherToHour)
        {
            yield return "gatherFromHour must be from 0 to 23, gatherToHour from 1 to 24, and the first below the second.";
        }

        if (GatherTicks < 1 || CrowdedBeds < 2)
        {
            yield return "gatherTicks must be at least 1 and crowdedBeds at least 2.";
        }
    }
}

/// <summary>
/// A level of quality of a room by score (see "Calidad de una habitación" in <c>docs/gdd/habitaciones.md</c>):
/// the highest level whose <see cref="From"/> the score reaches. It says which thought a bedroom or
/// a dining room of that level leaves, if any.
/// </summary>
public sealed class RoomLevelDef : Def
{
    /// <summary>Place in the ladder: lowest level first.</summary>
    public required int Order { get; init; }

    /// <summary>The fewest points of this level; the lowest level starts at 0 and takes every score below.</summary>
    public required int From { get; init; }

    /// <summary>The thought of waking in a bedroom of this level; none for a level that leaves none.</summary>
    public string? BedroomThought { get; init; }

    /// <summary>The thought of eating in a dining room of this level; none for a level that leaves none.</summary>
    public string? DiningThought { get; init; }

    internal override IEnumerable<DefReference> References()
    {
        yield return new DefReference(typeof(ThoughtDef), BedroomThought, "bedroomThought");
        yield return new DefReference(typeof(ThoughtDef), DiningThought, "diningThought");
    }

    internal override IEnumerable<string> Validate()
    {
        if (Order < 0 || From < 0)
        {
            yield return "order and from cannot be negative.";
        }
    }
}

/// <summary>A kind of furniture a room needs for a function: <see cref="Count"/> pieces with the tag.</summary>
public sealed class RoomNeed
{
    public required string Tag { get; init; }

    public required int Count { get; init; }
}

/// <summary>
/// A function a room can have (see "Funciones de una habitación" in <c>docs/gdd/habitaciones.md</c>),
/// from what it holds: it has it when it holds all of <see cref="Needs"/>, or, if the role says so,
/// when stockpile zones cover <see cref="StorageCellsPercent"/> of its cells. The furniture is told
/// by tag, so furniture of other eras joins by data.
/// </summary>
public sealed class RoomRoleDef : Def
{
    public required int Order { get; init; }

    public IReadOnlyList<RoomNeed> Needs { get; init; } = [];

    /// <summary>The role also holds when stockpile zones cover this percent of the cells; null if it does not depend on them.</summary>
    public int? StorageCellsPercent { get; init; }

    internal override IEnumerable<string> Validate()
    {
        if (Needs.Count == 0 && StorageCellsPercent is null)
        {
            yield return "a role needs something: needs or storageCellsPercent.";
        }

        if (Needs.Any(need => string.IsNullOrWhiteSpace(need.Tag) || need.Count < 1))
        {
            yield return "each need names a tag and wants at least one piece.";
        }

        if (StorageCellsPercent is < 1 or > 100)
        {
            yield return "storageCellsPercent must be between 1 and 100.";
        }
    }
}
