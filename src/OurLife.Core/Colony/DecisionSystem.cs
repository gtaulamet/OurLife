using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Pathfinding;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// Gives idle colonists a job, in this order: a critical need, work by work type, a low need,
/// wandering (see <c>docs/gdd/colonos-e-ia.md</c>). Every <see cref="AiConfigDef.RecheckTicks"/>,
/// a busy colonist checks whether a critical need should interrupt its job, and a wandering one
/// looks again for something to do. Reservations come from the jobs colonists already have, so two
/// never share a target. Draws from the random stream <c>colony.decision</c>.
/// </summary>
public sealed class DecisionSystem : ISimSystem
{
    // Search limits, not balance: they bound the work of one decision.
    // A target without a path is set aside, and the next nearest is tried, this many times.
    private const int PathAttempts = 3;

    // Random places a wandering colonist tries before it waits where it is.
    private const int WanderAttempts = 8;

    private static readonly int[] LevelOffsets = [0, 1, -1];

    private readonly ColonyRules _rules;
    private readonly PathFinder _paths = new();
    private readonly List<EntityId> _colonists = [];
    private readonly List<EntityId> _entities = [];
    private readonly List<Candidate> _candidates = [];
    private readonly List<(Candidate Candidate, int Distance, int CellIndex)> _reachable = [];
    private readonly List<TileCoord> _sites = [];
    private readonly List<TileCoord> _path = [];
    private readonly List<TileCoord> _goal = [];
    private readonly List<NeedDef> _needsBelow = [];

    // Whether this tick falls in the night of the AiConfig; set at the start of each tick.
    private bool _night;

    // What the stockpiles hold, shared with the other colony system and rebuilt once per tick.
    private readonly StorageIndex _storage;

    // Orders on cells that have somewhere to stand, shared by every colonist. They depend only on
    // the orders and on where creatures can walk, so they are found again only when either changes.
    private readonly List<Candidate> _workableCells = [];
    private (int Navigation, int Orders)? _workableCellsVersion;

    // Reservations, rebuilt every tick from the jobs, with how many jobs hold each cell: a stockpile
    // cell can be both a haul's destination and a stairs order. Only looked up, never iterated.
    private readonly Dictionary<int, int> _reservedCells = [];
    private readonly Dictionary<EntityId, int> _entityUses = [];

    public DecisionSystem(ColonyRules rules)
        : this(rules, new StorageIndex())
    {
    }

    /// <summary>With the stockpile index the other colony system shares, so it is built once per tick.</summary>
    internal DecisionSystem(ColonyRules rules, StorageIndex storage)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(storage);
        _rules = rules;
        _storage = storage;
    }

    public void Tick(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        _night = _rules.Ai.IsNight(simulation.CurrentDate.Hour);
        world.Query<Colonist, Position>(_colonists);
        ReserveCurrentJobs(simulation);

        foreach (var colonist in _colonists)
        {
            ForgetExpired(world.Get<Colonist>(colonist), simulation.CurrentTick);
            if (world.TryGet<Job>(colonist, out var job))
            {
                Reconsider(simulation, colonist, job);
            }
            else
            {
                Decide(simulation, colonist);
            }
        }
    }

    private static bool Satisfies(Job job, NeedDef need) => need.SatisfiedBy switch
    {
        NeedSatisfaction.Eating => job.Kind == JobKind.Eat || (job.Kind == JobKind.Harvest && job.ForFood),
        NeedSatisfaction.Sleeping => job.Kind == JobKind.Sleep,
        _ => false,
    };

    private static bool IsIgnored(Colonist person, EntityId entity, TileCoord cell)
    {
        foreach (var target in person.Unreachable)
        {
            if (entity.IsNone ? target.Entity.IsNone && target.Cell == cell : target.Entity == entity)
            {
                return true;
            }
        }

        return false;
    }

    private static void ForgetExpired(Colonist person, long tick)
    {
        for (var i = person.Unreachable.Count - 1; i >= 0; i--)
        {
            if (person.Unreachable[i].UntilTick <= tick)
            {
                person.Unreachable.RemoveAt(i);
            }
        }
    }

    private static bool AnyInRegion(List<TileCoord> cells, Connectivity connectivity, int region)
    {
        foreach (var cell in cells)
        {
            if (connectivity.RegionAt(cell) == region)
            {
                return true;
            }
        }

        return false;
    }

    private static void KeepInRegion(List<TileCoord> cells, Connectivity connectivity, int region)
    {
        for (var i = cells.Count - 1; i >= 0; i--)
        {
            if (connectivity.RegionAt(cells[i]) != region)
            {
                cells.RemoveAt(i);
            }
        }
    }

    private void ReserveCurrentJobs(Simulation simulation)
    {
        _reservedCells.Clear();
        _entityUses.Clear();
        foreach (var colonist in _colonists)
        {
            if (simulation.World.TryGet<Job>(colonist, out var job))
            {
                Reserve(simulation.Map, job);
            }
        }
    }

    private void Reserve(GameMap map, Job job)
    {
        // A hauler holds both the pile and the stockpile cell it carries to.
        if (job.Kind == JobKind.Haul)
        {
            ReserveCell(map, job.Destination);
        }

        if (HeldEntity(job) is { IsNone: false } held)
        {
            _entityUses[held] = _entityUses.GetValueOrDefault(held) + 1;
        }
        else if (job.Kind is JobKind.Mine or JobKind.DigStairs)
        {
            ReserveCell(map, job.Cell);

            // Stairs also dig out the cell below, which may have its own order to dig.
            if (job.Kind == JobKind.DigStairs)
            {
                ReserveCell(map, job.Cell with { Z = job.Cell.Z - 1 });
            }
        }
    }

    /// <summary>Undoes <see cref="Reserve"/>, for a job the colonist may give up.</summary>
    private void Unreserve(GameMap map, Job job)
    {
        if (job.Kind == JobKind.Haul)
        {
            UnreserveCell(map, job.Destination);
        }

        if (HeldEntity(job) is { IsNone: false } held)
        {
            var uses = _entityUses.GetValueOrDefault(held) - 1;
            if (uses > 0)
            {
                _entityUses[held] = uses;
            }
            else
            {
                _entityUses.Remove(held);
            }
        }
        else if (job.Kind is JobKind.Mine or JobKind.DigStairs)
        {
            UnreserveCell(map, job.Cell);
            if (job.Kind == JobKind.DigStairs)
            {
                UnreserveCell(map, job.Cell with { Z = job.Cell.Z - 1 });
            }
        }
    }

    private bool IsReserved(GameMap map, TileCoord cell) => _reservedCells.ContainsKey(map.Size.IndexOf(cell.X, cell.Y, cell.Z));

    private void ReserveCell(GameMap map, TileCoord cell)
    {
        var index = map.Size.IndexOf(cell.X, cell.Y, cell.Z);
        _reservedCells[index] = _reservedCells.GetValueOrDefault(index) + 1;
    }

    private void UnreserveCell(GameMap map, TileCoord cell)
    {
        var index = map.Size.IndexOf(cell.X, cell.Y, cell.Z);
        var holders = _reservedCells.GetValueOrDefault(index) - 1;
        if (holders > 0)
        {
            _reservedCells[index] = holders;
        }
        else
        {
            _reservedCells.Remove(index);
        }
    }

    /// <summary>The entity a job holds: none for a hauler that already lifted its load, which leaves the rest of the pile free.</summary>
    private static EntityId HeldEntity(Job job) =>
        job.Kind == JobKind.Haul && job.Stage == JobStage.Carrying ? EntityId.None : job.Target;

    /// <summary>
    /// A colonist with a job drops it only for something more urgent. It checks every
    /// <see cref="AiConfigDef.RecheckTicks"/>, staggered by entity, so a need that cannot be met
    /// does not search the whole map on every tick. While it looks, its own job reserves nothing,
    /// so it can choose the same target again for another reason.
    /// </summary>
    private void Reconsider(Simulation simulation, EntityId colonist, Job job)
    {
        if ((simulation.CurrentTick + colonist.Index) % _rules.Ai.RecheckTicks != 0)
        {
            return;
        }

        Unreserve(simulation.Map, job);
        if (!TryInterrupt(simulation, colonist, job))
        {
            Reserve(simulation.Map, job);
        }
    }

    /// <summary>True if the colonist got a more urgent job instead of <paramref name="job"/>.</summary>
    private bool TryInterrupt(Simulation simulation, EntityId colonist, Job job)
    {
        // Eating is short and gathering to eat already serves a need: neither is interrupted.
        var interruptible = job.Kind != JobKind.Eat && !(job.Kind == JobKind.Harvest && job.ForFood);
        if (interruptible)
        {
            foreach (var need in NeedsBelow(simulation, colonist, critical: true))
            {
                if (!Satisfies(job, need) && TryNeed(simulation, colonist, need))
                {
                    return true;
                }
            }
        }

        if (job.Kind != JobKind.Wander)
        {
            return false;
        }

        if (TryWork(simulation, colonist))
        {
            return true;
        }

        // Critical needs were tried above, and the job is wandering, which they always interrupt.
        foreach (var need in NeedsBelow(simulation, colonist, critical: false))
        {
            if (TryNeed(simulation, colonist, need))
            {
                return true;
            }
        }

        return false;
    }

    private void Decide(Simulation simulation, EntityId colonist)
    {
        foreach (var need in NeedsBelow(simulation, colonist, critical: true))
        {
            if (TryNeed(simulation, colonist, need))
            {
                return;
            }
        }

        if (TryWork(simulation, colonist))
        {
            return;
        }

        foreach (var need in NeedsBelow(simulation, colonist, critical: false))
        {
            if (TryNeed(simulation, colonist, need))
            {
                return;
            }
        }

        Wander(simulation, colonist);
    }

    /// <summary>
    /// The needs that come before any work, or, with <paramref name="critical"/> false, those that
    /// wait until there is no work: callers always try the first ones first, so a need with no way
    /// to meet it is searched for once per decision. In the order of the creature. The list is
    /// reused: read it before the next call.
    /// </summary>
    /// <remarks>
    /// A need comes first below its critical threshold, and a need met by sleeping also below its
    /// night threshold at night. By day, sleeping waits for the critical threshold: the low one
    /// does not count, so colonists keep their nights (see <c>docs/gdd/dia-y-noche.md</c>).
    /// </remarks>
    private List<NeedDef> NeedsBelow(Simulation simulation, EntityId colonist, bool critical)
    {
        _needsBelow.Clear();
        foreach (var state in simulation.World.Get<Needs>(colonist).Values)
        {
            var need = simulation.Content.Get<NeedDef>(state.Need);
            var sleep = need.SatisfiedBy == NeedSatisfaction.Sleeping;
            var urgent = state.Value < need.CriticalThreshold || (_night && sleep && state.Value < need.NightThresholdOrLow);
            var low = !urgent && state.Value < need.LowThreshold && !(sleep && !_night);
            if (critical ? urgent : low)
            {
                _needsBelow.Add(need);
            }
        }

        return _needsBelow;
    }

    private bool TryNeed(Simulation simulation, EntityId colonist, NeedDef need)
    {
        switch (need.SatisfiedBy)
        {
            case NeedSatisfaction.Sleeping:
                var cell = simulation.World.Get<Position>(colonist).Cell;
                Assign(simulation, colonist, new Job { Kind = JobKind.Sleep, Cell = cell }, []);
                return true;
            case NeedSatisfaction.Eating:
                _candidates.Clear();
                AddFoodPiles(simulation);
                if (TryCandidates(simulation, colonist))
                {
                    return true;
                }

                _candidates.Clear();
                AddFoodPlants(simulation);
                return TryCandidates(simulation, colonist, forFood: true);
            default:
                return false;
        }
    }

    private bool TryWork(Simulation simulation, EntityId colonist)
    {
        foreach (var jobs in _rules.WorkJobs)
        {
            _candidates.Clear();
            bool mine = false, stairs = false;
            foreach (var kind in jobs)
            {
                mine |= kind == JobKind.Mine;
                stairs |= kind == JobKind.DigStairs;
                AddWork(simulation, kind);
            }

            AddOrderedCells(simulation, mine, stairs);
            if (TryCandidates(simulation, colonist))
            {
                return true;
            }
        }

        return false;
    }

    private void AddWork(Simulation simulation, JobKind kind)
    {
        var world = simulation.World;
        var map = simulation.Map;
        switch (kind)
        {
            case JobKind.Mine:
            case JobKind.DigStairs:
                // Orders on cells are gathered in one pass by AddOrderedCells.
                break;
            case JobKind.Chop:
                world.Query<ChopOrder, Position>(_entities);
                foreach (var plant in _entities)
                {
                    AddEntity(simulation, kind, plant, capacity: 1);
                }

                break;
            case JobKind.Harvest:
                world.Query<HarvestOrder, Position>(_entities);
                foreach (var plant in _entities)
                {
                    if (!world.Has<Harvested>(plant))
                    {
                        AddEntity(simulation, kind, plant, capacity: 1);
                    }
                }

                break;
            case JobKind.Haul:
                // Loose piles with somewhere to go; the destination is chosen with the pile.
                var storage = Storage(simulation);
                if (storage.Zones.Count == 0)
                {
                    break;
                }

                world.Query<ItemStack, Position>(_entities);
                foreach (var pile in _entities)
                {
                    if (!storage.IsStored(simulation, pile) && storage.AnyRoomFor(simulation, world.Get<ItemStack>(pile).Def))
                    {
                        AddEntity(simulation, kind, pile, capacity: 1);
                    }
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    /// <summary>The cells with orders to dig walls or stairs that nobody has taken.</summary>
    private void AddOrderedCells(Simulation simulation, bool mine, bool stairs)
    {
        if (!mine && !stairs)
        {
            return;
        }

        var map = simulation.Map;
        RefreshWorkableCells(simulation);
        foreach (var candidate in _workableCells)
        {
            var wanted = candidate.Kind == JobKind.Mine
                ? mine && !IsReserved(map, candidate.Cell)
                : stairs && !IsReserved(map, candidate.Cell) && !IsReserved(map, new TileCoord(candidate.Cell.X, candidate.Cell.Y, candidate.Cell.Z - 1));
            if (wanted)
            {
                _candidates.Add(candidate);
            }
        }
    }

    /// <summary>
    /// The orders on cells that still make sense and have somewhere to stand: a wall buried in
    /// rock is skipped until digging reaches it. Found again only when the orders or the ways
    /// change (<see cref="GameMap.OrdersVersion"/>, <see cref="GameMap.NavigationVersion"/>).
    /// </summary>
    private void RefreshWorkableCells(Simulation simulation)
    {
        var map = simulation.Map;
        var version = (map.NavigationVersion, map.OrdersVersion);
        if (_workableCellsVersion == version)
        {
            return;
        }

        _workableCells.Clear();
        foreach (var index in map.OrderedCells)
        {
            var cell = map.Size.CoordOf(index);
            var flags = map.FlagsAt(cell);
            JobKind kind;
            if ((flags & TileFlags.MineDesignation) != 0 && map.ShapeAt(cell) == TileShape.Wall)
            {
                kind = JobKind.Mine;
            }
            else if ((flags & TileFlags.StairsDesignation) != 0 && Designations.CanDigStairs(map, cell))
            {
                kind = JobKind.DigStairs;
            }
            else
            {
                continue;
            }

            WorkSites.Find(map, kind, cell, _sites);
            if (_sites.Count > 0)
            {
                _workableCells.Add(new Candidate(kind, EntityId.None, cell));
            }
        }

        _workableCellsVersion = version;
    }

    private void AddFoodPiles(Simulation simulation)
    {
        var world = simulation.World;
        world.Query<ItemStack, Position>(_entities);
        foreach (var pile in _entities)
        {
            var stack = world.Get<ItemStack>(pile);
            if (simulation.Content.Get<ItemDef>(stack.Def).Kind == ItemKind.Food)
            {
                // A pile feeds as many colonists at once as it has units.
                AddEntity(simulation, JobKind.Eat, pile, capacity: stack.Count);
            }
        }
    }

    private void AddFoodPlants(Simulation simulation)
    {
        var world = simulation.World;
        world.Query<Plant, Position>(_entities);
        foreach (var plant in _entities)
        {
            if (_rules.FoodPlants.Contains(world.Get<Plant>(plant).Def) && !world.Has<Harvested>(plant))
            {
                AddEntity(simulation, JobKind.Harvest, plant, capacity: 1);
            }
        }
    }

    private void AddEntity(Simulation simulation, JobKind kind, EntityId entity, int capacity)
    {
        if (_entityUses.GetValueOrDefault(entity) < capacity)
        {
            _candidates.Add(new Candidate(kind, entity, simulation.World.Get<Position>(entity).Cell));
        }
    }

    /// <summary>
    /// Takes the nearest candidate the colonist can reach and finds a path to it. A candidate
    /// without a path is ignored for a while and the next one is tried.
    /// </summary>
    private bool TryCandidates(Simulation simulation, EntityId colonist, bool forFood = false)
    {
        if (_candidates.Count == 0)
        {
            return false;
        }

        var map = simulation.Map;
        var connectivity = simulation.Connectivity;
        var person = simulation.World.Get<Colonist>(colonist);
        var from = simulation.World.Get<Position>(colonist).Cell;
        var region = connectivity.RegionAt(from);

        var reachable = _reachable;
        reachable.Clear();
        foreach (var candidate in _candidates)
        {
            if (IsIgnored(person, candidate.Target, candidate.Cell))
            {
                continue;
            }

            WorkSites.Find(map, candidate.Kind, candidate.Cell, _sites);
            if (AnyInRegion(_sites, connectivity, region))
            {
                reachable.Add((candidate, PathFinder.Estimate(from, candidate.Cell), map.Size.IndexOf(candidate.Cell.X, candidate.Cell.Y, candidate.Cell.Z)));
            }
        }

        // Nearest first; ties by cell, then by entity, so the choice never depends on chance.
        reachable.Sort((a, b) =>
        {
            var order = a.Distance.CompareTo(b.Distance);
            order = order != 0 ? order : a.CellIndex.CompareTo(b.CellIndex);
            return order != 0 ? order : a.Candidate.Target.CompareTo(b.Candidate.Target);
        });

        for (var i = 0; i < reachable.Count && i < PathAttempts; i++)
        {
            var candidate = reachable[i].Candidate;
            var destination = default(TileCoord);
            if (candidate.Kind == JobKind.Haul && !TryDestination(simulation, candidate, region, out destination))
            {
                continue;
            }

            WorkSites.Find(map, candidate.Kind, candidate.Cell, _sites);
            KeepInRegion(_sites, connectivity, region);
            if (_paths.TryFind(map, from, _sites, _path))
            {
                var job = new Job
                {
                    Kind = candidate.Kind,
                    Target = candidate.Target,
                    Cell = candidate.Cell,
                    ForFood = forFood,
                    Destination = destination,
                };
                Assign(simulation, colonist, job, _path);
                return true;
            }

            person.Unreachable.Add(new IgnoredTarget
            {
                Entity = candidate.Target,
                Cell = candidate.Cell,
                UntilTick = simulation.CurrentTick + _rules.Ai.UnreachableCooldownTicks,
            });
        }

        return false;
    }

    private StorageIndex Storage(Simulation simulation) => _storage.Fresh(simulation);

    /// <summary>
    /// The stockpile cell nearest the pile, in the colonist's region, that accepts its item, has
    /// room and nobody else carries to. Ties go to the lowest cell index.
    /// </summary>
    private bool TryDestination(Simulation simulation, Candidate candidate, int region, out TileCoord destination)
    {
        var storage = Storage(simulation);
        var map = simulation.Map;
        var connectivity = simulation.Connectivity;
        var item = simulation.World.Get<ItemStack>(candidate.Target).Def;
        destination = default;
        var best = int.MaxValue;
        var bestIndex = int.MaxValue;
        foreach (var zone in storage.Zones)
        {
            foreach (var cell in simulation.World.Get<Stockpile>(zone).Cells)
            {
                if (cell == candidate.Cell || IsReserved(map, cell) || storage.SpaceFor(simulation, item, cell) == 0
                    || connectivity.RegionAt(cell) != region)
                {
                    continue;
                }

                var distance = PathFinder.Estimate(candidate.Cell, cell);
                var index = map.Size.IndexOf(cell.X, cell.Y, cell.Z);
                if (distance < best || (distance == best && index < bestIndex))
                {
                    (best, bestIndex, destination) = (distance, index, cell);
                }
            }
        }

        return best != int.MaxValue;
    }

    private void Wander(Simulation simulation, EntityId colonist)
    {
        var map = simulation.Map;
        var connectivity = simulation.Connectivity;
        var from = simulation.World.Get<Position>(colonist).Cell;
        var region = connectivity.RegionAt(from);
        var random = simulation.Random.Get("colony.decision");
        var radius = _rules.Ai.WanderRadius;

        for (var attempt = 0; attempt < WanderAttempts; attempt++)
        {
            var x = from.X + random.NextInt(-radius, radius + 1);
            var y = from.Y + random.NextInt(-radius, radius + 1);
            foreach (var dz in LevelOffsets)
            {
                var cell = new TileCoord(x, y, from.Z + dz);
                if (cell == from || !map.IsStandable(cell) || connectivity.RegionAt(cell) != region)
                {
                    continue;
                }

                _goal.Clear();
                _goal.Add(cell);
                if (_paths.TryFind(map, from, _goal, _path))
                {
                    Assign(simulation, colonist, new Job { Kind = JobKind.Wander, Cell = cell }, _path);
                    return;
                }
            }
        }

        // Nowhere to go: wait where it is.
        Assign(simulation, colonist, new Job { Kind = JobKind.Wander, Cell = from }, []);
    }

    private void Assign(Simulation simulation, EntityId colonist, Job job, List<TileCoord> path)
    {
        var world = simulation.World;
        world.Remove<Job>(colonist);
        job.Stage = JobStage.Moving;
        world.Add(colonist, job);

        var movement = world.Get<Movement>(colonist);
        movement.Stop();
        movement.Path.AddRange(path);
        Reserve(simulation.Map, job);
    }

    private readonly record struct Candidate(JobKind Kind, EntityId Target, TileCoord Cell);
}
