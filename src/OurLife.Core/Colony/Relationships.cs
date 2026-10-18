using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>How two colonists are related, from the closest tie: the first that holds.</summary>
public enum RelationKind
{
    None,
    Partner,
    Family,
    Friend,
    Rival,
}

/// <summary>
/// Read-only queries about what colonists think of each other and how they are related (see
/// "Relaciones y familia" in <c>docs/gdd/vida-y-generaciones.md</c>), for the systems and the
/// presentation, plus the few writes the social systems share.
/// </summary>
public static class Relationships
{
    /// <summary>What <paramref name="from"/> thinks of <paramref name="to"/>, or null if it does not know it.</summary>
    public static Relation? Of(Simulation simulation, EntityId from, EntityId to)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return simulation.World.TryGet<Relations>(from, out var relations) ? relations.Find(to) : null;
    }

    /// <summary>The opinion of <paramref name="from"/> about <paramref name="to"/>; 0 for a stranger.</summary>
    public static int Opinion(Simulation simulation, EntityId from, EntityId to) => Of(simulation, from, to)?.Opinion ?? 0;

    /// <summary>The closest tie between two colonists.</summary>
    public static RelationKind Kind(Simulation simulation, EntityId a, EntityId b)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var config = Config(simulation);
        if (PartnerOf(simulation, a) == b && !b.IsNone)
        {
            return RelationKind.Partner;
        }

        if (IsFamily(simulation, a, b))
        {
            return RelationKind.Family;
        }

        var ab = Opinion(simulation, a, b);
        var ba = Opinion(simulation, b, a);
        if (ab >= config.FriendOpinion && ba >= config.FriendOpinion)
        {
            return RelationKind.Friend;
        }

        return ab <= config.RivalOpinion || ba <= config.RivalOpinion ? RelationKind.Rival : RelationKind.None;
    }

    /// <summary>The living partner of a colonist, or none.</summary>
    public static EntityId PartnerOf(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return simulation.World.TryGet<Family>(colonist, out var family) && family.Partner is { Dead: false } partner
               && simulation.World.IsAlive(partner.Entity)
            ? partner.Entity
            : EntityId.None;
    }

    /// <summary>
    /// Whether two colonists are family: a parent and a child, brothers or sisters (a father or a
    /// mother in common), or a couple.
    /// </summary>
    public static bool IsFamily(Simulation simulation, EntityId a, EntityId b)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (a == b)
        {
            return false;
        }

        var world = simulation.World;
        world.TryGet<Family>(a, out var ofA);
        world.TryGet<Family>(b, out var ofB);
        if (ofA is null && ofB is null)
        {
            return false;
        }

        return Names(ofA?.Partner, b) || Names(ofA?.Father, b) || Names(ofA?.Mother, b) || Names(ofB?.Father, a) || Names(ofB?.Mother, a)
               || (ofA is not null && ofB is not null && SameParent(ofA, ofB));
    }

    /// <summary>The living brothers and sisters of a colonist: those with a father or a mother in common, by ascending id.</summary>
    public static IReadOnlyList<EntityId> SiblingsOf(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        if (!world.TryGet<Family>(colonist, out var own) || (own.Father is null && own.Mother is null))
        {
            return [];
        }

        var colonists = new List<EntityId>();
        world.Query<Colonist, Family>(colonists);
        return [.. colonists.Where(other => other != colonist && SameParent(own, world.Get<Family>(other)))];
    }

    /// <summary>Whether two families name the same father or the same mother; a parent without an entity matches nobody.</summary>
    private static bool SameParent(Family a, Family b) =>
        (a.Father is { Entity.IsNone: false } father && b.Father?.Entity == father.Entity)
        || (a.Mother is { Entity.IsNone: false } mother && b.Mother?.Entity == mother.Entity);

    /// <summary>Whether <paramref name="from"/> is attracted to <paramref name="to"/> by its orientation; a colonist without one, to the other sex.</summary>
    public static bool IsAttracted(Simulation simulation, EntityId from, EntityId to)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var same = world.Get<Colonist>(from).Gender == world.Get<Colonist>(to).Gender;
        var orientation = world.TryGet<Personality>(from, out var personality) ? personality.Orientation : Orientation.Opposite;
        return orientation switch
        {
            Orientation.Opposite => !same,
            Orientation.Same => same,
            _ => true,
        };
    }

    /// <summary>
    /// Whether a colonist may court and form a couple: without a living partner and, after losing
    /// one, once <see cref="SocialConfigDef.WidowYears"/> of life have passed.
    /// </summary>
    public static bool IsFree(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (!PartnerOf(simulation, colonist).IsNone)
        {
            return false;
        }

        var world = simulation.World;
        return !(world.TryGet<Family>(colonist, out var family) && family.WidowedAtAgeDays is { } widowed
                 && world.TryGet<Life>(colonist, out var life)
                 && life.AgeDays - widowed < Config(simulation).WidowYears * simulation.Time.DaysPerYear);
    }

    /// <summary>The relation of <paramref name="from"/> towards <paramref name="to"/>, created at 0 if it did not know it.</summary>
    internal static Relation Ensure(Simulation simulation, EntityId from, EntityId to)
    {
        var world = simulation.World;
        if (!world.TryGet<Relations>(from, out var relations))
        {
            relations = new Relations();
            world.Add(from, relations);
        }

        if (relations.Find(to) is { } relation)
        {
            return relation;
        }

        relation = new Relation { Other = to };
        var at = relations.Others.FindIndex(other => other.Other.CompareTo(to) > 0);
        relations.Others.Insert(at < 0 ? relations.Others.Count : at, relation);
        return relation;
    }

    /// <summary>The colonist as a member of someone's family, with its name in case it dies.</summary>
    internal static Relative RelativeOf(Simulation simulation, EntityId colonist)
    {
        var person = simulation.World.Get<Colonist>(colonist);
        return new Relative { Entity = colonist, Name = person.Name, Surname = person.Surname, Gender = person.Gender };
    }

    /// <summary>
    /// A colonist is gone: each other one mourns it, by how close they were, then forgets what it
    /// thought of it, and the family keeps its name as dead. Its partner is a widow or widower from now.
    /// </summary>
    internal static void ForgetTheDead(Simulation simulation, EntityId dead)
    {
        var world = simulation.World;
        var colonists = new List<EntityId>();
        world.Query<Colonist>(colonists);
        foreach (var colonist in colonists)
        {
            if (colonist == dead)
            {
                continue;
            }

            Mourn(simulation, colonist, dead);
            if (world.TryGet<Relations>(colonist, out var relations))
            {
                relations.Others.RemoveAll(relation => relation.Other == dead);
            }

            if (world.TryGet<RecentInteractions>(colonist, out var recent))
            {
                recent.Entries.RemoveAll(entry => entry.Other == dead);
            }

            if (world.TryGet<Pregnancy>(colonist, out var pregnancy) && pregnancy.Father.Entity == dead)
            {
                pregnancy.Father.Dead = true;
            }

            if (!world.TryGet<Family>(colonist, out var family))
            {
                continue;
            }

            foreach (var relative in new[] { family.Father, family.Mother, family.Partner }.Concat(family.Children))
            {
                if (relative is not null && relative.Entity == dead)
                {
                    relative.Dead = true;
                }
            }

            if (family.Partner is { Dead: true } partner && partner.Entity == dead)
            {
                family.WidowedAtAgeDays = world.TryGet<Life>(colonist, out var life) ? life.AgeDays : 0;
            }
        }
    }

    /// <summary>
    /// The thought of a death, by the closest tie to the dead (see "Pensamientos y ánimo" in
    /// <c>docs/gdd/vida-y-generaciones.md</c>); losing family also moves the personality.
    /// </summary>
    private static void Mourn(Simulation simulation, EntityId colonist, EntityId dead)
    {
        if (MoodRules.ConfigOf(simulation.Content) is not { } config)
        {
            return;
        }

        var family = simulation.World.TryGet<Family>(colonist, out var own) ? own : null;
        var social = Config(simulation);
        var relatives = IsFamily(simulation, colonist, dead);
        var friends = Opinion(simulation, colonist, dead) >= social.FriendOpinion && Opinion(simulation, dead, colonist) >= social.FriendOpinion;
        var thought =
            family?.Partner?.Entity == dead ? config.DeathPartner
            : family?.Children.Any(child => child.Entity == dead) == true ? config.DeathChild
            : family?.Father?.Entity == dead || family?.Mother?.Entity == dead ? config.DeathParent
            : relatives ? config.DeathSibling
            : friends ? config.DeathFriend
            : config.DeathColonist;
        MoodRules.Feel(simulation, colonist, thought);

        // Rare enough that building the rules for it costs nothing.
        if (config.Grief is { } grief && relatives && simulation.Content.All<PersonalityConfigDef>().Count == 1)
        {
            PersonalityRules.From(simulation.Content).Drift(simulation, colonist, grief.Trait, grief.Points);
        }
    }

    internal static SocialConfigDef Config(Simulation simulation) => ConfigOf(simulation.Content);

    /// <exception cref="InvalidOperationException">The content has no SocialConfig or several.</exception>
    internal static SocialConfigDef ConfigOf(DefDatabase content)
    {
        var configs = content.All<SocialConfigDef>();
        return configs.Count == 1
            ? configs[0]
            : throw new InvalidOperationException($"The colony needs exactly one SocialConfig def, found {configs.Count}.");
    }

    private static bool Names(Relative? relative, EntityId colonist) => relative is { } r && r.Entity == colonist && !colonist.IsNone;
}
