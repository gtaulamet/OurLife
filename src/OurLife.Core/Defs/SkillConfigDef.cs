namespace OurLife.Core.Defs;

/// <summary>
/// The numbers of the skills (see "Habilidades" in <c>docs/gdd/vida-y-generaciones.md</c>): there
/// is one skill per work type with <see cref="WorkTypeDef.Skill"/>. The content must define exactly one.
/// </summary>
public sealed class SkillConfigDef : Def
{
    public required int MaxLevel { get; init; }

    /// <summary>Experience to go from level 0 to 1; each level after that needs <see cref="ExperiencePerLevel"/> more.</summary>
    public required int ExperienceBase { get; init; }

    public required int ExperiencePerLevel { get; init; }

    /// <summary>Speed of work at level 0, in percent; each level adds <see cref="SpeedPerLevel"/>.</summary>
    public required int SpeedBase { get; init; }

    public required int SpeedPerLevel { get; init; }

    /// <summary>Levels a teacher needs over its apprentice to teach it.</summary>
    public required int TeachingMinLevels { get; init; }

    /// <summary>Experience a lesson gives: this, plus <see cref="TeachingPerLevel"/> for each level of difference.</summary>
    public required int TeachingBase { get; init; }

    public required int TeachingPerLevel { get; init; }

    /// <summary>Experience to go from <paramref name="level"/> to the next.</summary>
    public int ExperienceFor(int level) => ExperienceBase + (ExperiencePerLevel * level);

    /// <summary>Speed of work at <paramref name="level"/>, in percent.</summary>
    public int SpeedAt(int level) => SpeedBase + (SpeedPerLevel * level);

    internal override IEnumerable<string> Validate()
    {
        if (MaxLevel <= 0 || ExperienceBase <= 0 || ExperiencePerLevel < 0)
        {
            yield return "maxLevel and experienceBase must be positive, and experiencePerLevel zero or more.";
        }

        if (SpeedBase <= 0 || SpeedPerLevel < 0)
        {
            yield return "speedBase must be positive and speedPerLevel zero or more.";
        }

        if (TeachingMinLevels < 1 || TeachingBase < 0 || TeachingPerLevel < 0)
        {
            yield return "teachingMinLevels must be at least 1, and the experience of teaching zero or more.";
        }
    }
}
