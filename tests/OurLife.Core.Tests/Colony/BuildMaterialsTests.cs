using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;

namespace OurLife.Core.Tests.Colony;

/// <summary>
/// Why a plan does not always get built, with the real content (<c>docs/gdd/construccion.md</c>,
/// "Materiales y avisos"): furniture asks for planks, walls for the material chosen, and what the
/// colony holds is spread among the plans.
/// </summary>
public class BuildMaterialsTests
{
    private static readonly Lazy<DefDatabase> RealContent = new(() => DefLoader.Load(new FileSystemContentSource(RepoPaths.Content)));

    [Fact]
    public void A_timber_wall_is_built_from_logs_lying_loose_or_in_a_stockpile()
    {
        var loose = Flat();
        Place(loose, "wall", "timber", new TileCoord(8, 8, 1));
        loose.AddItems("log", 4, 2, 2, 1);
        loose.AddColonist(5, 5, 1);
        loose.Simulation.Run(900);
        loose.Map.ShapeAt(8, 8, 1).ShouldBe(TileShape.Wall);

        var stored = Flat();
        stored.Designate(DesignationKind.Stockpile, new TileCoord(2, 2, 1), new TileCoord(2, 2, 1));
        stored.Simulation.ApplyCommandsNow();
        Place(stored, "wall", "timber", new TileCoord(8, 8, 1));
        stored.AddItems("log", 4, 2, 2, 1);
        stored.AddColonist(5, 5, 1);
        stored.Simulation.Run(900);
        stored.Map.ShapeAt(8, 8, 1).ShouldBe(TileShape.Wall);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(3, false)]
    [InlineData(0, false)]
    public void A_bed_needs_four_planks_and_logs_do_not_stand_for_them(int planks, bool built)
    {
        var bed = Flat();
        Place(bed, "bed", "planks", new TileCoord(8, 8, 1));
        bed.AddItems("log", 20, 2, 2, 1);
        if (planks > 0)
        {
            bed.AddItems("plank", planks, 3, 3, 1);
        }

        bed.AddColonist(5, 5, 1);
        bed.Simulation.Run(1200);

        (bed.World.Store<Furniture>().Count == 1).ShouldBe(built);
        bed.World.Store<Blueprint>().Count.ShouldBe(built ? 0 : 1);
    }

    [Fact]
    public void Scattered_single_logs_must_finish_the_wall_they_started_instead_of_one_each_for_ten_walls()
    {
        var bed = Flat();
        for (var x = 6; x < 16; x++)
        {
            Place(bed, "wall", "timber", new TileCoord(x, 10, 1));
        }

        // Five logs in five piles of one: enough for two walls and a half, in a place far from them.
        for (var i = 0; i < 5; i++)
        {
            bed.AddItems("log", 1, 1 + i, 1, 1);
        }

        for (var i = 0; i < 5; i++)
        {
            bed.AddColonist(2 + i, 3, 1, name: "Builder " + i);
        }

        bed.Simulation.Run(3000);

        var walls = 0;
        for (var x = 6; x < 16; x++)
        {
            walls += bed.Map.ShapeAt(x, 10, 1) == TileShape.Wall ? 1 : 0;
        }

        walls.ShouldBe(2);
    }

    [Fact]
    public void Two_beds_and_six_planks_build_one_and_wait_for_the_rest()
    {
        var bed = Flat();
        Place(bed, "bed", "planks", new TileCoord(8, 8, 1));
        Place(bed, "bed", "planks", new TileCoord(10, 8, 1));
        bed.AddItems("plank", 6, 3, 3, 1);
        bed.AddColonist(5, 5, 1);
        bed.Simulation.Run(2000);

        bed.World.Store<Furniture>().Count.ShouldBe(1);
        bed.World.Store<Blueprint>().Count.ShouldBe(1);
    }

    [Fact]
    public void A_colonist_who_never_builds_leaves_the_plan_waiting()
    {
        var bed = Flat();
        Place(bed, "bed", "planks", new TileCoord(8, 8, 1));
        bed.AddItems("plank", 4, 3, 3, 1);
        var colonist = bed.AddColonist(5, 5, 1);
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "construction", 0));
        bed.Simulation.Run(1200);

        bed.World.Store<Furniture>().Count.ShouldBe(0);
    }

    [Fact]
    public void The_status_of_a_plan_says_what_it_waits_for()
    {
        var bed = Flat();
        Place(bed, "bed", "planks", new TileCoord(8, 8, 1));
        var plan = Plans(bed).Single();
        bed.AddItems("log", 20, 2, 2, 1);
        bed.AddItems("plank", 3, 3, 3, 1);

        // Logs are not planks, and the colony holds three of the four.
        BuildQueries.StatusOf(bed.Simulation, plan).ShouldBe(new BuildStatus(BuildBlock.Material, "plank", 4, 3));

        // With the planks but no colonist who does construction.
        bed.AddItems("plank", 1, 3, 4, 1);
        BuildQueries.StatusOf(bed.Simulation, plan).Block.ShouldBe(BuildBlock.Builder);

        var colonist = bed.AddColonist(5, 5, 1);
        BuildQueries.StatusOf(bed.Simulation, plan).Block.ShouldBe(BuildBlock.None);
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "construction", 0));
        bed.Simulation.ApplyCommandsNow();
        BuildQueries.StatusOf(bed.Simulation, plan).Block.ShouldBe(BuildBlock.Builder);
    }

    [Fact]
    public void A_plan_in_a_closed_pocket_is_unreachable()
    {
        // A floor cell at (2, 2) closed in by rock, and an open yard where the colonist stands.
        var top = string.Join("\n", Enumerable.Range(0, 12).Select(y => y switch
        {
            2 => "##.#########",
            >= 6 and <= 10 => "######......",
            _ => new string('#', 12),
        }));
        var bed = new ColonyTestBed(RealContent.Value, 1, ColonyTestBed.Level(12, 12, '#'), top);
        bed.AddItems("plank", 4, 8, 8, 1);
        bed.AddColonist(8, 8, 1);
        Place(bed, "bed", "planks", new TileCoord(2, 2, 1));

        BuildQueries.StatusOf(bed.Simulation, Plans(bed).Single()).Block.ShouldBe(BuildBlock.Unreachable);
    }

    [Fact]
    public void Planks_of_two_qualities_both_count_toward_a_bed()
    {
        var bed = Flat();
        Place(bed, "bed", "planks", new TileCoord(8, 8, 1));
        bed.AddItems("plank", 2, 3, 3, 1);
        bed.AddItems("plank", 2, 3, 5, 1);
        var piles = new List<EntityId>();
        bed.World.Query<ItemStack, Position>(piles);
        bed.World.Get<ItemStack>(piles[0]).Quality = "good";
        bed.AddColonist(5, 5, 1);
        bed.Simulation.Run(1500);

        bed.World.Store<Furniture>().Count.ShouldBe(1);
    }

    [Fact]
    public void A_plan_nobody_can_reach_does_not_keep_the_planks_from_one_that_can_be_built()
    {
        // The unreachable bed is placed first (lowest id) in a pocket closed by rock; the other stands in the yard.
        var top = string.Join("\n", Enumerable.Range(0, 12).Select(y => y switch
        {
            2 => "##.#########",
            >= 6 and <= 10 => "######......",
            _ => new string('#', 12),
        }));
        var bed = new ColonyTestBed(RealContent.Value, 1, ColonyTestBed.Level(12, 12, '#'), top);
        bed.AddItems("plank", 4, 8, 8, 1);
        bed.AddColonist(7, 8, 1);
        Place(bed, "bed", "planks", new TileCoord(2, 2, 1));
        Place(bed, "bed", "planks", new TileCoord(10, 8, 1));
        var plans = Plans(bed);
        BuildQueries.StatusOf(bed.Simulation, plans[0]).Block.ShouldBe(BuildBlock.Unreachable);
        BuildQueries.StatusOf(bed.Simulation, plans[1]).Block.ShouldBe(BuildBlock.None);

        bed.Simulation.Run(1500);

        bed.World.Store<Furniture>().Count.ShouldBe(1);
        Plans(bed).ShouldBe([plans[0]]);
    }

    [Fact]
    public void Planks_a_job_is_going_to_fetch_do_not_count_for_a_plan_and_the_status_says_who_comes_first()
    {
        var bed = Flat();
        var colonist = bed.AddColonist(5, 5, 1);
        bed.AddItems("plank", 4, 3, 3, 1);
        var pile = new List<EntityId>();
        bed.World.Query<ItemStack, Position>(pile);
        Place(bed, "bed", "planks", new TileCoord(8, 8, 1));
        var plan = Plans(bed).Single();
        BuildQueries.StatusOf(bed.Simulation, plan).Block.ShouldBe(BuildBlock.None);

        // A recipe is on its way for the whole pile.
        bed.World.Add(colonist, new Job { Kind = JobKind.Craft, Target = pile.Single(), Cell = new TileCoord(3, 3, 1) });

        BuildQueries.StatusOf(bed.Simulation, plan).ShouldBe(new BuildStatus(BuildBlock.Competing, "plank", 4, 4));
    }

    [Fact]
    public void Planks_another_plan_claims_first_make_the_later_plan_say_so()
    {
        var bed = Flat();
        bed.AddItems("plank", 6, 3, 3, 1);
        bed.AddColonist(5, 5, 1);
        Place(bed, "bed", "planks", new TileCoord(8, 8, 1));
        Place(bed, "bed", "planks", new TileCoord(10, 8, 1));
        var plans = Plans(bed);

        BuildQueries.StatusOf(bed.Simulation, plans[0]).Block.ShouldBe(BuildBlock.None);
        BuildQueries.StatusOf(bed.Simulation, plans[1]).Block.ShouldBe(BuildBlock.Competing);
    }

    private static List<EntityId> Plans(ColonyTestBed bed)
    {
        var plans = new List<EntityId>();
        bed.World.Query<Blueprint, Position>(plans);
        return plans;
    }

    private static ColonyTestBed Flat() => ColonyTestBed.Flat(24, 1, RealContent.Value);

    private static void Place(ColonyTestBed bed, string buildable, string material, TileCoord cell)
    {
        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand(buildable, material, cell, cell));
        bed.Simulation.ApplyCommandsNow();
    }
}
