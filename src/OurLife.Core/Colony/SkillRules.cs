using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// The skills (see "Habilidades" in <c>docs/gdd/vida-y-generaciones.md</c>), checked once: one per
/// work type with <see cref="WorkTypeDef.Skill"/>. Colonists gain experience working and being
/// taught, by their traits; each level makes the work faster.
/// </summary>
public sealed class SkillRules
{
    private const int Hundredths = 100;

    private SkillRules(SkillConfigDef config, IReadOnlyList<WorkTypeDef> skills)
    {
        Config = config;
        WorkTypes = skills;
    }

    public SkillConfigDef Config { get; }

    /// <summary>The work types that have a skill, by id.</summary>
    public IReadOnlyList<WorkTypeDef> WorkTypes { get; }

    /// <exception cref="InvalidOperationException">The content has no SkillConfig or several.</exception>
    public static SkillRules From(DefDatabase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var configs = content.All<SkillConfigDef>();
        if (configs.Count != 1)
        {
            throw new InvalidOperationException($"The colony needs exactly one SkillConfig def, found {configs.Count}.");
        }

        return new SkillRules(configs[0], [.. content.All<WorkTypeDef>().Where(type => type.Skill)]);
    }

    /// <summary>The skills of a colonist on arrival: each drawn between the scenario's levels, and one, drawn, with the bonus.</summary>
    public Skills Founder(ScenarioDef scenario, Pcg32 random)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(random);
        var skills = new Skills();
        foreach (var type in WorkTypes)
        {
            var level = Math.Min(Config.MaxLevel, random.NextInt(scenario.FounderSkillMin, scenario.FounderSkillMax + 1));
            skills.Values.Add(new SkillValue { WorkType = type.Id, Level = level });
        }

        if (skills.Values.Count > 0)
        {
            var strength = skills.Values[random.NextInt(skills.Values.Count)];
            strength.Level = Math.Min(Config.MaxLevel, strength.Level + scenario.FounderSkillBonus);
        }

        return skills;
    }

    /// <summary>
    /// The colonist gains <paramref name="points"/> of experience in the skill of a work type, times
    /// what its traits give, and goes up as many levels as they reach. Returns the levels gained.
    /// A work type without a skill, or a colonist without skills, gains nothing.
    /// </summary>
    public int Gain(Simulation simulation, EntityId colonist, WorkTypeDef workType, int points)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(workType);
        if (!workType.Skill || !simulation.World.TryGet<Skills>(colonist, out var skills) || skills.Find(workType.Id) is not { } skill)
        {
            return 0;
        }

        if (skill.Level >= Config.MaxLevel)
        {
            return 0;
        }

        skill.Experience += points * PersonalityRules.ExperiencePercent(simulation, colonist);
        var gained = 0;
        while (skill.Level < Config.MaxLevel && skill.Experience >= Config.ExperienceFor(skill.Level) * Hundredths)
        {
            skill.Experience -= Config.ExperienceFor(skill.Level) * Hundredths;
            skill.Level++;
            gained++;
        }

        if (skill.Level >= Config.MaxLevel)
        {
            skill.Experience = 0;
        }

        return gained;
    }

    /// <summary>
    /// A lesson: an apprentice learns the skill of a work type from a teacher with at least
    /// <see cref="SkillConfigDef.TeachingMinLevels"/> levels more, and gains experience by the
    /// difference. Returns the levels the apprentice gained; none if the teacher does not know enough.
    /// </summary>
    public int Teach(Simulation simulation, EntityId teacher, EntityId apprentice, WorkTypeDef workType)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(workType);
        if (Level(simulation, teacher, workType.Id) is not { } master || Level(simulation, apprentice, workType.Id) is not { } learner
            || master - learner < Config.TeachingMinLevels)
        {
            return 0;
        }

        return Gain(simulation, apprentice, workType, Config.TeachingBase + (Config.TeachingPerLevel * (master - learner)));
    }

    /// <summary>How fast the skill of a work type makes a colonist work, in percent; 100 for a work type without a skill or a colonist without skills.</summary>
    public int SpeedPercent(Simulation simulation, EntityId colonist, WorkTypeDef? workType) =>
        workType is { Skill: true } && Level(simulation, colonist, workType.Id) is { } level ? Config.SpeedAt(level) : 100;

    /// <summary>The level of a colonist in the skill of a work type, or null if it has no such skill.</summary>
    public static int? Level(Simulation simulation, EntityId colonist, string workType)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return simulation.World.TryGet<Skills>(colonist, out var skills) && skills.Find(workType) is { } skill ? skill.Level : null;
    }
}
