using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

// Sowing and reaping the farm zones (see docs/gdd/agricultura.md).
public sealed partial class JobSystem
{
    private readonly List<EntityId> _farmZoneBuffer = [];

    /// <summary>
    /// Sowing goes on while the cell is still in the zone that sent the colonist; reaping, while
    /// the crop is there, ripe, on a cell of some zone. A zone that disappears under the work ends it.
    /// </summary>
    private bool IsFarmValid(Simulation simulation, Job job)
    {
        var world = simulation.World;
        if (job.Kind == JobKind.Sow)
        {
            return job.Crop is not null && simulation.Content.TryGet<CropDef>(job.Crop, out _) && world.IsAlive(job.Recipient)
                   && world.TryGet<FarmZone>(job.Recipient, out var zone)
                   && zone.Cells.BinarySearch(job.Cell, Stockpiles.CellOrder.For(simulation.Map)) >= 0;
        }

        return world.IsAlive(job.Target) && world.TryGet<Crop>(job.Target, out var crop) && FarmRules.IsRipe(simulation, crop)
               && !FarmRules.ZoneAt(simulation, world.Get<Position>(job.Target).Cell, _farmZoneBuffer).IsNone;
    }

    /// <summary>
    /// Sows the cell once the work is done, if nothing took it in the meantime: a crop that grows,
    /// or a sapling for a tree. It costs nothing: there are no seeds.
    /// </summary>
    private bool Sow(Simulation simulation, Job job, int speed)
    {
        var crop = simulation.Content.Get<CropDef>(job.Crop!);
        if (!Advance(job, crop.SowTicks, speed))
        {
            return false;
        }

        var cell = job.Cell;
        if (FarmRules.CropAt(simulation, cell).IsNone && !Designations.PlantCells(simulation).Contains(cell))
        {
            FarmRules.Sow(simulation, crop, cell);
        }

        return true;
    }

    /// <summary>
    /// Reaps the crop: it leaves its units on its cell, where the haulers take them, and the cell is
    /// empty to be sown again. The units depend on the skill of farming of the reaper.
    /// </summary>
    private bool Reap(Simulation simulation, EntityId colonist, Job job, WorkTypeDef? workType, int speed)
    {
        var world = simulation.World;
        var crop = simulation.Content.Get<CropDef>(world.Get<Crop>(job.Target).Def);
        if (!Advance(job, crop.ReapTicks, speed))
        {
            return false;
        }

        var level = workType is null ? 0 : SkillRules.Level(simulation, colonist, workType.Id) ?? (world.Has<Skills>(colonist) ? 0 : _rules.Skills.Config.AverageLevel);
        var units = FarmRules.Yield(simulation, crop, Math.Min(level, _rules.Skills.Config.MaxLevel));
        var cell = world.Get<Position>(job.Target).Cell;
        simulation.Events.Emit(new EntityRemovedEvent(job.Target));
        world.DestroyEntity(job.Target);
        if (units > 0)
        {
            ItemPiles.Drop(simulation, crop.Item!, units, cell);
            _storage.Invalidate();
        }

        return true;
    }
}
