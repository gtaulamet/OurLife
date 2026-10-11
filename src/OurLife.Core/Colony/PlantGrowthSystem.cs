using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// Gathered plants bear fruit again on the tick their regrowth time is over, unless the season is
/// one of their <see cref="PlantDef.DormantSeasons"/>: then they wait for the next season that is
/// not. Only gathered plants are looked at, so checking every tick costs little and needs no
/// number of its own.
/// </summary>
public sealed class PlantGrowthSystem : ISimSystem
{
    private readonly List<EntityId> _plants = [];

    public void Tick(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        world.Query<Harvested>(_plants);
        if (_plants.Count == 0)
        {
            return;
        }

        var season = simulation.CurrentDate.SeasonId;
        foreach (var plant in _plants)
        {
            if (simulation.CurrentTick >= world.Get<Harvested>(plant).RegrowsAtTick
                && !simulation.Content.Get<PlantDef>(world.Get<Plant>(plant).Def).DormantSeasons.Contains(season))
            {
                world.Remove<Harvested>(plant);
                simulation.Events.Emit(new EntityChangedEvent(plant));
            }
        }
    }
}
