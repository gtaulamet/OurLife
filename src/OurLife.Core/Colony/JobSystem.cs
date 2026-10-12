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
public sealed class JobSystem : ISimSystem
{
    private const string RandomStream = "colony.jobs";

    private readonly ColonyRules _rules;
    private readonly PathFinder _paths = new();
    private readonly List<EntityId> _colonists = [];
    private readonly List<TileCoord> _path = [];
    private readonly List<TileCoord> _goal = [];

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
        var world = simulation.World;
        DropLoadsOfOtherJobs(simulation);
        world.Query<Job, Position>(_colonists);
        foreach (var colonist in _colonists)
        {
            var job = world.Get<Job>(colonist);
            if (!StillValid(simulation, job))
            {
                Drop(world, colonist);
                continue;
            }

            if (world.Get<Movement>(colonist).IsMoving)
            {
                continue;
            }

            if (job.Stage == JobStage.Moving)
            {
                if (!WorkSites.IsSite(simulation.Map, job.Kind, job.Cell, world.Get<Position>(colonist).Cell))
                {
                    Drop(world, colonist);
                    continue;
                }

                Start(simulation, job);
            }

            if (Work(simulation, colonist, job))
            {
                Drop(world, colonist);
            }
        }
    }

    /// <summary>
    /// A colonist who carries something but is no longer taking it to a stockpile, because its
    /// job ended or something more urgent came, leaves it where it stands.
    /// </summary>
    private void DropLoadsOfOtherJobs(Simulation simulation)
    {
        var world = simulation.World;
        world.Query<Carried, Position>(_carriers);
        foreach (var colonist in _carriers)
        {
            if (world.TryGet<Job>(colonist, out var job) && job.Kind == JobKind.Haul && job.Stage == JobStage.Carrying)
            {
                continue;
            }

            var carried = world.Get<Carried>(colonist);
            world.Remove<Carried>(colonist);
            ItemPiles.Drop(simulation, carried.Def, carried.Count, world.Get<Position>(colonist).Cell);
            _storage.Invalidate();
            simulation.Events.Emit(new EntityChangedEvent(colonist));
        }
    }

    private StorageIndex Storage(Simulation simulation) => _storage.Fresh(simulation);

    private bool StillValid(Simulation simulation, Job job)
    {
        var world = simulation.World;
        var map = simulation.Map;
        switch (job.Kind)
        {
            case JobKind.Mine:
                return StillOrdered(simulation, job.Cell, TileFlags.MineDesignation, map.ShapeAt(job.Cell) == TileShape.Wall);
            case JobKind.DigStairs:
                return StillOrdered(simulation, job.Cell, TileFlags.StairsDesignation, Designations.CanDigStairs(map, job.Cell));
            case JobKind.Chop:
                return world.IsAlive(job.Target) && world.Has<ChopOrder>(job.Target);
            case JobKind.Harvest:
                return world.IsAlive(job.Target) && world.Has<Plant>(job.Target) && !world.Has<Harvested>(job.Target)
                       && (job.ForFood || world.Has<HarvestOrder>(job.Target));
            case JobKind.Eat:
                return world.IsAlive(job.Target) && world.TryGet<ItemStack>(job.Target, out var stack) && stack.Count > 0;
            case JobKind.Haul:
                // On the way to the pile, it must still be there and loose; once carried, the
                // destination is checked on arrival.
                return job.Stage == JobStage.Carrying
                       || (world.IsAlive(job.Target) && world.Has<ItemStack>(job.Target) && !Storage(simulation).IsStored(simulation, job.Target));
            default:
                return true;
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
        if (job.Kind == JobKind.Wander)
        {
            var random = simulation.Random.Get(RandomStream);
            job.UntilTick = simulation.CurrentTick + random.NextInt(_rules.Ai.WanderWaitMin, _rules.Ai.WanderWaitMax + 1);
        }
    }

    /// <summary>Does one tick of work; true when the job is finished.</summary>
    private bool Work(Simulation simulation, EntityId colonist, Job job)
    {
        switch (job.Kind)
        {
            case JobKind.Mine:
                if (!Advance(job, WorkTicksOf(simulation.Map.MaterialAt(job.Cell))))
                {
                    return false;
                }

                Mine(simulation, job.Cell);
                return true;
            case JobKind.DigStairs:
                var below = new TileCoord(job.Cell.X, job.Cell.Y, job.Cell.Z - 1);
                if (!Advance(job, WorkTicksOf(simulation.Map.MaterialAt(below))))
                {
                    return false;
                }

                DigStairs(simulation, job.Cell);
                return true;
            case JobKind.Chop:
                if (!Advance(job, PlantOf(simulation, job.Target).Chop!.WorkTicks))
                {
                    return false;
                }

                Chop(simulation, job.Target);
                return true;
            case JobKind.Harvest:
                if (!Advance(job, PlantOf(simulation, job.Target).Harvest!.WorkTicks))
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
                return simulation.CurrentTick >= job.UntilTick;
            case JobKind.Haul:
                return job.Stage == JobStage.Carrying ? Deliver(simulation, colonist, job) : PickUp(simulation, colonist, job);
            default:
                throw new InvalidOperationException($"Unknown job {job.Kind}.");
        }
    }

    private static bool Advance(Job job, int workTicks)
    {
        job.Progress++;
        return job.Progress >= workTicks;
    }

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
    /// Lifts from the pile what fits at the destination and sets off there. True, ending the job
    /// with nothing lifted, if nothing fits any more or there is no way to the destination; in the
    /// second case the colonist leaves that cell alone for a while, so it does not choose it again at once.
    /// </summary>
    private bool PickUp(Simulation simulation, EntityId colonist, Job job)
    {
        var world = simulation.World;
        var stack = world.Get<ItemStack>(job.Target);
        var item = stack.Def;
        var count = Math.Min(stack.Count, Storage(simulation).SpaceFor(simulation, item, job.Destination));
        if (count <= 0)
        {
            return true;
        }

        var from = world.Get<Position>(colonist).Cell;
        _goal.Clear();
        _goal.Add(job.Destination);
        if (!simulation.Connectivity.AreConnected(from, job.Destination) || !_paths.TryFind(simulation.Map, from, _goal, _path))
        {
            world.Get<Colonist>(colonist).Unreachable.Add(new IgnoredTarget
            {
                Entity = EntityId.None,
                Cell = job.Destination,
                UntilTick = simulation.CurrentTick + _rules.Ai.UnreachableCooldownTicks,
            });
            return true;
        }

        ItemPiles.Take(simulation, job.Target, count);
        _storage.Invalidate();
        world.Add(colonist, new Carried { Def = item, Count = count });
        simulation.Events.Emit(new EntityChangedEvent(colonist));
        job.Stage = JobStage.Carrying;
        job.Cell = job.Destination;
        var movement = world.Get<Movement>(colonist);
        movement.Stop();
        movement.Path.AddRange(_path);
        return false;
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
        var count = Math.Min(carried.Count, Storage(simulation).SpaceFor(simulation, carried.Def, job.Destination));
        if (count == 0)
        {
            return true;
        }

        ItemPiles.Drop(simulation, carried.Def, count, job.Destination);
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
        foreach (var state in simulation.World.Get<Needs>(colonist).Values)
        {
            var need = simulation.Content.Get<NeedDef>(state.Need);
            if (need.SatisfiedBy == NeedSatisfaction.Eating)
            {
                state.Value = Math.Min(need.Max, state.Value + nutrition);
                state.MinutesAtZero = 0;
            }
        }

        ItemPiles.TakeOne(simulation, job.Target);
        return !simulation.World.IsAlive(job.Target) || IsFed(simulation, colonist, nutrition);
    }

    private static bool IsFed(Simulation simulation, EntityId colonist, int nutrition)
    {
        foreach (var state in simulation.World.Get<Needs>(colonist).Values)
        {
            var need = simulation.Content.Get<NeedDef>(state.Need);
            if (need.SatisfiedBy == NeedSatisfaction.Eating && state.Value < need.Max - (nutrition / 2))
            {
                return false;
            }
        }

        return true;
    }

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
