using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>A bed suggested or given to a colonist.</summary>
public readonly record struct BedAssignment(EntityId Bed, EntityId Colonist);

/// <summary>
/// Read-only queries about the quality of rooms and who sleeps in them, and the thoughts rooms leave
/// (see <c>docs/gdd/habitaciones.md</c>). The room itself comes from <see cref="Rooms"/>.
/// </summary>
public static class RoomRules
{
    /// <summary>The id of the role of the rooms where colonists eat sitting and gather.</summary>
    public const string DiningRole = "dining";

    /// <summary>The tag of furniture to sit on.</summary>
    public const string SeatTag = "seat";

    /// <summary>The tag of furniture to eat at: a chair needs one beside it.</summary>
    public const string TableTag = "table";

    /// <summary>The points of a room: its size, furniture, floors and walls.</summary>
    public static int Score(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);
        return room.Score;
    }

    /// <summary>The level of quality of a room by its points.</summary>
    public static RoomLevelDef Level(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);
        return room.Level;
    }

    /// <summary>The functions of a room, from what it holds.</summary>
    public static IReadOnlyList<RoomRoleDef> Roles(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);
        return room.Roles;
    }

    /// <summary>The living colonists whose beds are in the room, by the ascending id of the bed, each once.</summary>
    public static IReadOnlyList<EntityId> Owners(Simulation simulation, Room room)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(room);
        var owners = new List<EntityId>();
        foreach (var bed in room.Beds)
        {
            if (Beds.IsTaken(simulation, bed))
            {
                var owner = simulation.World.Get<Bed>(bed).Owner;
                if (!owners.Contains(owner))
                {
                    owners.Add(owner);
                }
            }
        }

        return owners;
    }

    /// <summary>
    /// A bed of the room for the colonist and for each of its living partner, children and siblings (in that order,
    /// children and siblings by ascending id), the colonist's own first and those for their stage of life before the
    /// rest (a cradle for a baby), and free ones before those of someone else. Nobody gets a bed twice, and one
    /// that fits nobody left is left out. Asking changes nothing: the presentation sends an
    /// <see cref="AssignBedCommand"/> for each.
    /// </summary>
    public static IReadOnlyList<BedAssignment> SuggestAssignments(Simulation simulation, Room room, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(room);
        var world = simulation.World;
        var assignments = new List<BedAssignment>();
        if (!Relationships.IsLivingColonist(world, colonist))
        {
            return assignments;
        }

        var people = new List<EntityId> { colonist };
        if (Relationships.PartnerOf(simulation, colonist) is { IsNone: false } partner)
        {
            people.Add(partner);
        }

        var everyone = new List<EntityId>();
        world.Query<Colonist, Family>(everyone);
        foreach (var other in everyone)
        {
            var family = world.Get<Family>(other);
            if (other != colonist && (family.Father?.Entity == colonist || family.Mother?.Entity == colonist) && !people.Contains(other))
            {
                people.Add(other);
            }
        }

        foreach (var sibling in Relationships.SiblingsOf(simulation, colonist))
        {
            if (!people.Contains(sibling))
            {
                people.Add(sibling);
            }
        }

        var used = new List<EntityId>();
        foreach (var person in people)
        {
            var best = EntityId.None;
            var bestRank = int.MaxValue;
            foreach (var bed in room.Beds)
            {
                if (used.Contains(bed) || !Beds.Fits(simulation, bed, person))
                {
                    continue;
                }

                var owner = world.Get<Bed>(bed).Owner;
                var ownerRank = owner == person ? 0 : Beds.IsTaken(simulation, bed) ? 2 : 1;
                var stages = simulation.Content.Get<BuildableDef>(world.Get<Furniture>(bed).Buildable).ForStages;
                var rank = (ownerRank * 2) + (stages.Count > 0 ? 0 : 1);
                if (rank < bestRank)
                {
                    (best, bestRank) = (bed, rank);
                }
            }

            if (!best.IsNone)
            {
                used.Add(best);
                assignments.Add(new BedAssignment(best, person));
            }
        }

        return assignments;
    }

    /// <summary>
    /// What a colonist thinks on waking in a bed of a room: the room's quality, and whether it slept among
    /// family or crowded in with strangers. The level thoughts replace one another, so a better bedroom
    /// does not add to the one of the night before.
    /// </summary>
    internal static void FeelWaking(Simulation simulation, EntityId colonist, Room room)
    {
        if (MoodRules.ConfigOf(simulation.Content) is not { } mood || RoomSetup.From(simulation.Content) is not { } setup)
        {
            return;
        }

        FeelLevel(simulation, colonist, room.Level.BedroomThought, setup.Levels.Select(level => level.BedroomThought));
        var owners = Owners(simulation, room);
        var related = 0;
        foreach (var owner in owners)
        {
            if (owners.Any(other => other != owner && Relationships.IsFamily(simulation, owner, other)))
            {
                related++;
            }
        }

        // The beds in use: one owner per bed, as a colonist has a single bed.
        var strangers = owners.Count - related > 0;
        var thought = owners.Count >= setup.Config.CrowdedBeds && strangers ? mood.SleptCrowded
            : owners.Count >= 2 && !strangers ? mood.FamilyHome
            : null;
        FeelLevel(simulation, colonist, thought, [mood.SleptCrowded, mood.FamilyHome]);
    }

    /// <summary>What a colonist thinks after eating sitting in a dining room: that it ate at a table, and the room's quality.</summary>
    internal static void FeelAteAtTable(Simulation simulation, EntityId colonist, Room? room)
    {
        if (MoodRules.ConfigOf(simulation.Content) is not { } mood)
        {
            return;
        }

        MoodRules.Feel(simulation, colonist, mood.AteAtTable);
        if (room is not null && RoomSetup.From(simulation.Content) is { } setup)
        {
            FeelLevel(simulation, colonist, room.Level.DiningThought, setup.Levels.Select(level => level.DiningThought));
        }
    }

    /// <summary>Leaves a thought of a group of which only one holds at a time: the others of the group go.</summary>
    private static void FeelLevel(Simulation simulation, EntityId colonist, string? thought, IEnumerable<string?> group)
    {
        if (simulation.World.TryGet<Mood>(colonist, out var state))
        {
            foreach (var other in group)
            {
                if (other is not null && other != thought)
                {
                    state.Thoughts.RemoveAll(active => active.Thought == other);
                }
            }
        }

        MoodRules.Feel(simulation, colonist, thought);
    }
}
