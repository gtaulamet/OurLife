using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// Runs at midnight (see "Crecimiento" in <c>docs/gdd/agricultura.md</c>): every crop whose
/// growing seasons include the season of the new day adds a day of growth, up to being ripe; in
/// the other seasons it waits, without dying. It also drops from the zones the cells that stopped
/// being a floor. Needs no random numbers.
/// </summary>
public sealed class FarmSystem : ISimSystem
{
    private readonly List<EntityId> _crops = [];

    public void Tick(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        if (FarmZones.Prune(simulation))
        {
            simulation.Events.Emit(new FarmZonesChangedEvent());
        }

        world.Query<Crop>(_crops);
        if (_crops.Count == 0)
        {
            return;
        }

        var season = simulation.CurrentDate.SeasonId;
        foreach (var entity in _crops)
        {
            var crop = world.Get<Crop>(entity);
            var def = simulation.Content.Get<CropDef>(crop.Def);
            if (crop.Days < def.GrowDays && def.GrowSeasons.Contains(season))
            {
                crop.Days++;
                simulation.Events.Emit(new EntityChangedEvent(entity));
            }
        }
    }
}
