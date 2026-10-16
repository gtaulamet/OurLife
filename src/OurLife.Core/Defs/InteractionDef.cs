namespace OurLife.Core.Defs;

/// <summary>
/// Something two colonists do together, like chatting or arguing (see "Interacciones" in
/// <c>docs/gdd/vida-y-generaciones.md</c>). Who can do it, how likely it is and what it changes
/// are data; teaching and courting are mechanics the code knows by their flags, not by id.
/// </summary>
public sealed class InteractionDef : Def
{
    /// <summary>Stages of life of the colonist who starts it.</summary>
    public required IReadOnlyList<string> InitiatorStages { get; init; }

    /// <summary>Stages of life of the other one.</summary>
    public required IReadOnlyList<string> ReceiverStages { get; init; }

    /// <summary>The other one may also be family of any stage, as a child plays with its parents.</summary>
    public bool FamilyReceivers { get; init; }

    public required int Weight { get; init; }

    /// <summary>Added to the weight for each 10 points of a trait of the one who starts over 50, by trait id.</summary>
    public IReadOnlyDictionary<string, int> InitiatorTraits { get; init; } = new Dictionary<string, int>();

    /// <summary>The same, for the traits of the other one.</summary>
    public IReadOnlyDictionary<string, int> ReceiverTraits { get; init; } = new Dictionary<string, int>();

    /// <summary>Only if the one who starts thinks at least this of the other.</summary>
    public int? MinOpinion { get; init; }

    /// <summary>Only if the one who starts thinks at most this of the other.</summary>
    public int? MaxOpinion { get; init; }

    /// <summary>Only if the other one's mood is under this (see <c>Thought</c> and mood, H3.6).</summary>
    public int? ReceiverMoodBelow { get; init; }

    /// <summary>The one who starts teaches the other a skill it knows enough more of (see <see cref="SkillConfigDef.TeachingMinLevels"/>).</summary>
    public bool Teaches { get; init; }

    /// <summary>
    /// Courting: both single and the one who starts attracted to the other. It works if the other
    /// is attracted back and thinks at least <see cref="RomanceMinOpinion"/> of the one who starts.
    /// </summary>
    public bool Romantic { get; init; }

    public int RomanceMinOpinion { get; init; }

    /// <summary>Romance both gain when courting works.</summary>
    public IntRange? Romance { get; init; }

    /// <summary>What each one comes to think of the other.</summary>
    public IntRange? Opinion { get; init; }

    /// <summary>What only the other one comes to think of the one who starts.</summary>
    public IntRange? ReceiverOpinion { get; init; }

    /// <summary>A chance that it goes wrong, by a trait of the other one, which changes the opinion instead.</summary>
    public Backfire? Backfire { get; init; }

    /// <summary>Traits it may move.</summary>
    public IReadOnlyList<DriftEffect> Drift { get; init; } = [];

    /// <summary>The thought it leaves the one who starts it; none if not set.</summary>
    public string? InitiatorThought { get; init; }

    /// <summary>The thought it leaves the other one.</summary>
    public string? ReceiverThought { get; init; }

    /// <summary>For courting that does not work, the thought it leaves the one who courted.</summary>
    public string? RejectedThought { get; init; }

    /// <summary>
    /// The kind of desire it may wake: to learn more from the teacher, to befriend the other, or,
    /// between friends, to make up. None if not set.
    /// </summary>
    public DesireKind? Desire { get; init; }

    internal override IEnumerable<DefReference> References()
    {
        foreach (var stage in InitiatorStages.Concat(ReceiverStages))
        {
            yield return new DefReference(typeof(LifeStageDef), stage, "stages");
        }

        foreach (var trait in InitiatorTraits.Keys.Concat(ReceiverTraits.Keys).Order(StringComparer.Ordinal))
        {
            yield return new DefReference(typeof(TraitDef), trait, "traits");
        }

        if (Backfire is not null)
        {
            yield return new DefReference(typeof(TraitDef), Backfire.Trait, "backfire.trait");
        }

        foreach (var drift in Drift)
        {
            yield return new DefReference(typeof(TraitDef), drift.Trait, "drift.trait");
        }

        yield return new DefReference(typeof(ThoughtDef), InitiatorThought, "initiatorThought");
        yield return new DefReference(typeof(ThoughtDef), ReceiverThought, "receiverThought");
        yield return new DefReference(typeof(ThoughtDef), RejectedThought, "rejectedThought");
    }

    internal override IEnumerable<string> Validate()
    {
        if (InitiatorStages.Count == 0 || ReceiverStages.Count == 0)
        {
            yield return "initiatorStages and receiverStages cannot be empty.";
        }

        if (Weight < 0)
        {
            yield return "weight must be zero or positive.";
        }

        if (Romantic && Romance is null)
        {
            yield return "a romantic interaction needs romance.";
        }

        if (new[] { Romance, Opinion, ReceiverOpinion, Backfire?.Opinion }.Any(range => range is { } r && r.Min > r.Max))
        {
            yield return "ranges need min <= max.";
        }

        if (Backfire is { ChancePercent: < 0 or > 100 } || Drift.Any(drift => drift.ChancePercent is < 0 or > 100))
        {
            yield return "chances must be between 0 and 100.";
        }
    }
}

/// <summary>A whole number drawn between <see cref="Min"/> and <see cref="Max"/>, both included.</summary>
public sealed class IntRange
{
    public required int Min { get; init; }

    public required int Max { get; init; }
}

/// <summary>When the other one's trait is under <see cref="Below"/>, a chance that the opinions change by <see cref="Opinion"/> instead.</summary>
public sealed class Backfire
{
    public required string Trait { get; init; }

    public required int Below { get; init; }

    public required int ChancePercent { get; init; }

    public required IntRange Opinion { get; init; }
}

/// <summary>Who an effect of an interaction falls on.</summary>
public enum InteractionSide
{
    Initiator,
    Receiver,
    Both,
}

/// <summary>A chance that an interaction moves a trait (see <c>PersonalityRules.Drift</c>).</summary>
public sealed class DriftEffect
{
    public required InteractionSide Who { get; init; }

    public required string Trait { get; init; }

    public required int Points { get; init; }

    public required int ChancePercent { get; init; }
}
