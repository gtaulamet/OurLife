using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

// Crafting at a workshop (see "El trabajo craft" in docs/gdd/oficios.md).
public sealed partial class DecisionSystem
{
    private readonly List<EntityId> _craftOrders = [];
    private readonly List<EntityId> _craftShops = [];
    private readonly List<EntityId> _craftJobs = [];
    private readonly List<EntityId> _craftPiles = [];
    private Predicate<EntityId>? _takenPile;

    /// <summary>
    /// Takes the first production order, in the order of the list, that the colonist can make now:
    /// of a recipe the village knows, at a free workshop of this work type that the colonist can
    /// reach, with its missing ingredients in piles nobody holds, within the fetch radius of the
    /// workshop. It reserves the workshop and sets off to the first pile, or to the workshop if
    /// nothing is missing. An order does not ask for more than it needs: what crafts under way
    /// will make counts.
    /// </summary>
    private bool TryCraft(Simulation simulation, EntityId colonist, WorkTypeDef workType)
    {
        var world = simulation.World;
        if (world.Store<WorkOrder>().Count == 0)
        {
            return false;
        }

        var content = simulation.Content;
        var person = world.Get<Colonist>(colonist);
        _craftOrders.Clear();
        _craftOrders.AddRange(WorkOrderRules.InOrder(simulation));
        foreach (var orderEntity in _craftOrders)
        {
            var order = world.Get<WorkOrder>(orderEntity);
            if (!content.TryGet<RecipeDef>(order.Recipe, out var recipe)
                || content.Get<BuildableDef>(recipe.Workshop).Workstation?.WorkType != workType.Id
                || !simulation.Unlocks.IsUnlocked(recipe, simulation.Progress)
                || !WorkOrderRules.Wants(simulation, orderEntity, _craftJobs))
            {
                continue;
            }

            Crafting.WorkshopsOf(simulation, recipe.Workshop, _craftShops);
            foreach (var shop in _craftShops)
            {
                var free = (order.Workshop.IsNone || order.Workshop == shop) && _entityUses.GetValueOrDefault(shop) == 0
                           && !IsIgnored(person, shop, default);
                if (free && TryStartCraft(simulation, colonist, person, orderEntity, recipe, shop))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Starts the craft of an order at a workshop if every missing ingredient has piles to cover
    /// it and the colonist finds a way to the first pile (or to the workshop). False, leaving the
    /// workshop alone for a while, if there is no way.
    /// </summary>
    private bool TryStartCraft(Simulation simulation, EntityId colonist, Colonist person, EntityId orderEntity, RecipeDef recipe, EntityId shopEntity)
    {
        var world = simulation.World;
        var map = simulation.Map;
        var connectivity = simulation.Connectivity;
        var shop = world.Get<Workshop>(shopEntity);
        var shopCell = world.Get<Position>(shopEntity).Cell;
        var from = world.Get<Position>(colonist).Cell;
        var region = connectivity.RegionAt(from);
        var radius = _rules.Ai.CraftFetchRadius;
        _takenPile ??= pile => _entityUses.GetValueOrDefault(pile) > 0;

        var firstPile = EntityId.None;
        foreach (var input in recipe.Inputs)
        {
            var missing = Crafting.Missing(simulation, shop, input);
            if (missing == 0)
            {
                continue;
            }

            if (Crafting.Available(simulation, input, shopCell, from, radius, _takenPile, _craftPiles) < missing)
            {
                return false;
            }

            if (firstPile.IsNone)
            {
                firstPile = Crafting.FindPile(simulation, input, shopCell, from, radius, _takenPile, _craftPiles);
            }
        }

        var job = new Job { Kind = JobKind.Craft, Recipient = shopEntity, Recipe = recipe.Id, Order = orderEntity, Destination = shopCell, Cell = shopCell };
        _goal.Clear();
        if (firstPile.IsNone)
        {
            WorkSites.Find(map, JobKind.Craft, shopCell, _sites);
            KeepInRegion(_sites, connectivity, region);
            _goal.AddRange(_sites);
        }
        else
        {
            job.Target = firstPile;
            job.Cell = world.Get<Position>(firstPile).Cell;
            _goal.Add(job.Cell);
        }

        if (_goal.Count == 0 || !_paths.TryFind(map, from, _goal, _path))
        {
            person.Unreachable.Add(new IgnoredTarget
            {
                Entity = shopEntity,
                Cell = shopCell,
                UntilTick = simulation.CurrentTick + _rules.Ai.UnreachableCooldownTicks,
            });
            return false;
        }

        Assign(simulation, colonist, job, _path);
        return true;
    }
}
