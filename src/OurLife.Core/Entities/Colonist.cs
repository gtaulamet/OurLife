using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Map;

namespace OurLife.Core.Entities;

public enum Gender
{
    Male,
    Female,
}

/// <summary>A member of the colony: who it is and the targets it gave up on for a while.</summary>
public sealed class Colonist
{
    /// <summary>Proper name, from the creature's name list. Names are not translated.</summary>
    public string Name { get; set; } = string.Empty;

    public Gender Gender { get; set; }

    /// <summary>Id of its <see cref="Defs.CreatureDef"/>.</summary>
    [DefRef(typeof(CreatureDef))]
    public string Creature { get; set; } = string.Empty;

    /// <summary>Targets it found no path to, ignored until their tick passes, in the order they were added.</summary>
    public List<IgnoredTarget> Unreachable { get; set; } = [];

    /// <summary>Tick it started wandering with nothing to do, or null while it does something else.</summary>
    public long? IdleSinceTick { get; set; }

    /// <summary>The player was told it is idle; cleared when it takes something to do.</summary>
    public bool IdleAlerted { get; set; }
}

/// <summary>
/// A colonist's own work priorities (see <c>docs/gdd/gestion.md</c>): only those that differ from
/// <see cref="AiConfigDef.DefaultWorkPriority"/>, sorted by work type id.
/// </summary>
public sealed class WorkPriorities
{
    public List<WorkPriority> Entries { get; set; } = [];
}

/// <summary>The priority of a work type for a colonist: 1 is the highest, 4 the lowest, 0 never.</summary>
public sealed class WorkPriority
{
    [DefRef(typeof(WorkTypeDef))]
    public string WorkType { get; set; } = string.Empty;

    public int Priority { get; set; }
}

/// <summary>A target a colonist ignores until <see cref="UntilTick"/>: an entity, or a cell when the entity is none.</summary>
public sealed class IgnoredTarget
{
    public EntityId Entity { get; set; }

    public TileCoord Cell { get; set; }

    public long UntilTick { get; set; }
}
