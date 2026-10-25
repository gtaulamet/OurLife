using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Core.Text;

namespace OurLife.Core.Colony;

/// <summary>
/// Runs at midnight (see "Deterioro" in <c>docs/gdd/agricultura.md</c>): every pile of an item that
/// rots loses <c>10 000 × speed / (spoilDays × 100)</c> of freshness, rounded up so an item lasts
/// its days, with a speed of 100 % in the open and <see cref="SpoilageConfigDef.CoveredPercent"/>
/// under something solid (a roof, a built floor, rock). A load in a colonist's hands rots at 100 %.
/// What reaches 0 disappears. The player is told once a day for each item of which
/// <see cref="SpoilageConfigDef.RotNoticeMinUnits"/> or more rotted. Content without a
/// <see cref="SpoilageConfigDef"/> has no spoilage. Needs no random numbers.
/// </summary>
public sealed class SpoilageSystem : ISimSystem
{
    /// <summary>The message when food rots, with <c>count</c> and <c>item</c>.</summary>
    public const string SpoiledKey = "event.food.spoiled";

    private readonly List<EntityId> _piles = [];
    private readonly SortedDictionary<string, int> _rotten = new(StringComparer.Ordinal);

    public void Tick(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var configs = simulation.Content.All<SpoilageConfigDef>();
        if (configs.Count != 1)
        {
            return;
        }

        var config = configs[0];
        var world = simulation.World;
        var map = simulation.Map;
        _rotten.Clear();

        world.Query<ItemStack, Position>(_piles);
        foreach (var pile in _piles)
        {
            var stack = world.Get<ItemStack>(pile);
            var item = simulation.Content.Get<ItemDef>(stack.Def);
            if (item.SpoilDays <= 0)
            {
                continue;
            }

            var speed = IsCovered(map, world.Get<Position>(pile).Cell) ? config.CoveredPercent : 100;
            var freshness = stack.FreshnessValue - DailyLoss(item, speed);
            if (freshness > 0)
            {
                stack.Freshness = ItemPiles.AsStored(freshness);
                simulation.Events.Emit(new EntityChangedEvent(pile));
                continue;
            }

            _rotten[item.Id] = _rotten.GetValueOrDefault(item.Id) + stack.Count;
            simulation.Events.Emit(new EntityRemovedEvent(pile));
            world.DestroyEntity(pile);
        }

        world.Query<Carried>(_piles);
        foreach (var carrier in _piles)
        {
            var carried = world.Get<Carried>(carrier);
            var item = simulation.Content.Get<ItemDef>(carried.Def);
            if (item.SpoilDays <= 0)
            {
                continue;
            }

            var freshness = (carried.Freshness ?? ItemStack.FullFreshness) - DailyLoss(item, 100);
            if (freshness > 0)
            {
                carried.Freshness = ItemPiles.AsStored(freshness);
                continue;
            }

            _rotten[item.Id] = _rotten.GetValueOrDefault(item.Id) + carried.Count;
            world.Remove<Carried>(carrier);

            // Hauling, delivering or feeding with nothing in the hands makes no sense: the job ends.
            if (world.TryGet<Job>(carrier, out var job) && job.Stage == JobStage.Carrying)
            {
                JobSystem.Drop(world, carrier);
            }

            simulation.Events.Emit(new EntityChangedEvent(carrier));
        }

        foreach (var (id, units) in _rotten)
        {
            if (units >= config.RotNoticeMinUnits)
            {
                var name = LocalizedText.Of(simulation.Content.Get<ItemDef>(id).NameKey!).With("count", units);
                simulation.Events.Emit(new NotificationEvent(
                    simulation.CurrentTick,
                    LocalizedText.Of(SpoiledKey).With("count", units).With("item", name),
                    NotificationSeverity.Warning));
            }
        }
    }

    /// <summary>Freshness an item of <see cref="ItemDef.SpoilDays"/> days loses in one day at a speed in percent.</summary>
    public static int DailyLoss(ItemDef item, int speedPercent)
    {
        ArgumentNullException.ThrowIfNull(item);
        var span = (long)item.SpoilDays * 100;
        return (int)((((long)ItemStack.FullFreshness * speedPercent) + span - 1) / span);
    }

    /// <summary>Something solid is above the cell, at any level: a roof, a built floor or rock.</summary>
    public static bool IsCovered(GameMap map, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(map);
        for (var z = cell.Z + 1; z < map.Size.Depth; z++)
        {
            if (map.ShapeAt(cell.X, cell.Y, z) != TileShape.Empty)
            {
                return true;
            }
        }

        return false;
    }
}
