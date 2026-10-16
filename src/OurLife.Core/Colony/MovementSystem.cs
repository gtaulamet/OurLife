using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Pathfinding;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// Walks the colonists along their routes. A straight step costs the creature's
/// <c>moveTicksPerTile</c> ticks and a diagonal one 1.4 times that, counted in thousandths of a
/// tick so the leftover carries to the next step. A colonist makes progress at the speed of its
/// stage of life (see <c>docs/gdd/vida-y-generaciones.md</c>), and while it carries a load for the
/// player, also at the speed of its skill in that work. A step the map no longer allows drops the job.
/// </summary>
public sealed class MovementSystem : ISimSystem
{
    private readonly ColonyRules? _rules;

    /// <param name="rules">The rules with the skills; without them, carrying goes at the speed of walking.</param>
    public MovementSystem(ColonyRules? rules = null)
    {
        _rules = rules;
    }

    /// <summary>Progress a creature makes in one tick at full speed: the units of <see cref="PathFinder.StepCost"/> times 100 %.</summary>
    public const int ProgressPerTick = PathFinder.StraightCost * FullSpeed;

    private const int FullSpeed = 100;

    private readonly List<EntityId> _walkers = [];

    public void Tick(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        Span<TileCoord> neighbours = stackalloc TileCoord[MapNavigation.MaxNeighbours];
        world.Query<Movement, Position>(_walkers);
        foreach (var walker in _walkers)
        {
            var movement = world.Get<Movement>(walker);
            if (!movement.IsMoving)
            {
                continue;
            }

            var position = world.Get<Position>(walker);
            var ticksPerTile = TicksPerTile(simulation, walker);
            movement.Progress += ProgressOf(simulation, walker, _rules);
            while (movement.IsMoving)
            {
                var next = movement.Path[movement.Next];
                var cost = ticksPerTile * PathFinder.StepCost(position.Cell, next) * FullSpeed;
                if (movement.Progress < cost)
                {
                    break;
                }

                if (!IsNeighbour(simulation.Map, position.Cell, next, neighbours))
                {
                    JobSystem.Drop(world, walker);
                    break;
                }

                position.X = next.X;
                position.Y = next.Y;
                position.Z = next.Z;
                movement.Progress -= cost;
                movement.Next++;
                if (!movement.IsMoving)
                {
                    movement.Stop();
                }
            }
        }
    }

    /// <summary>
    /// The progress a colonist needs for one step of its route, in the units of
    /// <see cref="Movement.Progress"/>. A read-only query: the presentation uses it to draw the
    /// walker between two cells.
    /// </summary>
    public static int StepCost(Simulation simulation, EntityId walker, TileCoord from, TileCoord to)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return TicksPerTile(simulation, walker) * PathFinder.StepCost(from, to) * FullSpeed;
    }

    /// <summary>
    /// The progress a colonist makes in one tick: <see cref="ProgressPerTick"/> at the speed of its
    /// stage of life and, with <paramref name="rules"/>, while it carries a load for the player, of
    /// its skill in that work.
    /// </summary>
    public static int ProgressOf(Simulation simulation, EntityId walker, ColonyRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var progress = PathFinder.StraightCost * LifeRules.MoveSpeedPercent(simulation, walker);
        if (rules is not null && simulation.World.TryGet<Job>(walker, out var job) && job.Stage == JobStage.Carrying
            && rules.WorkTypeOf(job) is { Skill: true } workType)
        {
            progress = Math.Max(1, progress * rules.Skills.SpeedPercent(simulation, walker, workType) / 100);
        }

        return progress;
    }

    private static int TicksPerTile(Simulation simulation, EntityId walker) =>
        simulation.Content.Get<CreatureDef>(simulation.World.Get<Colonist>(walker).Creature).MoveTicksPerTile;

    private static bool IsNeighbour(GameMap map, TileCoord from, TileCoord to, Span<TileCoord> neighbours)
    {
        var count = MapNavigation.GetNeighbours(map, from.X, from.Y, from.Z, neighbours);
        for (var i = 0; i < count; i++)
        {
            if (neighbours[i] == to)
            {
                return true;
            }
        }

        return false;
    }
}
