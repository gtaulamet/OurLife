using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
using OurLife.Core.Text;

namespace OurLife.Core.Colony;

/// <summary>
/// Needs go down every tick; a need satisfied by sleeping goes up instead while its owner sleeps.
/// A need that kills ends the colonist's life after its hours at zero; when the last colonist
/// dies the colony is lost. Integer arithmetic: the fraction of each tick is carried over.
/// </summary>
public sealed class NeedsSystem : ISimSystem
{
    private const int MinutesPerHour = 60;

    private readonly List<EntityId> _colonists = [];

    public void Tick(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var minutes = simulation.Time.MinutesPerTick;
        var deaths = 0;

        world.Query<Colonist, Needs>(_colonists);
        foreach (var colonist in _colonists)
        {
            var asleep = world.TryGet<Job>(colonist, out var job) && job.Kind == JobKind.Sleep && job.Stage == JobStage.Working;
            NeedDef? killer = null;
            foreach (var state in world.Get<Needs>(colonist).Values)
            {
                var need = simulation.Content.Get<NeedDef>(state.Need);
                var perHour = asleep && need.SatisfiedBy == NeedSatisfaction.Sleeping ? need.RecoveryPerHour : -need.DecayPerHour;
                state.Remainder += perHour * minutes;
                var whole = state.Remainder / MinutesPerHour;
                state.Remainder -= whole * MinutesPerHour;
                state.Value = Math.Clamp(state.Value + whole, 0, need.Max);
                state.MinutesAtZero = state.Value == 0 ? state.MinutesAtZero + minutes : 0;
                if (killer is null && need.DeathAfterHoursAtZero is { } hours && state.MinutesAtZero >= hours * MinutesPerHour)
                {
                    killer = need;
                }
            }

            if (killer is not null)
            {
                Die(simulation, colonist, killer);
                deaths++;
            }
        }

        if (deaths > 0 && world.Store<Colonist>().Count == 0)
        {
            simulation.Events.Emit(new NotificationEvent(simulation.CurrentTick, LocalizedText.Of("event.colony.lost"), NotificationSeverity.Danger));
            simulation.Events.Emit(new ColonyLostEvent(simulation.CurrentTick));
        }
    }

    /// <summary>Key of the message that tells a colonist died of a need, like <c>event.need.hunger.death</c>.</summary>
    public static string DeathKey(NeedDef need)
    {
        ArgumentNullException.ThrowIfNull(need);
        return $"event.need.{need.Id}.death";
    }

    private static void Die(Simulation simulation, EntityId colonist, NeedDef need)
    {
        // The entity is gone once the message is read, so it carries the name and gender itself.
        var person = simulation.World.Get<Colonist>(colonist);
        simulation.Events.Emit(new NotificationEvent(
            simulation.CurrentTick,
            LocalizedText.Of(DeathKey(need)).With("colonist", person.Name).With("colonist_gender", person.Gender),
            NotificationSeverity.Danger));
        simulation.Events.Emit(new EntityRemovedEvent(colonist));
        simulation.World.DestroyEntity(colonist);
    }
}
