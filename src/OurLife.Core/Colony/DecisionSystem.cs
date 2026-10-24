using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Pathfinding;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// Gives idle colonists a job, in this order: a critical need, work by work type, a low need,
/// work on its own with free will (<c>docs/gdd/libre-albedrio.md</c>), play for a stage that plays,
/// company with free will, wandering (see <c>docs/gdd/colonos-e-ia.md</c>), away from rivals with
/// free will. A baby, who does
/// not eat on its own, is fed as work and crawls near its mother (see "Crianza" in
/// <c>docs/gdd/vida-y-generaciones.md</c>). Every <see cref="AiConfigDef.RecheckTicks"/>,
/// a busy colonist checks whether a critical need should interrupt its job, and a wandering one
/// looks again for something to do. Reservations come from the jobs colonists already have, so two
/// never share a target. Draws from the random stream <c>colony.decision</c>, and the chances and
/// choices of free will from <c>colony.free_will</c>.
/// </summary>
public sealed partial class DecisionSystem : ISimSystem
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
    private readonly List<Reach> _reachable = [];
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

    // Piles a hauler is going to lift: nobody goes to eat from them, since the hauler may take them whole.
    private readonly HashSet<EntityId> _hauledPiles = [];

    // Items with no stockpile cell to go to in this search, so the other piles of them are skipped.
    private readonly HashSet<(string Item, string? Quality)> _noDestination = [];

    // Blueprints that lack materials and nobody supplies, by ascending id, with the item they lack;
    // and the items any of them lacks. Found when a work type with deliveries is searched.
    private readonly List<(EntityId Blueprint, string Item)> _supplyNeeded = [];
    private readonly HashSet<string> _neededItems = new(StringComparer.Ordinal);

    // Cells that work under way will make unstandable: wall blueprints and the cells ordered to
    // be channelled or taken down to air. Rebuilt every tick; only looked up, never iterated.
    private readonly HashSet<TileCoord> _closingCells = [];
    private readonly List<EntityId> _blueprints = [];

    // Items no blueprint can take from the colonist in this search.
    private readonly HashSet<string> _noRecipient = new(StringComparer.Ordinal);
    private readonly List<TileCoord> _recipientSites = [];
    private readonly List<JobKind> _cellJobs = [];
    private readonly List<EntityId> _playmates = [];
    private readonly List<TileCoord> _avoided = [];
    private int[] _priorities = [];

    // The work type that feeds babies, so a parent's priority for it can be read.
    private readonly WorkTypeDef _childcare;

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
        _childcare = rules.WorkTypeOf(new Job { Kind = JobKind.FeedBaby })!;
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
        _hauledPiles.Clear();
        FindClosingCells(simulation);
        foreach (var colonist in _colonists)
        {
            if (simulation.World.TryGet<Job>(colonist, out var job))
            {
                Reserve(simulation, job);
            }
        }
    }

    private void Reserve(Simulation simulation, Job job)
    {
        // A hauler holds both the pile and the stockpile cell it carries to, and a deliverer the
        // pile and the blueprint: both may lift a whole pile, so nobody goes to eat from it. A
        // feeder holds the baby and, of the food, the units the baby lacks.
        var map = simulation.Map;
        if (job.Kind == JobKind.Haul)
        {
            ReserveCell(map, job.Destination);
        }
        else if (job.Kind is JobKind.Deliver or JobKind.FeedBaby or JobKind.Craft or JobKind.Research)
        {
            // A crafter holds its workshop: one colonist at a time works at it.
            _entityUses[job.Recipient] = _entityUses.GetValueOrDefault(job.Recipient) + 1;
        }

        if (HeldEntity(job) is { IsNone: false } held)
        {
            _entityUses[held] = _entityUses.GetValueOrDefault(held) + UsesOf(job);
            if (job.Kind is JobKind.Haul or JobKind.Deliver or JobKind.Craft)
            {
                _hauledPiles.Add(held);
            }
        }
        else if (IsCellOrder(job.Kind) || job.Kind == JobKind.Sow)
        {
            ReserveCell(map, job.Cell);

            // Stairs and channels also dig out the cell below, which may have its own order to
            // dig; taking down stairs takes down their other half.
            if (OtherCellOf(map, job.Kind, job.Cell) is { } other)
            {
                ReserveCell(map, other);
            }
        }
    }

    /// <summary>Undoes <see cref="Reserve"/>, for a job the colonist may give up.</summary>
    private void Unreserve(Simulation simulation, Job job)
    {
        var map = simulation.Map;
        if (job.Kind == JobKind.Haul)
        {
            UnreserveCell(map, job.Destination);
        }
        else if (job.Kind is JobKind.Deliver or JobKind.FeedBaby or JobKind.Craft or JobKind.Research)
        {
            ReleaseEntity(job.Recipient);
        }

        if (HeldEntity(job) is { IsNone: false } held)
        {
            if (job.Kind is JobKind.Haul or JobKind.Deliver or JobKind.Craft)
            {
                _hauledPiles.Remove(held);
            }

            ReleaseEntity(held, UsesOf(job));
        }
        else if (IsCellOrder(job.Kind) || job.Kind == JobKind.Sow)
        {
            UnreserveCell(map, job.Cell);
            if (OtherCellOf(map, job.Kind, job.Cell) is { } other)
            {
                UnreserveCell(map, other);
            }
        }
    }

    /// <summary>Jobs on cells the player ordered, gathered in one pass by <see cref="AddOrderedCells"/>.</summary>
    private static bool IsCellOrder(JobKind kind) => kind is JobKind.Mine or JobKind.DigStairs or JobKind.Channel or JobKind.Deconstruct;

    /// <summary>
    /// The other cell a job on cells changes: the one below for stairs and channels, the other
    /// half for taking down stairs; null for the rest. Shapes do not change between reserving and
    /// releasing, which happen in the same decision.
    /// </summary>
    private static TileCoord? OtherCellOf(GameMap map, JobKind kind, TileCoord cell) => kind switch
    {
        JobKind.DigStairs or JobKind.Channel => cell with { Z = cell.Z - 1 },
        JobKind.Deconstruct => Construction.StairPartner(map, cell),
        _ => null,
    };

    /// <summary>
    /// The cells that waiting work will make unstandable. Work that waits while someone stands on
    /// its cell is never done from one of them: two builders of neighbouring walls, each on the
    /// other's cell, would wait for each other forever.
    /// </summary>
    private void FindClosingCells(Simulation simulation)
    {
        var world = simulation.World;
        var map = simulation.Map;
        _closingCells.Clear();
        world.Query<Blueprint, Position>(_blueprints);
        foreach (var blueprint in _blueprints)
        {
            if (simulation.Content.Get<BuildableDef>(world.Get<Blueprint>(blueprint).Buildable).Shape == BuildShape.Wall)
            {
                _closingCells.Add(world.Get<Position>(blueprint).Cell);
            }
        }

        foreach (var index in map.OrderedCells)
        {
            var cell = map.Size.CoordOf(index);
            var flags = map.FlagsAt(cell);
            if ((flags & TileFlags.ChannelDesignation) != 0)
            {
                _closingCells.Add(cell);
            }

            if ((flags & TileFlags.DeconstructDesignation) != 0)
            {
                _closingCells.UnionWith(Construction.CellsLeftOpen(simulation, cell));
            }
        }
    }

    /// <summary>The cells to work from; for work that waits on its cell, none that other work will close.</summary>
    private void FindSites(GameMap map, JobKind kind, TileCoord target)
    {
        WorkSites.Find(map, kind, target, _sites);
        if (kind is JobKind.Build or JobKind.Channel or JobKind.Deconstruct)
        {
            _sites.RemoveAll(_closingCells.Contains);
        }
    }

    /// <summary>How many uses of its target a job holds: one, but for a feeder on its way to the food, the units it holds for the baby.</summary>
    private static int UsesOf(Job job) => job.Kind == JobKind.FeedBaby ? Math.Max(1, job.Count) : 1;

    private void ReleaseEntity(EntityId entity, int count = 1)
    {
        var uses = _entityUses.GetValueOrDefault(entity) - count;
        if (uses > 0)
        {
            _entityUses[entity] = uses;
        }
        else
        {
            _entityUses.Remove(entity);
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

    /// <summary>
    /// The entity a job holds: none for a hauler, deliverer, feeder or crafter that already lifted its
    /// load, which leaves the rest of the pile free; a crafter at work holds no pile either.
    /// </summary>
    private static EntityId HeldEntity(Job job) =>
        job.Kind is JobKind.Haul or JobKind.Deliver or JobKind.FeedBaby or JobKind.Craft && job.Stage == JobStage.Carrying ? EntityId.None : job.Target;

    /// <summary>
    /// A colonist with a job drops it only for something more urgent. It checks every
    /// <see cref="AiConfigDef.RecheckTicks"/>, staggered by entity, so a need that cannot be met
    /// does not search the whole map on every tick. While it looks, its own job reserves nothing,
    /// so it can choose the same target again for another reason.
    /// </summary>
    private void Reconsider(Simulation simulation, EntityId colonist, Job job)
    {
        // A visitor on its way also looks at every tick whether its company is going elsewhere, so
        // it does not arrive where nobody is: only a new walk of the other makes it search a path.
        var recheck = (simulation.CurrentTick + colonist.Index) % _rules.Ai.RecheckTicks == 0;
        if (!recheck && !(job.Kind == JobKind.Visit && job.Stage == JobStage.Moving && Relationships.IsLivingColonist(simulation.World, job.Target)
                          && FreeWillRules.AimOf(simulation.World, job.Target) != job.Cell))
        {
            return;
        }

        Unreserve(simulation, job);
        if (!(recheck ? TryInterrupt(simulation, colonist, job) : TryFollow(simulation, colonist, job)))
        {
            Reserve(simulation, job);
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

        // Critical needs were tried above. Work and low needs interrupt wandering, playing and
        // keeping company, and work on its own on the way to its plant: once at it, it finishes, so
        // no work is lost. A wanderer may also work on its own, go to play or seek company, but a
        // player keeps its playmate and a visitor its company, which it follows on the way.
        if (job.Initiative)
        {
            return job.Stage == JobStage.Moving && TryWorkOrLowNeeds(simulation, colonist);
        }

        return job.Kind switch
        {
            JobKind.Wander => TryWorkOrLowNeeds(simulation, colonist) || TryInitiative(simulation, colonist) || TryPlay(simulation, colonist)
                              || TrySeek(simulation, colonist),
            JobKind.Play => TryWorkOrLowNeeds(simulation, colonist),
            JobKind.Visit => TryWorkOrLowNeeds(simulation, colonist) || TryFollow(simulation, colonist, job),
            _ => false,
        };
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

        if (!TryWorkOrLowNeeds(simulation, colonist) && !TryInitiative(simulation, colonist) && !TryPlay(simulation, colonist)
            && !TrySeek(simulation, colonist))
        {
            Wander(simulation, colonist);
        }
    }

    /// <summary>
    /// Work, and if there is none, the low needs. A colonist whose traits put its needs first, like
    /// a lazy one (see "Personalidad" in <c>docs/gdd/vida-y-generaciones.md</c>), looks after its
    /// low needs before working.
    /// </summary>
    /// <remarks>A colonist in a crisis does no work: it only looks after its needs and wanders.</remarks>
    private bool TryWorkOrLowNeeds(Simulation simulation, EntityId colonist)
    {
        if (MoodRules.CrisisOf(simulation, colonist) is not null)
        {
            return TryLowNeeds(simulation, colonist);
        }

        return PersonalityRules.NeedsBeforeWork(simulation, colonist)
            ? TryLowNeeds(simulation, colonist) || TryWork(simulation, colonist)
            : TryWork(simulation, colonist) || TryLowNeeds(simulation, colonist);
    }

    private bool TryLowNeeds(Simulation simulation, EntityId colonist)
    {
        foreach (var need in NeedsBelow(simulation, colonist, critical: false))
        {
            if (TryNeed(simulation, colonist, need))
            {
                return true;
            }
        }

        return false;
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
        var eatsAlone = LifeRules.Stage(simulation, colonist)?.EatsAlone ?? true;
        foreach (var state in simulation.World.Get<Needs>(colonist).Values)
        {
            var need = simulation.Content.Get<NeedDef>(state.Need);
            if (!eatsAlone && need.SatisfiedBy == NeedSatisfaction.Eating)
            {
                // A baby is fed by others, as their work.
                continue;
            }

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
                GoToSleep(simulation, colonist);
                return true;
            case NeedSatisfaction.Eating:
                _candidates.Clear();
                AddFoodPiles(simulation);

                // Hunger that is low, not critical, can wait to choose what to eat.
                if (TryCandidates(simulation, colonist, eatChoice: !IsCritical(simulation, colonist, need)))
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

    /// <summary>
    /// Sleeps in the colonist's bed, after taking the nearest free one it can reach if it has none
    /// (see <c>docs/gdd/muebles-y-camas.md</c>); on the ground where it stands if there is no bed
    /// or no way to it.
    /// </summary>
    private void GoToSleep(Simulation simulation, EntityId colonist)
    {
        var world = simulation.World;
        var from = world.Get<Position>(colonist).Cell;
        if (BedOf(simulation, colonist, from) is { IsNone: false } bed)
        {
            var cell = world.Get<Position>(bed).Cell;
            _goal.Clear();
            _goal.Add(cell);
            if (cell == from || _paths.TryFind(simulation.Map, from, _goal, _path))
            {
                // A free bed becomes the colonist's only once it can get there.
                if (world.Get<Bed>(bed).Owner != colonist)
                {
                    world.Get<Bed>(bed).Owner = colonist;
                    simulation.Events.Emit(new EntityChangedEvent(bed));
                }

                Assign(simulation, colonist, new Job { Kind = JobKind.Sleep, Target = bed, Cell = cell }, cell == from ? [] : _path);
                return;
            }
        }

        Assign(simulation, colonist, new Job { Kind = JobKind.Sleep, Cell = from }, []);
    }

    /// <summary>
    /// The colonist's bed if it can reach it; else the nearest free bed it can reach (ties to the
    /// lowest id), which <see cref="GoToSleep"/> gives it if there is a path; else none. Only beds
    /// for its stage of life count, like a cradle for a baby (see <see cref="Beds"/>). A bed whose
    /// owner is gone, or grew out of it, is free.
    /// </summary>
    private EntityId BedOf(Simulation simulation, EntityId colonist, TileCoord from)
    {
        var world = simulation.World;
        var connectivity = simulation.Connectivity;
        world.Query<Bed, Position>(_entities);
        _entities.RemoveAll(bed => !Beds.Fits(simulation, bed, colonist));
        var owned = _entities.Find(bed => world.Get<Bed>(bed).Owner == colonist);
        if (!owned.IsNone)
        {
            return connectivity.AreConnected(from, world.Get<Position>(owned).Cell) ? owned : EntityId.None;
        }

        var best = EntityId.None;
        var bestDistance = int.MaxValue;
        foreach (var bed in _entities)
        {
            var cell = world.Get<Position>(bed).Cell;
            var distance = PathFinder.Estimate(from, cell);
            if (!Beds.IsTaken(simulation, bed) && distance < bestDistance
                && connectivity.AreConnected(from, cell))
            {
                (best, bestDistance) = (bed, distance);
            }
        }

        return best;
    }

    /// <summary>
    /// Looks for work by the colonist's priorities, from the highest (see <c>docs/gdd/gestion.md</c>):
    /// at each one, its work types of that priority in the order of the defs. A work type it never
    /// does, or does not do at its stage of life (see <c>docs/gdd/vida-y-generaciones.md</c>), is
    /// skipped; its priorities still count when it grows up.
    /// </summary>
    private bool TryWork(Simulation simulation, EntityId colonist)
    {
        // Each type's priority is looked up once, then the levels are walked.
        var types = _rules.WorkTypes.Count;
        if (_priorities.Length < types)
        {
            _priorities = new int[types];
        }

        var stage = LifeRules.Stage(simulation, colonist);
        for (var type = 0; type < types; type++)
        {
            _priorities[type] = PriorityOf(simulation, colonist, stage, _rules.WorkTypes[type]);
        }

        for (var priority = WorkPriorityLevels.Highest; priority <= WorkPriorityLevels.Lowest; priority++)
        {
            for (var type = 0; type < types; type++)
            {
                // The one job of a workshop's work type is crafting, which takes its orders from the workshops.
                if (_priorities[type] == priority
                    && (_rules.WorkTypes[type].Workshop
                        ? TryCraft(simulation, colonist, _rules.WorkTypes[type])
                        : TryWorkType(simulation, colonist, _rules.WorkJobs[type])))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The colonist's priority for a work type, or never for one it does not do at its stage of life.</summary>
    private int PriorityOf(Simulation simulation, EntityId colonist, LifeStageDef? stage, WorkTypeDef workType) =>
        LifeRules.DoesWork(stage, workType)
            ? WorkPriorityRules.Of(simulation.World, colonist, workType.Id, _rules.Ai.DefaultWorkPriority)
            : WorkPriorityLevels.Never;

    private bool TryWorkType(Simulation simulation, EntityId colonist, IReadOnlyList<JobKind> jobs)
    {
        _candidates.Clear();
        _cellJobs.Clear();

        // The harvest comes before sowing: what is ripe is reaped first (see "Cosechar" in docs/gdd/agricultura.md).
        if (jobs.Contains(JobKind.Reap))
        {
            AddWork(simulation, JobKind.Reap);
            if (TryCandidates(simulation, colonist))
            {
                return true;
            }

            _candidates.Clear();
        }

        foreach (var kind in jobs)
        {
            if (kind == JobKind.Reap)
            {
                continue;
            }

            if (IsCellOrder(kind))
            {
                _cellJobs.Add(kind);
            }
            else
            {
                AddWork(simulation, kind);
            }
        }

        AddOrderedCells(simulation, _cellJobs);
        return TryCandidates(simulation, colonist);
    }

    private void AddWork(Simulation simulation, JobKind kind)
    {
        var world = simulation.World;
        var map = simulation.Map;
        switch (kind)
        {
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
                    var stack = world.Get<ItemStack>(pile);
                    if (!storage.IsStored(simulation, pile) && storage.AnyRoomFor(simulation, stack.Def, stack.Quality))
                    {
                        AddEntity(simulation, kind, pile, capacity: 1);
                    }
                }

                break;
            case JobKind.Deliver:
                // Piles of what some blueprint lacks, stored or not; the blueprint is chosen with the pile.
                FindSupplyNeeded(simulation);
                if (_neededItems.Count == 0)
                {
                    break;
                }

                world.Query<ItemStack, Position>(_entities);
                foreach (var pile in _entities)
                {
                    if (_neededItems.Contains(world.Get<ItemStack>(pile).Def))
                    {
                        AddEntity(simulation, kind, pile, capacity: 1);
                    }
                }

                break;
            case JobKind.FeedBaby:
                AddHungryBabies(simulation);
                break;
            case JobKind.Sow:
                AddSowableCells(simulation);
                break;
            case JobKind.Reap:
                AddRipeCrops(simulation);
                break;
            case JobKind.Build:
                world.Query<Blueprint, Position>(_entities);
                foreach (var blueprint in _entities)
                {
                    if (Construction.Needs(simulation, world.Get<Blueprint>(blueprint)).Missing == 0)
                    {
                        AddEntity(simulation, kind, blueprint, capacity: 1);
                    }
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    /// <summary>The cells with orders of the given jobs that nobody has taken.</summary>
    private void AddOrderedCells(Simulation simulation, List<JobKind> jobs)
    {
        if (jobs.Count == 0)
        {
            return;
        }

        var map = simulation.Map;
        RefreshWorkableCells(simulation);
        foreach (var candidate in _workableCells)
        {
            var wanted = jobs.Contains(candidate.Kind) && !IsReserved(map, candidate.Cell)
                         && !(OtherCellOf(map, candidate.Kind, candidate.Cell) is { } other && IsReserved(map, other));
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
            AddWorkableCell(map, cell, JobKind.Mine, (flags & TileFlags.MineDesignation) != 0 && Designations.CanMine(map, cell));
            AddWorkableCell(map, cell, JobKind.DigStairs, (flags & TileFlags.StairsDesignation) != 0 && Designations.CanDigStairs(map, cell));
            AddWorkableCell(map, cell, JobKind.Channel, (flags & TileFlags.ChannelDesignation) != 0 && Construction.CanChannel(map, cell));
            AddWorkableCell(
                map, cell, JobKind.Deconstruct, (flags & TileFlags.DeconstructDesignation) != 0 && Construction.CanDeconstruct(simulation, cell));
        }

        _workableCellsVersion = version;
    }

    private void AddWorkableCell(GameMap map, TileCoord cell, JobKind kind, bool ordered)
    {
        if (!ordered)
        {
            return;
        }

        WorkSites.Find(map, kind, cell, _sites);
        if (_sites.Count > 0)
        {
            _workableCells.Add(new Candidate(kind, EntityId.None, cell));
        }
    }

    /// <summary>The blueprints that lack materials and nobody supplies, and the items they lack.</summary>
    private void FindSupplyNeeded(Simulation simulation)
    {
        var world = simulation.World;
        _supplyNeeded.Clear();
        _neededItems.Clear();
        world.Query<Blueprint, Position>(_entities);
        foreach (var blueprint in _entities)
        {
            var (item, missing) = Construction.Needs(simulation, world.Get<Blueprint>(blueprint));
            if (missing > 0 && _entityUses.GetValueOrDefault(blueprint) == 0)
            {
                _supplyNeeded.Add((blueprint, item));
                _neededItems.Add(item);
            }
        }
    }

    private void AddFoodPiles(Simulation simulation)
    {
        var world = simulation.World;
        world.Query<ItemStack, Position>(_entities);
        foreach (var pile in _entities)
        {
            var stack = world.Get<ItemStack>(pile);
            if (simulation.Content.Get<ItemDef>(stack.Def).Kind == ItemKind.Food && !_hauledPiles.Contains(pile))
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
    private bool TryCandidates(Simulation simulation, EntityId colonist, bool forFood = false, bool initiative = false, bool eatChoice = false)
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

            FindSites(map, candidate.Kind, candidate.Cell);
            if (AnyInRegion(_sites, connectivity, region))
            {
                reachable.Add(new Reach(candidate, PathFinder.Estimate(from, candidate.Cell), map.Size.IndexOf(candidate.Cell.X, candidate.Cell.Y, candidate.Cell.Z)));
            }
        }

        if (eatChoice)
        {
            RankFoodChoice(simulation, reachable);
        }

        // Nearest first; ties by cell, then by entity, so the choice never depends on chance. A
        // colonist who chooses what to eat ranks first (see RankFoodChoice).
        reachable.Sort(CompareReach);

        // Only searches for a path count as attempts: a pile with nowhere to go is skipped for free.
        _noDestination.Clear();
        _noRecipient.Clear();
        _food = null;
        for (int i = 0, attempts = 0; i < reachable.Count && attempts < PathAttempts; i++)
        {
            var candidate = reachable[i].Candidate;
            var destination = default(TileCoord);
            var recipient = EntityId.None;
            if ((candidate.Kind == JobKind.Haul && !TryDestination(simulation, person, candidate, region, out destination))
                || (candidate.Kind == JobKind.Deliver && !TryRecipient(simulation, person, candidate, region, out recipient, out destination)))
            {
                continue;
            }

            // A feeder walks to the food first, then to the baby.
            var (target, cell, count) = (candidate.Target, candidate.Cell, 0);
            if (candidate.Kind == JobKind.FeedBaby)
            {
                if (ParentWillFeed(simulation, colonist, candidate.Target)
                    || !TryFood(simulation, colonist, candidate, region, out target, out cell, out var free))
                {
                    continue;
                }

                // It holds the units the baby lacks, of those nobody else holds.
                recipient = candidate.Target;
                var nutrition = simulation.Content.Get<ItemDef>(simulation.World.Get<ItemStack>(target).Def).Nutrition;
                count = Math.Clamp(Hunger.UnitsToFeed(simulation, recipient, nutrition), 1, free);
            }

            // A sower takes the crop of the zone as it is now, a reaper the crop it reaps: the job tells them.
            string? crop = null;
            if (candidate.Kind == JobKind.Sow)
            {
                if (!_sowByCell.TryGetValue(reachable[i].CellIndex, out var sowing))
                {
                    continue;
                }

                (recipient, crop) = sowing;
            }
            else if (candidate.Kind == JobKind.Reap)
            {
                crop = simulation.World.Get<Crop>(candidate.Target).Def;
            }

            attempts++;

            FindSites(map, candidate.Kind, cell);
            KeepInRegion(_sites, connectivity, region);
            if (_paths.TryFind(map, from, _sites, _path))
            {
                var job = new Job
                {
                    Kind = candidate.Kind,
                    Target = target,
                    Cell = cell,
                    ForFood = forFood,
                    Initiative = initiative,
                    Destination = destination,
                    Recipient = recipient,
                    Count = count,
                    Crop = crop,
                };
                Assign(simulation, colonist, job, _path);
                if (job.Kind == JobKind.FeedBaby)
                {
                    WaitForFood(simulation, recipient);
                }

                return true;
            }

            // What it could not reach: for a feeder, the food, and it tries the same baby again with
            // another pile, within the attempts.
            person.Unreachable.Add(new IgnoredTarget
            {
                Entity = target,
                Cell = cell,
                UntilTick = simulation.CurrentTick + _rules.Ai.UnreachableCooldownTicks,
            });
            if (candidate.Kind == JobKind.FeedBaby)
            {
                i--;
            }
        }

        return false;
    }

    private StorageIndex Storage(Simulation simulation) => _storage.Fresh(simulation);

    /// <summary>
    /// The stockpile cell nearest the pile, in the colonist's region, that accepts its item, has
    /// room, nobody else carries to and the colonist has not failed to reach. Ties go to the lowest
    /// cell index. Whether an item has somewhere to go does not depend on which pile it is in (the
    /// pile's own cell has no room for it), so a failure is remembered for the rest of the search.
    /// </summary>
    private bool TryDestination(Simulation simulation, Colonist person, Candidate candidate, int region, out TileCoord destination)
    {
        var storage = Storage(simulation);
        var map = simulation.Map;
        var connectivity = simulation.Connectivity;
        var stack = simulation.World.Get<ItemStack>(candidate.Target);
        var item = stack.Def;
        var kind = (item, stack.Quality);
        destination = default;
        if (_noDestination.Contains(kind))
        {
            return false;
        }

        var best = int.MaxValue;
        var bestIndex = int.MaxValue;
        foreach (var zone in storage.Zones)
        {
            foreach (var cell in simulation.World.Get<Stockpile>(zone).Cells)
            {
                if (cell == candidate.Cell || IsReserved(map, cell) || storage.SpaceFor(simulation, item, kind.Quality, cell) == 0
                    || connectivity.RegionAt(cell) != region || IsIgnored(person, EntityId.None, cell))
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

        if (best == int.MaxValue)
        {
            _noDestination.Add(kind);
            return false;
        }

        return true;
    }

    /// <summary>
    /// The blueprint nearest the pile that lacks its item, that nobody supplies, the colonist has
    /// not failed to reach and that can be worked on from the colonist's region. Ties go to the
    /// lowest id. As with <see cref="TryDestination"/>, a failure holds for every pile of the item.
    /// </summary>
    private bool TryRecipient(Simulation simulation, Colonist person, Candidate candidate, int region, out EntityId recipient, out TileCoord destination)
    {
        var world = simulation.World;
        var item = world.Get<ItemStack>(candidate.Target).Def;
        recipient = EntityId.None;
        destination = default;
        if (_noRecipient.Contains(item))
        {
            return false;
        }

        var best = int.MaxValue;
        foreach (var (blueprint, needed) in _supplyNeeded)
        {
            if (needed != item || IsIgnored(person, blueprint, default))
            {
                continue;
            }

            var cell = world.Get<Position>(blueprint).Cell;
            var distance = PathFinder.Estimate(candidate.Cell, cell);
            if (distance >= best)
            {
                continue;
            }

            WorkSites.Find(simulation.Map, JobKind.Build, cell, _recipientSites);
            if (AnyInRegion(_recipientSites, simulation.Connectivity, region))
            {
                (best, recipient, destination) = (distance, blueprint, cell);
            }
        }

        if (recipient.IsNone)
        {
            _noRecipient.Add(item);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Walks to a random place around, and waits there; with free will, away from rivals near it. A
    /// baby crawls near its mother instead, and waits where it is while someone brings it food.
    /// </summary>
    private void Wander(Simulation simulation, EntityId colonist)
    {
        var map = simulation.Map;
        var connectivity = simulation.Connectivity;
        var from = simulation.World.Get<Position>(colonist).Cell;
        var region = connectivity.RegionAt(from);
        var random = simulation.Random.Get("colony.decision");
        var radius = _rules.Ai.WanderRadius;
        var crawl = LifeRules.Stage(simulation, colonist)?.CrawlRadius;
        if (crawl is null && MoodRules.Isolates(simulation, colonist) && TryWanderAway(simulation, colonist, from, region, random, radius, OthersOf(simulation, colonist)))
        {
            return;
        }

        if (crawl is null && FindRivalsNear(simulation, colonist, from) && TryWanderAway(simulation, colonist, from, region, random, radius, _avoided))
        {
            return;
        }

        var centre = from;
        if (crawl is not null)
        {
            if (_entityUses.GetValueOrDefault(colonist) > 0)
            {
                Assign(simulation, colonist, new Job { Kind = JobKind.Wander, Cell = from }, []);
                return;
            }

            (centre, radius) = (CrawlCentre(simulation, colonist, from, crawl.Value), crawl.Value);
        }

        for (var attempt = 0; attempt < WanderAttempts; attempt++)
        {
            var x = centre.X + random.NextInt(-radius, radius + 1);
            var y = centre.Y + random.NextInt(-radius, radius + 1);
            foreach (var dz in LevelOffsets)
            {
                var cell = new TileCoord(x, y, centre.Z + dz);
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

    /// <summary>The cells of all colonists but one, into <see cref="_avoided"/>.</summary>
    private List<TileCoord> OthersOf(Simulation simulation, EntityId colonist)
    {
        _avoided.Clear();
        foreach (var other in _colonists)
        {
            if (other != colonist)
            {
                _avoided.Add(simulation.World.Get<Position>(other).Cell);
            }
        }

        return _avoided;
    }

    /// <summary>
    /// Wanders to the place, among those drawn, farthest from the nearest of <paramref name="avoided"/>:
    /// the cells of all other colonists for one who isolates itself (see "Crisis" in
    /// <c>docs/gdd/vida-y-generaciones.md</c>), those of its rivals for one with free will.
    /// </summary>
    private bool TryWanderAway(Simulation simulation, EntityId colonist, TileCoord from, int region, Pcg32 random, int radius, List<TileCoord> avoided)
    {
        var map = simulation.Map;
        var connectivity = simulation.Connectivity;
        TileCoord? best = null;
        var bestDistance = -1;
        for (var attempt = 0; attempt < WanderAttempts; attempt++)
        {
            var cell = new TileCoord(from.X + random.NextInt(-radius, radius + 1), from.Y + random.NextInt(-radius, radius + 1), from.Z);
            if (cell == from || !map.IsStandable(cell) || connectivity.RegionAt(cell) != region)
            {
                continue;
            }

            var nearest = int.MaxValue;
            foreach (var other in avoided)
            {
                nearest = Math.Min(nearest, PathFinder.Estimate(cell, other));
            }

            if (nearest > bestDistance)
            {
                (best, bestDistance) = (cell, nearest);
            }
        }

        _goal.Clear();
        if (best is not { } goal)
        {
            return false;
        }

        _goal.Add(goal);
        if (!_paths.TryFind(map, from, _goal, _path))
        {
            return false;
        }

        Assign(simulation, colonist, new Job { Kind = JobKind.Wander, Cell = goal }, _path);
        return true;
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
        Reserve(simulation, job);
    }

    private readonly record struct Candidate(JobKind Kind, EntityId Target, TileCoord Cell);

    /// <summary>A candidate the colonist can reach, with its distance and how it ranks when it chooses what to eat.</summary>
    private readonly record struct Reach(Candidate Candidate, int Distance, int CellIndex, int Group = 0, int NotMeal = 0, int Freshness = 0);
}
