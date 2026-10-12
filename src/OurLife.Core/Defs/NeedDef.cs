namespace OurLife.Core.Defs;

/// <summary>How a creature satisfies a need; the colony systems know each way.</summary>
public enum NeedSatisfaction
{
    /// <summary>Eating food, which restores the item's nutrition.</summary>
    Eating,

    /// <summary>Sleeping where it stands, which restores <see cref="NeedDef.RecoveryPerHour"/>.</summary>
    Sleeping,
}

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

    public required NeedSatisfaction SatisfiedBy { get; init; }

    /// <summary>Points restored per hour while sleeping. Only for needs satisfied by sleeping.</summary>
    public int RecoveryPerHour { get; init; }

    /// <summary>
    /// At night (see <see cref="AiConfigDef"/>), below this value the creature goes to sleep before
    /// any work. Only for needs satisfied by sleeping; without it, the low threshold counts at night.
    /// </summary>
    public int? NightThreshold { get; init; }

    /// <summary>The value below which, at night, the need comes before any work.</summary>
    public int NightThresholdOrLow => NightThreshold ?? LowThreshold;

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

        if (SatisfiedBy == NeedSatisfaction.Sleeping && RecoveryPerHour <= 0)
        {
            yield return "a need satisfied by sleeping needs a positive recoveryPerHour.";
        }

        if (SatisfiedBy != NeedSatisfaction.Sleeping && RecoveryPerHour != 0)
        {
            yield return "only needs satisfied by sleeping have recoveryPerHour.";
        }

        if (NightThreshold is not null && SatisfiedBy != NeedSatisfaction.Sleeping)
        {
            yield return "only needs satisfied by sleeping have nightThreshold.";
        }

        if (NightThreshold is { } night && !(night >= LowThreshold && night <= Max))
        {
            yield return "nightThreshold must satisfy lowThreshold <= nightThreshold <= max.";
        }
    }
}
