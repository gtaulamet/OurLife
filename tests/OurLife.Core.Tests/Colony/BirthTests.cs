using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Colony;

/// <summary>Conception, pregnancy, birth and the choice of children (H3.7, "Parejas, embarazo y nacimiento" in <c>docs/gdd/vida-y-generaciones.md</c>).</summary>
public class BirthTests
{
    private const int DaysPerYear = 60;
    private const int TicksPerDay = 24 * 60;

    /// <summary>
    /// The test content with a pace of a year of life a day and a conception that never fails, so a
    /// fertile couple conceives at the first midnight and the child is born at the next.
    /// </summary>
    private static readonly string CertainJson = ColonyTestBed.ContentJson
        .Replace("\"conceptionChancePerYear\": 40", "\"conceptionChancePerYear\": 100", StringComparison.Ordinal)
        .Replace(
            "{ \"type\": \"LifePace\", \"id\": \"normal\", \"lifeDaysPerDay\": 1 },",
            "{ \"type\": \"LifePace\", \"id\": \"normal\", \"lifeDaysPerDay\": 1 }, { \"type\": \"LifePace\", \"id\": \"yearly\", \"lifeDaysPerDay\": 60 },",
            StringComparison.Ordinal);

    internal static readonly Lazy<DefDatabase> Certain = new(() => TestContent.WithDefs(CertainJson));

    [Fact]
    public void A_couple_with_food_has_a_child_with_the_father_surname_traits_from_both_and_a_free_name()
    {
        var (bed, wife, husband) = Couple(seed: 3, pace: "very_fast");
        var events = new List<ISimEvent>();

        var day = RunUntil(bed, events, 150, () => Children(bed).Count > 0);

        day.ShouldBeGreaterThan(0, "no child was born in 150 days");
        var notices = events.OfType<NotificationEvent>().Select(notice => notice.Text.Key).ToList();
        notices.ShouldContain(BirthSystem.PregnantKey);
        notices.IndexOf(BirthSystem.PregnantKey).ShouldBeLessThan(notices.IndexOf(BirthSystem.BirthKey));
        var baby = Children(bed).Single();
        var person = bed.World.Get<Colonist>(baby);
        person.Surname.ShouldBe("Pérez");
        new[] { "Elvira", "Rodrigo" }.ShouldNotContain(person.Name);
        (person.Gender == Gender.Female ? new[] { "Urraca", "Jimena", "Edith", "Agnes" } : new[] { "Sancho", "Pedro", "Godric", "Hugh" })
            .ShouldContain(person.Name);
        bed.World.Get<Position>(baby).Cell.Z.ShouldBe(1);
        LifeRules.Stage(bed.Simulation, baby)!.Id.ShouldBe("baby");
        bed.World.Get<Skills>(baby).Values.ShouldAllBe(skill => skill.Level == 0);
        bed.World.Get<Needs>(baby).Values.ShouldAllBe(need => need.Value == 10_000 || need.Value > 9_000);
        bed.World.Has<Pregnancy>(wife).ShouldBeFalse();

        // Half the average of the parents and half drawn: between half the average and 50 more.
        foreach (var trait in bed.World.Get<Personality>(baby).Traits)
        {
            var parents = (bed.World.Get<Personality>(wife).Find(trait.Trait)!.Value + bed.World.Get<Personality>(husband).Find(trait.Trait)!.Value) / 2;
            trait.Value.ShouldBeInRange((parents / 2) - 2, (parents / 2) + 52);
        }

        var family = bed.World.Get<Family>(baby);
        family.Mother!.Entity.ShouldBe(wife);
        family.Father!.Entity.ShouldBe(husband);
        foreach (var parent in new[] { wife, husband })
        {
            bed.World.Get<Family>(parent).Children.Select(child => child.Entity).ShouldBe(new[] { baby });
            Relationships.Kind(bed.Simulation, parent, baby).ShouldBe(RelationKind.Family);
            Relationships.Opinion(bed.Simulation, baby, parent).ShouldBeGreaterThanOrEqualTo(30);
        }
    }

    [Fact]
    public void The_parents_are_glad_and_kinder_and_the_birth_is_told_with_both_names()
    {
        var (bed, wife, husband) = Couple(seed: 1, pace: "yearly", content: Certain.Value);
        var kindness = bed.World.Get<Personality>(wife).Find("kindness")!.Value;
        var events = new List<ISimEvent>();

        RunUntil(bed, events, 3, () => Children(bed).Count > 0).ShouldBeGreaterThan(0);

        var baby = Children(bed).Single();
        var birth = events.OfType<NotificationEvent>().Single(notice => notice.Text.Key == BirthSystem.BirthKey);
        birth.Text.Args["mother"].ShouldBe("Elvira Smith");
        birth.Text.Args["child"].ShouldBe($"{bed.World.Get<Colonist>(baby).Name} Pérez");
        birth.Text.Args["child_gender"].ShouldBe(bed.World.Get<Colonist>(baby).Gender == Gender.Female ? "female" : "male");
        foreach (var parent in new[] { wife, husband })
        {
            MoodRules.Thoughts(bed.Simulation, parent).Select(thought => thought.Id).ShouldContain("child_born");
            Relationships.Opinion(bed.Simulation, parent, baby).ShouldBe(40);
        }

        bed.World.Get<Personality>(wife).Find("kindness")!.Value.ShouldBeGreaterThanOrEqualTo(kindness + 2);
    }

    [Fact]
    public void With_children_forbidden_a_couple_neither_conceives_nor_wishes_for_a_child()
    {
        var (bed, wife, husband) = Couple(seed: 2, pace: "yearly", content: Certain.Value);
        bed.Simulation.Commands.Enqueue(new SetChildrenAllowedCommand(husband, Allowed: false));

        bed.Simulation.Run(10 * TicksPerDay);

        bed.World.Get<Family>(wife).ChildrenAllowed.ShouldBeFalse();
        bed.World.Has<Pregnancy>(wife).ShouldBeFalse();
        Children(bed).ShouldBeEmpty();
        foreach (var parent in new[] { wife, husband })
        {
            Wishes(bed, parent).ShouldNotContain("have_child");
        }
    }

    [Fact]
    public void Forbidding_children_drops_the_wish_for_one_without_frustrating_it_and_allowing_them_again_brings_them_back()
    {
        var (bed, wife, husband) = Couple(seed: 2, pace: "yearly", content: Certain.Value);
        bed.World.Add(wife, new Desires { Active = [new ActiveDesire { Desire = "have_child", UntilTick = 10 * TicksPerDay }] });

        bed.Simulation.Commands.Enqueue(new SetChildrenAllowedCommand(wife, Allowed: false));
        bed.Simulation.Run(TicksPerDay);

        Wishes(bed, wife).ShouldBeEmpty();
        MoodRules.Thoughts(bed.Simulation, wife).Select(thought => thought.Id).ShouldNotContain("desire_frustrated");
        bed.World.Get<Family>(husband).ChildrenAllowed.ShouldBeFalse();

        bed.Simulation.Commands.Enqueue(new SetChildrenAllowedCommand(husband, Allowed: true));
        bed.Simulation.Run(2 * TicksPerDay);

        bed.World.Get<Family>(wife).ChildrenAllowed.ShouldBeTrue();
        (Children(bed).Count + (bed.World.Has<Pregnancy>(wife) ? 1 : 0)).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void A_hungry_couple_does_not_conceive_until_it_has_eaten()
    {
        var (bed, wife, husband) = Couple(seed: 1, pace: "yearly", content: Certain.Value, food: false);
        bed.Need(husband, "hunger").Value = 3_900;

        bed.Simulation.Run(TicksPerDay);

        bed.World.Has<Pregnancy>(wife).ShouldBeFalse();

        bed.Need(husband, "hunger").Value = 10_000;
        bed.Need(wife, "hunger").Value = 10_000;
        bed.Simulation.Run(TicksPerDay);

        bed.World.Has<Pregnancy>(wife).ShouldBeTrue();
    }

    [Fact]
    public void Two_women_never_conceive()
    {
        var (bed, wife, partner) = Couple(seed: 1, pace: "yearly", content: Certain.Value);
        bed.World.Get<Colonist>(partner).Gender = Gender.Female;

        bed.Simulation.Run(4 * TicksPerDay);

        bed.World.Store<Pregnancy>().Count.ShouldBe(0);
        Children(bed).ShouldBeEmpty();
        LifeRules.From(bed.Simulation.Content).CanHaveChildren(bed.Simulation, wife, partner).ShouldBeFalse();
    }

    [Fact]
    public void Past_her_fertile_years_a_woman_does_not_conceive()
    {
        var (bed, wife, _) = Couple(seed: 1, pace: "yearly", content: Certain.Value);
        bed.World.Get<Life>(wife).AgeDays = 46 * DaysPerYear;

        bed.Simulation.Run(3 * TicksPerDay);

        bed.World.Has<Pregnancy>(wife).ShouldBeFalse();
    }

    [Fact]
    public void A_couple_who_can_have_children_wishes_for_one_and_the_wish_comes_true_with_the_pregnancy()
    {
        var content = TestContent.WithDefs(ColonyTestBed.ContentJson
            .Replace("\"id\": \"have_child\", \"kind\": \"haveChild\", \"chancePercent\": 30", "\"id\": \"have_child\", \"kind\": \"haveChild\", \"chancePercent\": 100", StringComparison.Ordinal)
            .Replace("\"conceptionChancePerYear\": 40", "\"conceptionChancePerYear\": 0", StringComparison.Ordinal));
        var (bed, wife, husband) = Couple(seed: 1, pace: "very_fast", content: content);

        bed.Simulation.Run(TicksPerDay);

        Wishes(bed, wife).ShouldBe(new[] { "have_child" });
        Wishes(bed, husband).ShouldBe(new[] { "have_child" });

        // It waits a year of life: 60 days of life at 8 a day are 7 game days.
        var left = bed.World.Get<Desires>(wife).Active.Single().UntilTick - bed.Simulation.CurrentTick;
        left.ShouldBeInRange(6L * TicksPerDay, 7L * TicksPerDay);

        bed.World.Add(wife, new Pregnancy { Father = Relationships.RelativeOf(bed.Simulation, husband) });
        bed.Simulation.Run(2 * 60);

        Wishes(bed, wife).ShouldBeEmpty();
        Wishes(bed, husband).ShouldBeEmpty();
        MoodRules.Thoughts(bed.Simulation, husband).Select(thought => thought.Id).ShouldContain("desire_fulfilled");
    }

    [Fact]
    public void Late_in_her_pregnancy_a_woman_works_slower()
    {
        var (bed, wife, husband) = Couple(seed: 1, pace: "very_fast");
        var rules = ColonyRules.From(bed.Simulation.Content);
        var mining = bed.Simulation.Content.Get<WorkTypeDef>("mining");
        var before = WorkSpeed.PercentOf(bed.Simulation, wife, mining, rules.Skills);

        bed.World.Add(wife, new Pregnancy { Father = Relationships.RelativeOf(bed.Simulation, husband), LifeDays = 29 });
        WorkSpeed.PercentOf(bed.Simulation, wife, mining, rules.Skills).ShouldBe(before);

        bed.World.Get<Pregnancy>(wife).LifeDays = 30;
        WorkSpeed.PercentOf(bed.Simulation, wife, mining, rules.Skills).ShouldBe(before * 90 / 100);
    }

    [Fact]
    public void A_child_of_the_couple_grows_up_to_be_an_adult()
    {
        var (bed, wife, husband) = Couple(seed: 4, pace: "yearly", content: Certain.Value);
        var events = new List<ISimEvent>();
        RunUntil(bed, events, 3, () => Children(bed).Count > 0).ShouldBeGreaterThan(0);

        // One child is enough to watch: the couple has no more.
        bed.Simulation.Commands.Enqueue(new SetChildrenAllowedCommand(wife, Allowed: false));
        var child = Children(bed).Single();

        RunUntil(bed, events, 25, () => LifeRules.Stage(bed.Simulation, child)?.Id == "adult").ShouldBeGreaterThan(0);

        bed.World.IsAlive(child).ShouldBeTrue();
        var told = events.OfType<NotificationEvent>().Select(notice => notice.Text.Key).ToList();
        told.ShouldContain("event.life.stage.child");
        told.ShouldContain("event.life.stage.adolescent");
        told.ShouldContain("event.life.stage.adult");
        told.ShouldNotContain("event.need.hunger.death");
        bed.World.Get<Family>(child).Father!.Entity.ShouldBe(husband);
    }

    [Fact]
    public void The_same_seed_gives_the_same_children()
    {
        static string Run(ulong seed)
        {
            var (bed, _, _) = Couple(seed, pace: "very_fast");
            bed.Simulation.Run(60 * TicksPerDay);
            return $"{Children(bed).Count}:{bed.Simulation.ComputeStateHash()}";
        }

        Run(8).ShouldBe(Run(8));
    }

    [Fact]
    public void A_couple_expecting_a_child_does_not_wish_for_another_and_a_widow_drops_the_wish()
    {
        var content = TestContent.WithDefs(ColonyTestBed.ContentJson
            .Replace("\"id\": \"have_child\", \"kind\": \"haveChild\", \"chancePercent\": 30", "\"id\": \"have_child\", \"kind\": \"haveChild\", \"chancePercent\": 100", StringComparison.Ordinal)
            .Replace("\"conceptionChancePerYear\": 40", "\"conceptionChancePerYear\": 0", StringComparison.Ordinal));
        var (bed, wife, husband) = Couple(seed: 1, pace: "very_fast", content: content);
        bed.World.Add(wife, new Pregnancy { Father = Relationships.RelativeOf(bed.Simulation, husband) });

        bed.Simulation.Run(TicksPerDay);

        Wishes(bed, wife).ShouldBeEmpty();
        Wishes(bed, husband).ShouldBeEmpty();

        bed.World.Remove<Pregnancy>(wife);
        bed.Simulation.Run(15 * TicksPerDay);
        Wishes(bed, husband).ShouldBe(new[] { "have_child" });
        bed.World.Get<Desires>(husband).Active.Single().Target.ShouldBe(wife);

        var frustrated = Frustrations(bed, husband);
        bed.World.Get<Needs>(wife).Values.ForEach(need => need.Value = 0);
        bed.World.Get<Needs>(wife).Find("hunger")!.MinutesAtZero = 72 * 60;
        bed.Simulation.Run(2 * 60);

        bed.World.IsAlive(wife).ShouldBeFalse();
        Wishes(bed, husband).ShouldBeEmpty();
        Frustrations(bed, husband).ShouldBe(frustrated);
    }

    [Fact]
    public void A_new_couple_starts_with_children_allowed_whatever_an_earlier_one_chose()
    {
        var (bed, wife, husband) = Couple(seed: 1, pace: "very_fast");
        var rules = SocialRules.From(bed.Simulation.Content);
        bed.Simulation.Commands.Enqueue(new SetChildrenAllowedCommand(wife, Allowed: false));
        bed.Simulation.ApplyCommandsNow();
        rules.Change(bed.Simulation, wife, husband, -90, 0);
        var other = bed.AddColonist(7, 7, 1, name: "Sancho");
        bed.World.Get<Colonist>(other).Gender = Gender.Male;
        bed.World.Add(other, new Life { AgeDays = 25 * DaysPerYear, Stage = "adult" });
        foreach (var (from, to) in new[] { (wife, other), (other, wife) })
        {
            var relation = Relationships.Ensure(bed.Simulation, from, to);
            relation.Opinion = 60;
            relation.Romance = 95;
        }

        rules.Change(bed.Simulation, wife, other, 1, 1, romance: 5);

        Relationships.PartnerOf(bed.Simulation, wife).ShouldBe(other);
        LifeRules.ChildrenAllowed(bed.Simulation, wife, other).ShouldBeTrue();
    }

    [Fact]
    public void Without_food_stored_for_the_village_a_couple_does_not_conceive()
    {
        var content = TestContent.WithDefs(CertainJson.Replace(
            "\"siblingOpinion\": 30", "\"siblingOpinion\": 30, \"foodReserveDays\": 30", StringComparison.Ordinal));
        var (bed, wife, _) = Couple(seed: 1, pace: "yearly", content: content, food: false);
        bed.AddItems("provisions", 50, 1, 13, 1);

        // 50 provisions feed two adults some 13 days: short of 30.
        bed.Simulation.Run(TicksPerDay);
        bed.World.Has<Pregnancy>(wife).ShouldBeFalse();

        bed.AddItems("provisions", 50, 3, 13, 1);
        bed.AddItems("provisions", 50, 5, 13, 1);
        bed.Simulation.Run(TicksPerDay);
        bed.World.Has<Pregnancy>(wife).ShouldBeTrue();
    }

    [Fact]
    public void Without_land_to_feed_one_more_a_couple_does_not_conceive()
    {
        var content = TestContent.WithDefs(CertainJson.Replace(
            "\"siblingOpinion\": 30", "\"siblingOpinion\": 30, \"foodPlantsPerColonist\": 4", StringComparison.Ordinal));
        var (bed, wife, _) = Couple(seed: 1, pace: "yearly", content: content);
        for (var x = 1; x < 12; x++)
        {
            bed.AddPlant("bush", x, 1, 1);
        }

        // Eleven bushes feed two colonists, not three.
        bed.Simulation.Run(TicksPerDay);
        bed.World.Has<Pregnancy>(wife).ShouldBeFalse();

        bed.AddPlant("bush", 12, 1, 1);
        bed.Simulation.Run(TicksPerDay);
        bed.World.Has<Pregnancy>(wife).ShouldBeTrue();
    }

    private static int Frustrations(ColonyTestBed bed, EntityId colonist) =>
        MoodRules.Thoughts(bed.Simulation, colonist).Count(thought => thought.Id == "desire_frustrated");

    /// <summary>
    /// A woman and a man of 25 in a couple, Elvira Smith and Rodrigo Pérez, with average traits,
    /// on an open floor with food for a long while unless told otherwise.
    /// </summary>
    internal static (ColonyTestBed Bed, EntityId Wife, EntityId Husband) Couple(
        ulong seed, string pace, DefDatabase? content = null, bool food = true)
    {
        var bed = ColonyTestBed.Flat(15, seed, content);
        bed.Simulation.LifePace = pace;
        var wife = bed.AddColonist(3, 3, 1, name: "Elvira");
        var husband = bed.AddColonist(5, 5, 1, name: "Rodrigo");
        bed.World.Get<Colonist>(wife).Surname = "Smith";
        bed.World.Get<Colonist>(husband).Surname = "Pérez";
        bed.World.Get<Colonist>(husband).Gender = Gender.Male;
        foreach (var (colonist, partner) in new[] { (wife, husband), (husband, wife) })
        {
            bed.World.Add(colonist, new Life { AgeDays = 25 * DaysPerYear, Stage = "adult" });
            bed.World.Add(colonist, new Personality
            {
                Traits = [.. new[] { "curiosity", "diligence", "intelligence", "kindness", "patience", "sociability" }
                    .Select(trait => new TraitValue { Trait = trait, Value = 50 })],
            });
            bed.World.Add(colonist, new Family { Partner = Relationships.RelativeOf(bed.Simulation, partner) });
            var relation = Relationships.Ensure(bed.Simulation, colonist, partner);
            relation.Opinion = 80;
            relation.Romance = 100;
        }

        if (food)
        {
            for (var x = 1; x < 14; x += 2)
            {
                bed.AddItems("provisions", 50, x, 13, 1);
            }
        }

        return (bed, wife, husband);
    }

    /// <summary>The colonists born in the game: those with a mother.</summary>
    internal static List<EntityId> Children(ColonyTestBed bed) =>
        bed.Colonists().Where(colonist => bed.World.TryGet<Family>(colonist, out var family) && family.Mother is not null).ToList();

    private static List<string> Wishes(ColonyTestBed bed, EntityId colonist) =>
        bed.World.TryGet<Desires>(colonist, out var desires) ? desires.Active.Select(active => active.Desire).ToList() : [];

    /// <summary>Runs day by day until the condition holds: the day it did, from 1, or 0 if it did not within the days.</summary>
    private static int RunUntil(ColonyTestBed bed, List<ISimEvent> events, int days, Func<bool> done)
    {
        for (var day = 1; day <= days; day++)
        {
            bed.Simulation.Run(TicksPerDay);
            bed.Simulation.Events.DrainTo(events);
            if (done())
            {
                return day;
            }
        }

        return 0;
    }
}
