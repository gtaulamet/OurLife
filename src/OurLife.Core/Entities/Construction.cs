using System.Text.Json.Serialization;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;

namespace OurLife.Core.Entities;

/// <summary>
/// A plan to build something on its cell (see <c>docs/gdd/construccion.md</c>), with the units of
/// the material's item brought so far.
/// </summary>
public sealed class Blueprint
{
    [DefRef(typeof(BuildableDef))]
    public string Buildable { get; set; } = string.Empty;

    [DefRef(typeof(MaterialDef))]
    public string Material { get; set; } = string.Empty;

    public int Delivered { get; set; }

    /// <summary>Quarter turns of a piece of furniture, from 0 to 3, kept when it is built.</summary>
    public int Rotation { get; set; }
}

/// <summary>
/// A piece of furniture on its cell (see <c>docs/gdd/muebles-y-camas.md</c>), turned
/// <see cref="Rotation"/> quarters. A bed also has <see cref="Bed"/>; a chest, a
/// <see cref="Stockpile"/> of its one cell.
/// </summary>
public sealed class Furniture
{
    [DefRef(typeof(BuildableDef))]
    public string Buildable { get; set; } = string.Empty;

    [DefRef(typeof(MaterialDef))]
    public string Material { get; set; } = string.Empty;

    /// <summary>Quarter turns, from 0 to 3; only the presentation uses them.</summary>
    public int Rotation { get; set; }

    /// <summary>
    /// Id of its <see cref="QualityDef"/>, rolled by whoever finished building it with the skill of
    /// construction; null for the default quality, and then not written, so furniture of a game
    /// from before it is saved as it was.
    /// </summary>
    [DefRef(typeof(QualityDef))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Quality { get; set; }
}

/// <summary>
/// A bed and the colonist it belongs to; none, or a colonist no longer alive, means it is free. Only a living
/// owner uses it (see <see cref="OurLife.Core.Colony.Beds.IsTaken"/>), so a bed the player assigns
/// (<c>AssignBedCommand</c>) needs no flag of its own: it is the owner that is set.
/// </summary>
public sealed class Bed
{
    public EntityId Owner { get; set; }
}
