using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>Work a colonist with free will does on its own (see "Trabajo por iniciativa" in <c>docs/gdd/libre-albedrio.md</c>).</summary>
public sealed partial class DecisionSystem
{
    private const string FreeWillStream = "colony.free_will";

    private readonly List<(InitiativeTaskDef Task, int Priority, long MissingPerMille, int Order)> _tasks = [];

    // The plants each task works on, by task id: those whose job gives items of its category; and
    // the work type of each task, in the order of the defs. Both found once.
    private Dictionary<string, HashSet<string>>? _taskPlants;
    private WorkTypeDef[]? _taskWorkTypes;

    // The stock of the village by category, counted once per tick, when a colonist first needs it.
    private readonly Dictionary<string, int> _stock = new(StringComparer.Ordinal);
    private long _stockTick = -1;

    // What the work on its own under way will bring, by category, counted at each decision from the
    // jobs as they are then: the colonists share out the targets.
    private readonly Dictionary<string, int> _incoming = new(StringComparer.Ordinal);
    private readonly List<EntityId> _nearPlants = [];

    /// <summary>
    /// With free will on, a colonist with nothing ordered to do may work on its own: it gathers or
    /// chops near the village what the village has under its target, with the chance its traits
    /// and mood give. Among the tasks it can do, the one of its highest priority first, and with
    /// the same priority, the one whose target lacks most. Not in a crisis.
    /// </summary>
    private bool TryInitiative(Simulation simulation, EntityId colonist)
    {
        if (!simulation.FreeWill || simulation.VillageCenter is null || FreeWillRules.ConfigOf(simulation.Content) is null
            || MoodRules.CrisisOf(simulation, colonist) is not null)
        {
            return false;
        }

        // The roll comes first, so a colonist that will not take the initiative counts nothing.
        if (simulation.Content.All<InitiativeTaskDef>().Count == 0
            || !simulation.Random.Get(FreeWillStream).Chance(FreeWillRules.InitiativeChance(simulation, colonist)))
        {
            return false;
        }

        FindTasks(simulation, colonist);
        if (_tasks.Count == 0)
        {
            return false;
        }

        _tasks.Sort((a, b) =>
        {
            var order = a.Priority.CompareTo(b.Priority);
            order = order != 0 ? order : b.MissingPerMille.CompareTo(a.MissingPerMille);
            return order != 0 ? order : a.Order.CompareTo(b.Order);
        });
        FindNearPlants(simulation);
        foreach (var (task, _, _, _) in _tasks)
        {
            _candidates.Clear();
            AddInitiativePlants(simulation, task);
            if (TryCandidates(simulation, colonist, initiative: true))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The tasks the colonist can do now: of a work type it does at its stage and priority, and
    /// whose target lacks something, counting what the work under way will bring.
    /// </summary>
    private void FindTasks(Simulation simulation, EntityId colonist)
    {
        _tasks.Clear();
        var tasks = simulation.Content.All<InitiativeTaskDef>();
        _taskWorkTypes ??= [.. tasks.Select(task => _rules.WorkTypeOf(new Job { Kind = task.Kind })!)];
        CountIncoming(simulation);
        var stage = LifeRules.Stage(simulation, colonist);
        for (var i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            var priority = PriorityOf(simulation, colonist, stage, _taskWorkTypes[i]);
            var target = FreeWillRules.TargetOf(simulation, task.Category) ?? 0;
            if (priority == WorkPriorityLevels.Never || target <= 0)
            {
                continue;
            }

            var have = StockOf(simulation, task.Category) + _incoming.GetValueOrDefault(task.Category);
            if (have < target)
            {
                _tasks.Add((task, priority, (target - have) * 1000L / target, i));
            }
        }
    }

    /// <summary>The units of a category the village has, as the HUD counts them.</summary>
    private int StockOf(Simulation simulation, string category)
    {
        if (_stockTick != simulation.CurrentTick)
        {
            _stock.Clear();
            foreach (var line in ColonyStock.ByCategory(simulation))
            {
                _stock[line.Category.Id] = line.Count;
            }

            _stockTick = simulation.CurrentTick;
        }

        return _stock.GetValueOrDefault(category);
    }

    /// <summary>
    /// What the jobs on its own will bring, by category: with enough colonists chopping for the
    /// wood target, the next one gathers food.
    /// </summary>
    private void CountIncoming(Simulation simulation)
    {
        _incoming.Clear();
        var world = simulation.World;
        var content = simulation.Content;
        world.Query<Job>(_entities);
        foreach (var worker in _entities)
        {
            var job = world.Get<Job>(worker);
            if (job.Initiative && world.TryGet<Plant>(job.Target, out var plant)
                && content.Get<PlantDef>(plant.Def).OutputOf(job.Kind) is { } output)
            {
                var category = content.Get<ItemDef>(output.Item).Category;
                _incoming[category] = _incoming.GetValueOrDefault(category) + output.Count;
            }
        }
    }

    /// <summary>
    /// The plants near the village a task can work on and nobody holds: none the player ordered,
    /// which have whoever the orders give them, and none already gathered.
    /// </summary>
    private void AddInitiativePlants(Simulation simulation, InitiativeTaskDef task)
    {
        var world = simulation.World;
        var plants = PlantsOf(simulation.Content, task);
        var kind = task.Kind;
        foreach (var plant in _nearPlants)
        {
            if (plants.Contains(world.Get<Plant>(plant).Def) && !world.Has<Sapling>(plant)
                && !world.Has<ChopOrder>(plant) && !world.Has<HarvestOrder>(plant)
                && !(kind == JobKind.Harvest && world.Has<Harvested>(plant)))
            {
                AddEntity(simulation, kind, plant, capacity: 1);
            }
        }
    }

    /// <summary>The plants near the village, by ascending id: one pass over the map for all the tasks of a decision.</summary>
    private void FindNearPlants(Simulation simulation)
    {
        var world = simulation.World;
        var center = simulation.VillageCenter!.Value;
        var radius = FreeWillRules.ConfigOf(simulation.Content)!.VillageRadius;
        _nearPlants.Clear();
        world.Query<Plant, Position>(_entities);
        foreach (var plant in _entities)
        {
            if (FreeWillRules.IsNear(center, radius, world.Get<Position>(plant).Cell))
            {
                _nearPlants.Add(plant);
            }
        }
    }

    private HashSet<string> PlantsOf(DefDatabase content, InitiativeTaskDef task)
    {
        _taskPlants ??= content.All<InitiativeTaskDef>().ToDictionary(
            each => each.Id,
            each => content.All<PlantDef>()
                .Where(plant => plant.OutputOf(each.Kind) is { } output && content.Get<ItemDef>(output.Item).Category == each.Category)
                .Select(plant => plant.Id)
                .ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);
        return _taskPlants[task.Id];
    }
}
