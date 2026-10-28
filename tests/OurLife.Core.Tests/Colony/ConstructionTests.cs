using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;

namespace OurLife.Core.Tests.Colony;

/// <summary>Blueprints, building, taking down and channels (<c>docs/gdd/construccion.md</c>). Tick 0 is 06:00.</summary>
public class ConstructionTests
{
    [Fact]
    public void A_wall_blueprint_and_logs_in_a_stockpile_make_a_timber_wall()
    {
        var bed = ColonyTestBed.Flat(16);
        Stockpile(bed, new TileCoord(2, 2, 1));
        bed.AddItems("log", 10, 2, 2, 1);
        Place(bed, "wall", "timber", new TileCoord(8, 8, 1));
        bed.AddColonist(5, 5, 1);

        bed.Simulation.Run(600);

        bed.Map.ShapeAt(8, 8, 1).ShouldBe(TileShape.Wall);
        bed.Map.MaterialAt(8, 8, 1)!.Id.ShouldBe("timber");
        bed.CountItems("log").ShouldBe(8);
        bed.World.Store<Blueprint>().Count.ShouldBe(0);
        bed.World.Store<Carried>().Count.ShouldBe(0);
    }

    [Fact]
    public void A_blueprint_with_no_material_around_waits_until_some_appears()
    {
        var bed = ColonyTestBed.Flat(16);
        Place(bed, "wall", "masonry", new TileCoord(8, 8, 1));
        bed.AddItems("log", 10, 2, 2, 1);
        bed.AddColonist(5, 5, 1);

        bed.Simulation.Run(300);
        bed.Map.ShapeAt(8, 8, 1).ShouldBe(TileShape.Floor);
        bed.World.Store<Blueprint>().Count.ShouldBe(1);

        bed.AddItems("stone", 3, 12, 12, 1);
        bed.Simulation.Run(400);
        bed.Map.ShapeAt(8, 8, 1).ShouldBe(TileShape.Wall);
        bed.Map.MaterialAt(8, 8, 1)!.Id.ShouldBe("masonry");
        bed.CountItems("stone").ShouldBe(1);
        bed.CountItems("log").ShouldBe(10);
    }

    [Fact]
    public void A_wall_does_not_rise_over_a_colonist_standing_on_its_cell()
    {
        var bed = ColonyTestBed.Flat(16);
        var cell = new TileCoord(8, 8, 1);
        Place(bed, "wall", "timber", cell);
        Blueprints(bed)[0].Plan.Delivered = 2;

        // A sleeper on the cell, rested enough to wake up after a few hours.
        var sleeper = bed.AddColonist(8, 8, 1, rest: 3000, name: "Sleeper");
        bed.World.Add(sleeper, new Job { Kind = JobKind.Sleep, Stage = JobStage.Working, Cell = cell });
        bed.AddColonist(4, 4, 1, name: "Builder");

        bed.Simulation.Run(200);
        bed.Map.ShapeAt(cell).ShouldBe(TileShape.Floor);
        bed.World.Get<Position>(sleeper).Cell.ShouldBe(cell);

        bed.Simulation.Run(600);
        bed.Map.ShapeAt(cell).ShouldBe(TileShape.Wall);
        bed.World.Get<Position>(sleeper).Cell.ShouldNotBe(cell);
    }

    [Fact]
    public void Dragging_a_wall_over_a_box_plans_only_its_border()
    {
        var bed = ColonyTestBed.Flat(16);

        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand("wall", "timber", new TileCoord(4, 4, 1), new TileCoord(8, 8, 1)));
        bed.Simulation.ApplyCommandsNow();

        var cells = Blueprints(bed).Select(blueprint => blueprint.Cell).ToList();
        cells.Count.ShouldBe(16);
        cells.ShouldAllBe(cell => cell.X == 4 || cell.X == 8 || cell.Y == 4 || cell.Y == 8);
    }

    [Fact]
    public void A_house_of_four_walls_with_a_door_is_built_and_entered_through_the_door()
    {
        var bed = ColonyTestBed.Flat(16);
        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand("wall", "timber", new TileCoord(4, 4, 1), new TileCoord(8, 8, 1)));
        bed.Designate(DesignationKind.Cancel, new TileCoord(6, 8, 1), new TileCoord(6, 8, 1));
        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand("door", "timber", new TileCoord(6, 8, 1), new TileCoord(6, 8, 1)));
        bed.Simulation.ApplyCommandsNow();
        Blueprints(bed).Count.ShouldBe(16);
        bed.AddItems("log", 25, 12, 2, 1);
        bed.AddItems("log", 25, 13, 2, 1);
        bed.AddItems("provisions", 50, 12, 12, 1);
        bed.AddColonist(11, 11, 1);
        bed.AddColonist(12, 11, 1);

        bed.Simulation.Run(3000);

        bed.World.Store<Blueprint>().Count.ShouldBe(0);
        bed.Map.ShapeAt(6, 8, 1).ShouldBe(TileShape.Door);
        for (var i = 4; i <= 8; i++)
        {
            bed.Map.ShapeAt(i, 4, 1).ShouldBe(TileShape.Wall);
            bed.Map.ShapeAt(4, i, 1).ShouldBe(TileShape.Wall);
            bed.Map.ShapeAt(8, i, 1).ShouldBe(TileShape.Wall);
        }

        bed.CountItems("log").ShouldBe(50 - (15 * 2) - 3);
        bed.Simulation.Connectivity.AreConnected(new TileCoord(11, 11, 1), new TileCoord(6, 6, 1)).ShouldBeTrue();

        // Without the door, the inside is closed off.
        bed.Map.SetTile(6, 8, 1, TileShape.Wall, (ushort)bed.Simulation.Content.IndexOf<MaterialDef>("timber"), TileFlags.None);
        bed.Simulation.Connectivity.AreConnected(new TileCoord(11, 11, 1), new TileCoord(6, 6, 1)).ShouldBeFalse();
    }

    [Fact]
    public void A_channel_opens_the_floor_and_leaves_a_ramp_to_walk_down_and_up()
    {
        var bed = ColonyTestBed.Flat(16);
        bed.Designate(DesignationKind.Channel, new TileCoord(8, 8, 1), new TileCoord(8, 8, 1));
        bed.AddColonist(5, 5, 1);

        bed.Simulation.Run(200);

        bed.Map.ShapeAt(8, 8, 1).ShouldBe(TileShape.Empty);
        bed.Map.ShapeAt(8, 8, 0).ShouldBe(TileShape.Ramp);
        (bed.Map.FlagsAt(8, 8, 1) & ~TileFlags.Discovered).ShouldBe(TileFlags.None);
        bed.CountItems("stone").ShouldBe(1);
        bed.Simulation.Connectivity.AreConnected(new TileCoord(5, 5, 1), new TileCoord(8, 8, 0)).ShouldBeTrue();
    }

    [Fact]
    public void Taking_down_a_timber_wall_leaves_a_floor_of_the_rock_below_and_a_log()
    {
        var bed = ColonyTestBed.Flat(16);
        Build(bed, TileShape.Wall, "timber", new TileCoord(8, 8, 1));
        bed.Designate(DesignationKind.Deconstruct, new TileCoord(8, 8, 1), new TileCoord(8, 8, 1));
        bed.AddColonist(5, 5, 1);

        bed.Simulation.Run(200);

        bed.Map.ShapeAt(8, 8, 1).ShouldBe(TileShape.Floor);
        bed.Map.MaterialAt(8, 8, 1)!.Id.ShouldBe("rock");
        bed.CountItems("log").ShouldBe(1);
    }

    [Fact]
    public void Stairs_are_built_on_two_levels_and_lead_up()
    {
        // A cliff to the east: rock up to level 1, its top floor on level 2.
        var bed = new ColonyTestBed(
            ColonyTestBed.Level(8, 8, '#'),
            "....####\n....####\n....####\n....####\n....####\n....####\n....####\n....####",
            "    ....\n    ....\n    ....\n    ....\n    ....\n    ....\n    ....\n    ....");
        bed.AddItems("stone", 10, 0, 0, 1);
        Place(bed, "stairs", "masonry", new TileCoord(2, 4, 1));
        Place(bed, "floor", "masonry", new TileCoord(3, 4, 2));
        bed.AddColonist(1, 1, 1);
        bed.Simulation.Connectivity.AreConnected(new TileCoord(1, 1, 1), new TileCoord(6, 4, 2)).ShouldBeFalse();

        bed.Simulation.Run(1200);

        bed.Map.ShapeAt(2, 4, 1).ShouldBe(TileShape.StairUp);
        bed.Map.ShapeAt(2, 4, 2).ShouldBe(TileShape.StairDown);
        bed.Map.ShapeAt(3, 4, 2).ShouldBe(TileShape.Floor);
        bed.Simulation.Connectivity.AreConnected(new TileCoord(1, 1, 1), new TileCoord(6, 4, 2)).ShouldBeTrue();
    }

    [Fact]
    public void A_ramp_needs_a_wall_beside_it_to_lead_up()
    {
        var bed = new ColonyTestBed(
            ColonyTestBed.Level(8, 3, '#'),
            "....####\n....####\n....####",
            "    ....\n    ....\n    ....");

        Place(bed, "ramp", "timber", new TileCoord(1, 1, 1));
        Place(bed, "ramp", "timber", new TileCoord(3, 1, 1));

        Blueprints(bed).Select(blueprint => blueprint.Cell).ShouldBe([new TileCoord(3, 1, 1)]);
    }

    [Fact]
    public void Cancelling_a_blueprint_leaves_what_was_delivered_on_its_cell()
    {
        var bed = ColonyTestBed.Flat(16);
        Place(bed, "wall", "timber", new TileCoord(8, 8, 1));
        Blueprints(bed)[0].Plan.Delivered = 2;

        bed.Designate(DesignationKind.Cancel, new TileCoord(7, 7, 1), new TileCoord(9, 9, 1));
        bed.Simulation.ApplyCommandsNow();

        bed.World.Store<Blueprint>().Count.ShouldBe(0);
        bed.CountItems("log").ShouldBe(2);
    }

    [Fact]
    public void A_blueprint_whose_place_changes_goes_away_with_what_it_had()
    {
        var bed = ColonyTestBed.Flat(16);
        Place(bed, "wall", "timber", new TileCoord(8, 8, 1));
        Blueprints(bed)[0].Plan.Delivered = 1;

        bed.Map.SetTile(8, 8, 1, TileShape.Ramp, bed.Map.MaterialIndexAt(8, 8, 1), TileFlags.None);
        bed.Simulation.Tick();

        bed.World.Store<Blueprint>().Count.ShouldBe(0);
        bed.CountItems("log").ShouldBe(1);
    }

    [Fact]
    public void Mining_skips_built_walls_and_taking_down_skips_natural_cells()
    {
        var bed = ColonyTestBed.Flat(16);
        Build(bed, TileShape.Wall, "timber", new TileCoord(8, 8, 1));

        bed.Designate(DesignationKind.Mine, new TileCoord(8, 8, 0), new TileCoord(8, 8, 1));
        bed.Designate(DesignationKind.Deconstruct, new TileCoord(7, 8, 0), new TileCoord(8, 8, 1));
        bed.Simulation.ApplyCommandsNow();

        bed.Map.FlagsAt(8, 8, 0).ShouldBe(TileFlags.MineDesignation);
        bed.Map.FlagsAt(8, 8, 1).ShouldBe(TileFlags.DeconstructDesignation);
        bed.Map.FlagsAt(7, 8, 0).ShouldBe(TileFlags.None);
        bed.Map.FlagsAt(7, 8, 1).ShouldBe(TileFlags.None);
    }

    [Fact]
    public void Orders_and_blueprints_keep_off_each_other()
    {
        var bed = ColonyTestBed.Flat(16);
        Place(bed, "wall", "timber", new TileCoord(8, 8, 1));
        bed.Designate(DesignationKind.Channel, new TileCoord(7, 8, 1), new TileCoord(8, 8, 1));
        bed.Simulation.ApplyCommandsNow();
        Place(bed, "wall", "timber", new TileCoord(7, 8, 1));

        bed.Map.FlagsAt(8, 8, 1).ShouldBe(TileFlags.None);
        bed.Map.FlagsAt(7, 8, 1).ShouldBe(TileFlags.ChannelDesignation);
        Blueprints(bed).Select(blueprint => blueprint.Cell).ShouldBe([new TileCoord(8, 8, 1)]);
    }

    [Fact]
    public void Builders_of_neighbouring_walls_never_stand_on_each_others_cell()
    {
        // A corridor one cell wide: the only place to build the first wall from is the second
        // wall's cell. Whoever built it from there would wait forever for the other builder to
        // leave the first wall's cell.
        var bed = new ColonyTestBed(ColonyTestBed.Level(5, 3, '#'), "#####\n#...#\n#####");
        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand("wall", "timber", new TileCoord(1, 1, 1), new TileCoord(2, 1, 1)));
        bed.Simulation.ApplyCommandsNow();
        foreach (var blueprint in Blueprints(bed))
        {
            blueprint.Plan.Delivered = 2;
        }

        var colonists = new[] { bed.AddColonist(1, 1, 1, name: "First"), bed.AddColonist(2, 1, 1, name: "Second") };

        bed.Simulation.Tick();

        var wallCells = Blueprints(bed).Select(blueprint => blueprint.Cell).ToHashSet();
        foreach (var colonist in colonists)
        {
            if (bed.JobOf(colonist) is { Kind: JobKind.Build })
            {
                var path = bed.World.Get<Movement>(colonist).Path;
                var site = path.Count > 0 ? path[^1] : bed.World.Get<Position>(colonist).Cell;
                wallCells.ShouldNotContain(site, $"{bed.World.Get<Colonist>(colonist).Name} builds {bed.JobOf(colonist)!.Cell} from {site}, path {string.Join(" ", path)}");
            }
        }

        bed.Simulation.Run(600);
        bed.Map.ShapeAt(2, 1, 1).ShouldBe(TileShape.Wall);
    }

    [Fact]
    public void Walls_go_only_on_firm_ground_never_on_a_floor_left_over_air()
    {
        var bed = new ColonyTestBed("###\n#.#\n###", "...\n...\n...");

        Place(bed, "wall", "timber", new TileCoord(1, 1, 1));
        Place(bed, "wall", "timber", new TileCoord(0, 0, 1));

        Blueprints(bed).Select(blueprint => blueprint.Cell).ShouldBe([new TileCoord(0, 0, 1)]);
    }

    [Fact]
    public void Taking_down_stairs_opens_the_upper_half_and_drops_what_lay_there_by_the_worker()
    {
        var bed = new ColonyTestBed(
            ColonyTestBed.Level(8, 8, '#'),
            "....####\n....####\n....####\n....####\n....####\n....####\n....####\n....####",
            "    ....\n    ....\n    ....\n    ....\n    ....\n    ....\n    ....\n    ....");
        Build(bed, TileShape.StairUp, "masonry", new TileCoord(2, 4, 1));
        Build(bed, TileShape.StairDown, "masonry", new TileCoord(2, 4, 2));
        bed.AddItems("log", 5, 2, 4, 2);
        bed.Designate(DesignationKind.Deconstruct, new TileCoord(2, 4, 1), new TileCoord(2, 4, 1));
        bed.AddColonist(1, 1, 1);

        bed.Simulation.Run(300);

        bed.Map.ShapeAt(2, 4, 1).ShouldBe(TileShape.Floor);
        bed.Map.ShapeAt(2, 4, 2).ShouldBe(TileShape.Empty);
        bed.CountItems("log").ShouldBe(5);
        bed.CountItems("stone").ShouldBe(2);
        var piles = new List<EntityId>();
        bed.World.Query<ItemStack, Position>(piles);
        piles.ShouldAllBe(pile => bed.Map.IsStandable(bed.World.Get<Position>(pile).Cell));
    }

    private static void Place(ColonyTestBed bed, string buildable, string material, TileCoord cell)
    {
        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand(buildable, material, cell, cell));
        bed.Simulation.ApplyCommandsNow();
    }

    private static void Stockpile(ColonyTestBed bed, TileCoord cell)
    {
        bed.Designate(DesignationKind.Stockpile, cell, cell);
        bed.Simulation.ApplyCommandsNow();
    }

    /// <summary>Puts something already built on a cell, as if colonists had built it.</summary>
    private static void Build(ColonyTestBed bed, TileShape shape, string material, TileCoord cell) =>
        bed.Map.SetTile(cell.X, cell.Y, cell.Z, shape, (ushort)bed.Simulation.Content.IndexOf<MaterialDef>(material), TileFlags.None);

    private static List<(EntityId Entity, Blueprint Plan, TileCoord Cell)> Blueprints(ColonyTestBed bed)
    {
        var blueprints = new List<EntityId>();
        bed.World.Query<Blueprint, Position>(blueprints);
        return [.. blueprints.Select(entity => (entity, bed.World.Get<Blueprint>(entity), bed.World.Get<Position>(entity).Cell))];
    }
}
