using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Pathfinding;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>A production order was created, changed or removed: the panel of orders reads them again.</summary>
public sealed record WorkOrdersChangedEvent : ISimEvent;

/// <summary>
/// Creates a production order (see "Órdenes de producción" in <c>docs/gdd/oficios.md</c>): make
/// <c>Quantity</c> units of the main output of a recipe, at any workshop of its kind or at
/// <c>Workshop</c>. It is ignored if the recipe does not exist, the mode is not
/// supported yet (O3 only has <see cref="WorkOrderMode.Count"/>), the quantity is not positive or
/// the workshop is not a standing workshop of the recipe. A recipe that is still locked is accepted:
/// the order waits, and says so (<see cref="WorkOrderStatus.Locked"/>).
/// </summary>
public sealed record CreateWorkOrderCommand(
    [property: DefRef(typeof(RecipeDef))] string Recipe,
    WorkOrderMode Mode,
    int Quantity,
    EntityId Workshop = default) : ISimCommand
{
    public void Apply(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        if (!simulation.Content.TryGet<RecipeDef>(Recipe, out var recipe) || Mode != WorkOrderMode.Count || Quantity < 1)
        {
            return;
        }

        if (!Workshop.IsNone
            && !(world.IsAlive(Workshop) && world.Has<Entities.Workshop>(Workshop) && world.Get<Furniture>(Workshop).Buildable == recipe.Workshop))
        {
            return;
        }

        var order = world.CreateEntity();
        world.Add(order, new WorkOrder { Recipe = Recipe, Mode = Mode, Quantity = Quantity, Workshop = Workshop });
        simulation.Events.Emit(new EntitySpawnedEvent(order));
        simulation.Events.Emit(new WorkOrdersChangedEvent());
    }
}

/// <summary>
/// Removes a production order. A craft already under way for it goes on to the end; the ingredients
/// delivered to a workshop that no other order uses, and nobody is working at, fall to the ground.
/// An order that does not exist is ignored.
/// </summary>
public sealed record RemoveWorkOrderCommand(EntityId Order) : ISimCommand
{
    public void Apply(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        if (!world.IsAlive(Order) || !world.TryGet<WorkOrder>(Order, out var order))
        {
            return;
        }

        var workshopKind = simulation.Content.TryGet<RecipeDef>(order.Recipe, out var recipe) ? recipe.Workshop : null;
        simulation.Events.Emit(new EntityRemovedEvent(Order));
        world.DestroyEntity(Order);
        simulation.Events.Emit(new WorkOrdersChangedEvent());
        if (workshopKind is not null)
        {
            WorkOrderRules.ReleaseUnusedInputs(simulation, workshopKind);
        }
    }
}

/// <summary>Why a production order is not being made, or that it is (see "Estado" in <c>docs/gdd/oficios.md</c>).</summary>
public enum WorkOrderStatus
{
    /// <summary>Somebody is making it, or all is ready for somebody to.</summary>
    Running,

    /// <summary>Ingredients are missing in the whole village.</summary>
    WaitingInputs,

    /// <summary>There is no workshop of the recipe.</summary>
    NoWorkshop,

    /// <summary>The recipe needs knowledge the village lacks (<see cref="WorkOrderRules.MissingKnowledge"/>).</summary>
    Locked,

    /// <summary>No colonist has a priority for the work of the workshop.</summary>
    NoWorker,

    Done,
}

/// <summary>Read-only queries and rules of production orders. O4 adds the other modes, pausing and reordering.</summary>
public static class WorkOrderRules
{
    /// <summary>The state of an order, calculated from the world: it is not saved.</summary>
    public static WorkOrderStatus Status(Simulation simulation, EntityId orderEntity)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var order = world.Get<WorkOrder>(orderEntity);
        if (IsDone(order))
        {
            return WorkOrderStatus.Done;
        }

        var recipe = simulation.Content.Get<RecipeDef>(order.Recipe);
        if (!simulation.Unlocks.IsUnlocked(recipe, simulation.Progress))
        {
            return WorkOrderStatus.Locked;
        }

        var shops = new List<EntityId>();
        Crafting.WorkshopsOf(simulation, recipe.Workshop, shops);
        if (!order.Workshop.IsNone)
        {
            shops.RemoveAll(shop => shop != order.Workshop);
        }

        if (shops.Count == 0)
        {
            return WorkOrderStatus.NoWorkshop;
        }

        var crafters = new List<EntityId>();
        world.Query<Colonist, Job>(crafters);
        if (crafters.Any(colonist => world.Get<Job>(colonist).Kind == JobKind.Craft && world.Get<Job>(colonist).Order == orderEntity))
        {
            return WorkOrderStatus.Running;
        }

        if (!AnyWorker(simulation, recipe))
        {
            return WorkOrderStatus.NoWorker;
        }

        foreach (var input in recipe.Inputs)
        {
            if (Held(simulation, shops, input) < input.Count)
            {
                return WorkOrderStatus.WaitingInputs;
            }
        }

        return WorkOrderStatus.Running;
    }

    /// <summary>The knowledge the village lacks to make the recipe of an order, for the text of a locked order.</summary>
    public static IReadOnlyList<string> MissingKnowledge(Simulation simulation, EntityId orderEntity)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var recipe = simulation.Content.Get<RecipeDef>(simulation.World.Get<WorkOrder>(orderEntity).Recipe);
        return simulation.Unlocks.Missing(recipe, simulation.Progress);
    }

    /// <summary>Whether an order has made what it asked for.</summary>
    public static bool IsDone(WorkOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return order.Done >= order.Quantity;
    }

    /// <summary>Units of the main output the crafts under way for an order will make.</summary>
    internal static int InProgress(Simulation simulation, EntityId orderEntity, List<EntityId> buffer)
    {
        var world = simulation.World;
        var units = 0;
        world.Query<Job>(buffer);
        foreach (var colonist in buffer)
        {
            var job = world.Get<Job>(colonist);
            if (job.Kind == JobKind.Craft && job.Order == orderEntity && job.Recipe is not null)
            {
                units += simulation.Content.Get<RecipeDef>(job.Recipe).MainOutput.Count;
            }
        }

        return units;
    }

    /// <summary>Removes the orders that name a workshop which is no longer there, so they do not wait for one forever.</summary>
    internal static void RemoveOrdersOf(Simulation simulation, EntityId workshop)
    {
        var world = simulation.World;
        var orders = new List<EntityId>();
        world.Query<WorkOrder>(orders);
        var removed = false;
        foreach (var order in orders)
        {
            if (world.Get<WorkOrder>(order).Workshop == workshop)
            {
                simulation.Events.Emit(new EntityRemovedEvent(order));
                world.DestroyEntity(order);
                removed = true;
            }
        }

        if (removed)
        {
            simulation.Events.Emit(new WorkOrdersChangedEvent());
        }
    }

    /// <summary>
    /// Makes the ingredients of the workshops of a kind fall to the ground when no order is left for
    /// their recipes and nobody works at them.
    /// </summary>
    internal static void ReleaseUnusedInputs(Simulation simulation, string workshopKind)
    {
        var world = simulation.World;
        var orders = new List<EntityId>();
        world.Query<WorkOrder>(orders);
        if (orders.Any(order => simulation.Content.TryGet<RecipeDef>(world.Get<WorkOrder>(order).Recipe, out var recipe) && recipe.Workshop == workshopKind))
        {
            return;
        }

        var shops = new List<EntityId>();
        Crafting.WorkshopsOf(simulation, workshopKind, shops);
        var crafters = new List<EntityId>();
        world.Query<Job>(crafters);
        foreach (var shop in shops)
        {
            if (!crafters.Any(colonist => world.Get<Job>(colonist).Kind == JobKind.Craft && world.Get<Job>(colonist).Recipient == shop))
            {
                Crafting.ReleaseInputs(simulation, shop);
            }
        }
    }

    private static bool AnyWorker(Simulation simulation, RecipeDef recipe)
    {
        var content = simulation.Content;
        var world = simulation.World;
        var workTypeId = content.Get<BuildableDef>(recipe.Workshop).Workstation?.WorkType;
        if (workTypeId is null)
        {
            return false;
        }

        var workType = content.Get<WorkTypeDef>(workTypeId);
        var defaultPriority = content.All<AiConfigDef>() is [{ } ai] ? ai.DefaultWorkPriority : WorkPriorityLevels.Lowest;
        var colonists = new List<EntityId>();
        world.Query<Colonist>(colonists);
        return colonists.Any(colonist => LifeRules.DoesWork(LifeRules.Stage(simulation, colonist), workType)
                                         && WorkPriorityRules.Of(world, colonist, workTypeId, defaultPriority) != WorkPriorityLevels.Never);
    }

    /// <summary>Units of what an input takes within the fetch radius of a workshop: piles, loads and the ingredients of the workshops.</summary>
    private static int Held(Simulation simulation, List<EntityId> shops, RecipeInput input)
    {
        var world = simulation.World;
        var total = 0;
        var reach = (simulation.Content.All<AiConfigDef>() is [{ } ai] ? ai.CraftFetchRadius : 0) * PathFinder.StraightCost;
        var piles = new List<EntityId>();
        world.Query<ItemStack, Position>(piles);
        foreach (var pile in piles)
        {
            var stack = world.Get<ItemStack>(pile);
            var cell = world.Get<Position>(pile).Cell;
            if (stack.Count > 0 && Crafting.Accepts(simulation, input, stack.Def, stack.Quality)
                && shops.Any(shop => PathFinder.Estimate(cell, world.Get<Position>(shop).Cell) <= reach))
            {
                total += stack.Count;
            }
        }

        var carriers = new List<EntityId>();
        world.Query<Carried>(carriers);
        foreach (var carrier in carriers)
        {
            var carried = world.Get<Carried>(carrier);
            if (Crafting.Accepts(simulation, input, carried.Def, carried.Quality))
            {
                total += carried.Count;
            }
        }

        foreach (var shop in shops)
        {
            total += Crafting.Held(simulation, simulation.World.Get<Workshop>(shop), input);
        }

        return total;
    }
}
