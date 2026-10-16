using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>Two colonists interacted: the presentation shows it over them. It does not go to the log.</summary>
public sealed record InteractionEvent(long Tick, EntityId Initiator, EntityId Receiver, string Interaction) : ISimEvent;

/// <summary>
/// Colonists interact with those around them (see "Interacciones" in
/// <c>docs/gdd/vida-y-generaciones.md</c>): every <see cref="SocialConfigDef.CheckTicks"/>, staggered
/// by colonist, an awake one may start an interaction with another nearby, chosen with its kind by
/// their personalities and their relation. At midnight, opinions of those who did not meet that
/// day come back a little towards 0. Draws from the random stream <c>social.interactions</c>.
/// </summary>
public sealed partial class SocialSystem : ISimSystem
{
    private const string RandomStream = "social.interactions";

    private readonly ColonyRules _colony;
    private readonly SocialRules _rules;
    private readonly PersonalityRules _personalities;

    // The stage of a colonist without one: an adult.
    private readonly string _adultStage;
    private readonly List<EntityId> _colonists = [];
    private readonly List<(EntityId Other, int Weight)> _near = [];
    private readonly List<(InteractionDef Interaction, int Weight)> _choices = [];

    public SocialSystem(ColonyRules colony, SocialRules rules, PersonalityRules personalities, LifeRules life)
    {
        ArgumentNullException.ThrowIfNull(colony);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(personalities);
        ArgumentNullException.ThrowIfNull(life);
        _colony = colony;
        _rules = rules;
        _personalities = personalities;
        _adultStage = life.AdultStage.Id;
    }

    public void Tick(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var time = simulation.Time;
        simulation.World.Query<Colonist, Position>(_colonists);
        if (simulation.CurrentTick % time.TicksPerDay == LifeSystem.MidnightOffset(time))
        {
            Forget(simulation);
        }

        foreach (var colonist in _colonists)
        {
            if ((simulation.CurrentTick + colonist.Index) % _rules.Config.CheckTicks == 0 && simulation.World.IsAlive(colonist))
            {
                TryStart(simulation, colonist);
            }
        }
    }

    private static bool CanInteract(Simulation simulation, EntityId colonist)
    {
        var asleep = simulation.World.TryGet<Job>(colonist, out var job) && job.Kind == JobKind.Sleep && job.Stage == JobStage.Working;
        return !asleep && (LifeRules.Stage(simulation, colonist)?.Interacts ?? true) && !MoodRules.Isolates(simulation, colonist);
    }

    private bool InStages(Simulation simulation, EntityId colonist, IReadOnlyList<string> stages) =>
        stages.Contains(LifeRules.Stage(simulation, colonist)?.Id ?? _adultStage);

    private void TryStart(Simulation simulation, EntityId colonist)
    {
        if (!CanInteract(simulation, colonist))
        {
            return;
        }

        FindNear(simulation, colonist);
        if (_near.Count == 0)
        {
            return;
        }

        var random = simulation.Random.Get(RandomStream);
        var chance = _rules.Config.StartChancePercent * PersonalityRules.InteractionChancePercent(simulation, colonist);
        if (!random.Chance(chance))
        {
            return;
        }

        if (_near.Sum(entry => entry.Weight) == 0)
        {
            return;
        }

        var other = Pick(_near, random);
        _choices.Clear();
        foreach (var interaction in _rules.Interactions)
        {
            if (Allowed(simulation, interaction, colonist, other) && WeightOf(simulation, interaction, colonist, other) is > 0 and var weight)
            {
                _choices.Add((interaction, weight));
            }
        }

        if (_choices.Count > 0)
        {
            Apply(simulation, Pick(_choices, random), colonist, other, random);
        }
    }

    private static T Pick<T>(List<(T Item, int Weight)> weighted, Pcg32 random)
    {
        var roll = random.NextInt(weighted.Sum(entry => entry.Weight));
        foreach (var (item, weight) in weighted)
        {
            if (roll < weight)
            {
                return item;
            }

            roll -= weight;
        }

        return weighted[^1].Item;
    }
}
