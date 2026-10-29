using System.Runtime.CompilerServices;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>Where a blueprint stands in the sharing out of materials (see <see cref="SupplyBudget"/>).</summary>
internal enum SupplyState
{
    /// <summary>Lacks nothing.</summary>
    Complete,

    /// <summary>No colonist who builds can reach a cell next to it: it takes no material from the others.</summary>
    Unreachable,

    /// <summary>Somebody is bringing it what it lacks.</summary>
    Supplied,

    /// <summary>Something has been brought already: it is finished before others are started.</summary>
    Begun,

    /// <summary>Nothing brought yet, and the budget covers it.</summary>
    Funded,

    /// <summary>Nothing brought yet, reachable, and the units not claimed by others do not cover it.</summary>
    Starved,
}

/// <summary>
/// How the units of building items lying around are shared among the blueprints, in order of
/// entity id. One rule for the colonists (<see cref="DecisionSystem"/>) and for the questions of the
/// interface (<see cref="BuildQueries.StatusOf"/>). A blueprint nothing has been brought to is
/// <see cref="SupplyState.Funded"/> only if the units of its item lying around cover what it lacks, less
/// what the blueprints under way still lack and less what other jobs have reserved (a recipe, a tool
/// to equip, a baby to feed); and only blueprints some colonist who builds can reach spend anything,
/// so one nobody can reach does not keep material from those that can be built.
/// </summary>
internal sealed class SupplyBudget
{
    private static readonly ConditionalWeakTable<Simulation, SupplyBudget> Shared = new();

    private readonly List<EntityId> _colonists = [];
    private readonly List<EntityId> _entities = [];
    private readonly List<Entry> _entries = [];
    private readonly List<int> _builderRegions = [];
    private readonly List<TileCoord> _sites = [];
    private readonly Dictionary<string, int> _left = new(StringComparer.Ordinal);
    private readonly Dictionary<EntityId, int> _reserved = [];
    private readonly Dictionary<EntityId, int> _delivering = [];
    private readonly Dictionary<EntityId, SupplyState> _states = [];

    /// <summary>The blueprints a pile may be brought to, by ascending id, with the item they lack.</summary>
    public List<(EntityId Blueprint, string Item)> Needed { get; } = [];

    /// <summary>Whether some colonist does construction at all.</summary>
    public bool AnyBuilder { get; private set; }

    /// <summary>The reusable budget of a simulation (single-threaded, like the simulation itself).</summary>
    public static SupplyBudget For(Simulation simulation) => Shared.GetValue(simulation, _ => new SupplyBudget());

    public SupplyState StateOf(EntityId blueprint) => _states.GetValueOrDefault(blueprint, SupplyState.Complete);

    /// <summary>
    /// Shares out the materials. <paramref name="uses"/> are the jobs that hold each blueprint, or null to
    /// count the colonists on their way to deliver to it; <paramref name="colonists"/> are all of them, or null to find them.
    /// </summary>
    public void Compute(Simulation simulation, List<EntityId>? colonists, IReadOnlyDictionary<EntityId, int>? uses)
    {
        if (colonists is null)
        {
            simulation.World.Query<Colonist, Position>(_colonists);
            colonists = _colonists;
        }

        var world = simulation.World;
        var map = simulation.Map;
        var connectivity = simulation.Connectivity;
        Needed.Clear();
        _states.Clear();
        _entries.Clear();
        _left.Clear();
        _reserved.Clear();
        _delivering.Clear();
        _builderRegions.Clear();

        FindBuilders(simulation, colonists);
        foreach (var colonist in colonists)
        {
            if (!world.TryGet<Job>(colonist, out var job))
            {
                continue;
            }

            if (job.Kind == JobKind.Deliver && world.IsAlive(job.Recipient) && world.Has<Blueprint>(job.Recipient))
            {
                _delivering[job.Recipient] = _delivering.GetValueOrDefault(job.Recipient) + 1;
                if (world.TryGet<Carried>(colonist, out var carried))
                {
                    _left[carried.Def] = _left.GetValueOrDefault(carried.Def) + carried.Count;
                }
            }
            else if (job.Kind == JobKind.Craft && !job.Target.IsNone && job.Stage != JobStage.Carrying)
            {
                _reserved[job.Target] = int.MaxValue;
            }
            else if (job.Kind == JobKind.Equip && !job.Target.IsNone)
            {
                Reserve(job.Target, 1);
            }
            else if (job.Kind == JobKind.FeedBaby && !job.Target.IsNone && job.Stage != JobStage.Carrying)
            {
                Reserve(job.Target, Math.Max(1, job.Count));
            }
        }

        world.Query<Blueprint, Position>(_entities);
        foreach (var blueprint in _entities)
        {
            var plan = world.Get<Blueprint>(blueprint);
            var (item, missing) = Construction.Needs(simulation, plan);
            var held = uses is null ? _delivering.GetValueOrDefault(blueprint) : uses.GetValueOrDefault(blueprint);
            var reachable = missing > 0 && Reaches(map, connectivity, world.Get<Position>(blueprint).Cell);
            _entries.Add(new Entry(blueprint, item, missing, plan.Delivered, held, reachable));

            // What the blueprints under way (some brought, or somebody on the way) still lack.
            if (missing > 0 && (held > 0 || (plan.Delivered > 0 && reachable)))
            {
                _left[item] = _left.GetValueOrDefault(item) - missing;
            }
        }

        world.Query<ItemStack, Position>(_entities);
        foreach (var pile in _entities)
        {
            var stack = world.Get<ItemStack>(pile);
            var free = stack.Count - Math.Min(stack.Count, _reserved.GetValueOrDefault(pile));
            _left[stack.Def] = _left.GetValueOrDefault(stack.Def) + free;
        }

        foreach (var entry in _entries)
        {
            var state = StateFor(entry);
            _states[entry.Blueprint] = state;
            if (state is SupplyState.Funded or SupplyState.Begun)
            {
                Needed.Add((entry.Blueprint, entry.Item));
            }
        }
    }

    private SupplyState StateFor(Entry entry)
    {
        if (entry.Missing == 0)
        {
            return SupplyState.Complete;
        }

        if (entry.Held != 0)
        {
            return SupplyState.Supplied;
        }

        if (!entry.Reachable)
        {
            return SupplyState.Unreachable;
        }

        if (entry.Delivered > 0)
        {
            return SupplyState.Begun;
        }

        var left = _left.GetValueOrDefault(entry.Item);
        if (left < entry.Missing)
        {
            return SupplyState.Starved;
        }

        _left[entry.Item] = left - entry.Missing;
        return SupplyState.Funded;
    }

    private void Reserve(EntityId pile, int units)
    {
        var now = _reserved.GetValueOrDefault(pile);
        _reserved[pile] = now > int.MaxValue - units ? int.MaxValue : now + units;
    }

    /// <summary>The regions of the colonists who do construction, with whether there is any.</summary>
    private void FindBuilders(Simulation simulation, List<EntityId> colonists)
    {
        var content = simulation.Content;
        var world = simulation.World;
        WorkTypeDef? workType = null;
        foreach (var type in content.All<WorkTypeDef>())
        {
            if (type.Jobs.Contains(JobKinds.Id(JobKind.Build)))
            {
                workType = type;
                break;
            }
        }

        AnyBuilder = false;
        if (workType is null)
        {
            return;
        }

        var defaultPriority = content.All<AiConfigDef>() is [{ } ai] ? ai.DefaultWorkPriority : WorkPriorityLevels.Lowest;
        foreach (var colonist in colonists)
        {
            if (LifeRules.DoesWork(LifeRules.Stage(simulation, colonist), workType)
                && WorkPriorityRules.Of(world, colonist, workType.Id, defaultPriority) != WorkPriorityLevels.Never)
            {
                AnyBuilder = true;
                _builderRegions.Add(simulation.Connectivity.RegionAt(world.Get<Position>(colonist).Cell));
            }
        }
    }

    private bool Reaches(GameMap map, Connectivity connectivity, TileCoord cell)
    {
        if (_builderRegions.Count == 0)
        {
            return false;
        }

        WorkSites.Find(map, JobKind.Build, cell, _sites);
        foreach (var site in _sites)
        {
            if (_builderRegions.Contains(connectivity.RegionAt(site)))
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct Entry(EntityId Blueprint, string Item, int Missing, int Delivered, int Held, bool Reachable);
}
