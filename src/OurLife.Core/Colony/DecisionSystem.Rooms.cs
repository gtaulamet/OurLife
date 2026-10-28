using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Pathfinding;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

// Sitting down to eat and gathering in the dining rooms (see "Comedor" and "Lugar de reunión" in docs/gdd/habitaciones.md).
public sealed partial class DecisionSystem
{
    /// <summary>
    /// The nearest free chair of a dining room, within the dining radius and in the colonist's region, with a
    /// table beside it; ties to the lowest id. A chair somebody holds, or the colonist failed to reach, is not free.
    /// </summary>
    private bool TrySeat(Simulation simulation, EntityId colonist, TileCoord from, int region, out Seat seat) =>
        TryFindSeat(simulation, colonist, from, region, radius: null, out seat);

    private bool TryFindSeat(Simulation simulation, EntityId colonist, TileCoord from, int region, int? radius, out Seat seat)
    {
        seat = default;
        if (RoomSetup.From(simulation.Content) is not { } setup || !simulation.HasMap)
        {
            return false;
        }

        var seats = Rooms.IndexOf(simulation).Seats(simulation);
        if (seats.Count == 0)
        {
            return false;
        }

        var limit = radius ?? setup.Config.DiningRadius;
        var person = simulation.World.Get<Colonist>(colonist);
        var connectivity = simulation.Connectivity;
        var bestDistance = int.MaxValue;
        foreach (var candidate in seats)
        {
            if (_entityUses.GetValueOrDefault(candidate.Chair) > 0 || IsIgnored(person, candidate.Chair, candidate.Cell))
            {
                continue;
            }

            // Near is measured in cells on the plane, like the other radii of free will; which is nearer, by the cost of the way.
            var distance = PathFinder.Estimate(from, candidate.Cell);
            if (distance >= bestDistance || !FreeWillRules.IsNear(from, limit, candidate.Cell) || connectivity.RegionAt(candidate.Cell) != region)
            {
                continue;
            }

            (seat, bestDistance) = (candidate, distance);
        }

        return bestDistance != int.MaxValue;
    }

    /// <summary>
    /// In the evening hours of the room config, a colonist who may seek company goes, with the chance its
    /// sociability and mood give, to sit at a free chair of a dining room within the gathering radius, and stays
    /// for the ticks of the config. True if it set off. The interactions of every day happen there, because
    /// those who sit together are within reach of each other.
    /// </summary>
    private bool TryGather(Simulation simulation, EntityId colonist, FreeWillConfigDef config)
    {
        if (RoomSetup.From(simulation.Content) is not { } setup)
        {
            return false;
        }

        var hour = simulation.CurrentDate.Hour;
        if (hour < setup.Config.GatherFromHour || hour >= setup.Config.GatherToHour || Rooms.IndexOf(simulation).Seats(simulation).Count == 0)
        {
            return false;
        }

        var random = simulation.Random.Get(FreeWillStream);
        if (!random.Chance(FreeWillRules.Chance(simulation, colonist, config, setup.Config.GatherChancePercent, config.SeekTrait)))
        {
            return false;
        }

        var map = simulation.Map;
        var from = simulation.World.Get<Position>(colonist).Cell;
        if (!TryFindSeat(simulation, colonist, from, simulation.Connectivity.RegionAt(from), setup.Config.GatherRadius, out var seat))
        {
            return false;
        }

        var job = new Job { Kind = JobKind.Gather, Recipient = seat.Chair, Cell = seat.Cell };
        if (seat.Cell == from)
        {
            Assign(simulation, colonist, job, []);
            return true;
        }

        _goal.Clear();
        _goal.Add(seat.Cell);
        if (!_paths.TryFind(map, from, _goal, _path))
        {
            simulation.World.Get<Colonist>(colonist).Unreachable.Add(new IgnoredTarget
            {
                Entity = seat.Chair,
                Cell = seat.Cell,
                UntilTick = simulation.CurrentTick + _rules.Ai.UnreachableCooldownTicks,
            });
            return false;
        }

        Assign(simulation, colonist, job, _path);
        return true;
    }
}
