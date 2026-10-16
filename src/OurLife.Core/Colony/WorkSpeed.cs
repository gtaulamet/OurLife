using OurLife.Core.Ecs;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// How fast a colonist works for the player, in percent of an average adult: its stage of life
/// times its traits (see <c>docs/gdd/vida-y-generaciones.md</c>). A job of <c>ticks</c> lasts
/// <c>ticks × 100 / speed</c>. A read-only query.
/// </summary>
public static class WorkSpeed
{
    public static int PercentOf(Simulation simulation, EntityId colonist) =>
        LifeRules.WorkSpeedPercent(simulation, colonist) * PersonalityRules.WorkSpeedPercent(simulation, colonist) / 100;
}
