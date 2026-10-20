using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>Work a colonist with free will does on its own (see "Trabajo por iniciativa" in <c>docs/gdd/libre-albedrio.md</c>).</summary>
public sealed partial class DecisionSystem
{
    private const string InitiativeStream = "colony.free_will";

    private readonly List<(InitiativeTaskDef Task, int Priority, long MissingPerMille, int Order)> _tasks = [];

    // The plants each task works on, by task id: those whose job gives items of its category.
    private Dictionary<string, HashSet<string>>? _taskPlants;

    // The stock of the village by category, counted once per tick, when a colonist first needs it,
    // with what the work on its own under way will bring: the colonists share out the targets.
    private readonly Dictionary<string, int> _stock = new(StringComparer.Ordinal);
    private long _stockTick = -1;

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

        FindTasks(simulation, colonist);
        if (_tasks.Count == 0
            || simulation.Random.Get(InitiativeStream).NextInt(100) >= FreeWillRules.InitiativeChancePercent(simulation, colonist))
        {
            return false;
        }

        _tasks.Sort((a, b) =>
        {
            var order = a.Priority.CompareTo(b.Priority);
            order = order != 0 ? order : b.MissingPerMille.CompareTo(a.MissingPerMille);
            return order != 0 ? order : a.Order.CompareTo(b.Order);
        });
        foreach (var (task, _, _, _) in _tasks)
        {
            _candidates.Clear();
            AddInitiativePlants(simulation, task);
            if (TryCandidates(simulation, colonist, initiative: true))
            {
                // Another colonist deciding in this tick counts what this one will bring.
                CountIncoming(simulation, simulation.World.Get<Job>(colonist));
                return true;
            }
        }

        return false;
    }

    /// <summary>The tasks the colonist can do now: of a work type it does at its stage and priority, and whose target lacks something.</summary>
    private void FindTasks(Simulation simulation, EntityId colonist)
    {
        _tasks.Clear();
        var content = simulation.Content;
        var tasks = content.All<InitiativeTaskDef>();
        if (tasks.Count == 0)
        {
            return;
        }

        var stage = LifeRules.Stage(simulation, colonist);
        var targets = FreeWillRules.Targets(simulation);
        for (var i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            var workType = content.Get<WorkTypeDef>(task.WorkType);
            var priority = LifeRules.DoesWork(stage, workType)
                ? WorkPriorityRules.Of(simulation.World, colonist, workType.Id, _rules.Ai.DefaultWorkPriority)
                : WorkPriorityLevels.Never;
            var target = targets.FirstOrDefault(each => each.Category == task.Category)?.Count ?? 0;
            if (priority == WorkPriorityLevels.Never || target <= 0)
            {
                continue;
            }

            var have = StockOf(simulation, task.Category);
            if (have < target)
            {
                _tasks.Add((task, priority, (target - have) * 1000L / target, i));
            }
        }
    }

    /// <summary>
    /// The units of a category the village has, as the HUD counts them, and those the work on its
    /// own under way will bring: with enough colonists chopping for the wood target, the next one
    /// gathers food.
    /// </summary>
    private int StockOf(Simulation simulation, string category)
    {
        if (_stockTick != simulation.CurrentTick)
        {
            _stock.Clear();
            foreach (var line in ColonyStock.ByCategory(simulation))
            {
                _stock[line.Category.Id] = line.Count;
            }

            var world = simulation.World;
            world.Query<Job>(_entities);
            foreach (var worker in _entities)
            {
                CountIncoming(simulation, world.Get<Job>(worker));
            }

            _stockTick = simulation.CurrentTick;
        }

        return _stock.GetValueOrDefault(category);
    }

    /// <summary>Adds what a job on its own will bring to the stock counted in this tick.</summary>
    private void CountIncoming(Simulation simulation, Job job)
    {
        if (!job.Initiative || !simulation.World.TryGet<Plant>(job.Target, out var plant))
        {
            return;
        }

        var def = simulation.Content.Get<PlantDef>(plant.Def);
        if ((job.Kind == JobKind.Harvest ? def.Harvest : job.Kind == JobKind.Chop ? def.Chop : null) is { } output)
        {
            var category = simulation.Content.Get<ItemDef>(output.Item).Category;
            _stock[category] = _stock.GetValueOrDefault(category) + output.Count;
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
        world.Query<Plant, Position>(_entities);
        foreach (var plant in _entities)
        {
            if (plants.Contains(world.Get<Plant>(plant).Def)
                && !world.Has<ChopOrder>(plant) && !world.Has<HarvestOrder>(plant)
                && !(task.Kind == JobKind.Harvest && world.Has<Harvested>(plant))
                && FreeWillRules.IsNearVillage(simulation, world.Get<Position>(plant).Cell))
            {
                AddEntity(simulation, task.Kind, plant, capacity: 1);
            }
        }
    }

    private HashSet<string> PlantsOf(DefDatabase content, InitiativeTaskDef task)
    {
        _taskPlants ??= content.All<InitiativeTaskDef>().ToDictionary(
            each => each.Id,
            each => content.All<PlantDef>()
                .Where(plant => (each.Kind == JobKind.Harvest ? plant.Harvest : plant.Chop) is { } output
                                && content.Get<ItemDef>(output.Item).Category == each.Category)
                .Select(plant => plant.Id)
                .ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);
        return _taskPlants[task.Id];
    }
}
