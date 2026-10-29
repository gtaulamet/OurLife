using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Pathfinding;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

// Where colonists sleep: each in a bed of their own, and a family under one roof (see "Dormitorios" in docs/gdd/habitaciones.md).
public sealed partial class DecisionSystem
{
    /// <summary>
    /// Sleeps in the colonist's bed, after taking the best free one it can reach if it has none
    /// (see <see cref="BedOf"/>); on the ground where it stands if there is no bed or no way to it. A bed
    /// it takes in a room is also kept for the family that has none yet (see <see cref="KeepBedsForKin"/>).
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
                // A free bed becomes the colonist's only once it can get there; the one it leaves is free.
                if (world.Get<Bed>(bed).Owner != colonist)
                {
                    FreeBedsOf(simulation, colonist);
                    world.Get<Bed>(bed).Owner = colonist;
                    simulation.Events.Emit(new EntityChangedEvent(bed));
                }

                KeepBedsForKin(simulation, colonist, bed);
                Assign(simulation, colonist, new Job { Kind = JobKind.Sleep, Target = bed, Cell = cell }, cell == from ? [] : _path);
                return;
            }
        }

        Assign(simulation, colonist, new Job { Kind = JobKind.Sleep, Cell = from }, []);
    }

    /// <summary>
    /// The colonist's bed if it can reach it, unless its partner (or, for a child, its parents) sleeps in another
    /// room that has a free bed: then that one, so the family lives together. With no bed of its own, the free bed
    /// it can reach in the room of its closest kin (partner, parents even if gone, children, siblings: the
    /// first of them that has a bed there), else the nearest; ties to the lowest id. Only beds
    /// for its stage of life count, like a cradle for a baby (see <see cref="Beds"/>). A bed whose
    /// owner is gone, or grew out of it, is free.
    /// </summary>
    private EntityId BedOf(Simulation simulation, EntityId colonist, TileCoord from)
    {
        var world = simulation.World;
        var connectivity = simulation.Connectivity;
        var beds = new List<EntityId>();
        world.Query<Bed, Position>(beds);
        beds.RemoveAll(bed => !Beds.Fits(simulation, bed, colonist));
        var owned = beds.Find(bed => world.Get<Bed>(bed).Owner == colonist);
        var kin = KinOf(simulation, colonist);
        if (!owned.IsNone)
        {
            if (!connectivity.AreConnected(from, world.Get<Position>(owned).Cell))
            {
                return EntityId.None;
            }

            return BedWithKin(simulation, colonist, kin, owned, from) is { IsNone: false } together ? together : owned;
        }

        var best = EntityId.None;
        var bestRank = int.MaxValue;
        var bestDistance = int.MaxValue;
        foreach (var bed in beds)
        {
            var cell = world.Get<Position>(bed).Cell;
            if (Beds.IsTaken(simulation, bed) || !connectivity.AreConnected(from, cell))
            {
                continue;
            }

            var rank = KinRank(simulation, kin, cell);
            var distance = PathFinder.Estimate(from, cell);
            if (rank < bestRank || (rank == bestRank && distance < bestDistance))
            {
                (best, bestRank, bestDistance) = (bed, rank, distance);
            }
        }

        return best;
    }

    /// <summary>
    /// A free bed, in the room where the partner of the colonist (or its parent, for a child) sleeps, when its own bed is
    /// in another room; none if it already sleeps with them, or they sleep outdoors or there is no free bed there.
    /// The first of them that sleeps in a room decides.
    /// </summary>
    private static EntityId BedWithKin(Simulation simulation, EntityId colonist, List<Kin> kin, EntityId owned, TileCoord from)
    {
        var world = simulation.World;
        var here = Rooms.At(simulation, world.Get<Position>(owned).Cell);
        var grownUp = Beds.IsGrownUp(simulation, colonist);
        foreach (var relative in kin)
        {
            if (relative.Kind is KinKind.Child or KinKind.Sibling
                || (relative.Kind == KinKind.Parent && grownUp)
                || !Relationships.IsLivingColonist(world, relative.Entity))
            {
                continue;
            }

            var theirs = Beds.OwnedBy(simulation, relative.Entity);
            if (theirs.IsNone || Rooms.At(simulation, world.Get<Position>(theirs).Cell) is not { } home)
            {
                continue;
            }

            if (here is not null && home.Id == here.Id)
            {
                return EntityId.None;
            }

            var best = EntityId.None;
            var bestDistance = int.MaxValue;
            foreach (var bed in home.Beds)
            {
                var cell = world.Get<Position>(bed).Cell;
                if (Beds.IsTaken(simulation, bed) || !Beds.Fits(simulation, bed, colonist) || !simulation.Connectivity.AreConnected(from, cell))
                {
                    continue;
                }

                var distance = PathFinder.Estimate(from, cell);
                if (distance < bestDistance)
                {
                    (best, bestDistance) = (bed, distance);
                }
            }

            if (!best.IsNone)
            {
                return best;
            }
        }

        return EntityId.None;
    }

    /// <summary>
    /// How near to the family a bed is: the position in <paramref name="kin"/> of the first relative that owns a bed
    /// in the same room (a bed whose owner is gone counts, so a child takes the room of its dead parents), or the
    /// largest number if the bed is outdoors or no relative sleeps there.
    /// </summary>
    private static int KinRank(Simulation simulation, List<Kin> kin, TileCoord cell)
    {
        if (kin.Count == 0 || Rooms.At(simulation, cell) is not { } room)
        {
            return int.MaxValue;
        }

        for (var index = 0; index < kin.Count; index++)
        {
            foreach (var bed in room.Beds)
            {
                if (simulation.World.Get<Bed>(bed).Owner == kin[index].Entity)
                {
                    return index;
                }
            }
        }

        return int.MaxValue;
    }

    /// <summary>
    /// A colonist that takes a bed in a room keeps one there for its partner, its children and its parents that have
    /// none (in that order, by ascending id within each), if there are free ones for their stage of life: a family moves
    /// into a house together. Only the owner sleeps in a bed, so the others find it waiting for them.
    /// </summary>
    private static void KeepBedsForKin(Simulation simulation, EntityId colonist, EntityId bed)
    {
        var world = simulation.World;
        if (Rooms.At(simulation, world.Get<Position>(bed).Cell) is not { } room)
        {
            return;
        }

        foreach (var relative in KinOf(simulation, colonist))
        {
            if (relative.Kind == KinKind.Sibling || !Relationships.IsLivingColonist(world, relative.Entity) || Beds.HasBed(simulation, relative.Entity))
            {
                continue;
            }

            foreach (var free in room.Beds)
            {
                if (!Beds.IsTaken(simulation, free) && Beds.Fits(simulation, free, relative.Entity))
                {
                    world.Get<Bed>(free).Owner = relative.Entity;
                    simulation.Events.Emit(new EntityChangedEvent(free));
                    break;
                }
            }
        }
    }

    /// <summary>Frees the beds the colonist owns, which it leaves for another.</summary>
    private static void FreeBedsOf(Simulation simulation, EntityId colonist)
    {
        var beds = new List<EntityId>();
        simulation.World.Query<Bed>(beds);
        foreach (var bed in beds)
        {
            if (simulation.World.Get<Bed>(bed).Owner == colonist)
            {
                simulation.World.Get<Bed>(bed).Owner = EntityId.None;
                simulation.Events.Emit(new EntityChangedEvent(bed));
            }
        }
    }

    /// <summary>
    /// The relatives a colonist lives with, closest first: its partner, its parents (named even if gone, to find the
    /// room they had), its children by ascending id and its siblings by ascending id.
    /// </summary>
    private static List<Kin> KinOf(Simulation simulation, EntityId colonist)
    {
        var world = simulation.World;
        var kin = new List<Kin>();
        if (!world.TryGet<Family>(colonist, out var family))
        {
            return kin;
        }

        if (Relationships.PartnerOf(simulation, colonist) is { IsNone: false } partner)
        {
            kin.Add(new Kin(partner, KinKind.Partner));
        }

        foreach (var parent in new[] { family.Mother, family.Father })
        {
            if (parent is { Entity.IsNone: false })
            {
                kin.Add(new Kin(parent.Entity, KinKind.Parent));
            }
        }

        var colonists = new List<EntityId>();
        world.Query<Colonist, Family>(colonists);
        foreach (var other in colonists)
        {
            var theirs = world.Get<Family>(other);
            if (other != colonist && (theirs.Father?.Entity == colonist || theirs.Mother?.Entity == colonist))
            {
                kin.Add(new Kin(other, KinKind.Child));
            }
        }

        foreach (var sibling in Relationships.SiblingsOf(simulation, colonist))
        {
            if (!kin.Exists(each => each.Entity == sibling))
            {
                kin.Add(new Kin(sibling, KinKind.Sibling));
            }
        }

        return kin;
    }

    private enum KinKind
    {
        Partner,
        Parent,
        Child,
        Sibling,
    }

    private readonly record struct Kin(EntityId Entity, KinKind Kind);
}
