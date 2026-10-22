using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
using OurLife.Core.Text;

namespace OurLife.Core.Colony;

/// <summary>The end of a colonist's life, whatever the cause, and of the colony when nobody is left.</summary>
internal static class ColonistDeath
{
    /// <summary>Tells the player with <paramref name="message"/>, drops what the colonist carried and removes it.</summary>
    public static void Die(Simulation simulation, EntityId colonist, LocalizedText message)
    {
        // The entity is gone once the message is read, so it carries the name and gender itself.
        simulation.Events.Emit(new NotificationEvent(
            simulation.CurrentTick, ColonyText.Freeze(message, simulation.World), NotificationSeverity.Danger));
        if (simulation.World.TryGet<Carried>(colonist, out var carried))
        {
            ItemPiles.Drop(simulation, carried.Def, carried.Count, simulation.World.Get<Position>(colonist).Cell, carried.Quality, carried.Freshness);
        }

        Relationships.ForgetTheDead(simulation, colonist);
        simulation.Events.Emit(new EntityRemovedEvent(colonist));
        simulation.World.DestroyEntity(colonist);
    }

    /// <summary>After some died: when nobody is left, the colony is lost.</summary>
    public static void CheckColonyLost(Simulation simulation)
    {
        if (simulation.World.Store<Colonist>().Count == 0)
        {
            simulation.Events.Emit(new NotificationEvent(simulation.CurrentTick, LocalizedText.Of("event.colony.lost"), NotificationSeverity.Danger));
            simulation.Events.Emit(new ColonyLostEvent(simulation.CurrentTick));
        }
    }
}
