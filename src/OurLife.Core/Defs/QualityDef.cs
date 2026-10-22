namespace OurLife.Core.Defs;

/// <summary>
/// One level of quality of what colonists make (see "Calidad" in <c>docs/gdd/oficios.md</c>):
/// poor, normal, good, excellent and masterwork in the medieval content. Levels are sorted by
/// <see cref="Order"/>, lowest quality first. A roll gives the first level whose
/// <see cref="Below"/> bound is greater than the score.
/// </summary>
public sealed class QualityDef : Def
{
    /// <summary>Place in the ladder of qualities: lowest quality first.</summary>
    public required int Order { get; init; }

    /// <summary>The level is what a roll gives while the score is lower than this (exclusive bound).</summary>
    public required int Below { get; init; }

    /// <summary>What the object is worth, in percent: the bonus of a tool, the beauty of a piece of furniture.</summary>
    public required int ValuePercent { get; init; }

    /// <summary>The comfort of a bed, in percent of its usual rest recovery.</summary>
    public required int ComfortPercent { get; init; }

    internal override IEnumerable<string> Validate()
    {
        if (Order < 0)
        {
            yield return "order must be zero or positive.";
        }

        if (Below <= 0)
        {
            yield return "below must be positive.";
        }

        if (ValuePercent <= 0 || ComfortPercent <= 0)
        {
            yield return "valuePercent and comfortPercent must be positive.";
        }
    }
}

/// <summary>
/// The rule of the quality roll: <c>score = d(<see cref="Die"/>) + <see cref="PerLevel"/> times the
/// level of the skill</c> (see "Calidad" in <c>docs/gdd/oficios.md</c>). The content must define
/// exactly one. Checked against the qualities by <c>QualityRules.From</c>.
/// </summary>
public sealed class QualityConfigDef : Def
{
    /// <summary>Id of the quality of anything made without a roll, and of every pile that does not say: normal.</summary>
    public required string DefaultQuality { get; init; }

    /// <summary>Sides of the die: the roll is an integer from 0 up to this, excluded.</summary>
    public required int Die { get; init; }

    /// <summary>Points each level of the skill adds to the score.</summary>
    public required int PerLevel { get; init; }

    internal override IEnumerable<DefReference> References()
    {
        yield return new DefReference(typeof(QualityDef), DefaultQuality, "defaultQuality");
    }

    internal override IEnumerable<string> Validate()
    {
        if (Die < 1)
        {
            yield return "die must be at least 1.";
        }

        if (PerLevel < 0)
        {
            yield return "perLevel must be zero or positive.";
        }
    }
}
