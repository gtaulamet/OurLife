using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// Once a day, in the morning, gathers each family under one roof (see "Dormitorios" in <c>docs/gdd/habitaciones.md</c>):
/// a couple sleeps in the same room, and the children that are not grown up with their parents, if the room has
/// the beds. The bond of the couple comes first: when the room has no free bed for them, a colonist who has no
/// kin sleeping in that room gives up its bed, and finds another when it goes to sleep. Nobody else is moved: a
/// grown child keeps its own bed, and a family without room stays as it is. Only the owners of the beds change;
/// colonists walk to their beds when they go to sleep. Draws no random numbers and takes colonists by ascending id.
/// </summary>
public sealed class HouseholdSystem : ISimSystem
{
    /// <summary>The hour of the day the families are gathered, when almost nobody is asleep.</summary>
    public const int Hour = 10;

    /// <summary>The offset to run <see cref="HouseholdSystem"/> once per day at <see cref="Hour"/>.</summary>
    public static int DailyOffset(TimeSettings time)
    {
        ArgumentNullException.ThrowIfNull(time);
        var minutes = (((Hour * 60) - (time.StartHour * 60)) % TimeSettings.MinutesPerDay + TimeSettings.MinutesPerDay) % TimeSettings.MinutesPerDay;
        return minutes / time.MinutesPerTick;
    }

    public void Tick(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (!Rooms.IsEnabled(simulation))
        {
            return;
        }

        var world = simulation.World;
        var colonists = new List<EntityId>();
        world.Query<Colonist, Family>(colonists);
        var placed = new HashSet<EntityId>();
        foreach (var head in colonists)
        {
            if (placed.Contains(head))
            {
                continue;
            }

            var adults = new List<EntityId> { head };
            if (Relationships.PartnerOf(simulation, head) is { IsNone: false } partner && !placed.Contains(partner))
            {
                adults.Add(partner);
            }

            var minors = new List<EntityId>();
            foreach (var other in colonists)
            {
                var family = world.Get<Family>(other);
                if (!placed.Contains(other) && !adults.Contains(other) && !Beds.IsGrownUp(simulation, other)
                    && (adults.Contains(family.Father?.Entity ?? EntityId.None) || adults.Contains(family.Mother?.Entity ?? EntityId.None)))
                {
                    minors.Add(other);
                }
            }

            if (adults.Count == 1 && minors.Count == 0)
            {
                continue;
            }

            placed.UnionWith(adults);
            placed.UnionWith(minors);
            Gather(simulation, adults, minors, placed);
        }
    }

    private static void Gather(Simulation simulation, List<EntityId> adults, List<EntityId> minors, HashSet<EntityId> placed)
    {
        var world = simulation.World;
        var rooms = new List<Room>();
        foreach (var adult in adults)
        {
            var bed = Beds.OwnedBy(simulation, adult);
            if (!bed.IsNone && Rooms.At(simulation, world.Get<Position>(bed).Cell) is { } room && !rooms.Exists(each => each.Id == room.Id))
            {
                rooms.Add(room);
            }
        }

        foreach (var room in rooms)
        {
            var beds = BedsFor(simulation, room, adults, placed);
            if (beds is null)
            {
                continue;
            }

            foreach (var (person, bed) in beds)
            {
                Give(simulation, person, bed);
            }

            foreach (var minor in minors)
            {
                if (BedIn(simulation, room, minor) is { IsNone: false })
                {
                    continue;
                }

                if (FreeBed(simulation, room, minor, taken: []) is { IsNone: false } free)
                {
                    Give(simulation, minor, free);
                }
            }

            return;
        }
    }

    /// <summary>
    /// The beds of the room for the people that do not have one there yet, or null if they do not fit: free beds first,
    /// then those of a colonist who has no kin sleeping in the room.
    /// </summary>
    private static List<(EntityId Person, EntityId Bed)>? BedsFor(Simulation simulation, Room room, List<EntityId> people, HashSet<EntityId> placed)
    {
        var chosen = new List<(EntityId, EntityId)>();
        var taken = new List<EntityId>();
        foreach (var person in people)
        {
            if (BedIn(simulation, room, person) is { IsNone: false } own)
            {
                taken.Add(own);
            }
        }

        foreach (var person in people)
        {
            if (BedIn(simulation, room, person) is { IsNone: false })
            {
                continue;
            }

            var bed = FreeBed(simulation, room, person, taken);
            if (bed.IsNone)
            {
                bed = EvictableBed(simulation, room, person, taken, people, placed);
            }

            if (bed.IsNone)
            {
                return null;
            }

            taken.Add(bed);
            chosen.Add((person, bed));
        }

        return chosen;
    }

    private static EntityId BedIn(Simulation simulation, Room room, EntityId person)
    {
        foreach (var bed in room.Beds)
        {
            if (simulation.World.Get<Bed>(bed).Owner == person && Beds.Fits(simulation, bed, person))
            {
                return bed;
            }
        }

        return EntityId.None;
    }

    /// <summary>A free bed of the room for the colonist that is not in <paramref name="taken"/>: one made for its stage of life (a cradle) first, then the lowest id.</summary>
    private static EntityId FreeBed(Simulation simulation, Room room, EntityId person, List<EntityId> taken) =>
        room.Beds.Where(bed => !taken.Contains(bed) && !Beds.IsTaken(simulation, bed) && Beds.Fits(simulation, bed, person))
            .OrderBy(bed => simulation.Content.Get<BuildableDef>(simulation.World.Get<Furniture>(bed).Buildable).ForStages.Count > 0 ? 0 : 1)
            .FirstOrDefault();

    private static EntityId EvictableBed(Simulation simulation, Room room, EntityId person, List<EntityId> taken, List<EntityId> unit, HashSet<EntityId> placed)
    {
        var world = simulation.World;
        foreach (var bed in room.Beds)
        {
            var owner = world.Get<Bed>(bed).Owner;
            if (!taken.Contains(bed) && Beds.IsTaken(simulation, bed) && Beds.Fits(simulation, bed, person)
                && !unit.Contains(owner) && !placed.Contains(owner) && !HasKinIn(simulation, room, owner))
            {
                return bed;
            }
        }

        return EntityId.None;
    }

    /// <summary>Whether the partner, a parent or a child of the colonist sleeps in the room.</summary>
    private static bool HasKinIn(Simulation simulation, Room room, EntityId colonist)
    {
        var world = simulation.World;
        foreach (var bed in room.Beds)
        {
            var owner = world.Get<Bed>(bed).Owner;
            if (owner != colonist && Relationships.IsLivingColonist(world, owner) && Relationships.IsFamily(simulation, colonist, owner)
                && !Relationships.SiblingsOf(simulation, colonist).Contains(owner))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Makes the bed the colonist's only one: the others it had are freed, and whoever had this one loses it.</summary>
    private static void Give(Simulation simulation, EntityId colonist, EntityId bed)
    {
        var owned = new List<EntityId>();
        simulation.World.Query<Bed>(owned);
        foreach (var each in owned)
        {
            var current = simulation.World.Get<Bed>(each);
            if (each != bed && current.Owner == colonist)
            {
                current.Owner = EntityId.None;
                simulation.Events.Emit(new EntityChangedEvent(each));
            }
        }

        simulation.World.Get<Bed>(bed).Owner = colonist;
        simulation.Events.Emit(new EntityChangedEvent(bed));
    }
}
