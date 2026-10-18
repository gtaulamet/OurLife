namespace OurLife.Core.Defs;

/// <summary>What wakes a desire and what fulfils it; the code knows each kind.</summary>
public enum DesireKind
{
    /// <summary>Taught by someone who knows <see cref="DesireDef.MinLevels"/> more; fulfilled by going up a level in that skill.</summary>
    LearnFrom,

    /// <summary>After chatting with someone it thinks between the opinions; fulfilled by their friendship.</summary>
    Befriend,

    /// <summary>Single, once a year of life at a stage of <see cref="DesireDef.Stages"/>; fulfilled by forming a couple.</summary>
    FindPartner,

    /// <summary>After <see cref="DesireDef.Nights"/> nights in a row on the ground; fulfilled by having a bed.</summary>
    OwnBed,

    /// <summary>After arguing with someone it thought at least <see cref="DesireDef.MinOpinion"/> of; fulfilled when the opinion is back where it was.</summary>
    Reconcile,

    /// <summary>
    /// In a couple that could conceive, once a year of life at a stage of <see cref="DesireDef.Stages"/>,
    /// about the partner; fulfilled when the couple expects a child or one is born, and dropped if
    /// the couple is no more.
    /// </summary>
    HaveChild,
}

/// <summary>
/// Something a colonist wishes for (see "Deseos" in <c>docs/gdd/vida-y-generaciones.md</c>). Fulfilled,
/// it leaves a good thought; if it expires, a bad one.
/// </summary>
public sealed class DesireDef : Def
{
    public required DesireKind Kind { get; init; }

    /// <summary>The chance that the desire is born when its moment comes, in percent.</summary>
    public required int ChancePercent { get; init; }

    /// <summary>A trait that scales the chance: times its value over 50; none if not set.</summary>
    public string? ScaledBy { get; init; }

    public int MinLevels { get; init; }

    public int MinOpinion { get; init; }

    public int MaxOpinion { get; init; }

    public int Nights { get; init; }

    public IReadOnlyList<string> Stages { get; init; } = [];

    internal override IEnumerable<DefReference> References() =>
        Stages.Select(stage => new DefReference(typeof(LifeStageDef), stage, "stages"))
            .Append(new DefReference(typeof(TraitDef), ScaledBy, "scaledBy"));

    internal override IEnumerable<string> Validate()
    {
        if (ChancePercent is < 0 or > 100)
        {
            yield return "chancePercent must be between 0 and 100.";
        }

        if (Kind == DesireKind.Befriend && MinOpinion > MaxOpinion)
        {
            yield return "a befriend desire needs minOpinion <= maxOpinion.";
        }
    }
}
