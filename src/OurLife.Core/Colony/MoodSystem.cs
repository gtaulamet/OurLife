using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
using OurLife.Core.Text;

namespace OurLife.Core.Colony;

/// <summary>
/// Once an hour (see "Pensamientos y ánimo", "Crisis" and "Deseos" in
/// <c>docs/gdd/vida-y-generaciones.md</c>): thoughts that faded go away, a crisis ends or, in a very
/// low mood, may start, and desires come true or are frustrated. At midnight, a colonist may get a
/// desire it can have once a year of life: a partner if single, a child in a couple that can have
/// one. Draws from the random stream <c>mood.crisis</c>.
/// </summary>
public sealed class MoodSystem : ISimSystem
{
    private const string RandomStream = "mood.crisis";

    private readonly ColonyRules _colony;
    private readonly PersonalityRules _personalities;
    private readonly LifeRules _life;
    private readonly MoodConfigDef _config;
    private readonly List<EntityId> _colonists = [];

    public MoodSystem(ColonyRules colony, PersonalityRules personalities, LifeRules life, MoodConfigDef config)
    {
        ArgumentNullException.ThrowIfNull(colony);
        ArgumentNullException.ThrowIfNull(personalities);
        ArgumentNullException.ThrowIfNull(life);
        ArgumentNullException.ThrowIfNull(config);
        _colony = colony;
        _personalities = personalities;
        _life = life;
        _config = config;
    }

    /// <summary>The message when a crisis starts, like <c>event.mood.crisis.isolate</c>, with <c>colonist</c>.</summary>
    public static string StartKey(CrisisDef crisis)
    {
        ArgumentNullException.ThrowIfNull(crisis);
        return $"event.mood.crisis.{crisis.Id}";
    }

    /// <summary>The message when a crisis ends, like <c>event.mood.crisis_end.isolate</c>, with <c>colonist</c>.</summary>
    public static string EndKey(CrisisDef crisis)
    {
        ArgumentNullException.ThrowIfNull(crisis);
        return $"event.mood.crisis_end.{crisis.Id}";
    }

    /// <summary>The ticks of an hour, and the offset that puts midnight on one of them.</summary>
    public static (int Interval, int Offset) Schedule(TimeSettings time)
    {
        ArgumentNullException.ThrowIfNull(time);
        var hour = time.TicksPerHour;
        return (hour, LifeSystem.MidnightOffset(time) % hour);
    }

    public void Tick(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var midnight = simulation.CurrentTick % simulation.Time.TicksPerDay == LifeSystem.MidnightOffset(simulation.Time);
        world.Query<Colonist, Position>(_colonists);
        foreach (var colonist in _colonists)
        {
            if (world.TryGet<Mood>(colonist, out var mood))
            {
                mood.Thoughts.RemoveAll(thought => thought.UntilTick <= simulation.CurrentTick);
            }

            UpdateCrisis(simulation, colonist);
            UpdateDesires(simulation, colonist);
            if (midnight)
            {
                ConsiderYearly(simulation, colonist);
            }
        }
    }

    private void UpdateCrisis(Simulation simulation, EntityId colonist)
    {
        var world = simulation.World;
        if (world.TryGet<Crisis>(colonist, out var crisis))
        {
            if (crisis.UntilTick <= simulation.CurrentTick)
            {
                world.Remove<Crisis>(colonist);
                if (simulation.Content.TryGet<CrisisDef>(crisis.Kind, out var ended))
                {
                    Notify(simulation, EndKey(ended), colonist);
                }

                simulation.Events.Emit(new EntityChangedEvent(colonist));
            }

            return;
        }

        var random = simulation.Random.Get(RandomStream);
        if (MoodRules.Value(simulation, colonist) >= _config.CrisisBelow || !random.Chance(_config.CrisisChancePercent * 100)
            || Choose(simulation, colonist, random) is not { } kind)
        {
            return;
        }

        var ticksPerHour = simulation.Time.TicksPerHour;
        world.Add(colonist, new Crisis { Kind = kind.Id, UntilTick = simulation.CurrentTick + ((long)kind.DurationHours * ticksPerHour) });
        foreach (var shift in kind.Drift)
        {
            _personalities.Drift(simulation, colonist, shift.Trait, shift.Points);
        }

        // Every crisis stops the work under way.
        if (world.TryGet<Job>(colonist, out var job) && _colony.WorkTypeOf(job) is not null)
        {
            JobSystem.Drop(world, colonist);
        }

        Notify(simulation, StartKey(kind), colonist);
        simulation.Events.Emit(new EntityChangedEvent(colonist));
    }

    /// <summary>A crisis drawn by weight plus each trait's coefficient for each 10 points over 50; none if all weigh 0.</summary>
    private static CrisisDef? Choose(Simulation simulation, EntityId colonist, Pcg32 random)
    {
        var weighted = new List<(CrisisDef Crisis, int Weight)>();
        foreach (var crisis in simulation.Content.All<CrisisDef>())
        {
            var weight = crisis.Weight;
            foreach (var (trait, coefficient) in crisis.Traits.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                weight += coefficient * (PersonalityRules.Value(simulation, colonist, trait) - PersonalityRules.Average) / 10;
            }

            if (weight > 0)
            {
                weighted.Add((crisis, weight));
            }
        }

        if (weighted.Count == 0)
        {
            return null;
        }

        var roll = random.NextInt(weighted.Sum(entry => entry.Weight));
        foreach (var (crisis, weight) in weighted)
        {
            if (roll < weight)
            {
                return crisis;
            }

            roll -= weight;
        }

        return weighted[^1].Crisis;
    }

    private static void UpdateDesires(Simulation simulation, EntityId colonist)
    {
        if (!simulation.World.TryGet<Desires>(colonist, out var desires))
        {
            return;
        }

        foreach (var active in desires.Active.ToList())
        {
            if (DesireRules.IsMoot(simulation, active))
            {
                desires.Active.Remove(active);
            }
            else if (DesireRules.IsFulfilled(simulation, colonist, active))
            {
                desires.Active.Remove(active);
                MoodRules.Feel(simulation, colonist, config => config.DesireFulfilled);
            }
            else if (active.UntilTick <= simulation.CurrentTick)
            {
                desires.Active.Remove(active);
                MoodRules.Feel(simulation, colonist, config => config.DesireFrustrated);
            }
        }
    }

    /// <summary>
    /// Once a year of life, a colonist at the right stage may wish for a partner if it is single,
    /// or for a child if its couple can have one and may (a forbidden child is not even wished for).
    /// </summary>
    private void ConsiderYearly(Simulation simulation, EntityId colonist)
    {
        if (LifeRules.AgeYears(simulation, colonist) is not { } years || LifeRules.Stage(simulation, colonist) is not { } stage)
        {
            return;
        }

        var partner = Relationships.PartnerOf(simulation, colonist);
        var single = Relationships.IsFree(simulation, colonist);
        if ((!single && (partner.IsNone || !_life.CanHaveChildren(simulation, colonist, partner)))
            || (simulation.World.TryGet<Desires>(colonist, out var existing) && existing.LastYearlyYear == years))
        {
            return;
        }

        DesireRules.DesiresOf(simulation.World, colonist).LastYearlyYear = years;
        if (single)
        {
            DesireRules.Consider(simulation, colonist, DesireKind.FindPartner, eligible: desire => desire.Stages.Contains(stage.Id));
        }
        else
        {
            DesireRules.Consider(
                simulation, colonist, DesireKind.HaveChild, eligible: desire => desire.Stages.Contains(stage.Id),
                level: DesireRules.ChildrenOf(simulation, colonist));
        }
    }

    private static void Notify(Simulation simulation, string key, EntityId colonist) =>
        simulation.Events.Emit(new NotificationEvent(
            simulation.CurrentTick,
            ColonyText.Freeze(LocalizedText.Of(key).With("colonist", colonist), simulation.World),
            NotificationSeverity.Warning));
}
