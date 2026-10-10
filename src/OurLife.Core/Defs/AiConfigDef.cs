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
    }
}
