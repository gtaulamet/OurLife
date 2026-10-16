namespace OurLife.Core.Defs;

/// <summary>
/// A trait of personality, an axis from 0 to 100 like sociability (see "Personalidad" in
/// <c>docs/gdd/vida-y-generaciones.md</c>). The code never names a trait: what one changes is in
/// its fields, and the systems that read them come with each milestone.
/// </summary>
public sealed class TraitDef : Def
{
    /// <summary>The speed of work it gives, in percent, from a value of 0 to one of 100; none if not set.</summary>
    public PercentRange? WorkSpeed { get; init; }

    /// <summary>Below this value the colonist looks after its low needs before working; never if not set.</summary>
    public int? NeedsBeforeWorkBelow { get; init; }

    /// <summary>The experience it gives, in percent, working and learning from others; none if not set.</summary>
    public PercentRange? Experience { get; init; }

    /// <summary>How likely it makes starting an interaction, in percent of the base chance; none if not set.</summary>
    public PercentRange? InteractionChance { get; init; }

    internal override IEnumerable<string> Validate()
    {
        if (WorkSpeed is { } speed && (speed.Low <= 0 || speed.High <= 0))
        {
            yield return "workSpeed must be positive at both ends.";
        }

        if (Experience is { } experience && (experience.Low < 0 || experience.High < 0))
        {
            yield return "experience must be zero or more at both ends.";
        }

        if (InteractionChance is { } chance && (chance.Low < 0 || chance.High < 0))
        {
            yield return "interactionChance must be zero or more at both ends.";
        }

        if (NeedsBeforeWorkBelow is < 0 or > 100)
        {
            yield return "needsBeforeWorkBelow must be between 0 and 100.";
        }
    }
}

/// <summary>A percent that goes linearly from <see cref="Low"/> at a trait of 0 to <see cref="High"/> at 100.</summary>
public sealed class PercentRange
{
    public required int Low { get; init; }

    public required int High { get; init; }

    /// <summary>The percent at a value from 0 to 100, in whole numbers.</summary>
    public int At(int value) => Low + ((High - Low) * value / 100);
}

/// <summary>How personalities are drawn, inherited and moved. The content must define exactly one.</summary>
public sealed class PersonalityConfigDef : Def
{
    /// <summary>A trait drawn is the average of this many rolls from 0 to 100, which gathers the values near 50.</summary>
    public required int Rolls { get; init; }

    /// <summary>The part of a child's trait, in percent, that is the average of its parents; the rest is drawn.</summary>
    public required int InheritedPercent { get; init; }

    /// <summary>How many points each trait can move in a game day.</summary>
    public required int MaxDriftPerDay { get; init; }

    /// <summary>The weights of each orientation when one is drawn.</summary>
    public required OrientationWeights Orientations { get; init; }

    /// <summary>How the sheet words a trait: the first band whose <see cref="TraitBand.UpTo"/> reaches its value.</summary>
    public IReadOnlyList<TraitBand> Words { get; init; } = [];

    internal override IEnumerable<string> Validate()
    {
        if (Rolls <= 0)
        {
            yield return "rolls must be positive.";
        }

        if (InheritedPercent is < 0 or > 100)
        {
            yield return "inheritedPercent must be between 0 and 100.";
        }

        if (MaxDriftPerDay < 0)
        {
            yield return "maxDriftPerDay must be zero or positive.";
        }

        if (Words.Count > 0 && (Words[^1].UpTo < 100 || Words.Zip(Words.Skip(1)).Any(pair => pair.Second.UpTo <= pair.First.UpTo)))
        {
            yield return "words need bands going up to 100.";
        }

        // Someone must be attracted to the other sex: a founding couple draws from those weights alone.
        if (Orientations.Opposite < 0 || Orientations.Same < 0 || Orientations.Both < 0 || Orientations.Opposite + Orientations.Both == 0)
        {
            yield return "orientations need weights of zero or more, and opposite or both above zero.";
        }
    }
}

/// <summary>The word a trait gets on the sheet, from very low to very high; an average one gets none.</summary>
public enum TraitWord
{
    VeryLow,
    Low,
    Average,
    High,
    VeryHigh,
}

/// <summary>A range of a trait, up to <see cref="UpTo"/> included, and its word.</summary>
public sealed class TraitBand
{
    public required int UpTo { get; init; }

    public required TraitWord Word { get; init; }
}

/// <summary>The weights of the orientations: attracted to the other sex, to the same, or to both.</summary>
public sealed class OrientationWeights
{
    public required int Opposite { get; init; }

    public required int Same { get; init; }

    public required int Both { get; init; }
}
