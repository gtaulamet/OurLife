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

    // Work types that have a loose-from-nothing tool lying in a stockpile, worked out once per tick (a membership test only, never iterated).
    private readonly HashSet<string> _typesWithStoredTool = new(StringComparer.Ordinal);
    private Simulation? _toolIndexOf;
    private long _toolIndexTick = -1;

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
        var world = simulation.World;

        // Only work that goes faster with a tool is worth a detour (a delivery or a hauling is not).
        if (!JobSystem.IsWork(world.Get<Job>(colonist)) || !AnyStoredTool(simulation, workType.Id)
            || Tools.EquippedFor(simulation, colonist, workType.Id) is not null)
        {
            return;
        }

        var from = world.Get<Position>(colonist).Cell;
        var reach = _rules.Ai.ToolFetchRadius * PathFinder.StraightCost;
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

    /// <summary>Whether some tool for the work type lies in a stockpile; the answer is worked out once per tick.</summary>
    private bool AnyStoredTool(Simulation simulation, string workType)
    {
        if (!ReferenceEquals(_toolIndexOf, simulation) || _toolIndexTick != simulation.CurrentTick)
        {
            _toolIndexOf = simulation;
            _toolIndexTick = simulation.CurrentTick;
            _typesWithStoredTool.Clear();
            var world = simulation.World;
            var storage = Storage(simulation);
            world.Query<ItemStack, Position>(_toolPiles);
            foreach (var pile in _toolPiles)
            {
                var stack = world.Get<ItemStack>(pile);
                if (stack.Count > 0 && simulation.Content.Get<ItemDef>(stack.Def).Tool is { } tool && storage.IsStored(simulation, pile))
                {
                    _typesWithStoredTool.Add(tool.WorkType);
                }
            }
        }

        return _typesWithStoredTool.Contains(workType);
    }
}
