using OurLife.Core.Colony;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Save;

/// <summary>
/// The stable names under which components and commands are saved (ADR-010), so renaming or
/// moving a class does not break saved games. Every component and command must be registered
/// here: saving a game with an unregistered one fails, and a test checks the commands.
/// </summary>
public static class SaveTypes
{
    private static readonly List<SaveType> ComponentList = [];
    private static readonly List<SaveType> CommandList = [];

    static SaveTypes()
    {
        Component<Colonist>("colonist");
        Component<Needs>("needs");
        Component<Position>("position");
        Component<Movement>("movement");
        Component<Job>("job");
        Component<ItemStack>("item_stack");
        Component<Plant>("plant");
        Component<ChopOrder>("chop_order");
        Component<HarvestOrder>("harvest_order");
        Component<Harvested>("harvested");
        Component<Sapling>("sapling");
        Component<Stockpile>("stockpile");
        Component<Carried>("carried");
        Component<Blueprint>("blueprint");
        Component<Furniture>("furniture");
        Component<Bed>("bed");
        Component<WorkPriorities>("work_priorities");
        Component<Life>("life");
        Component<Personality>("personality");
        Component<Skills>("skills");
        Component<Relations>("relations");
        Component<Family>("family");
        Component<RecentInteractions>("recent_interactions");
        Component<Mood>("mood");
        Component<Desires>("desires");
        Component<Crisis>("crisis");
        Component<Pregnancy>("pregnancy");
        Component<Workshop>("workshop");
        Component<WorkOrder>("work_order");
        Component<FarmZone>("farm_zone");
        Component<Crop>("crop");
        Component<Equipment>("equipment");

        Command<DesignateCommand>("designate");
        Command<SetStockpileFilterCommand>("set_stockpile_filter");
        Command<PlaceBlueprintCommand>("place_blueprint");
        Command<SetWorkPriorityCommand>("set_work_priority");
        Command<SetChildrenAllowedCommand>("set_children_allowed");
        Command<SetFreeWillCommand>("set_free_will");
        Command<SetStockTargetCommand>("set_stock_target");
        Command<CreateWorkOrderCommand>("create_work_order");
        Command<RemoveWorkOrderCommand>("remove_work_order");
        Command<SetWorkOrderCommand>("set_work_order");
        Command<SetResearchQueueCommand>("set_research_queue");
        Command<MoveWorkOrderCommand>("move_work_order");
        Command<PlaceFarmZoneCommand>("place_farm_zone");
        Command<SetFarmZoneCropCommand>("set_farm_zone_crop");
    }

    /// <summary>The registered component types, in registration order.</summary>
    public static IEnumerable<Type> ComponentTypes => ComponentList.Select(entry => entry.Type);

    /// <summary>The registered command types, in registration order.</summary>
    public static IEnumerable<Type> CommandTypes => CommandList.Select(entry => entry.Type);

    internal static SaveType? ComponentByType(Type type) => ComponentList.Find(entry => entry.Type == type);

    internal static SaveType? ComponentByName(string name) => ComponentList.Find(entry => entry.Name == name);

    internal static SaveType? CommandByType(Type type) => CommandList.Find(entry => entry.Type == type);

    internal static SaveType? CommandByName(string name) => CommandList.Find(entry => entry.Name == name);

    private static void Component<T>(string name)
        where T : class =>
        ComponentList.Add(new SaveType(name, typeof(T), (world, entity, component) => world.Add(entity, (T)component)));

    private static void Command<T>(string name)
        where T : class, ISimCommand =>
        CommandList.Add(new SaveType(name, typeof(T), null));

    /// <summary>A saved type: its stable name, its class and, for components, how to add one to an entity.</summary>
    internal sealed record SaveType(string Name, Type Type, Action<World, EntityId, object>? AddTo);
}
