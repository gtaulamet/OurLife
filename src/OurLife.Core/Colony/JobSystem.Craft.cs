using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

// The job of crafting at a workshop (see "El trabajo craft" in docs/gdd/oficios.md): fetch the
// missing ingredients one pile at a time, deliver them to the workshop, work, finish.
public sealed partial class JobSystem
{
    /// <summary>
    /// The kind whose sites a job walks to: the cell of the pile while it fetches an ingredient,
    /// the neighbourhood of the workshop otherwise.
    /// </summary>
    private static JobKind SiteKind(Job job) => job.Kind == JobKind.Craft && !job.Target.IsNone ? JobKind.Haul : job.Kind;

    /// <summary>
    /// A craft goes on while its workshop stands and its recipe exists; fetching, while the pile is
    /// there; and working, while every ingredient is at the workshop.
    /// </summary>
    private static bool IsCraftValid(Simulation simulation, Job job)
    {
        var world = simulation.World;
        if (job.Recipe is null || !simulation.Content.TryGet<RecipeDef>(job.Recipe, out var recipe)
            || !world.IsAlive(job.Recipient) || !world.TryGet<Workshop>(job.Recipient, out var shop))
        {
            return false;
        }

        if (job.Stage == JobStage.Carrying)
        {
            return true;
        }

        if (!job.Target.IsNone)
        {
            return world.IsAlive(job.Target) && world.TryGet<ItemStack>(job.Target, out var pile) && pile.Count > 0;
        }

        return Crafting.HasAll(simulation, recipe, shop);
    }

    private bool Craft(Simulation simulation, EntityId colonist, Job job, WorkTypeDef? workType, int speed)
    {
        var recipe = simulation.Content.Get<RecipeDef>(job.Recipe!);
        if (job.Stage == JobStage.Carrying)
        {
            return DeliverIngredient(simulation, colonist, job, recipe);
        }

        if (!job.Target.IsNone)
        {
            return PickUpIngredient(simulation, colonist, job, recipe);
        }

        if (!Advance(job, recipe.WorkTicks, speed))
        {
            return false;
        }

        FinishCraft(simulation, colonist, job, recipe, workType);
        return true;
    }

    /// <summary>
    /// Lifts from the pile what the workshop still lacks of the ingredient it holds, and sets off to
    /// the workshop. True, ending the job with nothing lifted, if nothing is lacking any more or
    /// there is no way to the workshop; in the second case the colonist leaves the workshop alone
    /// for a while.
    /// </summary>
    private bool PickUpIngredient(Simulation simulation, EntityId colonist, Job job, RecipeDef recipe)
    {
        var world = simulation.World;
        var map = simulation.Map;
        var shop = world.Get<Workshop>(job.Recipient);
        var stack = world.Get<ItemStack>(job.Target);
        var wanted = 0;
        foreach (var input in recipe.Inputs)
        {
            if (Crafting.Accepts(simulation, input, stack.Def, stack.Quality))
            {
                wanted = Crafting.Missing(simulation, shop, input);
                if (wanted > 0)
                {
                    break;
                }
            }
        }

        var count = Math.Min(stack.Count, wanted);
        if (count <= 0)
        {
            return true;
        }

        var shopCell = world.Get<Position>(job.Recipient).Cell;
        var from = world.Get<Position>(colonist).Cell;
        var atSite = WorkSites.IsSite(map, JobKind.Craft, shopCell, from);
        if (!atSite)
        {
            WorkSites.Find(map, JobKind.Craft, shopCell, _goal);
            _goal.RemoveAll(cell => !simulation.Connectivity.AreConnected(from, cell));
            if (_goal.Count == 0 || !_paths.TryFind(map, from, _goal, _path))
            {
                world.Get<Colonist>(colonist).Unreachable.Add(new IgnoredTarget
                {
                    Entity = job.Recipient,
                    Cell = shopCell,
                    UntilTick = simulation.CurrentTick + _rules.Ai.UnreachableCooldownTicks,
                });
                return true;
            }
        }

        var (quality, freshness, item) = (stack.Quality, stack.Freshness, stack.Def);
        ItemPiles.Take(simulation, job.Target, count);
        _storage.Invalidate();
        world.Add(colonist, new Carried { Def = item, Count = count, Quality = quality, Freshness = freshness });
        simulation.Events.Emit(new EntityChangedEvent(colonist));
        job.Stage = JobStage.Carrying;
        job.Target = EntityId.None;
        job.Cell = shopCell;
        job.Progress = 0;
        var movement = world.Get<Movement>(colonist);
        movement.Stop();
        if (!atSite)
        {
            movement.Path.AddRange(_path);
        }

        return false;
    }

    /// <summary>
    /// Beside the workshop, leaves the load with its ingredients, then goes on to the next missing
    /// one, or to work if none is. True, ending the job, if the colonist is not beside the workshop
    /// or something is still missing with nowhere to fetch it from: what was delivered stays there.
    /// </summary>
    private bool DeliverIngredient(Simulation simulation, EntityId colonist, Job job, RecipeDef recipe)
    {
        var world = simulation.World;
        if (!world.TryGet<Carried>(colonist, out var carried)
            || !WorkSites.IsSite(simulation.Map, JobKind.Craft, job.Cell, world.Get<Position>(colonist).Cell))
        {
            return true;
        }

        Crafting.Deliver(simulation, job.Recipient, carried.Def, carried.Count, carried.Quality, carried.Freshness);
        world.Remove<Carried>(colonist);
        simulation.Events.Emit(new EntityChangedEvent(colonist));
        return !ContinueCraft(simulation, colonist, job, recipe);
    }

    /// <summary>
    /// Sets off to the nearest pile of the first ingredient still missing, or starts working when
    /// all are at the workshop. False if something is missing and there is no pile to go to.
    /// </summary>
    private bool ContinueCraft(Simulation simulation, EntityId colonist, Job job, RecipeDef recipe)
    {
        var world = simulation.World;
        var map = simulation.Map;
        var shop = world.Get<Workshop>(job.Recipient);
        var shopCell = world.Get<Position>(job.Recipient).Cell;
        var from = world.Get<Position>(colonist).Cell;
        _pileInUse ??= pile => PileInUse(pile);
        foreach (var input in recipe.Inputs)
        {
            if (Crafting.Missing(simulation, shop, input) == 0)
            {
                continue;
            }

            var pile = Crafting.FindPile(simulation, input, shopCell, from, _rules.Ai.CraftFetchRadius, _pileInUse, _piles);
            if (pile.IsNone)
            {
                return false;
            }

            var cell = world.Get<Position>(pile).Cell;
            _goal.Clear();
            _goal.Add(cell);
            if (!_paths.TryFind(map, from, _goal, _path))
            {
                return false;
            }

            job.Target = pile;
            job.Cell = cell;
            job.Stage = JobStage.Moving;
            job.Progress = 0;
            var movement = world.Get<Movement>(colonist);
            movement.Stop();
            movement.Path.AddRange(_path);
            return true;
        }

        job.Stage = JobStage.Working;
        job.Target = EntityId.None;
        job.Cell = shopCell;
        job.Progress = 0;
        return true;
    }

    /// <summary>Whether a colonist has the pile as the target of its job: hauling it, eating from it, fetching it.</summary>
    private bool PileInUse(EntityId pile)
    {
        var world = _current!.World;
        world.Query<Job>(_pileUsers);
        foreach (var user in _pileUsers)
        {
            var job = world.Get<Job>(user);
            if (job.Target == pile && job.Stage != JobStage.Carrying)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Consumes the ingredients, rolls the quality if the recipe has one, leaves the outputs on the
    /// cell of the workshop, or by it if nobody can stand there, and counts them for the order.
    /// </summary>
    private void FinishCraft(Simulation simulation, EntityId colonist, Job job, RecipeDef recipe, WorkTypeDef? workType)
    {
        var world = simulation.World;
        string? quality = null;
        if (recipe.Quality && workType is not null && simulation.Content.All<QualityConfigDef>().Count == 1)
        {
            var rules = QualityRules.From(simulation.Content);
            quality = rules.Store(rules.Roll(simulation, colonist, workType.Id).Id);
        }

        Crafting.Consume(simulation, recipe, world.Get<Workshop>(job.Recipient));
        var cell = Construction.StandableNear(simulation.Map, world.Get<Position>(job.Recipient).Cell);
        foreach (var output in recipe.Outputs)
        {
            ItemPiles.Drop(simulation, output.Item, output.Count, cell, quality);
        }

        _storage.Invalidate();
        simulation.Events.Emit(new EntityChangedEvent(job.Recipient));
        if (!job.Order.IsNone && world.IsAlive(job.Order) && world.TryGet<WorkOrder>(job.Order, out var order))
        {
            order.Done += recipe.MainOutput.Count;
            simulation.Events.Emit(new EntityChangedEvent(job.Order));
            simulation.Events.Emit(new WorkOrdersChangedEvent());
        }
    }

    /// <summary>
    /// Ingredients nobody has used for a day fall to the ground by the workshop, to be hauled
    /// (see "El trabajo craft" in docs/gdd/oficios.md). Checked once an hour.
    /// </summary>
    private void ReleaseStaleInputs(Simulation simulation)
    {
        var world = simulation.World;
        world.Query<Workshop>(_workshops);
        foreach (var shop in _workshops)
        {
            var inputs = world.Get<Workshop>(shop);
            if (inputs.Inputs.Count == 0 || simulation.CurrentTick - inputs.InputsSinceTick < simulation.Time.TicksPerDay)
            {
                continue;
            }

            world.Query<Job>(_pileUsers);
            if (!_pileUsers.Any(user => world.Get<Job>(user).Kind == JobKind.Craft && world.Get<Job>(user).Recipient == shop))
            {
                Crafting.ReleaseInputs(simulation, shop);
                _storage.Invalidate();
            }
        }
    }
}
