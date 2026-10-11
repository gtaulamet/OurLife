using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Pathfinding;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// Walks the colonists along their routes. A straight step costs the creature's
/// <c>moveTicksPerTile</c> ticks and a diagonal one 1.4 times that, counted in tenths of a tick
/// so the leftover carries to the next step. A step the map no longer allows drops the job.
/// </summary>
public sealed class MovementSystem : ISimSystem
{
    /// <summary>Progress a creature makes in one tick, in the same units as <see cref="PathFinder.StepCost"/>.</summary>
    public const int ProgressPerTick = PathFinder.StraightCost;

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
            movement.Progress += ProgressPerTick;
            while (movement.IsMoving)
            {
                var next = movement.Path[movement.Next];
                var cost = StepCost(simulation, walker, position.Cell, next);
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
        var creature = simulation.Content.Get<CreatureDef>(simulation.World.Get<Colonist>(walker).Creature);
        return creature.MoveTicksPerTile * PathFinder.StepCost(from, to);
    }

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
