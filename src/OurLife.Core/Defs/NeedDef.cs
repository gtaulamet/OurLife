namespace OurLife.Core.Defs;

/// <summary>
/// Something a creature has to keep up, like hunger or rest. Values go from 0 (empty) to
/// <see cref="Max"/> (fully satisfied) and drop over time.
/// </summary>
public sealed class NeedDef : Def
{
    public required int Max { get; init; }

    public required int DecayPerHour { get; init; }

    /// <summary>Below this value the creature looks after the need when it has nothing better to do.</summary>
    public required int LowThreshold { get; init; }

    /// <summary>Below this value the need comes before any work.</summary>
    public required int CriticalThreshold { get; init; }

    /// <summary>Hours at zero before the creature dies. Null means the need never kills.</summary>
    public int? DeathAfterHoursAtZero { get; init; }

    internal override IEnumerable<string> Validate()
    {
        if (Max <= 0)
        {
            yield return "max must be positive.";
        }

        if (DecayPerHour < 0)
        {
            yield return "decayPerHour must be zero or positive.";
        }

        if (!(CriticalThreshold >= 0 && CriticalThreshold < LowThreshold && LowThreshold < Max))
        {
            yield return "thresholds must satisfy 0 <= criticalThreshold < lowThreshold < max.";
        }

        if (DeathAfterHoursAtZero is <= 0)
        {
            yield return "deathAfterHoursAtZero must be positive when set.";
        }
    }
}
