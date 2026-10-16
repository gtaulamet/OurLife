using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;

namespace OurLife.Core.Tests.Colony;

/// <summary>Skills: gained working and from a teacher, and faster work (H3.4, <c>docs/gdd/vida-y-generaciones.md</c>).</summary>
public class SkillTests
{
    private static readonly string[] SkillIds = ["construction", "gathering", "hauling", "mining", "woodcutting"];

    private static SkillRules Rules => SkillRules.From(ColonyTestBed.Content);

    private static WorkTypeDef Type(string id) => ColonyTestBed.Content.Get<WorkTypeDef>(id);

    [Fact]
    public void The_founders_arrive_with_every_skill_and_a_strength()
    {
        var bed = ColonyTestBed.Flat(15);

        var colonists = ColonyStart.Settle(bed.Simulation, ColonyStart.TheOnlyScenario(bed.Simulation.Content), "Valdeolmos");

        foreach (var colonist in colonists)
        {
            var skills = bed.World.Get<Skills>(colonist).Values;
            skills.Select(skill => skill.WorkType).ShouldBe(SkillIds);
            skills.ShouldAllBe(skill => skill.Level >= 3 && skill.Level <= 14 && skill.Experience == 0);

            // Drawn from 3 to 10, and one of them 4 more.
            skills.Max(skill => skill.Level).ShouldBeGreaterThanOrEqualTo(7);
        }
    }

    [Fact]
    public void Each_level_needs_more_experience_and_makes_the_work_faster()
    {
        var config = Rules.Config;

        config.ExperienceFor(0).ShouldBe(600);
        config.ExperienceFor(7).ShouldBe(1_650);
        config.SpeedAt(0).ShouldBe(80);
        config.SpeedAt(10).ShouldBe(100);
        config.SpeedAt(20).ShouldBe(120);
    }

    [Fact]
    public void A_colonist_who_chops_gains_experience_goes_up_a_level_and_chops_faster()
    {
        var bed = ColonyTestBed.Flat(10);
        var colonist = bed.AddColonist(2, 2, 1);
        bed.World.Add(colonist, new Skills { Values = [new SkillValue { WorkType = "woodcutting", Level = 0, Experience = 59_000 }] });
        bed.AddPlant("tree", 3, 2, 1);
        bed.Designate(DesignationKind.Chop, new TileCoord(0, 0, 1), new TileCoord(9, 9, 1));

        TicksUntil(bed, () => bed.CountItems("log") > 0);

        // 10 points make level 1, and from then on the tree takes 73 ticks instead of 75 (82 % instead of 80 %): 63 points left.
        var skill = bed.World.Get<Skills>(colonist).Find("woodcutting")!;
        skill.Level.ShouldBe(1);
        skill.Experience.ShouldBe(6_300);
        SkillRules.Level(bed.Simulation, colonist, "woodcutting").ShouldBe(1);
    }

    [Fact]
    public void A_master_chops_a_tree_in_two_thirds_of_the_time_of_a_beginner()
    {
        static long TicksToChop(int level)
        {
            var bed = ColonyTestBed.Flat(10);
            var colonist = bed.AddColonist(2, 2, 1);
            bed.World.Add(colonist, new Skills { Values = [new SkillValue { WorkType = "woodcutting", Level = level }] });
            bed.AddPlant("tree", 3, 2, 1);
            bed.Designate(DesignationKind.Chop, new TileCoord(0, 0, 1), new TileCoord(9, 9, 1));
            return TicksUntil(bed, () => bed.CountItems("log") > 0);
        }

        // 60 ticks of chopping: 75 at 80 % (level 0), 50 at 120 % (level 20).
        (TicksToChop(0) - TicksToChop(20)).ShouldBe(25);
    }

    [Theory]
    [InlineData(0, 30 * 75)]
    [InlineData(50, 30 * 100)]
    [InlineData(100, 30 * 125)]
    public void Intelligence_sets_the_experience_of_a_tick_of_work(int intelligence, int hundredths)
    {
        var bed = new ColonyTestBed(ColonyTestBed.Level(5, 3, '#'), "#....\n.....\n.....");
        var colonist = bed.AddColonist(1, 0, 1);
        bed.World.Add(colonist, new Skills { Values = [new SkillValue { WorkType = "mining", Level = 10 }] });
        bed.World.Add(colonist, new Personality { Traits = [new TraitValue { Trait = "intelligence", Value = intelligence }] });
        bed.Designate(DesignationKind.Mine, new TileCoord(0, 0, 1), new TileCoord(0, 0, 1));

        TicksUntil(bed, () => bed.Map.ShapeAt(new TileCoord(0, 0, 1)) != TileShape.Wall);

        // Level 10 works at 100 %: 30 ticks of rock.
        bed.World.Get<Skills>(colonist).Find("mining")!.Experience.ShouldBe(hundredths);
    }

    [Fact]
    public void A_hauler_learns_while_carrying()
    {
        var bed = ColonyTestBed.Flat(12);
        var colonist = bed.AddColonist(1, 1, 1);
        bed.World.Add(colonist, new Skills { Values = [new SkillValue { WorkType = "hauling", Level = 5 }] });
        bed.AddItems("log", 5, 2, 2, 1);
        bed.Designate(DesignationKind.Stockpile, new TileCoord(10, 10, 1), new TileCoord(10, 10, 1));

        TicksUntil(bed, () => bed.CountItemsAt("log", new TileCoord(10, 10, 1)) == 5);

        bed.World.Get<Skills>(colonist).Find("hauling")!.Experience.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void An_apprentice_learns_from_a_teacher_who_knows_three_levels_more()
    {
        var bed = ColonyTestBed.Flat(8);
        var teacher = bed.AddColonist(2, 2, 1);
        var apprentice = bed.AddColonist(3, 2, 1);
        bed.World.Add(teacher, new Skills { Values = [new SkillValue { WorkType = "mining", Level = 10 }, new SkillValue { WorkType = "woodcutting", Level = 7 }] });
        bed.World.Add(apprentice, new Skills { Values = [new SkillValue { WorkType = "mining", Level = 5 }, new SkillValue { WorkType = "woodcutting", Level = 5 }] });

        Rules.Teach(bed.Simulation, teacher, apprentice, Type("mining")).ShouldBe(0);
        Rules.Teach(bed.Simulation, teacher, apprentice, Type("woodcutting")).ShouldBe(0);

        // 100 and 30 for each of the 5 levels of difference.
        bed.World.Get<Skills>(apprentice).Find("mining")!.Experience.ShouldBe(250 * 100);
        bed.World.Get<Skills>(apprentice).Find("woodcutting")!.Experience.ShouldBe(0);
        bed.World.Get<Skills>(teacher).Find("mining")!.Experience.ShouldBe(0);
    }

    [Fact]
    public void A_lesson_can_raise_a_level_and_nothing_goes_past_the_last_one()
    {
        var bed = ColonyTestBed.Flat(8);
        var teacher = bed.AddColonist(2, 2, 1);
        var apprentice = bed.AddColonist(3, 2, 1);
        var master = bed.AddColonist(4, 2, 1);
        bed.World.Add(teacher, new Skills { Values = [new SkillValue { WorkType = "mining", Level = 20 }] });
        bed.World.Add(apprentice, new Skills { Values = [new SkillValue { WorkType = "mining", Level = 0 }] });
        bed.World.Add(master, new Skills { Values = [new SkillValue { WorkType = "mining", Level = 20 }] });

        // 100 + 30 × 20 = 700 points: 600 for level 1, 100 left.
        Rules.Teach(bed.Simulation, teacher, apprentice, Type("mining")).ShouldBe(1);
        bed.World.Get<Skills>(apprentice).Find("mining")!.Experience.ShouldBe(100 * 100);
        Rules.Gain(bed.Simulation, master, Type("mining"), 10_000).ShouldBe(0);
        bed.World.Get<Skills>(master).Find("mining")!.ShouldSatisfyAllConditions(
            skill => skill.Level.ShouldBe(20), skill => skill.Experience.ShouldBe(0));
    }

    [Fact]
    public void The_speed_of_a_skill_stops_at_the_last_level_and_starts_at_0_for_one_still_missing()
    {
        var bed = ColonyTestBed.Flat(8);
        var colonist = bed.AddColonist(2, 2, 1);
        var average = bed.AddColonist(3, 2, 1);
        bed.World.Add(colonist, new Skills { Values = [new SkillValue { WorkType = "mining", Level = 500 }] });
        var rules = Rules;

        rules.SpeedPercent(bed.Simulation, colonist, Type("mining")).ShouldBe(120);
        rules.SpeedPercent(bed.Simulation, colonist, Type("woodcutting")).ShouldBe(80);
        rules.SpeedPercent(bed.Simulation, average, Type("woodcutting")).ShouldBe(100);
    }

    [Fact]
    public void A_skill_the_colonist_lacks_is_learned_from_level_0_and_a_new_level_is_told()
    {
        var bed = ColonyTestBed.Flat(8);
        var colonist = bed.AddColonist(2, 2, 1);
        bed.World.Add(colonist, new Skills { Values = [new SkillValue { WorkType = "construction", Level = 4 }, new SkillValue { WorkType = "mining", Level = 4 }] });
        bed.DrainEvents();

        Rules.Gain(bed.Simulation, colonist, Type("hauling"), 700).ShouldBe(1);

        bed.World.Get<Skills>(colonist).Values.Select(skill => skill.WorkType).ShouldBe(new[] { "construction", "hauling", "mining" });
        SkillRules.Level(bed.Simulation, colonist, "hauling").ShouldBe(1);
        bed.DrainEvents().ShouldContain(new EntityChangedEvent(colonist));
    }

    [Fact]
    public void A_skilled_hauler_carries_faster()
    {
        var bed = ColonyTestBed.Flat(8);
        var rules = ColonyRules.From(bed.Simulation.Content);
        var progress = new List<int>();
        foreach (var level in new[] { 0, 10, 20 })
        {
            var colonist = bed.AddColonist(2, 2, 1);
            bed.World.Add(colonist, new Skills { Values = [new SkillValue { WorkType = "hauling", Level = level }] });
            bed.World.Add(colonist, new Job { Kind = JobKind.Haul, Stage = JobStage.Carrying });
            progress.Add(MovementSystem.ProgressOf(bed.Simulation, colonist, rules));
        }

        progress.ShouldBe(new[] { 800, 1_000, 1_200 });
    }

    [Fact]
    public void Founders_cannot_start_past_the_last_level()
    {
        var json = ColonyTestBed.ContentJson.Replace("\"founderSkillMax\": 10,", "\"founderSkillMax\": 18,", StringComparison.Ordinal);
        json.ShouldNotBe(ColonyTestBed.ContentJson);
        var content = TestContent.WithDefs(json);

        Should.Throw<InvalidOperationException>(() => SkillRules.From(content).Founder(ColonyStart.TheOnlyScenario(content), new Core.Sim.Pcg32(1, 1)))
            .Message.ShouldContain("past the last level");
    }

    private static long TicksUntil(ColonyTestBed bed, Func<bool> done)
    {
        var start = bed.Simulation.CurrentTick;
        while (!done())
        {
            bed.Simulation.Tick();
            (bed.Simulation.CurrentTick - start).ShouldBeLessThan(3_000);
        }

        return bed.Simulation.CurrentTick - start;
    }
}
