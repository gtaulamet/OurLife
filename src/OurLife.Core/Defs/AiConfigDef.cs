namespace OurLife.Core.Defs;

/// <summary>
/// Numbers of the colonists' behaviour (see <c>docs/gdd/colonos-e-ia.md</c>). The colony systems
/// need exactly one.
/// </summary>
public sealed class AiConfigDef : Def
{
    /// <summary>Ticks it takes to eat one unit of food.</summary>
    public required int EatTicksPerUnit { get; init; }

    /// <summary>Farthest a wandering colonist walks, in cells on each axis.</summary>
    public required int WanderRadius { get; init; }

    /// <summary>Fewest ticks a wandering colonist waits after walking.</summary>
    public required int WanderWaitMin { get; init; }

    /// <summary>Most ticks a wandering colonist waits after walking.</summary>
    public required int WanderWaitMax { get; init; }

    /// <summary>Ticks a colonist ignores a target it found no path to.</summary>
    public required int UnreachableCooldownTicks { get; init; }

    /// <summary>How often a busy colonist checks for a critical need and a wandering one looks for work.</summary>
    public required int RecheckTicks { get; init; }

    /// <summary>Hour the night starts, when colonists prefer to sleep (see <c>docs/gdd/dia-y-noche.md</c>).</summary>
    public required int NightStartHour { get; init; }

    /// <summary>Hour the night ends. It may be smaller than the start: the night then crosses midnight.</summary>
    public required int NightEndHour { get; init; }

    /// <summary>Share of a buildable's cost that taking it down gives back, in percent (see <c>docs/gdd/construccion.md</c>).</summary>
    public required int DeconstructRefundPercent { get; init; }

    /// <summary>Priority of a work type for a colonist who has none of its own, from 1 (highest) to 4 (see <c>docs/gdd/gestion.md</c>).</summary>
    public required int DefaultWorkPriority { get; init; }

    /// <summary>Hours a colonist wanders with nothing to do before the player is told.</summary>
    public required int IdleAlertHours { get; init; }

    /// <summary>
    /// Farthest from a workshop, in cells, a pile can be for a colonist to fetch it as an
    /// ingredient (see "El trabajo craft" in <c>docs/gdd/oficios.md</c>).
    /// </summary>
    public required int CraftFetchRadius { get; init; }

    /// <summary>True from <see cref="NightStartHour"/> until just before <see cref="NightEndHour"/>.</summary>
    public bool IsNight(int hour) =>
        NightStartHour < NightEndHour
            ? hour >= NightStartHour && hour < NightEndHour
            : hour >= NightStartHour || hour < NightEndHour;

    internal override IEnumerable<string> Validate()
    {
        if (EatTicksPerUnit <= 0)
        {
            yield return "eatTicksPerUnit must be positive.";
        }

        if (WanderRadius <= 0)
        {
            yield return "wanderRadius must be positive.";
        }

        if (WanderWaitMin < 0 || WanderWaitMax < WanderWaitMin)
        {
            yield return "wander waits must satisfy 0 <= wanderWaitMin <= wanderWaitMax.";
        }

        if (UnreachableCooldownTicks <= 0)
        {
            yield return "unreachableCooldownTicks must be positive.";
        }

        if (RecheckTicks <= 0)
        {
            yield return "recheckTicks must be positive.";
        }

        if (NightStartHour is < 0 or > 23 || NightEndHour is < 0 or > 23)
        {
            yield return "nightStartHour and nightEndHour must be hours from 0 to 23.";
        }
        else if (NightStartHour == NightEndHour)
        {
            yield return "the night must last: nightStartHour and nightEndHour must differ.";
        }

        if (DeconstructRefundPercent is < 0 or > 100)
        {
            yield return "deconstructRefundPercent must be between 0 and 100.";
        }

        if (DefaultWorkPriority is < WorkPriorityLevels.Highest or > WorkPriorityLevels.Lowest)
        {
            yield return "defaultWorkPriority must be from 1 to 4.";
        }

        if (IdleAlertHours <= 0)
        {
            yield return "idleAlertHours must be positive.";
        }

        if (CraftFetchRadius <= 0)
        {
            yield return "craftFetchRadius must be positive.";
        }
    }
}
