using OurLife.Core.Colony;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Colony;

/// <summary>Personality: drawn, inherited, moved by experiences, and what it changes (H3.3, <c>docs/gdd/vida-y-generaciones.md</c>).</summary>
public class PersonalityTests
{
    private static readonly string[] TraitIds = ["curiosity", "diligence", "intelligence", "kindness", "patience", "sociability"];

    private static PersonalityRules Rules => PersonalityRules.From(ColonyTestBed.Content);

    [Fact]
    public void The_founders_arrive_with_every_trait_and_an_orientation()
    {
        var bed = ColonyTestBed.Flat(15);

        var colonists = ColonyStart.Settle(bed.Simulation, ColonyStart.TheOnlyScenario(bed.Simulation.Content), "Valdeolmos");

        foreach (var colonist in colonists)
        {
            var personality = bed.World.Get<Personality>(colonist);
            personality.Traits.Select(trait => trait.Trait).ShouldBe(TraitIds);
            personality.Traits.ShouldAllBe(trait => trait.Value >= 0 && trait.Value <= 100 && trait.MovedToday == 0);
        }

        colonists.SelectMany(colonist => bed.World.Get<Personality>(colonist).Traits.Select(trait => trait.Value)).Distinct().Count().ShouldBeGreaterThan(10);
    }

    [Fact]
    public void Drawn_traits_gather_near_the_middle_and_orientations_follow_their_weights()
    {
        var random = new Pcg32(7, 1);
        var values = new List<int>();
        var orientations = new Dictionary<Orientation, int>();
        for (var i = 0; i < 3_000; i++)
        {
            var personality = Rules.Draw(random);
            values.AddRange(personality.Traits.Select(trait => trait.Value));
            orientations[personality.Orientation] = orientations.GetValueOrDefault(personality.Orientation) + 1;
        }

        values.Average().ShouldBeInRange(48, 52);
        values.Min().ShouldBeGreaterThanOrEqualTo(0);
        values.Max().ShouldBeLessThanOrEqualTo(100);

        // Averaging three rolls makes the ends rare: about 1 % below 10, where one roll would give 10 %.
        (values.Count(value => value < 10) / (double)values.Count).ShouldBeLessThan(0.03);
        (orientations[Orientation.Opposite] / 3_000.0).ShouldBeInRange(0.76, 0.84);
        (orientations[Orientation.Same] / 3_000.0).ShouldBeInRange(0.05, 0.11);
        (orientations[Orientation.Both] / 3_000.0).ShouldBeInRange(0.09, 0.15);
    }

    [Fact]
    public void A_child_takes_half_of_its_parents_and_half_drawn()
    {
        var random = new Pcg32(3, 1);
        var mother = WithAll(90);
        var father = WithAll(70);
        var values = new List<int>();
        for (var i = 0; i < 1_000; i++)
        {
            var child = Rules.Inherit(mother, father, random);
            child.Traits.Select(trait => trait.Trait).ShouldBe(TraitIds);
            values.AddRange(child.Traits.Select(trait => trait.Value));
        }

        // Half of 80 and half of a draw from 0 to 100.
        values.ShouldAllBe(value => value >= 40 && value <= 90);
        values.Average().ShouldBeInRange(63, 67);
    }

    [Fact]
    public void Experiences_move_a_trait_at_most_three_points_a_day()
    {
        var bed = ColonyTestBed.Flat(8);
        var colonist = bed.AddColonist(2, 2, 1);
        bed.World.Add(colonist, WithAll(50));
        var rules = Rules;

        rules.Drift(bed.Simulation, colonist, "kindness", 2).ShouldBe(2);
        rules.Drift(bed.Simulation, colonist, "kindness", 2).ShouldBe(1);
        rules.Drift(bed.Simulation, colonist, "kindness", 1).ShouldBe(0);
        rules.Drift(bed.Simulation, colonist, "kindness", -5).ShouldBe(-5);
        rules.Drift(bed.Simulation, colonist, "kindness", -5).ShouldBe(-1);
        PersonalityRules.Value(bed.Simulation, colonist, "kindness").ShouldBe(47);
        rules.Drift(bed.Simulation, colonist, "patience", -3).ShouldBe(-3);

        // At midnight the day changes, and the trait may move again.
        bed.Simulation.Run(LifeSystem.MidnightOffset(bed.Simulation.Time) + 1);
        rules.Drift(bed.Simulation, colonist, "kindness", -3).ShouldBe(-3);
        PersonalityRules.Value(bed.Simulation, colonist, "kindness").ShouldBe(44);
    }

    [Fact]
    public void Children_and_teenagers_move_twice_as_much_and_no_trait_leaves_0_to_100()
    {
        var bed = ColonyTestBed.Flat(8);
        var child = bed.AddColonist(2, 2, 1);
        bed.World.Add(child, new Life { AgeDays = 8 * 60, Stage = "child" });
        bed.World.Add(child, WithAll(99));
        var stranger = bed.AddColonist(4, 4, 1);

        Rules.Drift(bed.Simulation, child, "kindness", -1).ShouldBe(-2);
        Rules.Drift(bed.Simulation, child, "sociability", 1).ShouldBe(1);
        PersonalityRules.Value(bed.Simulation, child, "sociability").ShouldBe(100);
        Rules.Drift(bed.Simulation, stranger, "kindness", 1).ShouldBe(0);
        PersonalityRules.Value(bed.Simulation, stranger, "kindness").ShouldBe(PersonalityRules.Average);
    }

    [Theory]
    [InlineData(50, 0)]
    [InlineData(100, -3)]
    [InlineData(0, 3)]
    public void Diligence_sets_the_speed_of_work(int diligence, int moreTicks)
    {
        // 30 ticks of rock, times 100 over the speed: 27 at 110 %, 33 at 90 %.
        TicksToDig(diligence).ShouldBe(TicksToDig(null) + moreTicks);
    }

    private static long TicksToDig(int? diligence)
    {
        var bed = new ColonyTestBed(ColonyTestBed.Level(5, 3, '#'), "#....\n.....\n.....");
        var colonist = bed.AddColonist(1, 0, 1);
        if (diligence is { } value)
        {
            bed.World.Add(colonist, new Personality { Traits = [new TraitValue { Trait = "diligence", Value = value }] });
        }

        bed.Designate(DesignationKind.Mine, new TileCoord(0, 0, 1), new TileCoord(0, 0, 1));
        while (bed.Map.ShapeAt(new TileCoord(0, 0, 1)) == TileShape.Wall)
        {
            bed.Simulation.Tick();
            bed.Simulation.CurrentTick.ShouldBeLessThan(500);
        }

        return bed.Simulation.CurrentTick;
    }

    [Fact]
    public void A_lazy_colonist_eats_before_working_and_a_diligent_one_works_first()
    {
        static JobKind FirstJob(int diligence)
        {
            var bed = new ColonyTestBed(ColonyTestBed.Level(8, 3, '#'), "#.......\n........\n........");
            var colonist = bed.AddColonist(1, 0, 1, hunger: 3_000);
            bed.World.Add(colonist, new Personality { Traits = [new TraitValue { Trait = "diligence", Value = diligence }] });
            bed.AddItems("provisions", 5, 6, 2, 1);
            bed.Designate(DesignationKind.Mine, new TileCoord(0, 0, 1), new TileCoord(0, 0, 1));

            bed.Simulation.Tick();

            return bed.JobOf(colonist)!.Kind;
        }

        // Hungry, but not starving: the hunger is under its low threshold and over the critical one.
        FirstJob(10).ShouldBe(JobKind.Eat);
        FirstJob(90).ShouldBe(JobKind.Mine);
    }

    private static Personality WithAll(int value) =>
        new() { Traits = [.. TraitIds.Select(id => new TraitValue { Trait = id, Value = value })] };
}
