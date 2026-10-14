using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;

namespace OurLife.Core.Tests.Colony;

/// <summary>Furniture, beds and chests (<c>docs/gdd/muebles-y-camas.md</c>). Tick 0 is 06:00; the night starts at tick 900.</summary>
public class FurnitureTests
{
    private const int Night = 900;

    [Fact]
    public void A_colonist_in_a_bed_recovers_rest_faster_than_one_on_the_ground()
    {
        var bed = ColonyTestBed.Flat(16);
        Furnish(bed, "bed", new TileCoord(3, 3, 1));
        bed.Simulation.Run(Night);
        var inBed = bed.AddColonist(4, 3, 1, rest: 2000, name: "In bed");
        var onGround = bed.AddColonist(12, 12, 1, rest: 2000, name: "On the ground");

        bed.Simulation.Run(120);

        bed.JobOf(inBed)!.Target.ShouldBe(BedAt(bed, new TileCoord(3, 3, 1)));
        bed.World.Get<Position>(inBed).Cell.ShouldBe(new TileCoord(3, 3, 1));
        bed.Need(inBed, "rest").Value.ShouldBeGreaterThan(bed.Need(onGround, "rest").Value);
    }

    [Fact]
    public void A_colonist_without_a_bed_takes_the_free_one_and_the_next_sleeps_on_the_ground()
    {
        var bed = ColonyTestBed.Flat(16);
        Furnish(bed, "bed", new TileCoord(3, 3, 1));
        bed.Simulation.Run(Night);
        var first = bed.AddColonist(6, 6, 1, rest: 2000, name: "First");
        var second = bed.AddColonist(7, 6, 1, rest: 2000, name: "Second");

        bed.Simulation.Run(60);

        var beds = Beds(bed);
        beds.Count.ShouldBe(1);
        bed.World.Get<Bed>(beds[0]).Owner.ShouldBe(first);
        bed.World.Get<Position>(first).Cell.ShouldBe(new TileCoord(3, 3, 1));
        bed.JobOf(second)!.Kind.ShouldBe(JobKind.Sleep);
        bed.JobOf(second)!.Target.IsNone.ShouldBeTrue();
    }

    [Fact]
    public void A_bed_is_free_again_when_its_owner_is_gone()
    {
        var bed = ColonyTestBed.Flat(16);
        Furnish(bed, "bed", new TileCoord(3, 3, 1));
        var owner = bed.AddColonist(12, 12, 1, name: "Owner");
        bed.World.Get<Bed>(Beds(bed)[0]).Owner = owner;
        bed.World.DestroyEntity(owner);
        bed.Simulation.Run(Night);
        var sleeper = bed.AddColonist(6, 6, 1, rest: 2000, name: "Sleeper");

        bed.Simulation.Run(60);

        bed.World.Get<Bed>(Beds(bed)[0]).Owner.ShouldBe(sleeper);
    }

    [Fact]
    public void A_finished_chest_takes_loose_logs_and_taking_it_down_leaves_them_there()
    {
        var bed = ColonyTestBed.Flat(16);
        var chest = new TileCoord(8, 8, 1);
        Furnish(bed, "chest", chest);
        bed.AddItems("log", 10, 2, 2, 1);
        bed.AddColonist(5, 5, 1);

        bed.Simulation.Run(300);
        Piles(bed, "log").ShouldBe([(chest, 10)]);

        bed.Designate(DesignationKind.Deconstruct, chest, chest);
        bed.Simulation.Run(200);

        bed.World.Store<Furniture>().Count.ShouldBe(0);
        bed.World.Store<Stockpile>().Count.ShouldBe(0);
        bed.CountItems("log").ShouldBe(12);
        Piles(bed, "log").ShouldContain(pile => pile.Cell == chest && pile.Count >= 10);
    }

    [Fact]
    public void A_bed_planned_turned_once_is_built_turned_once()
    {
        var bed = ColonyTestBed.Flat(16);
        bed.AddItems("log", 10, 2, 2, 1);
        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand("bed", "timber", new TileCoord(8, 8, 1), new TileCoord(8, 8, 1), Rotation: 5));
        bed.AddColonist(5, 5, 1);

        bed.Simulation.Run(500);

        var furniture = bed.World.Get<Furniture>(Beds(bed).Single());
        furniture.Rotation.ShouldBe(1);
        bed.CountItems("log").ShouldBe(6);
    }

    [Fact]
    public void Furniture_keeps_off_stockpiles_other_furniture_and_loose_ground_and_walls_keep_off_furniture()
    {
        var bed = new ColonyTestBed("#####\n##.##\n#####", ".....\n.....\n.....");
        Furnish(bed, "table", new TileCoord(0, 0, 1));
        bed.Designate(DesignationKind.Stockpile, new TileCoord(4, 0, 1), new TileCoord(4, 2, 1));
        bed.Simulation.ApplyCommandsNow();

        Place(bed, "bed", new TileCoord(0, 0, 1));
        Place(bed, "bed", new TileCoord(4, 1, 1));
        Place(bed, "bed", new TileCoord(2, 1, 1));
        Place(bed, "wall", new TileCoord(0, 0, 1));
        Place(bed, "bed", new TileCoord(1, 1, 1));

        var blueprints = new List<EntityId>();
        bed.World.Query<Blueprint, Position>(blueprints);
        blueprints.Select(blueprint => bed.World.Get<Position>(blueprint).Cell).ShouldBe([new TileCoord(1, 1, 1)]);
    }

    [Fact]
    public void The_cells_to_plan_are_the_ones_the_command_takes_and_asking_changes_nothing()
    {
        // A table, a stockpile, loose ground and walls leave a mixed box (docs/gdd/vista-previa-de-muebles.md).
        var bed = new ColonyTestBed("#####\n##.##\n#####", ".....\n.....\n.....");
        Furnish(bed, "table", new TileCoord(0, 0, 1));
        bed.Designate(DesignationKind.Stockpile, new TileCoord(4, 0, 1), new TileCoord(4, 2, 1));
        bed.Simulation.ApplyCommandsNow();
        var hash = bed.Simulation.ComputeStateHash();
        var (from, to) = (new TileCoord(0, 0, 1), new TileCoord(4, 2, 1));

        var cells = Construction.CellsToPlan(bed.Simulation, bed.Simulation.Content.Get<BuildableDef>("bed"), from, to);

        bed.Simulation.ComputeStateHash().ShouldBe(hash);
        cells.Count.ShouldBe(10);
        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand("bed", "timber", from, to));
        bed.Simulation.ApplyCommandsNow();
        var blueprints = new List<EntityId>();
        bed.World.Query<Blueprint, Position>(blueprints);
        blueprints.Select(blueprint => bed.World.Get<Position>(blueprint).Cell).ShouldBe(cells);
    }

    [Fact]
    public void A_stockpile_skips_furniture_and_cancel_leaves_a_chest_alone()
    {
        var bed = ColonyTestBed.Flat(16);
        Furnish(bed, "table", new TileCoord(4, 4, 1));
        Furnish(bed, "chest", new TileCoord(5, 4, 1));
        Place(bed, "bed", new TileCoord(3, 4, 1));

        bed.Designate(DesignationKind.Stockpile, new TileCoord(3, 4, 1), new TileCoord(6, 4, 1));
        bed.Simulation.ApplyCommandsNow();
        var marked = new List<EntityId>();
        bed.World.Query<Stockpile>(marked);
        marked.Where(zone => !bed.World.Has<Furniture>(zone)).Select(zone => bed.World.Get<Stockpile>(zone).Cells).Single()
            .ShouldBe([new TileCoord(6, 4, 1)]);
        bed.Designate(DesignationKind.Cancel, new TileCoord(0, 0, 1), new TileCoord(15, 15, 1));
        bed.Simulation.ApplyCommandsNow();

        var zones = new List<EntityId>();
        bed.World.Query<Stockpile>(zones);
        zones.Count.ShouldBe(1);
        bed.World.Has<Furniture>(zones[0]).ShouldBeTrue();
        bed.World.Store<Furniture>().Count.ShouldBe(2);
    }

    /// <summary>Plans a piece of furniture with all its material and finishes it at once.</summary>
    private static void Furnish(ColonyTestBed bed, string buildable, TileCoord cell)
    {
        Place(bed, buildable, cell);
        var blueprints = new List<EntityId>();
        bed.World.Query<Blueprint, Position>(blueprints);
        var blueprint = blueprints.Single(entity => bed.World.Get<Position>(entity).Cell == cell);
        Construction.Finish(bed.Simulation, blueprint, cell);
    }

    private static void Place(ColonyTestBed bed, string buildable, TileCoord cell)
    {
        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand(buildable, "timber", cell, cell));
        bed.Simulation.ApplyCommandsNow();
    }

    private static List<EntityId> Beds(ColonyTestBed bed)
    {
        var beds = new List<EntityId>();
        bed.World.Query<Bed>(beds);
        return beds;
    }

    private static EntityId BedAt(ColonyTestBed bed, TileCoord cell) =>
        Beds(bed).Single(entity => bed.World.Get<Position>(entity).Cell == cell);

    private static List<(TileCoord Cell, int Count)> Piles(ColonyTestBed bed, string item)
    {
        var piles = new List<EntityId>();
        bed.World.Query<ItemStack, Position>(piles);
        return [.. piles.Where(pile => bed.World.Get<ItemStack>(pile).Def == item)
            .Select(pile => (bed.World.Get<Position>(pile).Cell, bed.World.Get<ItemStack>(pile).Count))];
    }
}
