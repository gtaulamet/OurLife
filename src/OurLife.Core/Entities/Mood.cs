using OurLife.Core.Defs;
using OurLife.Core.Ecs;

namespace OurLife.Core.Entities;

/// <summary>
/// The thoughts of events a colonist has, each until its tick (see "Pensamientos y ánimo" in
/// <c>docs/gdd/vida-y-generaciones.md</c>). Thoughts of a situation, like hunger, are not kept:
/// they come from the needs. The mood comes from both.
/// </summary>
public sealed class Mood
{
    public List<ActiveThought> Thoughts { get; set; } = [];

    /// <summary>Nights in a row it slept on the ground.</summary>
    public int FloorNights { get; set; }
}

/// <summary>A thought of an event, until the tick it fades.</summary>
public sealed class ActiveThought
{
    [DefRef(typeof(ThoughtDef))]
    public string Thought { get; set; } = string.Empty;

    public long UntilTick { get; set; }
}

/// <summary>A colonist's desires, oldest first (see "Deseos" in <c>docs/gdd/vida-y-generaciones.md</c>).</summary>
public sealed class Desires
{
    public List<ActiveDesire> Active { get; set; } = [];

    /// <summary>The game day the last desire was born, or null.</summary>
    public long? LastBornDay { get; set; }

    /// <summary>The year of life of the last yearly chance of a desire, so it comes once a year.</summary>
    public int? LastYearlyYear { get; set; }
}

/// <summary>A desire, about another colonist or a skill, and the tick it is frustrated if not fulfilled.</summary>
public sealed class ActiveDesire
{
    [DefRef(typeof(DesireDef))]
    public string Desire { get; set; } = string.Empty;

    /// <summary>The colonist it is about, or none.</summary>
    public EntityId Target { get; set; }

    /// <summary>The skill it is about, by work type, or null.</summary>
    [DefRef(typeof(WorkTypeDef))]
    public string? WorkType { get; set; }

    /// <summary>For a desire to learn, the level of the skill when it was born.</summary>
    public int Level { get; set; }

    public long UntilTick { get; set; }
}

/// <summary>A crisis under way, until its tick (see "Crisis" in <c>docs/gdd/vida-y-generaciones.md</c>).</summary>
public sealed class Crisis
{
    [DefRef(typeof(CrisisDef))]
    public string Kind { get; set; } = string.Empty;

    public long UntilTick { get; set; }
}
