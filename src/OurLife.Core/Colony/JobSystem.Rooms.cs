using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

// Eating sitting at a table (see "Comedor" in docs/gdd/habitaciones.md).
public sealed partial class JobSystem
{
    /// <summary>
    /// The three steps of a meal at a table: at the pile, lift the units the colonist lacks and set off to the
    /// chair; arrived, sit down; seated, eat one unit every <see cref="AiConfigDef.EatTicksPerUnit"/> ticks. The
    /// colonist holds the food all the time (<see cref="Carried"/>). True when the job is over.
    /// </summary>
    private bool EatAtTable(Simulation simulation, EntityId colonist, Job job)
    {
        var world = simulation.World;
        if (!world.TryGet<Carried>(colonist, out var carried))
        {
            return TakeToTable(simulation, colonist, job);
        }

        if (job.Stage == JobStage.Carrying)
        {
            // The walk is over: sit if it got to the chair, else the meal is lost and the food drops where it stands.
            if (world.Get<Position>(colonist).Cell != job.Cell)
            {
                return true;
            }

            job.Stage = JobStage.Working;
            job.Progress = 0;
            return false;
        }

        var item = simulation.Content.Get<ItemDef>(carried.Def);
        if (IsFed(simulation, colonist, item.Nutrition))
        {
            return FinishMeal(simulation, colonist, job);
        }

        if (!Advance(job, _rules.Ai.EatTicksPerUnit))
        {
            return false;
        }

        job.Progress = 0;
        job.Count++;
        Nourish(simulation, colonist, item.Nutrition);
        if (item.Meal)
        {
            MoodRules.Feel(simulation, colonist, config => config.MealThought);
        }

        carried.Count--;
        if (carried.Count == 0)
        {
            world.Remove<Carried>(colonist);
        }

        simulation.Events.Emit(new EntityChangedEvent(colonist));
        return (carried.Count == 0 || IsFed(simulation, colonist, item.Nutrition)) && FinishMeal(simulation, colonist, job);
    }

    /// <summary>
    /// At the pile: lifts the units the colonist lacks, at most the pile, and sets off to the chair. If there is no
    /// way to the chair it eats there, standing, as it would have without a dining room. True, ending the job with
    /// nothing lifted, if it is fed already or nothing is left.
    /// </summary>
    private bool TakeToTable(Simulation simulation, EntityId colonist, Job job)
    {
        var world = simulation.World;
        var stack = world.Get<ItemStack>(job.Target);
        var item = simulation.Content.Get<ItemDef>(stack.Def);
        var units = Hunger.UnitsToFeed(simulation, colonist, item.Nutrition);
        var count = Math.Min(stack.Count, units);
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
                Entity = job.Recipient,
                Cell = job.Destination,
                UntilTick = simulation.CurrentTick + _rules.Ai.UnreachableCooldownTicks,
            });
            job.Kind = JobKind.Eat;
            job.Recipient = EntityId.None;
            job.Destination = default;
            return false;
        }

        var (quality, freshness) = (stack.Quality, stack.Freshness);
        ItemPiles.Take(simulation, job.Target, count);
        _storage.Invalidate();
        world.Add(colonist, new Carried { Def = stack.Def, Count = count, Quality = quality, Freshness = freshness });
        simulation.Events.Emit(new EntityChangedEvent(colonist));
        job.Stage = JobStage.Carrying;
        job.Cell = job.Destination;
        job.Progress = 0;
        var movement = world.Get<Movement>(colonist);
        movement.Stop();
        movement.Path.AddRange(_path);
        return false;
    }

    /// <summary>The meal at the table is over: the colonist thinks it ate at a table, and of the room, if it ate anything. Always true.</summary>
    private static bool FinishMeal(Simulation simulation, EntityId colonist, Job job)
    {
        if (job.Count > 0)
        {
            RoomRules.FeelAteAtTable(simulation, colonist, Rooms.At(simulation, job.Cell));
        }

        return true;
    }
}
