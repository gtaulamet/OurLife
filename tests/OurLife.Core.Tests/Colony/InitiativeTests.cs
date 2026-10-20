using System.Text.RegularExpressions;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Save;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Colony;

/// <summary>
/// Work on its own (L3 of IP3.5, "Trabajo por iniciativa" in <c>docs/gdd/libre-albedrio.md</c>).
/// The test content has a village of radius 6, targets of 150 food and 40 wood, and a chance of 60 %.
/// </summary>
public class InitiativeTests
{
    private static readonly TileCoord Center = new(8, 8, 1);

    [Fact]
    public void A_colonist_gathers_on_its_own_while_food_is_under_target()
    {
        var bed = FreeBed();
        var bush = bed.AddPlant("bush", 11, 8, 1);
        var colonist = bed.AddColonist(6, 8, 1);

        var initiative = RunWatching(bed, colonist, 300);

        initiative.ShouldContain(JobKind.Harvest);
        bed.World.Has<Harvested>(bush).ShouldBeTrue();
        bed.CountItems("berries").ShouldBe(8);
    }

    [Fact]
    public void With_the_target_met_nobody_gathers_on_its_own()
    {
        var bed = FreeBed();
        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand("food", 10));
        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand("wood", 0));
        bed.AddItems("berries", 10, 2, 2, 1);
        var bush = bed.AddPlant("bush", 11, 8, 1);
        var colonist = bed.AddColonist(6, 8, 1);

        RunWatching(bed, colonist, 600).ShouldBeEmpty();
        bed.World.Has<Harvested>(bush).ShouldBeFalse();
    }

    [Fact]
    public void A_colonist_who_never_gathers_does_not_on_its_own_and_another_does()
    {
        var bed = FreeBed();
        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand("wood", 0));
        bed.AddPlant("bush", 11, 8, 1);
        bed.AddPlant("bush", 5, 8, 1);
        var never = bed.AddColonist(6, 9, 1);
        var other = bed.AddColonist(10, 9, 1, name: "Sancha");
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(never, "gathering", WorkPriorityLevels.Never));

        var seen = RunWatching(bed, [never, other], 600);

        seen[never].ShouldBeEmpty();
        seen[other].ShouldContain(JobKind.Harvest);
    }

    [Fact]
    public void With_free_will_off_nobody_works_on_its_own_and_the_game_is_as_without_it()
    {
        var with = OffBed(ColonyTestBed.Content);
        var without = OffBed(ContentWithoutFreeWill());

        RunWatching(with.Bed, with.Colonist, 1_000).ShouldBeEmpty();
        without.Bed.Simulation.Run(1_000);

        with.Bed.Simulation.ComputeStateHash().ShouldBe(without.Bed.Simulation.ComputeStateHash());
    }

    [Fact]
    public void A_tree_far_from_the_village_is_never_chopped_on_its_own_but_is_by_an_order()
    {
        var bed = FreeBed();
        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand("food", 0));
        var far = bed.AddPlant("tree", 15, 15, 1);
        var colonist = bed.AddColonist(8, 8, 1);

        RunWatching(bed, colonist, 600).ShouldBeEmpty();
        bed.World.IsAlive(far).ShouldBeTrue();

        bed.Designate(DesignationKind.Chop, new TileCoord(15, 15, 1), new TileCoord(15, 15, 1));
        bed.Simulation.Run(600);
        bed.World.IsAlive(far).ShouldBeFalse();
    }

    [Fact]
    public void An_order_comes_before_work_on_its_own_even_of_a_higher_priority()
    {
        var bed = FreeBed();
        bed.AddPlant("bush", 9, 8, 1);
        bed.AddPlant("tree", 12, 12, 1);
        bed.Designate(DesignationKind.Chop, new TileCoord(12, 12, 1), new TileCoord(12, 12, 1));
        var colonist = bed.AddColonist(8, 8, 1);
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "gathering", WorkPriorityLevels.Highest));

        bed.Simulation.Tick();

        bed.JobOf(colonist)!.Kind.ShouldBe(JobKind.Chop);
        bed.JobOf(colonist)!.Initiative.ShouldBeFalse();
    }

    [Fact]
    public void Work_on_its_own_gives_way_on_the_way_to_an_order_that_comes_later()
    {
        // A long walk to the bush, so the next recheck finds it still on the way.
        var bed = ColonyTestBed.Flat(32);
        bed.Simulation.FreeWill = true;
        bed.Simulation.VillageCenter = new TileCoord(16, 16, 1);
        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand("wood", 0));
        var bush = bed.AddPlant("bush", 21, 16, 1);
        bed.AddPlant("tree", 2, 2, 1);
        var colonist = bed.AddColonist(0, 16, 1);
        RunUntil(bed, () => bed.JobOf(colonist) is { Initiative: true }, 300).ShouldBeTrue();

        bed.Designate(DesignationKind.Chop, new TileCoord(2, 2, 1), new TileCoord(2, 2, 1));
        RunUntil(bed, () => bed.JobOf(colonist) is { Kind: JobKind.Chop, Initiative: false }, 40).ShouldBeTrue();

        bed.World.Has<Harvested>(bush).ShouldBeFalse();
    }

    [Fact]
    public void Work_on_its_own_already_started_is_finished_before_an_order()
    {
        var bed = FreeBed();
        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand("food", 0));
        var tree = bed.AddPlant("tree", 9, 8, 1);
        bed.AddPlant("tree", 3, 3, 1);
        var colonist = bed.AddColonist(8, 9, 1);
        RunUntil(bed, () => bed.JobOf(colonist) is { Initiative: true, Stage: JobStage.Working }, 300).ShouldBeTrue();

        bed.Designate(DesignationKind.Chop, new TileCoord(3, 3, 1), new TileCoord(3, 3, 1));
        RunUntil(bed, () => !bed.World.IsAlive(tree) || bed.JobOf(colonist) is { Initiative: false }, 200);

        bed.World.IsAlive(tree).ShouldBeFalse();
    }

    [Fact]
    public void An_adolescent_gathers_on_its_own_but_does_not_chop_and_a_child_does_neither()
    {
        var bed = FreeBed();
        bed.AddPlant("tree", 10, 10, 1);
        bed.AddPlant("bush", 6, 6, 1);
        var adolescent = bed.AddColonist(8, 8, 1);
        bed.World.Add(adolescent, new Life { AgeDays = 15 * 60, Stage = "adolescent" });
        var child = bed.AddColonist(9, 9, 1, name: "Mencía");
        bed.World.Add(child, new Life { AgeDays = 8 * 60, Stage = "child" });

        var seen = RunWatching(bed, [adolescent, child], 600);

        seen[adolescent].ShouldContain(JobKind.Harvest);
        seen[adolescent].ShouldNotContain(JobKind.Chop);
        seen[child].ShouldBeEmpty();
    }

    [Fact]
    public void In_a_crisis_there_is_no_work_on_its_own()
    {
        var bed = FreeBed();
        bed.AddPlant("bush", 10, 8, 1);
        var colonist = bed.AddColonist(8, 8, 1);
        bed.World.Add(colonist, new Crisis { Kind = "isolate", UntilTick = 1_000_000 });

        RunWatching(bed, colonist, 600).ShouldBeEmpty();
    }

    [Fact]
    public void Work_on_its_own_under_way_ends_even_if_the_target_is_met_meanwhile()
    {
        var bed = FreeBed();
        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand("wood", 0));
        var bush = bed.AddPlant("bush", 13, 8, 1);
        var colonist = bed.AddColonist(3, 8, 1);
        RunUntil(bed, () => bed.JobOf(colonist) is { Initiative: true }, 300).ShouldBeTrue();

        bed.AddItems("provisions", 50, 2, 2, 1);
        bed.AddItems("provisions", 50, 2, 3, 1);
        bed.AddItems("provisions", 50, 2, 4, 1);
        bed.Simulation.Run(200);

        bed.World.Has<Harvested>(bush).ShouldBeTrue();
    }

    [Fact]
    public void Turning_free_will_off_ends_the_work_on_its_own_under_way()
    {
        var bed = FreeBed();
        var bush = bed.AddPlant("bush", 13, 8, 1);
        var colonist = bed.AddColonist(3, 8, 1);
        RunUntil(bed, () => bed.JobOf(colonist) is { Initiative: true }, 300).ShouldBeTrue();

        bed.Simulation.Commands.Enqueue(new SetFreeWillCommand(false));
        bed.Simulation.Run(2);

        bed.JobOf(colonist)?.Initiative.ShouldNotBe(true);
        RunWatching(bed, colonist, 300).ShouldBeEmpty();
        bed.World.Has<Harvested>(bush).ShouldBeFalse();
    }

    [Theory]
    [InlineData(WorkPriorityLevels.Lowest, JobKind.Chop)]
    [InlineData(WorkPriorityLevels.Highest, JobKind.Harvest)]
    public void The_target_that_lacks_most_goes_first_unless_another_has_a_higher_priority(int gathering, JobKind expected)
    {
        // Food lacks half its target and wood all of it.
        var bed = FreeBed();
        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand("food", 20));
        bed.AddItems("berries", 10, 2, 2, 1);
        bed.AddPlant("bush", 9, 8, 1);
        bed.AddPlant("tree", 7, 9, 1);
        var colonist = bed.AddColonist(8, 8, 1);
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "gathering", gathering));
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "woodcutting", WorkPriorityLevels.Lowest));

        RunUntil(bed, () => bed.JobOf(colonist) is { Initiative: true }, 300).ShouldBeTrue();

        bed.JobOf(colonist)!.Kind.ShouldBe(expected);
    }

    [Fact]
    public void Colonists_share_out_the_targets_counting_what_the_work_under_way_will_bring()
    {
        // Two trees of four logs fill the wood target of 8: never more choppers than that, and once
        // one chops, wood lacks half and food all, so the others gather.
        var bed = FreeBed();
        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand("wood", 8));
        foreach (var (x, y) in new[] { (5, 5), (11, 5), (5, 11), (11, 11) })
        {
            bed.AddPlant("tree", x, y, 1);
            bed.AddPlant("bush", x + 1, y + 1, 1);
        }

        var colonists = new[] { bed.AddColonist(8, 8, 1), bed.AddColonist(8, 9, 1), bed.AddColonist(9, 8, 1), bed.AddColonist(9, 9, 1) };
        var first = new Dictionary<EntityId, JobKind>();
        RunUntil(
            bed,
            () =>
            {
                foreach (var colonist in colonists)
                {
                    if (!first.ContainsKey(colonist) && bed.JobOf(colonist) is { Initiative: true } job)
                    {
                        first[colonist] = job.Kind;
                    }
                }

                return first.Count == colonists.Length;
            },
            300).ShouldBeTrue();

        first.Values.Count(kind => kind == JobKind.Chop).ShouldBe(1);
        first.Values.Count(kind => kind == JobKind.Harvest).ShouldBe(3);
    }

    [Theory]
    [InlineData(0, false, 30)]
    [InlineData(50, false, 60)]
    [InlineData(100, false, 90)]
    [InlineData(50, true, 30)]
    public void A_diligent_colonist_is_keener_than_a_lazy_one_and_a_low_mood_halves_it(int diligence, bool lowMood, int expected)
    {
        var bed = FreeBed();
        var colonist = bed.AddColonist(8, 8, 1);
        bed.World.Add(colonist, new Personality { Traits = [new TraitValue { Trait = "diligence", Value = diligence }] });
        if (lowMood)
        {
            MoodRules.Feel(bed.Simulation, colonist, "death_child");
            MoodRules.Feel(bed.Simulation, colonist, "death_child");
        }

        FreeWillRules.InitiativeChancePercent(bed.Simulation, colonist).ShouldBe(expected);
    }

    [Fact]
    public void Work_on_its_own_is_saved_and_goes_on_after_loading()
    {
        var bed = FreeBed();
        bed.AddPlant("bush", 13, 8, 1);
        var colonist = bed.AddColonist(3, 8, 1);
        RunUntil(bed, () => bed.JobOf(colonist) is { Initiative: true }, 300).ShouldBeTrue();

        var file = new MemoryStream();
        SaveGame.Write(file, bed.Simulation, new SaveInfo("game-1", IsAutosave: false, "test", DateTimeOffset.UnixEpoch));
        var loaded = SaveGame.Load(new MemoryStream(file.ToArray()), ColonyTestBed.Content);
        ColonySystems.AddTo(loaded);

        loaded.World.Get<Job>(colonist).Initiative.ShouldBeTrue();
        loaded.ComputeStateHash().ShouldBe(bed.Simulation.ComputeStateHash());
        loaded.Run(300);
        bed.Simulation.Run(300);
        loaded.ComputeStateHash().ShouldBe(bed.Simulation.ComputeStateHash());
    }

    /// <summary>A flat bed of 16 × 16 with free will on and the village in its middle.</summary>
    private static ColonyTestBed FreeBed()
    {
        var bed = ColonyTestBed.Flat(16);
        bed.Simulation.FreeWill = true;
        bed.Simulation.VillageCenter = Center;
        return bed;
    }

    /// <summary>A bed with free will off, a bush and a tree near the village and one colonist.</summary>
    private static (ColonyTestBed Bed, EntityId Colonist) OffBed(DefDatabase content)
    {
        var bed = ColonyTestBed.Flat(16, content: content);
        bed.Simulation.VillageCenter = Center;
        bed.AddPlant("bush", 10, 8, 1);
        bed.AddPlant("tree", 6, 10, 1);
        return (bed, bed.AddColonist(8, 8, 1));
    }

    /// <summary>The test content without free will: no FreeWillConfig and no InitiativeTask.</summary>
    private static DefDatabase ContentWithoutFreeWill()
    {
        var json = Regex.Replace(ColonyTestBed.ContentJson, @"\{ ""type"": ""FreeWillConfig"".*?""targetMax"": 999 \},", string.Empty, RegexOptions.Singleline);
        json = Regex.Replace(json, @"\{ ""type"": ""InitiativeTask""[^}]*\},", string.Empty);
        json.ShouldNotContain("FreeWillConfig");
        json.ShouldNotContain("InitiativeTask");
        return TestContent.WithDefs(json);
    }

    /// <summary>The jobs a colonist took on its own in so many ticks.</summary>
    private static List<JobKind> RunWatching(ColonyTestBed bed, EntityId colonist, int ticks) => RunWatching(bed, [colonist], ticks)[colonist];

    private static Dictionary<EntityId, List<JobKind>> RunWatching(ColonyTestBed bed, EntityId[] colonists, int ticks)
    {
        var seen = colonists.ToDictionary(colonist => colonist, _ => new List<JobKind>());
        for (var tick = 0; tick < ticks; tick++)
        {
            bed.Simulation.Tick();
            foreach (var colonist in colonists)
            {
                if (bed.JobOf(colonist) is { Initiative: true } job && !seen[colonist].Contains(job.Kind))
                {
                    seen[colonist].Add(job.Kind);
                }
            }
        }

        return seen;
    }

    private static bool RunUntil(ColonyTestBed bed, Func<bool> done, int ticks)
    {
        for (var tick = 0; tick < ticks && !done(); tick++)
        {
            bed.Simulation.Tick();
        }

        return done();
    }
}
