using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// How fast a colonist works for the player, in percent of an average adult: its stage of life
/// times its traits times its skill in the work times its mood times the end of a pregnancy (see <c>docs/gdd/vida-y-generaciones.md</c>), and
/// never under 1 %. A job of <c>ticks</c> lasts <c>ticks × 100 / speed</c>. A read-only query.
/// </summary>
public static class WorkSpeed
{
    /// <param name="workType">The work being done, for its skill; null leaves the skill out.</param>
    public static int PercentOf(Simulation simulation, EntityId colonist, WorkTypeDef? workType, SkillRules skills)
    {
        ArgumentNullException.ThrowIfNull(skills);
        var percent = LifeRules.WorkSpeedPercent(simulation, colonist) * PersonalityRules.WorkSpeedPercent(simulation, colonist) / 100;
        percent = percent * skills.SpeedPercent(simulation, colonist, workType) / 100;
        percent = percent * LifeRules.PregnancyWorkSpeedPercent(simulation, colonist) / 100;
        return Math.Max(1, percent * MoodRules.WorkSpeedPercent(simulation, colonist) / 100);
    }
}
