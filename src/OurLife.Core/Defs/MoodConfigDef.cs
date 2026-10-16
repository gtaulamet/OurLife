namespace OurLife.Core.Defs;

/// <summary>
/// The numbers of mood, crises and desires, and the thought each event leaves (see "Pensamientos y
/// ánimo" and "Deseos" in <c>docs/gdd/vida-y-generaciones.md</c>). The code knows the events; the
/// content says which thought each one gives. The content must define exactly one.
/// </summary>
public sealed class MoodConfigDef : Def
{
    /// <summary>The mood without thoughts.</summary>
    public required int Base { get; init; }

    /// <summary>The speed of work by mood: the first band whose <see cref="MoodBand.UpTo"/> reaches the mood.</summary>
    public required IReadOnlyList<MoodBand> WorkSpeed { get; init; }

    /// <summary>Under this mood a colonist may break down.</summary>
    public required int CrisisBelow { get; init; }

    /// <summary>The chance of a crisis each hour under <see cref="CrisisBelow"/>, in percent.</summary>
    public required int CrisisChancePercent { get; init; }

    public required int MaxDesires { get; init; }

    /// <summary>Days of the game between two new desires of a colonist.</summary>
    public required int DaysBetweenDesires { get; init; }

    /// <summary>Days of the game a desire waits to be fulfilled before it is frustrated.</summary>
    public required int DesireExpireDays { get; init; }

    /// <summary>What losing a member of the family does to the personality.</summary>
    public TraitShift? Grief { get; init; }

    public string? SleptInBed { get; init; }

    public string? SleptOnFloor { get; init; }

    public string? Learned { get; init; }

    public string? NewPartner { get; init; }

    public string? Breakup { get; init; }

    public string? DeathPartner { get; init; }

    public string? DeathChild { get; init; }

    public string? DeathParent { get; init; }

    public string? DeathSibling { get; init; }

    public string? DeathFriend { get; init; }

    public string? DeathColonist { get; init; }

    public string? DesireFulfilled { get; init; }

    public string? DesireFrustrated { get; init; }

    internal override IEnumerable<DefReference> References()
    {
        var thoughts = new[]
        {
            SleptInBed, SleptOnFloor, Learned, NewPartner, Breakup, DeathPartner, DeathChild, DeathParent, DeathSibling, DeathFriend, DeathColonist,
            DesireFulfilled, DesireFrustrated,
        };
        foreach (var thought in thoughts)
        {
            yield return new DefReference(typeof(ThoughtDef), thought, "thought");
        }

        yield return new DefReference(typeof(TraitDef), Grief?.Trait, "grief.trait");
    }

    internal override IEnumerable<string> Validate()
    {
        if (WorkSpeed.Count == 0 || WorkSpeed[^1].UpTo < 100 || WorkSpeed.Any(band => band.Percent <= 0)
            || WorkSpeed.Zip(WorkSpeed.Skip(1)).Any(pair => pair.Second.UpTo <= pair.First.UpTo))
        {
            yield return "workSpeed needs bands going up to 100, each with a positive percent.";
        }

        if (Base is < 0 or > 100 || CrisisChancePercent is < 0 or > 100)
        {
            yield return "base and crisisChancePercent must be between 0 and 100.";
        }

        if (MaxDesires < 0 || DaysBetweenDesires < 0 || DesireExpireDays <= 0)
        {
            yield return "maxDesires and daysBetweenDesires must be zero or more, and desireExpireDays positive.";
        }
    }
}

/// <summary>A range of mood, up to <see cref="UpTo"/> included, and the speed of work it gives.</summary>
public sealed class MoodBand
{
    public required int UpTo { get; init; }

    public required int Percent { get; init; }
}
