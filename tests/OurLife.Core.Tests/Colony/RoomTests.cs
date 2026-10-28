using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Save;
using OurLife.Core.Sim;
using OurLife.Core.Text;

namespace OurLife.Core.Tests.Colony;

/// <summary>
/// Rooms, their score and level, dormitories and beds, and the dining room (<c>docs/gdd/habitaciones.md</c>). The
/// houses are 5 × 5 with the door in the south wall at the middle, so the room inside is 3 × 3 with its cells at 3..5.
/// Tick 0 is 06:00; the night starts at tick 900.
/// </summary>
public class RoomTests
{
    private const int Night = 900;

    // ---- Detection ----

    [Fact]
    public void A_house_of_four_walls_with_a_door_is_a_room_of_nine_cells_and_the_wall_and_the_door_are_not()
    {
        var village = Village();
        House(village);

        var room = Rooms.At(village.Simulation, new TileCoord(4, 4, 1));

        room.ShouldNotBeNull();
        room.Cells.Count.ShouldBe(9);
        room.Cells.ShouldAllBe(cell => cell.X >= 3 && cell.X <= 5 && cell.Y >= 3 && cell.Y <= 5 && cell.Z == 1);
        room.Id.ShouldBe(village.Map.Size.IndexOf(3, 3, 1));
        Rooms.At(village.Simulation, new TileCoord(4, 6, 1)).ShouldBeNull();
        Rooms.At(village.Simulation, new TileCoord(2, 2, 1)).ShouldBeNull();
        Rooms.At(village.Simulation, new TileCoord(12, 12, 1)).ShouldBeNull();
    }

    [Fact]
    public void A_gap_in_a_wall_or_a_missing_floor_makes_the_space_outdoors_and_closing_it_brings_the_room_back_with_the_same_level()
    {
        var village = Village();
        House(village);
        Furnish(village, "bed", 3, 3);
        var inside = new TileCoord(4, 4, 1);
        var before = Rooms.At(village.Simulation, inside)!;

        // A wall taken down: it is outdoors; a floor channelled away: the same; the wall put back: the room is back.
        village.Map.SetTile(2, 4, 1, TileShape.Floor, Material(village, "rock"), TileFlags.None);
        Rooms.At(village.Simulation, inside).ShouldBeNull();
        village.Map.SetTile(2, 4, 1, TileShape.Wall, Material(village, "rock"), TileFlags.None);
        Rooms.At(village.Simulation, inside)!.Score.ShouldBe(before.Score);
        village.Map.SetTile(5, 5, 1, TileShape.Empty, GameMap.NoMaterial, TileFlags.None);
        Rooms.At(village.Simulation, inside).ShouldBeNull();
        village.Map.SetTile(5, 5, 1, TileShape.Floor, Material(village, "rock"), TileFlags.None);

        var again = Rooms.At(village.Simulation, inside)!;
        again.Id.ShouldBe(before.Id);
        again.Level.Id.ShouldBe(before.Level.Id);
        again.Score.ShouldBe(before.Score);
    }

    [Theory]
    [InlineData(12, true)]
    [InlineData(13, false)]
    public void A_space_of_more_than_the_largest_room_is_outdoors(int side, bool isRoom)
    {
        var village = new ColonyTestBed(RoomContent.Defs, 1, ColonyTestBed.Level(24, 24, '#'), ColonyTestBed.Level(24, 24, '.'), ColonyTestBed.Level(24, 24, '_'));
        House(village, 1, 1, side + 2);

        var room = Rooms.At(village.Simulation, new TileCoord(2, 2, 1));

        (room is not null).ShouldBe(isRoom);
        if (isRoom)
        {
            room!.Cells.Count.ShouldBe(144);
        }
    }

    [Fact]
    public void A_chamber_dug_in_the_rock_with_a_door_is_a_room_like_a_house()
    {
        var village = new ColonyTestBed(RoomContent.Defs, 1, ColonyTestBed.Level(9, 9, '#'), ColonyTestBed.Level(9, 9, '#'));
        var rock = Material(village, "rock");
        for (var x = 3; x <= 5; x++)
        {
            for (var y = 3; y <= 5; y++)
            {
                village.Map.SetTile(x, y, 1, TileShape.Floor, rock, TileFlags.None);
            }
        }

        // The way out: a door in the rock, and a corridor.
        village.Map.SetTile(4, 6, 1, TileShape.Door, rock, TileFlags.None);
        village.Map.SetTile(4, 7, 1, TileShape.Floor, rock, TileFlags.None);

        var room = Rooms.At(village.Simulation, new TileCoord(4, 4, 1));

        room.ShouldNotBeNull();
        room.Cells.Count.ShouldBe(9);
        Rooms.At(village.Simulation, new TileCoord(4, 7, 1)).ShouldBeNull();
    }

    [Fact]
    public void Ramps_and_stairs_open_a_room_and_a_closed_space_under_the_minimum_is_not_a_room()
    {
        var village = Village();
        House(village, 2, 2, 5);
        village.Map.SetTile(3, 3, 1, TileShape.Ramp, Material(village, "rock"), TileFlags.None);
        Rooms.At(village.Simulation, new TileCoord(4, 4, 1)).ShouldBeNull();

        village.Map.SetTile(3, 3, 1, TileShape.Floor, Material(village, "rock"), TileFlags.None);
        House(village, 10, 2, 4, door: false);

        // 2 × 2 inside, closed: the minimum is 4 cells, so it is a room; 1 × 2 is not.
        Rooms.At(village.Simulation, new TileCoord(11, 3, 1)).ShouldNotBeNull();
        House(village, 2, 10, 4, door: false);
        village.Map.SetTile(3, 12, 1, TileShape.Wall, Material(village, "rock"), TileFlags.None);
        village.Map.SetTile(4, 12, 1, TileShape.Wall, Material(village, "rock"), TileFlags.None);
        Rooms.At(village.Simulation, new TileCoord(3, 11, 1)).ShouldBeNull();
    }

    [Fact]
    public void Content_without_a_room_config_has_no_rooms()
    {
        var bed = ColonyTestBed.Flat(10);

        Rooms.IsEnabled(bed.Simulation).ShouldBeFalse();
        Rooms.At(bed.Simulation, new TileCoord(3, 3, 1)).ShouldBeNull();
        Rooms.All(bed.Simulation).ShouldBeEmpty();
    }

    [Fact]
    public void A_room_is_roofed_when_every_cell_has_something_solid_above()
    {
        var village = Village();
        House(village);
        Rooms.At(village.Simulation, new TileCoord(4, 4, 1))!.Roofed.ShouldBeFalse();

        for (var x = 3; x <= 5; x++)
        {
            for (var y = 3; y <= 5; y++)
            {
                village.Map.SetTile(x, y, 2, TileShape.Wall, Material(village, "rock"), TileFlags.None);
            }
        }

        Rooms.At(village.Simulation, new TileCoord(4, 4, 1))!.Roofed.ShouldBeTrue();
        village.Map.SetTile(4, 4, 2, TileShape.Empty, GameMap.NoMaterial, TileFlags.None);
        Rooms.At(village.Simulation, new TileCoord(4, 4, 1))!.Roofed.ShouldBeFalse();
    }

    // ---- Score and level ----

    [Fact]
    public void The_example_house_scores_20_then_54_then_79_and_is_decent_then_comfortable_then_luxurious()
    {
        var village = Village();
        House(village);
        Furnish(village, "bed", 3, 3);
        Furnish(village, "table", 5, 3);
        Furnish(village, "chair", 5, 4);
        Furnish(village, "chest", 3, 5);

        var plain = Rooms.At(village.Simulation, new TileCoord(4, 4, 1))!;
        (plain.Score, plain.Level.Id, plain.SizePoints, plain.FurniturePoints).ShouldBe((20, "decent", 9, 11));

        Style(village, floor: "planks", walls: "masonry");
        var planked = Rooms.At(village.Simulation, new TileCoord(4, 4, 1))!;
        (planked.Score, planked.Level.Id, planked.FloorPoints, planked.WallPoints).ShouldBe((54, "comfortable", 18, 16));

        Style(village, floor: "ashlar", walls: "ashlar");
        var ashlar = Rooms.At(village.Simulation, new TileCoord(4, 4, 1))!;
        (ashlar.Score, ashlar.Level.Id, ashlar.FloorPoints, ashlar.WallPoints).ShouldBe((79, "luxurious", 27, 32));
    }

    [Theory]
    [InlineData(0, "miserable")]
    [InlineData(7, "miserable")]
    [InlineData(8, "poor")]
    [InlineData(17, "poor")]
    [InlineData(18, "decent")]
    [InlineData(35, "decent")]
    [InlineData(36, "comfortable")]
    [InlineData(60, "comfortable")]
    [InlineData(61, "luxurious")]
    [InlineData(500, "luxurious")]
    [InlineData(-3, "miserable")]
    public void The_level_is_the_highest_whose_points_the_score_reaches(int score, string level)
    {
        RoomSetup.From(RoomContent.Defs)!.LevelFor(score).Id.ShouldBe(level);
    }

    [Fact]
    public void A_workshop_in_the_room_subtracts_beauty_a_stone_bench_one_and_a_forge_four()
    {
        var village = Village();
        House(village);
        Furnish(village, "bed", 3, 3);
        Furnish(village, "table", 5, 3);
        Furnish(village, "chair", 5, 4);
        Furnish(village, "chest", 3, 5);
        Rooms.At(village.Simulation, new TileCoord(4, 4, 1))!.Score.ShouldBe(20);

        Furnish(village, "mason_bench", 4, 5);
        Rooms.At(village.Simulation, new TileCoord(4, 4, 1))!.Score.ShouldBe(19);

        Furnish(village, "forge", 4, 3);
        var room = Rooms.At(village.Simulation, new TileCoord(4, 4, 1))!;
        room.Score.ShouldBe(15);
        room.Has("workshop").ShouldBeTrue();
    }

    [Theory]
    [InlineData("excellent", 15)]
    [InlineData("good", 13)]
    [InlineData(null, 13)]
    [InlineData("poor", 12)]
    public void A_piece_of_furniture_adds_its_beauty_by_the_value_of_its_quality(string? quality, int score)
    {
        // A bed is 4: excellent (150 %) is 6, good (120 %) 4.8 and counts 4, normal 4 and poor (75 %) 3, over the 9 of the size.
        var village = Village();
        House(village);
        Furnish(village, "bed", 3, 3, quality);

        Rooms.At(village.Simulation, new TileCoord(4, 4, 1))!.Score.ShouldBe(score);
    }

    [Fact]
    public void The_size_stops_adding_at_the_cap_and_a_negative_piece_never_makes_the_total_round_up()
    {
        var village = new ColonyTestBed(RoomContent.Defs, 1, ColonyTestBed.Level(24, 24, '#'), ColonyTestBed.Level(24, 24, '.'), ColonyTestBed.Level(24, 24, '_'));
        House(village, 1, 1, 12);

        Rooms.At(village.Simulation, new TileCoord(5, 5, 1))!.SizePoints.ShouldBe(36);
        Furnish(village, "mason_bench", 5, 5, "poor");

        // -1 at 75 % is -0.75: truncated toward zero, not -1 and not a gain.
        Rooms.At(village.Simulation, new TileCoord(6, 6, 1))!.FurniturePoints.ShouldBe(0);
    }

    // ---- Roles ----

    [Fact]
    public void A_room_has_the_functions_of_what_it_holds_by_tag()
    {
        var village = Village();
        House(village);
        Roles(village).ShouldBeEmpty();

        Furnish(village, "bed", 3, 3);
        Roles(village).ShouldBe(["bedroom"]);

        Furnish(village, "table", 5, 3);
        Roles(village).ShouldBe(["bedroom"]);

        Furnish(village, "chair", 5, 4);
        Roles(village).ShouldBe(["bedroom", "dining"]);

        Furnish(village, "mason_bench", 4, 5);
        Roles(village).ShouldBe(["bedroom", "dining", "workshop"]);
    }

    [Fact]
    public void A_room_is_storage_with_two_chests_or_when_stockpile_zones_cover_half_of_its_cells()
    {
        var village = Village();
        House(village);
        Furnish(village, "chest", 3, 3);
        Roles(village).ShouldBeEmpty();
        Furnish(village, "chest", 5, 5);
        Roles(village).ShouldBe(["storage"]);

        var other = Village();
        House(other);
        other.Designate(DesignationKind.Stockpile, new TileCoord(3, 3, 1), new TileCoord(4, 3, 1));
        other.Simulation.ApplyCommandsNow();
        Roles(other).ShouldBeEmpty();
        other.Designate(DesignationKind.Stockpile, new TileCoord(3, 4, 1), new TileCoord(5, 4, 1));
        other.Simulation.ApplyCommandsNow();
        Roles(other).ShouldBe(["storage"]);
    }

    [Fact]
    public void All_gives_the_rooms_with_furniture_by_id_and_At_finds_the_empty_ones()
    {
        var village = Village();
        House(village, 2, 2, 5);
        House(village, 10, 2, 5);
        House(village, 2, 10, 5);
        Furnish(village, "bed", 11, 3);
        Furnish(village, "bed", 3, 11);

        var all = Rooms.All(village.Simulation);

        all.Select(room => room.Id).ShouldBe(all.Select(room => room.Id).Order().ToList());
        all.Count.ShouldBe(2);
        Rooms.At(village.Simulation, new TileCoord(4, 4, 1)).ShouldNotBeNull();
        Rooms.All(village.Simulation).Count.ShouldBe(2);
    }

    // ---- Changes ----

    [Fact]
    public void Taking_down_a_wall_changes_the_rooms_by_the_next_tick_and_says_so_once()
    {
        var village = Village();
        House(village);
        Furnish(village, "bed", 3, 3);
        village.Simulation.Tick();
        village.DrainEvents().OfType<RoomsChangedEvent>().ShouldNotBeEmpty();

        village.Simulation.Tick();
        village.DrainEvents().OfType<RoomsChangedEvent>().ShouldBeEmpty();

        village.Map.SetTile(2, 4, 1, TileShape.Floor, Material(village, "rock"), TileFlags.None);
        village.Simulation.Tick();
        village.DrainEvents().OfType<RoomsChangedEvent>().Count().ShouldBe(1);
        Rooms.All(village.Simulation).ShouldBeEmpty();

        village.Map.SetTile(2, 4, 1, TileShape.Wall, Material(village, "rock"), TileFlags.None);
        village.Simulation.Tick();
        village.DrainEvents().OfType<RoomsChangedEvent>().Count().ShouldBe(1);
        Rooms.All(village.Simulation).Count.ShouldBe(1);

        village.Simulation.Run(50);
        village.DrainEvents().OfType<RoomsChangedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void Digging_far_from_the_rooms_does_not_tell_the_presentation()
    {
        var village = Village();
        House(village);
        Furnish(village, "bed", 3, 3);
        village.Simulation.Run(3);
        village.DrainEvents();

        village.Map.SetTile(15, 15, 1, TileShape.Wall, Material(village, "rock"), TileFlags.None);
        village.Simulation.Tick();

        village.DrainEvents().OfType<RoomsChangedEvent>().ShouldBeEmpty();
    }

    // ---- Dormitories ----

    [Fact]
    public void A_colonist_who_wakes_in_a_bed_of_a_decent_room_has_two_thoughts_and_in_the_open_only_the_first()
    {
        var village = Village();
        House(village);
        Furnish(village, "bed", 3, 3);
        Furnish(village, "table", 5, 3);
        Furnish(village, "chair", 5, 4);
        Furnish(village, "chest", 3, 5);
        Furnish(village, "bed", 12, 12);
        village.Simulation.Run(Night);
        var indoors = village.AddColonist(3, 4, 1, rest: 2_000, name: "Indoors");
        var outdoors = village.AddColonist(13, 12, 1, rest: 2_000, name: "Outdoors");

        village.Simulation.Run(Night);

        // The colonists may have chatted on the way to bed: only what they thought of the night is compared.
        Thoughts(village, indoors).Where(each => each != "chatted").Order().ToList().ShouldBe(["bedroom_decent", "slept_in_bed"]);
        Thoughts(village, outdoors).Where(each => each != "chatted").ShouldBe(["slept_in_bed"]);
    }

    [Theory]
    [InlineData(0, "bedroom_miserable")]
    [InlineData(1, null)]
    [InlineData(2, "bedroom_decent")]
    [InlineData(3, "bedroom_comfortable")]
    [InlineData(4, "bedroom_luxurious")]
    public void Each_level_leaves_its_bedroom_thought_and_the_poor_one_leaves_none(int rank, string? thought)
    {
        var village = Village();
        var sleeper = village.AddColonist(1, 1, 1);
        var level = RoomSetup.From(RoomContent.Defs)!.Levels[rank];
        var room = new Room(
            1, 1, [new TileCoord(3, 3, 1)], false, 0, 0, 0, 0, level, [], [], []);

        RoomRules.FeelWaking(village.Simulation, sleeper, room);

        Thoughts(village, sleeper).Where(each => each.StartsWith("bedroom_", StringComparison.Ordinal)).ShouldBe(thought is null ? [] : [thought]);
    }

    [Fact]
    public void A_better_bedroom_replaces_the_thought_of_a_worse_one_instead_of_adding_to_it()
    {
        var village = Village();
        var sleeper = village.AddColonist(1, 1, 1);
        var levels = RoomSetup.From(RoomContent.Defs)!.Levels;

        RoomRules.FeelWaking(village.Simulation, sleeper, new Room(1, 1, [], false, 0, 0, 0, 0, levels[0], [], [], []));
        RoomRules.FeelWaking(village.Simulation, sleeper, new Room(1, 1, [], false, 0, 0, 0, 0, levels[3], [], [], []));

        Thoughts(village, sleeper).ShouldBe(["bedroom_comfortable"]);
    }

    [Fact]
    public void A_change_found_by_a_query_before_the_tick_is_still_told_once()
    {
        var village = Village();
        House(village);
        Furnish(village, "bed", 3, 3);
        village.Simulation.Tick();
        village.DrainEvents();

        village.Map.SetTile(2, 4, 1, TileShape.Floor, Material(village, "rock"), TileFlags.None);
        Rooms.All(village.Simulation).ShouldBeEmpty();
        village.Simulation.Tick();
        village.DrainEvents().OfType<RoomsChangedEvent>().Count().ShouldBe(1);

        village.Simulation.Run(5);
        village.DrainEvents().OfType<RoomsChangedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void Deleting_a_zone_and_drawing_another_of_the_same_size_finds_the_rooms_again()
    {
        var village = Village();
        House(village, 2, 2, 5);
        House(village, 10, 2, 5);
        village.Designate(DesignationKind.Stockpile, new TileCoord(3, 3, 1), new TileCoord(4, 3, 1));
        village.Simulation.ApplyCommandsNow();
        var first = Rooms.All(village.Simulation).Select(room => room.Id).ToList();
        first.Count.ShouldBe(1);

        var zones = new List<EntityId>();
        village.World.Query<Stockpile>(zones);
        village.World.DestroyEntity(zones.Single());
        village.Designate(DesignationKind.Stockpile, new TileCoord(11, 3, 1), new TileCoord(12, 3, 1));
        village.Simulation.ApplyCommandsNow();

        var second = Rooms.All(village.Simulation).Select(room => room.Id).ToList();
        second.Count.ShouldBe(1);
        second[0].ShouldNotBe(first[0]);
    }

    [Fact]
    public void The_quality_of_a_piece_of_furniture_that_changes_changes_the_score_without_anyone_invalidating()
    {
        var village = Village();
        House(village);
        var bed = Furnish(village, "bed", 3, 3);
        var before = Rooms.At(village.Simulation, new TileCoord(4, 4, 1))!.Score;

        village.World.Get<Furniture>(bed).Quality = "poor";

        Rooms.At(village.Simulation, new TileCoord(4, 4, 1))!.Score.ShouldNotBe(before);
    }

    // ---- Reserved beds ----

    [Fact]
    public void A_reserved_bed_is_used_only_by_its_owner_and_another_colonist_sleeps_on_the_ground()
    {
        var village = Village();
        var bed = Furnish(village, "bed", 3, 3);
        var owner = village.AddColonist(12, 12, 1, name: "Owner");
        var stranger = village.AddColonist(4, 4, 1, name: "Stranger");
        village.Simulation.Commands.Enqueue(new AssignBedCommand(bed, owner));
        village.Simulation.ApplyCommandsNow();
        village.World.Get<Bed>(bed).Owner.ShouldBe(owner);

        village.Simulation.Run(Night);
        village.Need(owner, "rest").Value = 2_000;
        village.Need(stranger, "rest").Value = 2_000;
        village.Simulation.Run(120);

        village.JobOf(owner)!.Target.ShouldBe(bed);
        village.JobOf(stranger)!.Kind.ShouldBe(JobKind.Sleep);
        village.JobOf(stranger)!.Target.IsNone.ShouldBeTrue();
        village.World.Get<Bed>(bed).Owner.ShouldBe(owner);
    }

    [Fact]
    public void When_the_owner_dies_the_bed_is_free_and_another_colonist_takes_the_bed()
    {
        var village = Village();
        var bed = Furnish(village, "bed", 3, 3);
        var owner = village.AddColonist(12, 12, 1, name: "Owner");
        village.Simulation.Commands.Enqueue(new AssignBedCommand(bed, owner));
        village.Simulation.ApplyCommandsNow();

        ColonistDeath.Die(village.Simulation, owner, LocalizedText.Of("event.colonist.died"));

        Beds.IsTaken(village.Simulation, bed).ShouldBeFalse();
        village.Simulation.Run(Night);
        var next = village.AddColonist(6, 6, 1, rest: 2_000, name: "Next");
        village.Simulation.Run(60);
        village.World.Get<Bed>(bed).Owner.ShouldBe(next);
    }

    [Fact]
    public void Reassigning_frees_the_previous_bed_and_taking_a_bed_from_its_owner_frees_the_owners_claim()
    {
        var village = Village();
        var first = Furnish(village, "bed", 3, 3);
        var second = Furnish(village, "bed", 4, 3);
        var a = village.AddColonist(12, 12, 1, name: "A");
        var b = village.AddColonist(13, 12, 1, name: "B");

        Assign(village, first, a);
        Assign(village, second, a);
        village.World.Get<Bed>(first).Owner.IsNone.ShouldBeTrue();
        village.World.Get<Bed>(second).Owner.ShouldBe(a);

        Assign(village, second, b);
        village.World.Get<Bed>(second).Owner.ShouldBe(b);
        Beds.HasBed(village.Simulation, a).ShouldBeFalse();

        Assign(village, second, EntityId.None);
        village.World.Get<Bed>(second).Owner.IsNone.ShouldBeTrue();
    }

    [Fact]
    public void A_cradle_is_not_assigned_to_an_adult_and_a_gone_colonist_or_bed_is_ignored()
    {
        var village = Village();
        var cradle = Furnish(village, "cradle", 3, 3);
        var adult = village.AddColonist(12, 12, 1, name: "Adult");
        var gone = village.AddColonist(13, 12, 1, name: "Gone");
        village.World.DestroyEntity(gone);

        Assign(village, cradle, adult);
        Assign(village, cradle, gone);
        Assign(village, adult, adult);

        village.World.Get<Bed>(cradle).Owner.IsNone.ShouldBeTrue();
    }

    // ---- Families ----

    [Fact]
    public void A_couple_and_their_child_in_three_beds_all_sleep_at_home_with_their_family()
    {
        var village = Village();
        var (mother, father, child) = Family(village);
        var room = RoomWithBeds(village, mother, father, child);

        foreach (var member in new[] { mother, father, child })
        {
            RoomRules.FeelWaking(village.Simulation, member, room);
            Thoughts(village, member).Where(each => each is "family_home" or "slept_crowded").ShouldBe(["family_home"]);
        }
    }

    [Fact]
    public void A_third_bed_of_a_stranger_gives_everyone_in_the_room_the_crowded_thought_instead()
    {
        var village = Village();
        var (mother, father, stranger) = (village.AddColonist(12, 12, 1, name: "Mother"), village.AddColonist(13, 12, 1, name: "Father"),
            village.AddColonist(14, 12, 1, name: "Stranger"));
        village.World.Add(mother, new Family { Partner = Relationships.RelativeOf(village.Simulation, father) });
        village.World.Add(father, new Family { Partner = Relationships.RelativeOf(village.Simulation, mother) });
        var room = RoomWithBeds(village, mother, father, stranger);

        foreach (var member in new[] { mother, father, stranger })
        {
            RoomRules.FeelWaking(village.Simulation, member, room);
            Thoughts(village, member).Where(each => each is "family_home" or "slept_crowded").ShouldBe(["slept_crowded"]);
        }
    }

    [Fact]
    public void Two_strangers_in_a_room_leave_neither_thought_and_a_couple_alone_sleeps_at_home()
    {
        var village = Village();
        var (a, b) = (village.AddColonist(12, 12, 1, name: "A"), village.AddColonist(13, 12, 1, name: "B"));
        var strangers = RoomWithBeds(village, a, b);
        RoomRules.FeelWaking(village.Simulation, a, strangers);
        Thoughts(village, a).Where(each => each is "family_home" or "slept_crowded").ShouldBeEmpty();

        village.World.Add(a, new Family { Partner = Relationships.RelativeOf(village.Simulation, b) });
        village.World.Add(b, new Family { Partner = Relationships.RelativeOf(village.Simulation, a) });
        RoomRules.FeelWaking(village.Simulation, a, strangers);
        Thoughts(village, a).Where(each => each is "family_home" or "slept_crowded").ShouldBe(["family_home"]);
    }

    [Fact]
    public void The_owners_of_a_room_are_the_living_colonists_whose_beds_are_in_it()
    {
        var village = Village();
        var (a, b) = (village.AddColonist(12, 12, 1, name: "A"), village.AddColonist(13, 12, 1, name: "B"));
        var room = RoomWithBeds(village, a, b);
        RoomRules.Owners(village.Simulation, room).ShouldBe([a, b]);

        village.World.DestroyEntity(b);

        RoomRules.Owners(village.Simulation, room).ShouldBe([a]);
    }

    // ---- Suggestions ----

    [Fact]
    public void The_suggestion_gives_each_member_of_the_family_a_bed_of_its_stage_and_never_repeats_a_bed()
    {
        var village = Village();
        House(village, 2, 2, 7);
        var beds = new[] { Furnish(village, "bed", 3, 3), Furnish(village, "bed", 4, 3), Furnish(village, "bed", 5, 3), Furnish(village, "cradle", 6, 3) };
        var mother = village.AddColonist(12, 12, 1, name: "Mother");
        var father = village.AddColonist(13, 12, 1, name: "Father");
        var child = village.AddColonist(14, 12, 1, name: "Child");
        var baby = village.AddColonist(15, 12, 1, name: "Baby");
        var sibling = village.AddColonist(16, 12, 1, name: "Sibling");
        village.World.Add(child, new Life { AgeDays = 6 * 60, Stage = "child" });
        village.World.Add(baby, new Life { AgeDays = 20, Stage = "baby" });
        village.World.Add(mother, new Family
        {
            Partner = Relationships.RelativeOf(village.Simulation, father),
            Children = [Relationships.RelativeOf(village.Simulation, child), Relationships.RelativeOf(village.Simulation, baby)],
        });
        village.World.Add(father, new Family { Partner = Relationships.RelativeOf(village.Simulation, mother) });
        village.World.Add(child, new Family { Mother = Relationships.RelativeOf(village.Simulation, mother) });
        village.World.Add(baby, new Family { Mother = Relationships.RelativeOf(village.Simulation, mother) });
        var room = Rooms.At(village.Simulation, new TileCoord(4, 5, 1))!;

        var suggestion = RoomRules.SuggestAssignments(village.Simulation, room, mother);

        suggestion.Select(each => each.Colonist).ShouldBe([mother, father, child, baby]);
        suggestion.Select(each => each.Bed).Distinct().Count().ShouldBe(4);
        suggestion.Single(each => each.Colonist == baby).Bed.ShouldBe(beds[3]);
        suggestion.Where(each => each.Colonist != baby).ShouldAllBe(each => beds.Take(3).Contains(each.Bed));
        suggestion.Select(each => each.Colonist).ShouldNotContain(sibling);

        // Asking changed nothing: the beds are still free.
        village.World.Get<Bed>(beds[0]).Owner.IsNone.ShouldBeTrue();
    }

    [Fact]
    public void The_suggestion_leaves_out_who_has_no_bed_that_fits_and_prefers_the_beds_they_already_own()
    {
        var village = Village();
        House(village);
        var first = Furnish(village, "bed", 3, 3);
        var second = Furnish(village, "bed", 4, 3);
        var mother = village.AddColonist(12, 12, 1, name: "Mother");
        var baby = village.AddColonist(15, 12, 1, name: "Baby");
        village.World.Add(baby, new Life { AgeDays = 20, Stage = "baby" });
        village.World.Add(mother, new Family { Children = [Relationships.RelativeOf(village.Simulation, baby)] });
        village.World.Add(baby, new Family { Mother = Relationships.RelativeOf(village.Simulation, mother) });
        Assign(village, second, mother);
        var room = Rooms.At(village.Simulation, new TileCoord(4, 4, 1))!;

        var suggestion = RoomRules.SuggestAssignments(village.Simulation, room, mother);

        suggestion.ShouldBe([new BedAssignment(second, mother)]);
        first.IsNone.ShouldBeFalse();
    }

    // ---- Dining ----

    [Fact]
    public void A_hungry_colonist_with_food_at_hand_sits_at_the_table_eats_and_thinks_of_the_meal_and_of_the_room()
    {
        var village = Dining();
        var food = new TileCoord(12, 12, 1);
        village.AddItems("provisions", 10, food.X, food.Y, 1);
        var diner = village.AddColonist(10, 10, 1, hunger: 3_000, name: "Diner");

        var (sat, chair) = (false, new TileCoord(3, 4, 1));
        var seen = new List<JobKind>();
        for (var tick = 0; tick < 600 && village.Need(diner, "hunger").Value < 9_000; tick++)
        {
            village.Simulation.Tick();
            if (village.JobOf(diner) is { } job)
            {
                seen.Add(job.Kind);
                sat |= job.Kind == JobKind.EatAtTable && job.Stage == JobStage.Working && village.World.Get<Position>(diner).Cell == chair;
            }
        }

        sat.ShouldBeTrue();
        seen.ShouldNotContain(JobKind.Eat);
        village.Need(diner, "hunger").Value.ShouldBeGreaterThan(8_000);
        Thoughts(village, diner).Order().ToList().ShouldBe(["ate_at_table", "dining_decent"]);
        village.World.Has<Carried>(diner).ShouldBeFalse();
        village.CountItems("provisions").ShouldBeGreaterThan(5);
    }

    [Fact]
    public void With_critical_hunger_the_colonist_eats_where_the_food_is_and_gets_no_thought_of_a_table()
    {
        var village = Dining();
        village.AddItems("provisions", 10, 12, 12, 1);
        var starving = village.AddColonist(10, 10, 1, hunger: 1_000, name: "Starving");

        var kinds = new HashSet<JobKind>();
        for (var tick = 0; tick < 400; tick++)
        {
            village.Simulation.Tick();
            if (village.JobOf(starving) is { } job)
            {
                kinds.Add(job.Kind);
            }
        }

        kinds.ShouldContain(JobKind.Eat);
        kinds.ShouldNotContain(JobKind.EatAtTable);
        Thoughts(village, starving).ShouldNotContain("ate_at_table");
    }

    [Fact]
    public void A_meal_at_a_table_is_not_dropped_half_way_by_another_critical_need()
    {
        var village = WideDining();
        village.AddItems("provisions", 10, 24, 12, 1);
        var diner = village.AddColonist(24, 13, 1, hunger: 3_000, name: "Diner");
        for (var tick = 0; tick < 400 && village.JobOf(diner) is not { Kind: JobKind.EatAtTable, Stage: JobStage.Carrying }; tick++)
        {
            village.Simulation.Tick();
        }

        village.JobOf(diner)!.Kind.ShouldBe(JobKind.EatAtTable);
        village.Need(diner, "rest").Value = 0;
        for (var tick = 0; tick < 300 && village.JobOf(diner) is { Kind: JobKind.EatAtTable }; tick++)
        {
            village.Simulation.Tick();
        }

        // The meal went on to its end (the colonist is fed) before anything else was taken up.
        village.Need(diner, "hunger").Value.ShouldBeGreaterThan(8_000);
        village.CountItems("provisions").ShouldBeGreaterThan(5);
    }

    private static ColonyTestBed WideDining()
    {
        var village = new ColonyTestBed(RoomContent.Defs, 1, ColonyTestBed.Level(80, 20, '#'), ColonyTestBed.Level(80, 20, '.'), ColonyTestBed.Level(80, 20, '_'));
        House(village);
        Furnish(village, "table", 4, 4);
        Furnish(village, "chair", 3, 4);
        Style(village, floor: "planks", walls: "rock");
        village.Simulation.Tick();
        return village;
    }

    [Fact]
    public void The_dining_radius_is_measured_from_the_pile_the_food_comes_from_not_from_the_colonist()
    {
        // The pile is next to the chair and the colonist is far from it, but near the pile: it sits.
        var near = WideDining();
        near.AddItems("provisions", 10, 20, 12, 1);
        var diner = near.AddColonist(40, 12, 1, hunger: 3_000, name: "Diner");
        var kinds = new HashSet<JobKind>();
        for (var tick = 0; tick < 300; tick++)
        {
            near.Simulation.Tick();
            if (near.JobOf(diner) is { } job)
            {
                kinds.Add(job.Kind);
            }
        }

        kinds.ShouldContain(JobKind.EatAtTable);

        // The colonist is next to the chair but the pile is out of the radius of it: the food would be carried too far, so it eats standing.
        var far = WideDining();
        far.AddItems("provisions", 10, 45, 12, 1);
        var other = far.AddColonist(24, 8, 1, hunger: 3_000, name: "Other");
        kinds.Clear();
        for (var tick = 0; tick < 300; tick++)
        {
            far.Simulation.Tick();
            if (far.JobOf(other) is { } job)
            {
                kinds.Add(job.Kind);
            }
        }

        kinds.ShouldContain(JobKind.Eat);
        kinds.ShouldNotContain(JobKind.EatAtTable);
    }

    [Fact]
    public void Without_a_dining_room_nothing_changes_and_eating_standing_costs_no_thought()
    {
        var village = Village();
        village.AddItems("provisions", 10, 12, 12, 1);
        var diner = village.AddColonist(10, 10, 1, hunger: 3_000, name: "Diner");
        var kinds = new HashSet<JobKind>();

        for (var tick = 0; tick < 400 && village.Need(diner, "hunger").Value < 9_000; tick++)
        {
            village.Simulation.Tick();
            if (village.JobOf(diner) is { } job)
            {
                kinds.Add(job.Kind);
            }
        }

        kinds.ShouldContain(JobKind.Eat);
        kinds.ShouldNotContain(JobKind.EatAtTable);
        Thoughts(village, diner).ShouldBeEmpty();
        MoodRules.Value(village.Simulation, diner).ShouldBeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public void A_room_with_a_table_and_a_chair_not_beside_it_is_a_dining_room_without_seats_and_the_diner_stands()
    {
        var village = Village();
        House(village);
        Furnish(village, "table", 3, 3);
        Furnish(village, "chair", 5, 5);
        village.AddItems("provisions", 10, 12, 12, 1);
        var diner = village.AddColonist(10, 10, 1, hunger: 3_000, name: "Diner");

        Roles(village).ShouldBe(["dining"]);
        village.Simulation.Run(200);

        Thoughts(village, diner).ShouldNotContain("ate_at_table");
        village.Need(diner, "hunger").Value.ShouldBeGreaterThan(3_000);
    }

    [Fact]
    public void Two_diners_take_different_chairs_and_a_chair_taken_is_not_offered_to_a_third()
    {
        var village = Dining();
        Furnish(village, "chair", 4, 5);
        village.AddItems("provisions", 30, 12, 12, 1);
        var diners = new[]
        {
            village.AddColonist(10, 10, 1, hunger: 3_000, name: "A"), village.AddColonist(10, 11, 1, hunger: 3_000, name: "B"),
            village.AddColonist(10, 12, 1, hunger: 3_000, name: "C"),
        };

        var chairs = new HashSet<EntityId>();
        for (var tick = 0; tick < 300; tick++)
        {
            village.Simulation.Tick();
            var atTable = diners.Select(village.JobOf).Where(job => job is { Kind: JobKind.EatAtTable }).Select(job => job!.Recipient).ToList();
            atTable.Distinct().Count().ShouldBe(atTable.Count);
            chairs.UnionWith(atTable);
        }

        chairs.Count.ShouldBe(2);
    }

    // ---- Gathering ----

    [Fact]
    public void In_the_evening_with_free_will_colonists_sit_in_the_dining_room_and_chat_and_with_it_off_nobody_goes()
    {
        var withWill = GatheringVillage(freeWill: true);
        var without = GatheringVillage(freeWill: false);

        var sat = 0;
        var sittingWithout = 0;
        for (var tick = 0; tick < 960; tick++)
        {
            withWill.Simulation.Tick();
            without.Simulation.Tick();
            if (tick >= 750)
            {
                sat = Math.Max(sat, withWill.Colonists().Count(colonist => withWill.JobOf(colonist) is { Kind: JobKind.Gather, Stage: JobStage.Working }));
                sittingWithout += without.Colonists().Count(colonist => without.JobOf(colonist) is { Kind: JobKind.Gather });
            }
        }

        sat.ShouldBeGreaterThanOrEqualTo(2);
        sittingWithout.ShouldBe(0);
        var chatted = withWill.Colonists().Count(colonist => withWill.World.TryGet<Mood>(colonist, out var mood) && mood.Thoughts.Count > 0);
        chatted.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Before_the_gathering_hours_nobody_goes_to_the_dining_room()
    {
        var village = GatheringVillage(freeWill: true);

        for (var tick = 0; tick < 700; tick++)
        {
            village.Simulation.Tick();
            village.Colonists().Count(colonist => village.JobOf(colonist) is { Kind: JobKind.Gather }).ShouldBe(0);
        }
    }

    [Fact]
    public void A_seated_guest_is_called_away_by_a_need_and_the_chair_is_free_again()
    {
        var village = GatheringVillage(freeWill: true);
        EntityId guest = EntityId.None;
        for (var tick = 0; tick < 960 && guest.IsNone; tick++)
        {
            village.Simulation.Tick();
            guest = village.Colonists().FirstOrDefault(colonist => village.JobOf(colonist) is { Kind: JobKind.Gather, Stage: JobStage.Working });
        }

        guest.IsNone.ShouldBeFalse();
        village.Need(guest, "hunger").Value = 100;
        village.AddItems("provisions", 5, 18, 18, 1);
        village.Simulation.Run(35);

        village.JobOf(guest)?.Kind.ShouldNotBe(JobKind.Gather);
    }

    // ---- Save and determinism ----

    [Fact]
    public void A_reserved_bed_and_a_meal_at_the_table_in_the_middle_are_saved_and_the_game_goes_on_the_same()
    {
        var village = Dining();
        var bed = Furnish(village, "bed", 5, 5);
        var owner = village.AddColonist(12, 10, 1, name: "Owner");
        Assign(village, bed, owner);
        village.AddItems("provisions", 10, 12, 12, 1);
        var diner = village.AddColonist(10, 10, 1, hunger: 3_000, name: "Diner");
        for (var tick = 0; tick < 400 && village.JobOf(diner) is not { Kind: JobKind.EatAtTable, Stage: JobStage.Carrying or JobStage.Working }; tick++)
        {
            village.Simulation.Tick();
        }

        village.JobOf(diner)!.Kind.ShouldBe(JobKind.EatAtTable);
        var output = new MemoryStream();
        SaveGame.Write(output, village.Simulation, new SaveInfo("rooms", IsAutosave: false, "test", DateTimeOffset.UnixEpoch));
        var loaded = SaveGame.Load(new MemoryStream(output.ToArray()), RoomContent.Defs);
        ColonySystems.AddTo(loaded);

        loaded.ComputeStateHash().ShouldBe(village.Simulation.ComputeStateHash());
        loaded.World.Get<Bed>(bed).Owner.ShouldBe(owner);
        Rooms.At(loaded, new TileCoord(4, 4, 1))!.Score.ShouldBe(Rooms.At(village.Simulation, new TileCoord(4, 4, 1))!.Score);
        village.Simulation.Run(600);
        loaded.Run(600);
        loaded.ComputeStateHash().ShouldBe(village.Simulation.ComputeStateHash());
    }

    [Fact]
    public void The_same_seed_gives_the_same_game_with_rooms_meals_and_gatherings()
    {
        string Play()
        {
            var village = GatheringVillage(freeWill: true);
            village.AddItems("provisions", 20, 12, 12, 1);
            village.Simulation.Run(1_500);
            return village.Simulation.ComputeStateHash().ToString();
        }

        Play().ShouldBe(Play());
    }

    [Fact]
    public void The_rooms_are_not_part_of_the_state()
    {
        var village = Dining();
        var hash = village.Simulation.ComputeStateHash();

        Rooms.All(village.Simulation).ShouldNotBeEmpty();
        Rooms.Invalidate(village.Simulation);
        Rooms.At(village.Simulation, new TileCoord(4, 4, 1));

        village.Simulation.ComputeStateHash().ShouldBe(hash);
    }

    // ---- Setup validation ----

    private static string ConfigJson(string id, int maxCells = 150) =>
        $"{{ \"type\": \"RoomConfig\", \"id\": \"{id}\", \"minCells\": 4, \"maxCells\": {maxCells}, \"sizePointsPerCell\": 1, \"sizeCap\": 36, "
        + "\"diningRadius\": 25, \"gatherRadius\": 30, \"gatherChancePercent\": 50, \"gatherFromHour\": 18, \"gatherToHour\": 22, \"gatherTicks\": 180, \"crowdedBeds\": 3 }";

    private static string LevelJson(string id, int order, int from) => $"{{ \"type\": \"RoomLevel\", \"id\": \"{id}\", \"order\": {order}, \"from\": {from} }}";

    [Fact]
    public void A_room_setup_with_levels_that_do_not_go_up_or_do_not_start_at_zero_is_rejected()
    {
        var config = ConfigJson("default");

        Should.Throw<InvalidOperationException>(() => RoomSetup.From(TestContent.WithDefs($"[{config}]"))).Message.ShouldContain("at least one RoomLevel");
        Should.Throw<InvalidOperationException>(() => RoomSetup.From(TestContent.WithDefs($"[{config}, {LevelJson("a", 0, 3)}]"))).Message.ShouldContain("start at 0");
        Should.Throw<InvalidOperationException>(() => RoomSetup.From(TestContent.WithDefs($"[{config}, {LevelJson("a", 0, 0)}, {LevelJson("b", 1, 0)}]")))
            .Message.ShouldContain("higher");
        Should.Throw<InvalidOperationException>(() => RoomSetup.From(TestContent.WithDefs($"[{config}, {ConfigJson("other")}, {LevelJson("a", 0, 0)}]")))
            .Message.ShouldContain("at most one");
        RoomSetup.From(TestContent.Minimal).ShouldBeNull();
    }

    [Fact]
    public void The_room_defs_reject_numbers_out_of_range()
    {
        Should.Throw<DefValidationException>(() => TestContent.WithDefs($"[{ConfigJson("default", maxCells: 2)}]"));
        Should.Throw<DefValidationException>(() => TestContent.WithDefs("[{ \"type\": \"RoomRole\", \"id\": \"empty\", \"order\": 0 }]"));
        Should.Throw<DefValidationException>(() => TestContent.WithDefs(
            "[{ \"type\": \"Material\", \"id\": \"m\", \"kind\": \"stone\", \"color\": \"#000000\", \"mineWorkTicks\": 5, \"beauty\": { \"wall\": 1, \"floor\": 1, \"roof\": 2 } }]"));
    }

    // ---- Helpers ----

    private static ColonyTestBed Village(ulong seed = 1) =>
        new(RoomContent.Defs, seed, ColonyTestBed.Level(20, 20, '#'), ColonyTestBed.Level(20, 20, '.'), ColonyTestBed.Level(20, 20, '_'));

    private static ushort Material(ColonyTestBed village, string id) => (ushort)village.Simulation.Content.IndexOf<MaterialDef>(id);

    /// <summary>A house with walls of rock around a <c>size − 2</c> square, and a door in the middle of its south wall.</summary>
    private static void House(ColonyTestBed village, int x0 = 2, int y0 = 2, int size = 5, bool door = true)
    {
        var rock = Material(village, "rock");
        for (var x = x0; x < x0 + size; x++)
        {
            for (var y = y0; y < y0 + size; y++)
            {
                if (x == x0 || y == y0 || x == x0 + size - 1 || y == y0 + size - 1)
                {
                    village.Map.SetTile(x, y, 1, TileShape.Wall, rock, TileFlags.None);
                }
            }
        }

        if (door)
        {
            village.Map.SetTile(x0 + (size / 2), y0 + size - 1, 1, TileShape.Door, rock, TileFlags.None);
        }
    }

    /// <summary>Retiles the floor of the 3 × 3 room of the house at 2, 2 and its walls and door, keeping what stands on them.</summary>
    private static void Style(ColonyTestBed village, string floor, string walls)
    {
        for (var x = 2; x <= 6; x++)
        {
            for (var y = 2; y <= 6; y++)
            {
                var inside = x is >= 3 and <= 5 && y is >= 3 and <= 5;
                var shape = inside ? TileShape.Floor : (x, y) == (4, 6) ? TileShape.Door : TileShape.Wall;
                village.Map.SetTile(x, y, 1, shape, Material(village, inside ? floor : walls), village.Map.FlagsAt(x, y, 1));
            }
        }
    }

    private static EntityId Furnish(ColonyTestBed village, string buildable, int x, int y, string? quality = null)
    {
        var cell = new TileCoord(x, y, 1);
        village.Simulation.Commands.Enqueue(new PlaceBlueprintCommand(buildable, "timber", cell, cell));
        village.Simulation.ApplyCommandsNow();
        var blueprints = new List<EntityId>();
        village.World.Query<Blueprint, Position>(blueprints);
        Construction.Finish(village.Simulation, blueprints.Single(each => village.World.Get<Position>(each).Cell == cell), cell);
        var piece = Construction.FurnitureAt(village.Simulation, cell);
        if (quality is not null)
        {
            village.World.Get<Furniture>(piece).Quality = quality;
        }

        return piece;
    }

    private static void Assign(ColonyTestBed village, EntityId bed, EntityId colonist)
    {
        village.Simulation.Commands.Enqueue(new AssignBedCommand(bed, colonist));
        village.Simulation.ApplyCommandsNow();
    }

    private static List<string> Roles(ColonyTestBed village) =>
        [.. Rooms.At(village.Simulation, new TileCoord(4, 4, 1))?.Roles.Select(role => role.Id) ?? []];

    private static List<string> Thoughts(ColonyTestBed village, EntityId colonist) =>
        village.World.TryGet<Mood>(colonist, out var mood) ? [.. mood.Thoughts.Select(each => each.Thought)] : [];

    /// <summary>A house with a table and a chair beside it, a floor of planks and so a decent dining room.</summary>
    private static ColonyTestBed Dining()
    {
        var village = Village();
        House(village);
        Furnish(village, "table", 4, 4);
        Furnish(village, "chair", 3, 4);
        Style(village, floor: "planks", walls: "rock");
        village.Simulation.Tick();
        return village;
    }

    /// <summary>The dining room with three chairs around the table, free will on or off, and three colonists with full needs nearby.</summary>
    private static ColonyTestBed GatheringVillage(bool freeWill)
    {
        var village = Village(seed: 5);
        House(village);
        Furnish(village, "table", 4, 4);
        Furnish(village, "chair", 3, 4);
        Furnish(village, "chair", 5, 4);
        Furnish(village, "chair", 4, 5);
        village.Simulation.Commands.Enqueue(new SetFreeWillCommand(freeWill));
        foreach (var (name, x) in new[] { ("A", 9), ("B", 10), ("C", 11) })
        {
            var colonist = village.AddColonist(x, 10, 1, name: name);
            village.World.Add(colonist, new Personality { Traits = [new TraitValue { Trait = "sociability", Value = 100 }] });
        }

        return village;
    }

    private static Room RoomWithBeds(ColonyTestBed village, params EntityId[] owners)
    {
        House(village, 2, 2, 7);
        var beds = new List<EntityId>();
        for (var i = 0; i < owners.Length; i++)
        {
            var bed = Furnish(village, "bed", 3 + i, 3);
            village.World.Get<Bed>(bed).Owner = owners[i];
            beds.Add(bed);
        }

        var room = Rooms.At(village.Simulation, new TileCoord(4, 5, 1))!;
        room.Beds.ShouldBe(beds);
        return room;
    }

    private static (EntityId Mother, EntityId Father, EntityId Child) Family(ColonyTestBed village)
    {
        var (mother, father, child) = (village.AddColonist(12, 12, 1, name: "Mother"), village.AddColonist(13, 12, 1, name: "Father"),
            village.AddColonist(14, 12, 1, name: "Child"));
        village.World.Add(child, new Life { AgeDays = 6 * 60, Stage = "child" });
        village.World.Add(mother, new Family
        {
            Partner = Relationships.RelativeOf(village.Simulation, father), Children = [Relationships.RelativeOf(village.Simulation, child)],
        });
        village.World.Add(father, new Family
        {
            Partner = Relationships.RelativeOf(village.Simulation, mother), Children = [Relationships.RelativeOf(village.Simulation, child)],
        });
        village.World.Add(child, new Family
        {
            Mother = Relationships.RelativeOf(village.Simulation, mother), Father = Relationships.RelativeOf(village.Simulation, father),
        });
        return (mother, father, child);
    }
}
