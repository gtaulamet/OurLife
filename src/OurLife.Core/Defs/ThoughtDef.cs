namespace OurLife.Core.Defs;

/// <summary>Under which threshold of a need a thought of the situation holds.</summary>
public enum NeedLevel
{
    Low,
    Critical,
}

/// <summary>
/// Something a colonist thinks, which adds to its mood (see "Pensamientos y ánimo" in
/// <c>docs/gdd/vida-y-generaciones.md</c>). A thought of a situation, like being hungry, holds while
/// a need is under a threshold; a thought of an event lasts its hours.
/// </summary>
public sealed class ThoughtDef : Def
{
    public required int Mood { get; init; }

    /// <summary>Hours it lasts after the event; 0 for a thought of a situation.</summary>
    public required int DurationHours { get; init; }

    /// <summary>How many of it can add up at once; a new one beyond replaces the oldest.</summary>
    public int StackLimit { get; init; } = 1;

    /// <summary>For a thought of a situation: the need under its <see cref="Below"/> threshold.</summary>
    public string? Need { get; init; }

    public NeedLevel Below { get; init; }

    /// <summary>A thought of a situation this one stands in for, like starving for hungry.</summary>
    public string? Replaces { get; init; }

    internal override IEnumerable<DefReference> References()
    {
        yield return new DefReference(typeof(NeedDef), Need, "need");
        yield return new DefReference(typeof(ThoughtDef), Replaces, "replaces");
    }

    internal override IEnumerable<string> Validate()
    {
        if (DurationHours < 0 || StackLimit < 1)
        {
            yield return "durationHours must be zero or more, and stackLimit at least 1.";
        }

        if ((DurationHours == 0) != (Need is not null))
        {
            yield return "a thought lasts some hours after an event, or holds while a need is low: give durationHours or need, not both.";
        }
    }
}
