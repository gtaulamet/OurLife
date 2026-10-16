using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
using OurLife.Core.Text;

namespace OurLife.Core.Colony;

/// <summary>
/// Runs at midnight (see <c>docs/gdd/vida-y-generaciones.md</c>): each colonist adds the days of
/// life of the pace, may reach the next stage of life and, from the first age of the old age
/// table, may die of old age. Draws from the random stream <c>life.aging</c>.
/// </summary>
public sealed class LifeSystem : ISimSystem
{
    /// <summary>The message when a colonist dies of old age, with <c>colonist</c> and <c>years</c>.</summary>
    public const string OldAgeDeathKey = "event.life.old_age";

    private const string RandomStream = "life.aging";

    private readonly LifeRules _rules;
    private readonly List<EntityId> _colonists = [];

    public LifeSystem(LifeRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules;
    }

    /// <summary>The message when a colonist reaches a stage, like <c>event.life.stage.adult</c>, with <c>colonist</c>.</summary>
    public static string GrownKey(LifeStageDef stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        return $"event.life.stage.{stage.Id}";
    }

    /// <summary>
    /// The tick of the first midnight, and of every midnight after it once per day: a game starts
    /// at the start hour of the calendar. <see cref="ColonySystems"/> runs this system there.
    /// </summary>
    public static int MidnightOffset(TimeSettings time)
    {
        ArgumentNullException.ThrowIfNull(time);
        var minutesToMidnight = (TimeSettings.MinutesPerDay - (time.StartHour * 60)) % TimeSettings.MinutesPerDay;
        return minutesToMidnight / time.MinutesPerTick;
    }

    public void Tick(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var pace = _rules.PaceOf(simulation);
        var daysPerYear = simulation.Time.DaysPerYear;
        var deaths = 0;

        world.Query<Colonist, Life>(_colonists);
        foreach (var colonist in _colonists)
        {
            var life = world.Get<Life>(colonist);
            life.AgeDays = checked(life.AgeDays + pace.LifeDaysPerDay);
            var stage = _rules.StageAt(life.AgeDays, daysPerYear);
            if (stage.Id != life.Stage)
            {
                life.Stage = stage.Id;
                simulation.Events.Emit(new EntityChangedEvent(colonist));
                simulation.Events.Emit(new NotificationEvent(
                    simulation.CurrentTick,
                    ColonyText.Freeze(LocalizedText.Of(GrownKey(stage)).With("colonist", colonist), world),
                    NotificationSeverity.Info));
            }

            if (DiesOfOldAge(simulation, life.AgeDays, pace.LifeDaysPerDay))
            {
                var message = LocalizedText.Of(OldAgeDeathKey).With("colonist", colonist).With("years", life.AgeDays / daysPerYear);
                ColonistDeath.Die(simulation, colonist, message);
                deaths++;
            }
        }

        if (deaths > 0)
        {
            ColonistDeath.CheckColonyLost(simulation);
        }
    }

    private bool DiesOfOldAge(Simulation simulation, int ageDays, int lifeDaysPerDay)
    {
        var chance = _rules.OldAgeChance(ageDays, lifeDaysPerDay, simulation.Time.DaysPerYear);
        if (chance <= 0)
        {
            return false;
        }

        return chance >= LifeRules.Certain || simulation.Random.Get(RandomStream).NextInt(LifeRules.Certain) < chance;
    }
}
