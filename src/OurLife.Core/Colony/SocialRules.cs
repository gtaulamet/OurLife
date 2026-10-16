using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
using OurLife.Core.Text;

namespace OurLife.Core.Colony;

/// <summary>
/// The content of interactions and relations (see <c>docs/gdd/vida-y-generaciones.md</c>), checked
/// once, and the changes of opinion with what follows from them: friendships and rivalries told
/// when they start, couples formed and broken.
/// </summary>
public sealed class SocialRules
{
    public const string FriendsKey = "event.social.friends";
    public const string RivalsKey = "event.social.rivals";
    public const string CoupleKey = "event.social.couple";
    public const string BreakupKey = "event.social.breakup";

    private const int MaxOpinion = 100;
    private const int MaxRomance = 100;

    private SocialRules(SocialConfigDef config, IReadOnlyList<InteractionDef> interactions)
    {
        Config = config;
        Interactions = interactions;
    }

    public SocialConfigDef Config { get; }

    /// <summary>The interactions of the content, by id.</summary>
    public IReadOnlyList<InteractionDef> Interactions { get; }

    /// <exception cref="InvalidOperationException">The content has no SocialConfig or several.</exception>
    public static SocialRules From(DefDatabase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new SocialRules(Relationships.ConfigOf(content), content.All<InteractionDef>());
    }

    /// <summary>
    /// Colonists who arrive together know each other a little: each thinks of each other a drawn
    /// opinion, in ascending order of both ids. With a <paramref name="couple"/>, the first two
    /// arrive as a couple.
    /// </summary>
    internal void Introduce(Simulation simulation, IReadOnlyList<EntityId> colonists, FoundingCouple? couple, Pcg32 random)
    {
        foreach (var colonist in colonists)
        {
            foreach (var other in colonists)
            {
                if (other != colonist)
                {
                    Relationships.Ensure(simulation, colonist, other).Opinion = Math.Clamp(
                        random.NextInt(Config.FounderOpinion.Min, Config.FounderOpinion.Max + 1), -MaxOpinion, MaxOpinion);
                }
            }
        }

        if (couple is not null && colonists.Count >= 2)
        {
            var (a, b) = (colonists[0], colonists[1]);
            foreach (var (from, to) in new[] { (a, b), (b, a) })
            {
                var relation = Relationships.Ensure(simulation, from, to);
                relation.Opinion = Math.Clamp(couple.Opinion, -MaxOpinion, MaxOpinion);
                relation.Romance = Math.Clamp(couple.Romance, 0, MaxRomance);
            }

            Join(simulation, a, b);
        }
    }

    /// <summary>
    /// Changes what two colonists think of each other, then sees what follows: a couple that breaks
    /// up or forms, and a friendship or rivalry that starts, told in the log.
    /// </summary>
    internal void Change(Simulation simulation, EntityId a, EntityId b, int aOpinion, int bOpinion, int romance = 0)
    {
        Move(simulation, a, b, aOpinion, romance);
        Move(simulation, b, a, bOpinion, romance);

        var partner = Relationships.PartnerOf(simulation, a) == b;
        if (partner && (Relationships.Opinion(simulation, a, b) < Config.BreakupBelow || Relationships.Opinion(simulation, b, a) < Config.BreakupBelow))
        {
            Split(simulation, a, b);
        }
        else if (!partner && CanFormCouple(simulation, a, b))
        {
            Join(simulation, a, b);
            MoodRules.Feel(simulation, a, config => config.NewPartner);
            MoodRules.Feel(simulation, b, config => config.NewPartner);
            Notify(simulation, CoupleKey, a, b);
            return;
        }

        Tell(simulation, a, b, Relationships.Kind(simulation, a, b));
    }

    /// <summary>
    /// Tells a friendship or rivalry that starts, unless it was told and has not really ended:
    /// it ends for the log once both opinions are <see cref="SocialConfigDef.RetellMargin"/> back past
    /// the threshold.
    /// </summary>
    private void Tell(Simulation simulation, EntityId a, EntityId b, RelationKind kind)
    {
        var ab = Relationships.Ensure(simulation, a, b);
        var ba = Relationships.Ensure(simulation, b, a);
        var tie = kind switch
        {
            RelationKind.Friend => Tie.Friends,
            RelationKind.Rival => Tie.Rivals,
            _ => Tie.None,
        };

        if (tie != Tie.None && ab.Told != tie)
        {
            ab.Told = ba.Told = tie;
            Notify(simulation, tie == Tie.Friends ? FriendsKey : RivalsKey, a, b);
        }
        else if (tie == Tie.None
                 && ((ab.Told == Tie.Friends && Math.Max(ab.Opinion, ba.Opinion) < Config.FriendOpinion - Config.RetellMargin)
                     || (ab.Told == Tie.Rivals && Math.Min(ab.Opinion, ba.Opinion) > Config.RivalOpinion + Config.RetellMargin)))
        {
            ab.Told = ba.Told = Tie.None;
        }
    }

    private static void Move(Simulation simulation, EntityId from, EntityId to, int opinion, int romance)
    {
        var relation = Relationships.Ensure(simulation, from, to);
        relation.Opinion = Math.Clamp(relation.Opinion + opinion, -MaxOpinion, MaxOpinion);
        relation.Romance = Math.Clamp(relation.Romance + romance, 0, MaxRomance);
    }

    private bool CanFormCouple(Simulation simulation, EntityId a, EntityId b)
    {
        bool Wants(EntityId from, EntityId to) =>
            Relationships.Of(simulation, from, to) is { } relation
            && relation.Opinion >= Config.CoupleOpinion && relation.Romance >= Config.CoupleRomance
            && Relationships.IsAttracted(simulation, from, to) && Relationships.IsFree(simulation, from);

        return Wants(a, b) && Wants(b, a) && !Relationships.IsFamily(simulation, a, b);
    }

    /// <summary>The two become a couple; a partner who died goes to the late ones.</summary>
    private static void Join(Simulation simulation, EntityId a, EntityId b)
    {
        foreach (var (colonist, partner) in new[] { (a, b), (b, a) })
        {
            var family = FamilyOf(simulation, colonist);
            if (family.Partner is { Dead: true } late)
            {
                family.LatePartners.Add(late);
            }

            family.Partner = Relationships.RelativeOf(simulation, partner);
            family.WidowedAtAgeDays = null;
        }
    }

    /// <summary>The couple breaks up, and the romance is gone: to be a couple again, they must court again.</summary>
    private void Split(Simulation simulation, EntityId a, EntityId b)
    {
        FamilyOf(simulation, a).Partner = null;
        FamilyOf(simulation, b).Partner = null;
        Relationships.Ensure(simulation, a, b).Romance = 0;
        Relationships.Ensure(simulation, b, a).Romance = 0;
        MoodRules.Feel(simulation, a, config => config.Breakup);
        MoodRules.Feel(simulation, b, config => config.Breakup);
        Notify(simulation, BreakupKey, a, b);
    }

    private static Family FamilyOf(Simulation simulation, EntityId colonist)
    {
        if (!simulation.World.TryGet<Family>(colonist, out var family))
        {
            family = new Family();
            simulation.World.Add(colonist, family);
        }

        return family;
    }

    private static void Notify(Simulation simulation, string key, EntityId a, EntityId b) =>
        simulation.Events.Emit(new NotificationEvent(
            simulation.CurrentTick,
            ColonyText.Freeze(LocalizedText.Of(key).With("a", a).With("b", b), simulation.World),
            NotificationSeverity.Info));
}
