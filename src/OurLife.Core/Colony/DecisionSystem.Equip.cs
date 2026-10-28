using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Pathfinding;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

// Fetching a tool before the work it speeds up (see "Herramientas" in docs/gdd/oficios.md).
public sealed partial class DecisionSystem
{
    private readonly List<EntityId> _toolPiles = [];

    /// <summary>
    /// The colonist just took a job of a work type. If it has no tool for that type and a free one
    /// lies in a stockpile within the radius of the village, it goes to fetch it first: the job it
    /// took is given up, and the colonist takes it again, with the tool, when it decides next. The
    /// best tool wins (by the speed it adds with its quality), then the nearest, then the lowest id.
    /// Nothing changes if there is no such tool or no way to it.
    /// </summary>
    private void FetchToolFirst(Simulation simulation, EntityId colonist, WorkTypeDef workType)
    {
        var content = simulation.Content;
        if (!Tools.Exists(content, workType.Id) || Tools.EquippedFor(simulation, colonist, workType.Id) is not null)
        {
            return;
        }

        var world = simulation.World;
        var from = world.Get<Position>(colonist).Cell;
        var reach = _rules.Ai.CraftFetchRadius * PathFinder.StraightCost;
        var storage = Storage(simulation);
        var best = EntityId.None;
        var bestSpeed = -1;
        var bestDistance = int.MaxValue;
        world.Query<ItemStack, Position>(_toolPiles);
        foreach (var pile in _toolPiles)
        {
            var stack = world.Get<ItemStack>(pile);
            if (stack.Count <= 0 || content.Get<ItemDef>(stack.Def) is not { Tool: { } tool } item || tool.WorkType != workType.Id
                || _entityUses.GetValueOrDefault(pile) > 0)
            {
                continue;
            }

            var cell = world.Get<Position>(pile).Cell;
            var distance = PathFinder.Estimate(from, cell);
            if (distance > reach || !simulation.Connectivity.AreConnected(from, cell) || !storage.IsStored(simulation, pile))
            {
                continue;
            }

            var speed = Tools.SpeedPercent(simulation, item, stack.Quality);
            if (speed > bestSpeed || (speed == bestSpeed && distance < bestDistance))
            {
                (best, bestSpeed, bestDistance) = (pile, speed, distance);
            }
        }

        if (best.IsNone)
        {
            return;
        }

        var target = world.Get<Position>(best).Cell;
        _goal.Clear();
        _goal.Add(target);
        if (target != from && !_paths.TryFind(simulation.Map, from, _goal, _path))
        {
            return;
        }

        var path = target == from ? [] : _path;
        Unreserve(simulation, world.Get<Job>(colonist));
        Assign(simulation, colonist, new Job { Kind = JobKind.Equip, Target = best, Cell = target }, path);
    }
}
