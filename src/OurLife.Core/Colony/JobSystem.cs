using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Pathfinding;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// Carries out the colonists' jobs: checks that the target still makes sense, waits for the
/// colonist to arrive, does the work tick by tick and applies the result (see the table of jobs
/// in <c>docs/gdd/colonos-e-ia.md</c>). A job that no longer makes sense is dropped, and an order
/// that lost its meaning is cleared.
/// </summary>
public sealed partial class JobSystem : ISimSystem
{
    private const string RandomStream = "colony.jobs";

    private readonly ColonyRules _rules;
    private readonly PathFinder _paths = new();
    private readonly List<EntityId> _colonists = [];
    private readonly List<TileCoord> _path = [];
    private readonly List<TileCoord> _goal = [];
    private readonly List<EntityId> _blueprints = [];
    private readonly List<EntityId> _occupants = [];
    private int? _blueprintsCheckedAt;

    // The simulation of the tick in progress, for the predicate that tells which piles are in use.
    private Simulation? _current;
    private Predicate<EntityId>? _pileInUse;
    private readonly List<EntityId> _pileUsers = [];
    private readonly List<EntityId> _piles = [];
    private readonly List<EntityId> _workshops = [];

    // Whether this tick falls in the night of the AiConfig; set at the start of each tick.
    private bool _night;

    // What the stockpiles hold, shared with the other colony system and rebuilt once per tick.
    private readonly StorageIndex _storage;
    private readonly List<EntityId> _carriers = [];

    public JobSystem(ColonyRules rules)
        : this(rules, new StorageIndex())
    {
    }

    /// <summary>With the stockpile index the other colony system shares, so it is built once per tick.</summary>
    internal JobSystem(ColonyRules rules, StorageIndex storage)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(storage);
        _rules = rules;
        _storage = storage;
    }

    /// <summary>Stops a colonist's job and its walk; the colonist decides again on the next tick.</summary>
    public static void Drop(World world, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(world);
        world.Remove<Job>(colonist);
        if (world.TryGet<Movement>(colonist, out var movement))
        {
            movement.Stop();
        }
    }

    public void Tick(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        _night = _rules.Ai.IsNight(simulation.CurrentDate.Hour);
        _current = simulation;
        var world = simulation.World;
        RemoveSenselessBlueprints(simulation);
        DropLoadsOfOtherJobs(simulation);
        if (simulation.CurrentTick % simulation.Time.TicksPerHour == 0)
        {
            ReleaseStaleInputs(simulation);
        }

        world.Query<Job, Position>(_colonists);
        foreach (var colonist in _colonists)
        {
            var job = world.Get<Job>(colonist);
            if (!StillValid(simulation, colonist, job))
            {
                GiveUpIfLeft(simulation, colonist, job);
                Drop(world, colonist);
                continue;
            }

            // Carrying for the player is the work of hauling, of supplying a building and of feeding a baby.
            var workType = _rules.WorkTypeOf(job);
            if (workType is not null && job.Stage == JobStage.Carrying && job.Kind != JobKind.Craft)
            {
                _rules.Skills.Gain(simulation, colonist, workType, 1);
            }

            if (world.Get<Movement>(colonist).IsMoving)
            {
                continue;
            }

            if (job.Stage == JobStage.Moving)
            {
                if (!WorkSites.IsSite(simulation.Map, SiteKind(job), job.Cell, world.Get<Position>(colonist).Cell))
                {
                    Drop(world, colonist);
                    continue;
                }

                // A visitor whose company is going elsewhere does not wait where nobody will be: it
                // stays on its way, and the decisions of the next tick follow the other.
                if (job.Kind == JobKind.Visit && !FreeWillRules.WillBeNear(simulation, colonist, job.Target))
                {
                    continue;
                }

                Start(simulation, job);
            }

            var progress = job.Progress;
            var finished = Work(simulation, colonist, job, workType);

            // A tick of work that advanced it teaches, once (a tick carrying taught already);
            // waiting for a cell to clear does not.
            if (workType is not null && job.Progress > progress && job.Stage != JobStage.Carrying)
            {
                _rules.Skills.Gain(simulation, colonist, workType, 1);
                if (_rules.Knowledge is { } knowledge && job.Kind != JobKind.Research && _rules.PracticedBy(workType.Id) is { Count: > 0 } practiced)
                {
                    ResearchRules.AddPractice(simulation, practiced, knowledge);
                }
            }

            if (finished)
            {
                if (job.Kind == JobKind.Sleep)
                {
                    WakeUp(simulation, colonist, job);
                }

                Drop(world, colonist);
            }
        }
    }

    /// <summary>
    /// A colonist that slept its fill thinks of where it slept, and after nights in a row on the
    /// ground may wish for a bed (see "Pensamientos y ánimo" and "Deseos" in <c>docs/gdd/vida-y-generaciones.md</c>).
    /// </summary>
    private static void WakeUp(Simulation simulation, EntityId colonist, Job job)
    {
        var world = simulation.World;
        var inBed = !job.Target.IsNone && world.IsAlive(job.Target) && world.Has<Bed>(job.Target)
                    && world.Get<Position>(job.Target).Cell == world.Get<Position>(colonist).Cell;
        MoodRules.Feel(simulation, colonist, config => inBed ? config.SleptInBed : config.SleptOnFloor);
        if (MoodRules.ConfigOf(simulation.Content) is null)
        {
            return;
        }

        var mood = MoodRules.MoodOf(world, colonist);
        mood.FloorNights = inBed ? 0 : mood.FloorNights + 1;
        var nights = mood.FloorNights;
        if (!inBed)
        {
            DesireRules.Consider(simulation, colonist, DesireKind.OwnBed, eligible: desire => nights >= desire.Nights);
        }
    }

    /// <summary>
    /// Blueprints whose place changed so that their shape no longer fits go away, with what they
    /// had (see <see cref="Construction.RemoveSenseless"/>). Only the terrain can make them
    /// senseless, so they are checked only after it changed.
    /// </summary>
    private void RemoveSenselessBlueprints(Simulation simulation)
    {
        var version = simulation.Map.NavigationVersion;
        if (_blueprintsCheckedAt == version)
        {
            return;
        }

        Construction.RemoveSenseless(simulation, _blueprints, _rules.Ai.DeconstructRefundPercent);

        // A chest that went away was a stockpile, and what was dropped may lie on one.
        _storage.Invalidate();
        _blueprintsCheckedAt = simulation.Map.NavigationVersion;
    }

    /// <summary>
    /// A colonist who carries something but is no longer taking it to a stockpile or a blueprint,
    /// because its job ended or something more urgent came, leaves it where it stands.
    /// </summary>
    private void DropLoadsOfOtherJobs(Simulation simulation)
    {
        var world = simulation.World;
        world.Query<Carried, Position>(_carriers);
        foreach (var colonist in _carriers)
        {
            if (world.TryGet<Job>(colonist, out var job) && job.Kind is JobKind.Haul or JobKind.Deliver or JobKind.FeedBaby or JobKind.Craft
                && job.Stage == JobStage.Carrying)
            {
                continue;
            }

            var carried = world.Get<Carried>(colonist);
            world.Remove<Carried>(colonist);
            ItemPiles.Drop(simulation, carried.Def, carried.Count, world.Get<Position>(colonist).Cell, carried.Quality, carried.Freshness);
            _storage.Invalidate();
            simulation.Events.Emit(new EntityChangedEvent(colonist));
        }
    }

    private StorageIndex Storage(Simulation simulation) => _storage.Fresh(simulation);

    private bool StillValid(Simulation simulation, EntityId colonist, Job job)
    {
        var world = simulation.World;
        var map = simulation.Map;
        switch (job.Kind)
        {
            case JobKind.Mine:
                return StillOrdered(simulation, job.Cell, TileFlags.MineDesignation, map.ShapeAt(job.Cell) == TileShape.Wall);
            case JobKind.DigStairs:
                return StillOrdered(simulation, job.Cell, TileFlags.StairsDesignation, Designations.CanDigStairs(map, job.Cell));
            // Work on its own needs no order, but free will: turning it off ends it.
            case JobKind.Chop:
                return world.IsAlive(job.Target) && (job.Initiative ? simulation.FreeWill : world.Has<ChopOrder>(job.Target));
            case JobKind.Harvest:
                return world.IsAlive(job.Target) && world.Has<Plant>(job.Target) && !world.Has<Harvested>(job.Target)
                       && (job.ForFood || (job.Initiative ? simulation.FreeWill : world.Has<HarvestOrder>(job.Target)));
            case JobKind.Eat:
                return world.IsAlive(job.Target) && world.TryGet<ItemStack>(job.Target, out var stack) && stack.Count > 0;
            case JobKind.Haul:
                // On the way to the pile, it must still be there and loose; once carried, the
                // destination is checked on arrival.
                return job.Stage == JobStage.Carrying
                       || (world.IsAlive(job.Target) && world.Has<ItemStack>(job.Target) && !Storage(simulation).IsStored(simulation, job.Target));
            case JobKind.Deliver:
                // The blueprint must be there and, before lifting, still lack something; and so must the pile.
                return world.IsAlive(job.Recipient) && world.TryGet<Blueprint>(job.Recipient, out var plan)
                       && (job.Stage == JobStage.Carrying
                           || (world.IsAlive(job.Target) && world.Has<ItemStack>(job.Target) && Construction.Needs(simulation, plan).Missing > 0));
            case JobKind.Build:
                return world.IsAlive(job.Target) && world.TryGet<Blueprint>(job.Target, out var built)
                       && Construction.Needs(simulation, built).Missing == 0;
            case JobKind.FeedBaby:
                // The baby must still be there and, before lifting, so must the food.
                return Relationships.IsLivingColonist(world, job.Recipient)
                       && (job.Stage == JobStage.Carrying || (world.IsAlive(job.Target) && world.TryGet<ItemStack>(job.Target, out var food) && food.Count > 0));
            // With free will, nobody stays by a rival.
            case JobKind.Play:
                return Relationships.IsLivingColonist(world, job.Target) && !(FreeWillRules.IsActive(simulation) && Relationships.IsRival(simulation, colonist, job.Target));
            // Seeking company is free will too. Both must still be up for company and, once the
            // visitor waits, the other must not be heading off out of reach.
            case JobKind.Visit:
                if (!FreeWillRules.StillWorthVisiting(simulation, colonist, job.Target))
                {
                    return false;
                }

                return job.Stage != JobStage.Working || FreeWillRules.WillBeNear(simulation, colonist, job.Target);
            case JobKind.Craft:
                return IsCraftValid(simulation, job);
            case JobKind.Research:
                return world.IsAlive(job.Recipient) && world.Has<Workshop>(job.Recipient) && ResearchRules.Current(simulation) is not null;
            case JobKind.Deconstruct:
                return StillOrdered(simulation, job.Cell, TileFlags.DeconstructDesignation, Construction.CanDeconstruct(simulation, job.Cell));
            case JobKind.Channel:
                return StillOrdered(simulation, job.Cell, TileFlags.ChannelDesignation, Construction.CanChannel(map, job.Cell));
            default:
                return true;
        }
    }

    /// <summary>
    /// A visitor whose company left while it waited, before they met, gives up on it, as when it
    /// stops following it (see "Vida social con intención" in <c>docs/gdd/libre-albedrio.md</c>).
    /// </summary>
    private void GiveUpIfLeft(Simulation simulation, EntityId colonist, Job job)
    {
        if (job.Kind == JobKind.Visit && job.Stage == JobStage.Working && FreeWillRules.StillWorthVisiting(simulation, colonist, job.Target)
            && !SocialSystem.MetLately(simulation, colonist, job.Target, SocialSystem.PairCooldownTicks(simulation)))
        {
            FreeWillRules.LeaveAlone(simulation, colonist, job.Target, _rules.Ai.UnreachableCooldownTicks);
        }
    }

    /// <summary>True if the cell keeps its order and the order still makes sense; a senseless order is cleared.</summary>
    private static bool StillOrdered(Simulation simulation, TileCoord cell, TileFlags order, bool makesSense)
    {
        var map = simulation.Map;
        var flags = map.FlagsAt(cell);
        if ((flags & order) == 0)
        {
            return false;
        }

        if (!makesSense)
        {
            map.SetFlags(cell.X, cell.Y, cell.Z, flags & ~order);
            simulation.Events.Emit(new DesignationsChangedEvent(cell, cell));
        }

        return makesSense;
    }

    private void Start(Simulation simulation, Job job)
    {
        job.Stage = JobStage.Working;
        job.Progress = 0;
        if (JobKinds.IsLeisure(job.Kind))
        {
            var random = simulation.Random.Get(RandomStream);
            job.UntilTick = simulation.CurrentTick + random.NextInt(_rules.Ai.WanderWaitMin, _rules.Ai.WanderWaitMax + 1);
        }
    }

    /// <summary>
    /// Does one tick of work; true when the job is finished. Work for the player lasts longer for
    /// a colonist who works slower (see <c>docs/gdd/vida-y-generaciones.md</c>); gathering to eat
    /// and eating do not.
    /// </summary>
    private bool Work(Simulation simulation, EntityId colonist, Job job, WorkTypeDef? workType)
    {
        var speed = IsWork(job) ? WorkSpeed.PercentOf(simulation, colonist, workType, _rules.Skills) : 100;
        switch (job.Kind)
        {
            case JobKind.Mine:
                if (!Advance(job, WorkTicksOf(simulation.Map.MaterialAt(job.Cell)), speed))
                {
                    return false;
                }

                Mine(simulation, job.Cell);
                return true;
            case JobKind.DigStairs:
                var below = new TileCoord(job.Cell.X, job.Cell.Y, job.Cell.Z - 1);
                if (!Advance(job, WorkTicksOf(simulation.Map.MaterialAt(below)), speed))
                {
                    return false;
                }

                DigStairs(simulation, job.Cell);
                return true;
            case JobKind.Chop:
                if (!Advance(job, PlantOf(simulation, job.Target).Chop!.WorkTicks, speed))
                {
                    return false;
                }

                Chop(simulation, job.Target);
                return true;
            case JobKind.Harvest:
                if (!Advance(job, PlantOf(simulation, job.Target).Harvest!.WorkTicks, speed))
                {
                    return false;
                }

                var food = Harvest(simulation, job.Target, simulation.World.Get<Position>(colonist).Cell);
                return !(job.ForFood && TurnIntoMeal(simulation, colonist, job, food));
            case JobKind.Eat:
                return Eat(simulation, colonist, job);
            case JobKind.Sleep:
                // At night a rested colonist sleeps on until morning.
                return NeedsFull(simulation, colonist, NeedSatisfaction.Sleeping) && !_night;
            case JobKind.Wander:
            case JobKind.Play:
            case JobKind.Visit:
                return simulation.CurrentTick >= job.UntilTick;
            case JobKind.FeedBaby:
                return job.Stage == JobStage.Carrying ? Feed(simulation, colonist, job, speed) : PickUpFood(simulation, colonist, job);
            case JobKind.Haul:
                return job.Stage == JobStage.Carrying ? Deliver(simulation, colonist, job) : PickUp(simulation, colonist, job);
            case JobKind.Deliver:
                return job.Stage == JobStage.Carrying ? Supply(simulation, colonist, job) : PickUp(simulation, colonist, job);
            case JobKind.Build:
                return Build(simulation, colonist, job, speed);
            case JobKind.Craft:
                return Craft(simulation, colonist, job, workType, speed);
            case JobKind.Research:
                return Research(simulation, job, speed);
            case JobKind.Deconstruct:
                return Deconstruct(simulation, colonist, job, speed);
            case JobKind.Channel:
                if (AnyColonistOn(simulation, job.Cell))
                {
                    return false;
                }

                var under = simulation.Map.ShapeAt(job.Cell with { Z = job.Cell.Z - 1 }) == TileShape.Wall ? job.Cell with { Z = job.Cell.Z - 1 } : job.Cell;
                if (!Advance(job, WorkTicksOf(simulation.Map.MaterialAt(under)), speed))
                {
                    return false;
                }

                DigChannel(simulation, job.Cell, simulation.World.Get<Position>(colonist).Cell);
                _storage.Invalidate();
                return true;
            default:
                throw new InvalidOperationException($"Unknown job {job.Kind}.");
        }
    }

    /// <summary>
    /// Builds a blueprint with all its materials. A wall does not rise over a colonist: the work
    /// waits while someone stands on the cell.
    /// </summary>
    private bool Build(Simulation simulation, EntityId colonist, Job job, int speed)
    {
        var world = simulation.World;
        var buildable = simulation.Content.Get<BuildableDef>(world.Get<Blueprint>(job.Target).Buildable);
        if (buildable.Shape == BuildShape.Wall && AnyColonistOn(simulation, world.Get<Position>(job.Target).Cell))
        {
            return false;
        }

        if (!Advance(job, buildable.WorkTicks, speed))
        {
            return false;
        }

        Construction.Finish(simulation, job.Target, world.Get<Position>(colonist).Cell, colonist);
        _storage.Invalidate();
        return true;
    }

    /// <summary>
    /// Takes down what was built, in half the time it took to build. A floor, or the upper half of
    /// stairs, that turns into air waits while someone stands on it.
    /// </summary>
    private bool Deconstruct(Simulation simulation, EntityId colonist, Job job, int speed)
    {
        foreach (var open in Construction.CellsLeftOpen(simulation, job.Cell))
        {
            if (AnyColonistOn(simulation, open))
            {
                return false;
            }
        }

        var buildable = Construction.BuildableToTakeDown(simulation, job.Cell);
        if (!Advance(job, Math.Max(1, buildable.WorkTicks / 2), speed))
        {
            return false;
        }

        Construction.TakeDown(simulation, job.Cell, simulation.World.Get<Position>(colonist).Cell, _rules.Ai.DeconstructRefundPercent);
        _storage.Invalidate();
        return true;
    }

    /// <summary>
    /// Opens the floor to the level below: the cell turns into air and a wall below into a ramp,
    /// which may leave what digging it would. Piles on the cell fall below if there is ground to
    /// land on, or else on the worker's cell.
    /// </summary>
    private static void DigChannel(Simulation simulation, TileCoord cell, TileCoord worker)
    {
        var map = simulation.Map;
        var below = cell with { Z = cell.Z - 1 };
        const TileFlags cleared = TileFlags.ChannelDesignation | TileFlags.StairsDesignation | TileFlags.Grass;
        map.SetTile(cell.X, cell.Y, cell.Z, TileShape.Empty, GameMap.NoMaterial, map.FlagsAt(cell) & ~cleared);
        var material = map.MaterialAt(below);
        var dugWall = map.ShapeAt(below) == TileShape.Wall;
        if (dugWall)
        {
            map.SetTile(below.X, below.Y, below.Z, TileShape.Ramp, map.MaterialIndexAt(below.X, below.Y, below.Z), map.FlagsAt(below) & ~TileFlags.MineDesignation);
        }

        simulation.Events.Emit(new DesignationsChangedEvent(below, cell));
        Construction.MovePiles(simulation, cell, map.IsStandable(below) ? below : worker);
        if (dugWall)
        {
            DropMinedItem(simulation, material, below);
        }
    }

    /// <summary>True if some colonist stands on the cell.</summary>
    private bool AnyColonistOn(Simulation simulation, TileCoord cell)
    {
        var world = simulation.World;
        world.Query<Colonist, Position>(_occupants);
        foreach (var colonist in _occupants)
        {
            if (world.Get<Position>(colonist).Cell == cell)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Advance(Job job, int workTicks)
    {
        job.Progress++;
        return job.Progress >= workTicks;
    }

    /// <summary>Whether the job is work for the player, which goes at the speed of the worker.</summary>
    private static bool IsWork(Job job) => job.Kind switch
    {
        JobKind.Mine or JobKind.DigStairs or JobKind.Channel or JobKind.Chop or JobKind.Build or JobKind.Deconstruct or JobKind.FeedBaby
            or JobKind.Craft or JobKind.Research => true,
        JobKind.Harvest => !job.ForFood,
        _ => false,
    };

    /// <summary>The same, for work that lasts <c>ticks × 100 / speed</c> ticks, at least one.</summary>
    private static bool Advance(Job job, int workTicks, int speedPercent) =>
        Advance(job, (int)Math.Max(1, (long)workTicks * 100 / speedPercent));

    /// <summary>Ticks to dig a material; a cell without one, if it ever happens, takes one tick.</summary>
    private static int WorkTicksOf(MaterialDef? material) => material?.MineWorkTicks ?? 1;

    private static PlantDef PlantOf(Simulation simulation, EntityId plant) =>
        simulation.Content.Get<PlantDef>(simulation.World.Get<Plant>(plant).Def);

    private static void Mine(Simulation simulation, TileCoord cell)
    {
        var map = simulation.Map;
        var material = map.MaterialAt(cell);
        map.SetTile(cell.X, cell.Y, cell.Z, TileShape.Floor, map.MaterialIndexAt(cell.X, cell.Y, cell.Z), map.FlagsAt(cell) & ~TileFlags.MineDesignation);
        simulation.Events.Emit(new DesignationsChangedEvent(cell, cell));
        DropMinedItem(simulation, material, cell);
    }

    private static void DigStairs(Simulation simulation, TileCoord cell)
    {
        var map = simulation.Map;
        const TileFlags cleared = TileFlags.StairsDesignation | TileFlags.MineDesignation | TileFlags.Grass;
        var top = map.ShapeAt(cell) == TileShape.StairUp ? TileShape.StairUpDown : TileShape.StairDown;
        map.SetTile(cell.X, cell.Y, cell.Z, top, map.MaterialIndexAt(cell.X, cell.Y, cell.Z), map.FlagsAt(cell) & ~cleared);

        // The cell below is dug out, but keeps its own order of stairs, so a stairwell goes on.
        const TileFlags clearedBelow = TileFlags.MineDesignation | TileFlags.Grass;

        var below = new TileCoord(cell.X, cell.Y, cell.Z - 1);
        var belowShape = map.ShapeAt(below);
        var material = map.MaterialAt(below);
        var bottom = belowShape == TileShape.StairDown ? TileShape.StairUpDown : TileShape.StairUp;
        map.SetTile(below.X, below.Y, below.Z, bottom, map.MaterialIndexAt(below.X, below.Y, below.Z), map.FlagsAt(below) & ~clearedBelow);
        simulation.Events.Emit(new DesignationsChangedEvent(below, cell));
        if (belowShape == TileShape.Wall)
        {
            DropMinedItem(simulation, material, below);
        }
    }

    private static void DropMinedItem(Simulation simulation, MaterialDef? material, TileCoord cell)
    {
        if (material?.MinedItem is { } item && simulation.Random.Get(RandomStream).Chance(material.MinedItemChance))
        {
            ItemPiles.Drop(simulation, item, 1, cell);
        }
    }

    private static void Chop(Simulation simulation, EntityId plant)
    {
        var world = simulation.World;
        var def = PlantOf(simulation, plant);
        var cell = world.Get<Position>(plant).Cell;
        if (def.BlocksMovement)
        {
            simulation.Map.SetFlags(cell.X, cell.Y, cell.Z, simulation.Map.FlagsAt(cell) & ~TileFlags.Blocked);
        }

        simulation.Events.Emit(new EntityRemovedEvent(plant));
        world.DestroyEntity(plant);
        ItemPiles.Drop(simulation, def.Chop!.Item, def.Chop.Count, cell);
    }

    /// <summary>
    /// Gathers from a plant and returns the pile that got what it gave. It falls on the plant's
    /// cell, or on the gatherer's if a plant that blocks the way, like a fruit tree, fills its own.
    /// </summary>
    private static EntityId Harvest(Simulation simulation, EntityId plant, TileCoord gatherer)
    {
        var world = simulation.World;
        var def = PlantOf(simulation, plant);
        world.Remove<HarvestOrder>(plant);
        world.Add(plant, new Harvested { RegrowsAtTick = simulation.CurrentTick + ((long)def.RegrowDays * simulation.Time.TicksPerDay) });
        simulation.Events.Emit(new EntityChangedEvent(plant));
        var cell = world.Get<Position>(plant).Cell;
        return ItemPiles.Drop(simulation, def.Harvest!.Item, def.Harvest.Count, simulation.Map.IsStandable(cell) ? cell : gatherer);
    }

    /// <summary>
    /// A colonist who gathered to eat goes on to eat what it gathered, before anything else can
    /// take it. False if there is no way to the food, and the job simply ends.
    /// </summary>
    private bool TurnIntoMeal(Simulation simulation, EntityId colonist, Job job, EntityId food)
    {
        var world = simulation.World;
        var cell = world.Get<Position>(food).Cell;
        var from = world.Get<Position>(colonist).Cell;
        _goal.Clear();
        _goal.Add(cell);
        if (!simulation.Connectivity.AreConnected(from, cell) || !_paths.TryFind(simulation.Map, from, _goal, _path))
        {
            return false;
        }

        job.Kind = JobKind.Eat;
        job.Stage = JobStage.Moving;
        job.Target = food;
        job.Cell = cell;
        job.Progress = 0;
        job.ForFood = false;
        var movement = world.Get<Movement>(colonist);
        movement.Stop();
        movement.Path.AddRange(_path);
        return true;
    }

    /// <summary>
    /// Lifts from the pile what fits at the destination, or what the blueprint lacks, and sets off
    /// there. True, ending the job with nothing lifted, if nothing fits any more or there is no way
    /// to the destination; in the second case the colonist leaves that cell, or that blueprint,
    /// alone for a while, so it does not choose it again at once.
    /// </summary>
    private bool PickUp(Simulation simulation, EntityId colonist, Job job)
    {
        var world = simulation.World;
        var stack = world.Get<ItemStack>(job.Target);
        var item = stack.Def;
        var supplying = job.Kind == JobKind.Deliver;
        var wanted = supplying
            ? Construction.Needs(simulation, world.Get<Blueprint>(job.Recipient)).Missing
            : Storage(simulation).SpaceFor(simulation, item, stack.Quality, job.Destination);
        var count = Math.Min(stack.Count, wanted);
        if (count <= 0)
        {
            return true;
        }

        var from = world.Get<Position>(colonist).Cell;
        _goal.Clear();
        if (supplying)
        {
            WorkSites.Find(simulation.Map, JobKind.Build, job.Destination, _goal);
        }
        else
        {
            _goal.Add(job.Destination);
        }

        _goal.RemoveAll(cell => !simulation.Connectivity.AreConnected(from, cell));
        if (_goal.Count == 0 || !_paths.TryFind(simulation.Map, from, _goal, _path))
        {
            world.Get<Colonist>(colonist).Unreachable.Add(new IgnoredTarget
            {
                Entity = supplying ? job.Recipient : EntityId.None,
                Cell = job.Destination,
                UntilTick = simulation.CurrentTick + _rules.Ai.UnreachableCooldownTicks,
            });
            return true;
        }

        var (quality, freshness) = (stack.Quality, stack.Freshness);
        ItemPiles.Take(simulation, job.Target, count);
        _storage.Invalidate();
        world.Add(colonist, new Carried { Def = item, Count = count, Quality = quality, Freshness = freshness });
        simulation.Events.Emit(new EntityChangedEvent(colonist));
        job.Stage = JobStage.Carrying;
        job.Cell = job.Destination;
        var movement = world.Get<Movement>(colonist);
        movement.Stop();
        movement.Path.AddRange(_path);
        return false;
    }

    /// <summary>
    /// Next to the blueprint, hands over what it lacks. Anything left over stays carried and falls
    /// where the colonist stands when the job ends.
    /// </summary>
    private static bool Supply(Simulation simulation, EntityId colonist, Job job)
    {
        var world = simulation.World;
        if (!WorkSites.IsSite(simulation.Map, JobKind.Build, job.Destination, world.Get<Position>(colonist).Cell))
        {
            return true;
        }

        var plan = world.Get<Blueprint>(job.Recipient);
        var carried = world.Get<Carried>(colonist);
        var count = Math.Min(carried.Count, Construction.Needs(simulation, plan).Missing);
        if (count == 0)
        {
            return true;
        }

        plan.Delivered += count;
        carried.Count -= count;
        if (carried.Count == 0)
        {
            world.Remove<Carried>(colonist);
        }

        simulation.Events.Emit(new EntityChangedEvent(job.Recipient));
        simulation.Events.Emit(new EntityChangedEvent(colonist));
        return true;
    }

    /// <summary>
    /// At the destination, leaves what fits there. Whatever does not fit, because the stockpile
    /// changed on the way, stays carried and falls on the cell when the job ends: a second pile the
    /// cell does not keep, which is loose and hauled again (see <see cref="StorageIndex"/>).
    /// </summary>
    private bool Deliver(Simulation simulation, EntityId colonist, Job job)
    {
        var world = simulation.World;
        if (world.Get<Position>(colonist).Cell != job.Destination)
        {
            return true;
        }

        var carried = world.Get<Carried>(colonist);
        var count = Math.Min(carried.Count, Storage(simulation).SpaceFor(simulation, carried.Def, carried.Quality, job.Destination));
        if (count == 0)
        {
            return true;
        }

        ItemPiles.Drop(simulation, carried.Def, count, job.Destination, carried.Quality, carried.Freshness);
        _storage.Invalidate();
        carried.Count -= count;
        if (carried.Count == 0)
        {
            world.Remove<Carried>(colonist);
        }

        simulation.Events.Emit(new EntityChangedEvent(colonist));

        return true;
    }

    /// <summary>
    /// Lifts from the food pile what the baby needs to be fed, as many units as it lacks and no
    /// more than the job holds (<see cref="Job.Count"/>), and sets off to it (see "Crianza" in
    /// <c>docs/gdd/vida-y-generaciones.md</c>). True, ending the job
    /// with nothing lifted, if the baby is fed already or there is no way to it; in the second case
    /// the colonist leaves the baby alone for a while.
    /// </summary>
    private bool PickUpFood(Simulation simulation, EntityId colonist, Job job)
    {
        var world = simulation.World;
        var stack = world.Get<ItemStack>(job.Target);
        var item = stack.Def;
        var units = Hunger.UnitsToFeed(simulation, job.Recipient, simulation.Content.Get<ItemDef>(item).Nutrition);
        var count = Math.Min(units, Math.Min(stack.Count, Math.Max(1, job.Count)));
        if (count <= 0)
        {
            return true;
        }

        if (!TrySetOffTo(simulation, colonist, job.Recipient))
        {
            world.Get<Colonist>(colonist).Unreachable.Add(new IgnoredTarget
            {
                Entity = job.Recipient,
                Cell = world.Get<Position>(job.Recipient).Cell,
                UntilTick = simulation.CurrentTick + _rules.Ai.UnreachableCooldownTicks,
            });
            return true;
        }

        var (foodQuality, foodFreshness) = (stack.Quality, stack.Freshness);
        ItemPiles.Take(simulation, job.Target, count);
        _storage.Invalidate();
        world.Add(colonist, new Carried { Def = item, Count = count, Quality = foodQuality, Freshness = foodFreshness });
        simulation.Events.Emit(new EntityChangedEvent(colonist));
        job.Stage = JobStage.Carrying;
        job.Progress = 0;
        return false;
    }

    /// <summary>
    /// Beside the baby, gives it a unit of what it carries every <see cref="AiConfigDef.EatTicksPerUnit"/>
    /// ticks at the speed of the feeder's work, until it is fed or the food runs out. If the baby crawled off, follows it. Food left
    /// over stays carried and falls where the colonist stands when the job ends.
    /// </summary>
    private bool Feed(Simulation simulation, EntityId colonist, Job job, int speed)
    {
        var world = simulation.World;
        if (!world.TryGet<Carried>(colonist, out var carried))
        {
            return true;
        }

        var nutrition = simulation.Content.Get<ItemDef>(carried.Def).Nutrition;
        if (Hunger.UnitsToFeed(simulation, job.Recipient, nutrition) == 0)
        {
            return true;
        }

        if (!WorkSites.IsSite(simulation.Map, JobKind.FeedBaby, world.Get<Position>(job.Recipient).Cell, world.Get<Position>(colonist).Cell))
        {
            job.Progress = 0;
            return !TrySetOffTo(simulation, colonist, job.Recipient);
        }

        if (!Advance(job, _rules.Ai.EatTicksPerUnit, speed))
        {
            return false;
        }

        job.Progress = 0;
        Nourish(simulation, job.Recipient, nutrition);
        carried.Count--;
        if (carried.Count == 0)
        {
            world.Remove<Carried>(colonist);
        }

        simulation.Events.Emit(new EntityChangedEvent(colonist));
        return carried.Count == 0 || Hunger.UnitsToFeed(simulation, job.Recipient, nutrition) == 0;
    }

    /// <summary>Walks the colonist to beside the baby, or stops it there; false if there is no way.</summary>
    private bool TrySetOffTo(Simulation simulation, EntityId colonist, EntityId baby)
    {
        var world = simulation.World;
        var from = world.Get<Position>(colonist).Cell;
        WorkSites.Find(simulation.Map, JobKind.FeedBaby, world.Get<Position>(baby).Cell, _goal);
        _goal.RemoveAll(cell => !simulation.Connectivity.AreConnected(from, cell));
        var movement = world.Get<Movement>(colonist);
        if (_goal.Contains(from))
        {
            movement.Stop();
            return true;
        }

        if (_goal.Count == 0 || !_paths.TryFind(simulation.Map, from, _goal, _path))
        {
            return false;
        }

        movement.Stop();
        movement.Path.AddRange(_path);
        return true;
    }

    /// <summary>
    /// Eats one unit every <see cref="AiConfigDef.EatTicksPerUnit"/> ticks, until the needs
    /// satisfied by eating lack less than half a unit or the pile runs out.
    /// </summary>
    private bool Eat(Simulation simulation, EntityId colonist, Job job)
    {
        var nutrition = simulation.Content.Get<ItemDef>(simulation.World.Get<ItemStack>(job.Target).Def).Nutrition;
        if (IsFed(simulation, colonist, nutrition))
        {
            return true;
        }

        if (!Advance(job, _rules.Ai.EatTicksPerUnit))
        {
            return false;
        }

        job.Progress = 0;
        Nourish(simulation, colonist, nutrition);
        ItemPiles.TakeOne(simulation, job.Target);
        return !simulation.World.IsAlive(job.Target) || IsFed(simulation, colonist, nutrition);
    }

    /// <summary>A unit of food of this nutrition goes into the needs satisfied by eating.</summary>
    private static void Nourish(Simulation simulation, EntityId colonist, int nutrition)
    {
        foreach (var state in simulation.World.Get<Needs>(colonist).Values)
        {
            var need = simulation.Content.Get<NeedDef>(state.Need);
            if (need.SatisfiedBy == NeedSatisfaction.Eating)
            {
                state.Value = Math.Min(need.Max, state.Value + nutrition);
                state.MinutesAtZero = 0;
            }
        }
    }

    private static bool IsFed(Simulation simulation, EntityId colonist, int nutrition) => Hunger.UnitsToFeed(simulation, colonist, nutrition) == 0;

    private static bool NeedsFull(Simulation simulation, EntityId colonist, NeedSatisfaction way)
    {
        foreach (var state in simulation.World.Get<Needs>(colonist).Values)
        {
            var need = simulation.Content.Get<NeedDef>(state.Need);
            if (need.SatisfiedBy == way && state.Value < need.Max)
            {
                return false;
            }
        }

        return true;
    }
}
