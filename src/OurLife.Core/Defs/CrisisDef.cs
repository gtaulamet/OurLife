namespace OurLife.Core.Defs;

/// <summary>What a colonist in crisis does instead of its life as usual.</summary>
public enum CrisisBehavior
{
    /// <summary>Only looks after its needs and wanders.</summary>
    RefuseWork,

    /// <summary>Does not work or interact, and wanders away from the others.</summary>
    Isolate,
}

/// <summary>A mild breakdown of a colonist in a very low mood (see "Crisis" in <c>docs/gdd/vida-y-generaciones.md</c>).</summary>
public sealed class CrisisDef : Def
{
    public required int Weight { get; init; }

    /// <summary>Added to the weight for each 10 points of a trait over 50, by trait id.</summary>
    public IReadOnlyDictionary<string, int> Traits { get; init; } = new Dictionary<string, int>();

    public required int DurationHours { get; init; }

    public required CrisisBehavior Behavior { get; init; }

    /// <summary>Traits it moves when it starts.</summary>
    public IReadOnlyList<TraitShift> Drift { get; init; } = [];

    internal override IEnumerable<DefReference> References() =>
        Traits.Keys.Order(StringComparer.Ordinal).Concat(Drift.Select(shift => shift.Trait))
            .Select(trait => new DefReference(typeof(TraitDef), trait, "traits"));

    internal override IEnumerable<string> Validate()
    {
        if (Weight < 0 || DurationHours <= 0)
        {
            yield return "weight must be zero or more, and durationHours positive.";
        }
    }
}

/// <summary>A trait moved by some points.</summary>
public sealed class TraitShift
{
    public required string Trait { get; init; }

    public required int Points { get; init; }
}
