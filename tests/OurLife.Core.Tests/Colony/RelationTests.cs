using OurLife.Core.Colony;
using OurLife.Core.Entities;

namespace OurLife.Core.Tests.Colony;

/// <summary>Founders, couples, forgetting and the dead (H3.5, "Relaciones y familia" in <c>docs/gdd/vida-y-generaciones.md</c>).</summary>
public class RelationTests
{
    private const int TicksPerDay = 24 * 60;

    [Fact]
    public void The_founders_know_each_other_a_little_and_two_of_them_arrive_as_a_couple()
    {
        var bed = ColonyTestBed.Flat(15, seed: 4);

        var colonists = ColonyStart.Settle(bed.Simulation, ColonyStart.TheOnlyScenario(bed.Simulation.Content), "Valdeolmos");

        var (wife, husband) = (colonists[0], colonists[1]);
        Relationships.Kind(bed.Simulation, wife, husband).ShouldBe(RelationKind.Partner);
        Relationships.PartnerOf(bed.Simulation, husband).ShouldBe(wife);
        Relationships.IsAttracted(bed.Simulation, wife, husband).ShouldBeTrue();
        Relationships.IsAttracted(bed.Simulation, husband, wife).ShouldBeTrue();
        foreach (var (from, to) in new[] { (wife, husband), (husband, wife) })
        {
            Relationships.Of(bed.Simulation, from, to)!.Opinion.ShouldBe(70);
            Relationships.Of(bed.Simulation, from, to)!.Romance.ShouldBe(100);
            LifeRules.AgeYears(bed.Simulation, from)!.Value.ShouldBeInRange(20, 30);
        }

        foreach (var colonist in colonists)
        {
            var others = bed.World.Get<Relations>(colonist).Others;
            others.Select(relation => relation.Other).ShouldBe(colonists.Where(other => other != colonist));
            others.Where(relation => relation.Romance == 0).ShouldAllBe(relation => relation.Opinion >= -10 && relation.Opinion <= 20);
        }
    }

    [Fact]
    public void Two_who_love_each_other_form_a_couple_and_split_when_one_turns_against_the_other()
    {
        var (bed, a, b) = SocialTests.TwoInAHut(1, 50, 50, 50);
        var rules = SocialRules.From(bed.Simulation.Content);
        Love(bed, a, b, 60, 95);

        rules.Change(bed.Simulation, a, b, 1, 1, romance: 5);

        Relationships.Kind(bed.Simulation, a, b).ShouldBe(RelationKind.Partner);
        Notices(bed).ShouldBe(new[] { SocialRules.CoupleKey });

        rules.Change(bed.Simulation, a, b, -70, 0);

        Relationships.PartnerOf(bed.Simulation, a).IsNone.ShouldBeTrue();
        Relationships.PartnerOf(bed.Simulation, b).IsNone.ShouldBeTrue();
        Notices(bed).ShouldBe(new[] { SocialRules.BreakupKey });
    }

    [Fact]
    public void After_a_breakup_the_romance_is_gone_and_liking_each_other_again_is_not_enough()
    {
        var (bed, a, b) = SocialTests.TwoInAHut(1, 50, 50, 50);
        var rules = SocialRules.From(bed.Simulation.Content);
        Love(bed, a, b, 60, 100);
        rules.Change(bed.Simulation, a, b, 0, 0);
        rules.Change(bed.Simulation, a, b, -70, 0);
        Notices(bed);

        rules.Change(bed.Simulation, a, b, 80, 10);

        Relationships.PartnerOf(bed.Simulation, a).IsNone.ShouldBeTrue();
        Relationships.Of(bed.Simulation, a, b)!.Romance.ShouldBe(0);
        Notices(bed).ShouldNotContain(SocialRules.CoupleKey);
    }

    [Fact]
    public void Family_never_becomes_a_couple()
    {
        var (bed, mother, son) = SocialTests.TwoInAHut(1, 50, 50, 50);
        bed.World.Add(son, new Family { Mother = Relationships.RelativeOf(bed.Simulation, mother) });
        Love(bed, mother, son, 90, 100);

        SocialRules.From(bed.Simulation.Content).Change(bed.Simulation, mother, son, 1, 1);

        Relationships.Kind(bed.Simulation, mother, son).ShouldBe(RelationKind.Family);
    }

    [Fact]
    public void A_couple_needs_both_to_be_attracted_and_two_women_who_are_can_form_one()
    {
        var (bed, a, b) = SocialTests.TwoInAHut(1, 50, 50, 50, Gender.Female);
        var rules = SocialRules.From(bed.Simulation.Content);
        Love(bed, a, b, 80, 100);

        // Attracted to men, as they are without saying otherwise.
        rules.Change(bed.Simulation, a, b, 1, 1);
        Relationships.Kind(bed.Simulation, a, b).ShouldBe(RelationKind.Friend);

        bed.World.Get<Personality>(a).Orientation = Orientation.Same;
        rules.Change(bed.Simulation, a, b, 1, 1);
        Relationships.Kind(bed.Simulation, a, b).ShouldBe(RelationKind.Friend);

        bed.World.Get<Personality>(b).Orientation = Orientation.Both;
        rules.Change(bed.Simulation, a, b, 1, 1);
        Relationships.Kind(bed.Simulation, a, b).ShouldBe(RelationKind.Partner);
    }

    [Fact]
    public void A_friendship_that_comes_and_goes_is_told_once_until_it_really_ends()
    {
        var (bed, a, b) = SocialTests.TwoInAHut(1, 50, 50, 50, Gender.Female);
        var rules = SocialRules.From(bed.Simulation.Content);
        Love(bed, a, b, 39, 0);

        rules.Change(bed.Simulation, a, b, 1, 1);
        rules.Change(bed.Simulation, a, b, -2, -2);
        rules.Change(bed.Simulation, a, b, 2, 2);
        Notices(bed).ShouldBe(new[] { SocialRules.FriendsKey });

        rules.Change(bed.Simulation, a, b, -12, -12);
        rules.Change(bed.Simulation, a, b, 12, 12);
        Notices(bed).ShouldBe(new[] { SocialRules.FriendsKey });
    }

    [Fact]
    public void Opinions_fade_without_meeting_but_not_between_family_under_thirty()
    {
        // Each one walled in, out of reach of the others.
        var bed = new ColonyTestBed(ColonyTestBed.Level(13, 1, '#'), ".#####.#####.");
        var a = bed.AddColonist(0, 0, 1);
        var b = bed.AddColonist(6, 0, 1, name: "Urraca");
        var child = bed.AddColonist(12, 0, 1, name: "Jimena");
        Relationships.Ensure(bed.Simulation, a, b).Opinion = 20;
        Relationships.Ensure(bed.Simulation, b, a).Opinion = -20;
        Relationships.Ensure(bed.Simulation, a, child).Opinion = 31;
        bed.World.Add(child, new Family { Mother = Relationships.RelativeOf(bed.Simulation, a) });

        // Two midnights.
        bed.Simulation.Run(LifeSystem.MidnightOffset(bed.Simulation.Time) + TicksPerDay + 1);

        Relationships.Opinion(bed.Simulation, a, b).ShouldBe(18);
        Relationships.Opinion(bed.Simulation, b, a).ShouldBe(-18);
        Relationships.Opinion(bed.Simulation, a, child).ShouldBe(30);
        Relationships.Kind(bed.Simulation, a, child).ShouldBe(RelationKind.Family);
    }

    [Fact]
    public void The_dead_are_forgotten_by_all_and_kept_by_their_family_and_a_widow_waits_a_year()
    {
        var bed = ColonyTestBed.Flat(15, seed: 4);
        var colonists = ColonyStart.Settle(bed.Simulation, ColonyStart.TheOnlyScenario(bed.Simulation.Content), "Valdeolmos");
        var (wife, husband, other) = (colonists[0], colonists[1], colonists[2]);
        var name = bed.World.Get<Colonist>(husband).Name;

        ColonistDeath.Die(bed.Simulation, husband, Core.Text.LocalizedText.Of("event.need.hunger.death").With("colonist", husband));

        bed.World.Get<Relations>(wife).Find(husband).ShouldBeNull();
        (bed.World.TryGet<RecentInteractions>(wife, out var recent) ? recent.Entries : []).ShouldNotContain(entry => entry.Other == husband);
        bed.World.Get<Relations>(other).Find(husband).ShouldBeNull();
        var family = bed.World.Get<Family>(wife);
        family.Partner!.Dead.ShouldBeTrue();
        family.Partner.Name.ShouldBe(name);
        family.WidowedAtAgeDays.ShouldBe(bed.World.Get<Life>(wife).AgeDays);
        Relationships.IsFree(bed.Simulation, wife).ShouldBeFalse();

        bed.World.Get<Life>(wife).AgeDays += 60;
        Relationships.IsFree(bed.Simulation, wife).ShouldBeTrue();
    }

    [Fact]
    public void Brothers_and_sisters_share_a_father_or_a_mother_and_a_parent_from_outside_matches_nobody()
    {
        var bed = ColonyTestBed.Flat(10);
        var mother = bed.AddColonist(1, 1, 1);
        var elder = bed.AddColonist(2, 2, 1, name: "Urraca");
        var younger = bed.AddColonist(3, 3, 1, name: "Jimena");
        var stranger = bed.AddColonist(4, 4, 1, name: "Edith");
        var spouse = bed.AddColonist(5, 5, 1, name: "Agnes");
        foreach (var child in new[] { elder, younger })
        {
            bed.World.Add(child, new Family { Mother = Relationships.RelativeOf(bed.Simulation, mother) });
        }

        // Both with a father from outside the colony, with no entity.
        var outside = new Relative { Name = "Pedro" };
        bed.World.Add(stranger, new Family { Father = outside });
        bed.World.Add(spouse, new Family { Father = new Relative { Name = "Sancho" }, Partner = Relationships.RelativeOf(bed.Simulation, stranger) });

        Relationships.SiblingsOf(bed.Simulation, elder).ShouldBe(new[] { younger });
        Relationships.SiblingsOf(bed.Simulation, mother).ShouldBeEmpty();
        Relationships.SiblingsOf(bed.Simulation, stranger).ShouldBeEmpty();
        Relationships.SiblingsOf(bed.Simulation, spouse).ShouldBeEmpty();
        Relationships.IsFamily(bed.Simulation, elder, younger).ShouldBeTrue();
        Relationships.IsFamily(bed.Simulation, stranger, elder).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, Core.Defs.TraitWord.VeryLow)]
    [InlineData(15, Core.Defs.TraitWord.VeryLow)]
    [InlineData(16, Core.Defs.TraitWord.Low)]
    [InlineData(50, Core.Defs.TraitWord.Average)]
    [InlineData(65, Core.Defs.TraitWord.High)]
    [InlineData(100, Core.Defs.TraitWord.VeryHigh)]
    public void A_trait_gets_the_word_of_its_band(int value, Core.Defs.TraitWord word)
    {
        PersonalityRules.From(ColonyTestBed.Content).WordOf(value).ShouldBe(word);
    }

    private static void Love(ColonyTestBed bed, Core.Ecs.EntityId a, Core.Ecs.EntityId b, int opinion, int romance)
    {
        foreach (var (from, to) in new[] { (a, b), (b, a) })
        {
            var relation = Relationships.Ensure(bed.Simulation, from, to);
            (relation.Opinion, relation.Romance) = (opinion, romance);
        }
    }

    private static List<string> Notices(ColonyTestBed bed) =>
        [.. bed.DrainEvents().OfType<Core.Sim.NotificationEvent>().Select(notice => notice.Text.Key)];
}
