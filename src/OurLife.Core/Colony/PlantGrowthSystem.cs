using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// Gathered plants bear fruit again on the tick their regrowth time is over. Only gathered plants
/// are looked at, so checking every tick costs little and needs no number of its own.
/// </summary>
public sealed class PlantGrowthSystem : ISimSystem
{
    private readonly List<EntityId> _plants = [];

    public void Tick(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        world.Query<Harvested>(_plants);
        foreach (var plant in _plants)
        {
            if (simulation.CurrentTick >= world.Get<Harvested>(plant).RegrowsAtTick)
            {
                world.Remove<Harvested>(plant);
                simulation.Events.Emit(new EntityChangedEvent(plant));
            }
        }
    }
}
