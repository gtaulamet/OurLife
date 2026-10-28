using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Core.Text;

namespace OurLife.Core.Tests.Colony;

/// <summary>
/// Farm zones, crops, sowing and reaping, the choice of what to eat, spoilage and the food of the
/// colony (<c>docs/gdd/agricultura.md</c>, criteria 1 to 11). The test calendar has 15 days per
/// season: spring is days 0 to 14 of the year, summer 15 to 29, autumn 30 to 44 and winter 45 to 59.
/// Tick 0 is 06:00 of day 0; the first midnight is at tick 1080 and then every 1440 ticks.
/// </summary>
public class FarmTests
{
    private const int TicksPerDay = 1440;

    private const string Extras = """
        { "type": "Material", "id": "soil", "kind": "soil", "color": "#7A5A3A", "mineWorkTicks": 30 },
        { "type": "WorkType", "id": "farming", "order": 5, "jobs": ["sow", "reap"], "light": true, "skill": true },
        { "type": "Knowledge", "id": "agriculture", "cost": 120 },
        { "type": "Item", "id": "wheat", "category": "misc", "kind": "raw", "visual": "item.wheat", "stackLimit": 100, "spoilDays": 200, "reserveValue": 800 },
        { "type": "Item", "id": "flour", "category": "misc", "kind": "raw", "visual": "item.flour", "stackLimit": 100, "spoilDays": 90, "reserveValue": 800 },
        { "type": "Item", "id": "cabbage", "category": "food", "kind": "food", "visual": "item.cabbage", "stackLimit": 50, "nutrition": 300, "spoilDays": 30 },
        { "type": "Item", "id": "bread", "category": "food", "kind": "food", "visual": "item.bread", "stackLimit": 100, "nutrition": 800, "spoilDays": 20, "meal": true },
        { "type": "Crop", "id": "wheat", "item": "wheat", "count": 8, "growDays": 10, "growSeasons": ["spring", "summer", "autumn"],
          "sowTicks": 40, "reapTicks": 60, "yieldBasePercent": 70, "yieldPercentPerLevel": 3, "visual": "crop.wheat" },
        { "type": "Crop", "id": "cabbage", "item": "cabbage", "count": 5, "growDays": 8, "growSeasons": ["spring", "summer", "autumn"],
          "sowTicks": 40, "reapTicks": 50, "yieldBasePercent": 70, "yieldPercentPerLevel": 3, "visual": "crop.cabbage" },
        { "type": "Crop", "id": "peas", "requires": ["agriculture"], "item": "cabbage", "count": 6, "growDays": 8, "growSeasons": ["spring", "summer", "autumn"],
          "sowTicks": 40, "reapTicks": 50, "visual": "crop.peas" },
        { "type": "Crop", "id": "grove", "tree": "tree", "sowTicks": 60, "spacing": 2 },
        { "type": "SpoilageConfig", "id": "default", "coveredPercent": 50, "rotNoticeMinUnits": 5 },
        { "type": "WorldGen", "id": "test_valley", "width": 32, "height": 32, "depth": 16,
          "valleyFloor": 5, "hillHeight": 4, "valleyHalfWidth": 4, "valleyMeander": 2,
          "terrainNoiseScale": 12, "terrainAmplitude": 2, "detailNoiseScale": 6, "forestNoiseScale": 8,
          "soilMaterial": "soil", "soilDepthMin": 1, "soilDepthMax": 2, "rockExposureElevation": 4, "rockExposureWobble": 1,
          "strata": [{ "material": "rock" }], "strataWobble": 1, "grassCoverage": 5000, "groveFactorMax": 2,
          "vegetation": [{ "plant": "tree", "chance": 100, "minElevation": -10, "maxElevation": 10 }] }
        """;

    /// <summary>The test content with the farm: soil, the farming work type, crops, spoilage, a tree that spreads, and a thought for meals.</summary>
    internal static string Json(int seedChance = 0, string? birth = null)
    {
        var json = ColonyTestBed.ContentJson
            .Replace(
                """{ "type": "Plant", "id": "tree", "visual": "plant.tree", "blocksMovement": true,""",
                $$"""{ "type": "Plant", "id": "tree", "visual": "plant.tree", "blocksMovement": true, "regrowth": { "seedChanceBasisPoints": {{seedChance}}, "seedRadius": 3, "growDays": 10, "maxCoverPerMille": 500 },""",
                StringComparison.Ordinal)
            .Replace(
                """{"type": "Thought", "id": "child_born", "mood": 12, "durationHours": 72, "stackLimit": 2},""",
                """{"type": "Thought", "id": "child_born", "mood": 12, "durationHours": 72, "stackLimit": 2}, {"type": "Thought", "id": "ate_meal", "mood": 3, "durationHours": 8},""",
                StringComparison.Ordinal)
            .Replace("\"childBorn\": \"child_born\"}", "\"childBorn\": \"child_born\", \"mealThought\": \"ate_meal\"}", StringComparison.Ordinal);
        if (birth is not null)
        {
            json = json.Replace("\"siblingOpinion\": 30", $"\"siblingOpinion\": 30, {birth}", StringComparison.Ordinal)
                .Replace("\"conceptionChancePerYear\": 40", "\"conceptionChancePerYear\": 100", StringComparison.Ordinal)
                .Replace(
                    "{ \"type\": \"LifePace\", \"id\": \"normal\", \"lifeDaysPerDay\": 1 },",
                    "{ \"type\": \"LifePace\", \"id\": \"normal\", \"lifeDaysPerDay\": 1 }, { \"type\": \"LifePace\", \"id\": \"yearly\", \"lifeDaysPerDay\": 60 },",
                    StringComparison.Ordinal);
        }

        return json.TrimEnd().TrimEnd(']') + ", " + Extras + "]";
    }

    private static readonly Lazy<DefDatabase> ContentLazy = new(() => TestContent.WithDefs(Json()));

    internal static DefDatabase Content => ContentLazy.Value;

    /// <summary>A flat field of soil on level 1, under open air, over rock.</summary>
    internal static ColonyTestBed Field(int size = 16, ulong seed = 1, DefDatabase? content = null)
    {
        var bed = ColonyTestBed.Flat(size, seed, content ?? Content);
        var soil = (ushort)bed.Simulation.Content.IndexOf<MaterialDef>("soil");
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                bed.Map.SetTileQuietly(x, y, 1, TileShape.Floor, soil, TileFlags.None);
            }
        }

        return bed;
    }

    /// <summary>Jumps the clock to an hour of a day of the year, counting from day 0 of the first year.</summary>
    internal static void GoTo(ColonyTestBed bed, int day, int hour = 12) => bed.Simulation.CurrentTick = ((long)day * TicksPerDay) + (hour * 60) - 360;

    internal static EntityId Zone(ColonyTestBed bed, string crop, TileCoord from, TileCoord to)
    {
        bed.Simulation.Commands.Enqueue(new PlaceFarmZoneCommand(from, to, crop));
        bed.Simulation.ApplyCommandsNow();
        var zones = new List<EntityId>();
        bed.World.Query<FarmZone>(zones);
        return zones.Last();
    }

    private static List<EntityId> Crops(ColonyTestBed bed)
    {
        var crops = new List<EntityId>();
        bed.World.Query<Crop>(crops);
        return crops;
    }

    private static EntityId Farmer(ColonyTestBed bed, int x, int y, int level = 10, string name = "Elvira")
    {
        var colonist = bed.AddColonist(x, y, 1, name: name);
        bed.World.Add(colonist, new Skills { Values = [new SkillValue { WorkType = "farming", Level = level }] });
        return colonist;
    }

    // Criterion 1.
    [Fact]
    public void A_zone_takes_the_free_cells_of_the_box_and_skips_plants_and_stockpiles()
    {
        var bed = Field();
        bed.AddPlant("bush", 4, 4, 1);
        bed.AddPlant("tree", 6, 6, 1);

        var zone = Zone(bed, "wheat", new TileCoord(7, 7, 1), new TileCoord(3, 3, 1));

        var cells = bed.World.Get<FarmZone>(zone).Cells;
        cells.Count.ShouldBe(23);
        cells.ShouldNotContain(new TileCoord(4, 4, 1));
        cells.ShouldNotContain(new TileCoord(6, 6, 1));
        cells.ShouldBe(cells.OrderBy(cell => (cell.Y * 16) + cell.X).ToList());
        bed.World.Get<FarmZone>(zone).Crop.ShouldBe("wheat");

        // Another box over the same cells gets nothing; a stockpile takes one cell of the first.
        var again = bed.World.Store<FarmZone>().Count;
        bed.Simulation.Commands.Enqueue(new PlaceFarmZoneCommand(new TileCoord(3, 3, 1), new TileCoord(7, 7, 1), "cabbage"));
        bed.Simulation.ApplyCommandsNow();
        bed.World.Store<FarmZone>().Count.ShouldBe(again);
    }

    [Fact]
    public void A_stockpile_in_the_box_leaves_one_cell_less()
    {
        var bed = Field();
        bed.AddPlant("bush", 4, 4, 1);
        bed.AddPlant("tree", 6, 6, 1);
        bed.Designate(DesignationKind.Stockpile, new TileCoord(5, 5, 1), new TileCoord(5, 5, 1));
        bed.Simulation.ApplyCommandsNow();

        var zone = Zone(bed, "wheat", new TileCoord(3, 3, 1), new TileCoord(7, 7, 1));

        bed.World.Get<FarmZone>(zone).Cells.Count.ShouldBe(22);
        bed.World.Get<FarmZone>(zone).Cells.ShouldNotContain(new TileCoord(5, 5, 1));
    }

    [Fact]
    public void Only_flat_natural_soil_under_open_air_with_nothing_on_it_is_a_valid_cell()
    {
        var bed = Field();
        var map = bed.Map;
        var rock = (ushort)bed.Simulation.Content.IndexOf<MaterialDef>("rock");
        var timber = (ushort)bed.Simulation.Content.IndexOf<MaterialDef>("timber");
        map.SetTileQuietly(2, 2, 1, TileShape.Floor, rock, TileFlags.None);
        map.SetTileQuietly(3, 2, 1, TileShape.Floor, timber, TileFlags.None);
        map.SetTileQuietly(4, 2, 1, TileShape.Ramp, rock, TileFlags.None);
        map.SetFlags(5, 2, 1, TileFlags.MineDesignation);
        map.SetFlags(6, 2, 1, TileFlags.Furniture);
        bed.AddItems("log", 3, 7, 2, 1);

        var cells = FarmRules.CellsToZone(bed.Simulation, new TileCoord(1, 2, 1), new TileCoord(8, 2, 1));

        // Rock, a built floor, a ramp, an order and furniture are no soil to plough; a pile on soil does not matter.
        cells.Select(cell => cell.X).ShouldBe([1, 7, 8]);
    }

    [Fact]
    public void A_roof_over_the_soil_is_not_open_air()
    {
        var bed = new ColonyTestBed(
            FarmTests.Content,
            1,
            ColonyTestBed.Level(8, 8, '#'),
            ColonyTestBed.Level(8, 8, '.'),
            string.Join("\n", Enumerable.Range(0, 8).Select(row => row < 4 ? "........" : "        ")));
        var soil = (ushort)bed.Simulation.Content.IndexOf<MaterialDef>("soil");
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                bed.Map.SetTileQuietly(x, y, 1, TileShape.Floor, soil, TileFlags.None);
            }
        }

        var cells = FarmRules.CellsToZone(bed.Simulation, new TileCoord(0, 0, 1), new TileCoord(7, 7, 1));

        cells.Count.ShouldBe(32);
        cells.ShouldAllBe(cell => cell.Y >= 4);
    }

    [Fact]
    public void A_crop_the_colony_does_not_know_is_not_planted_and_a_zone_changes_its_crop_for_what_is_sown_next()
    {
        var bed = Field();
        bed.Simulation.Commands.Enqueue(new PlaceFarmZoneCommand(new TileCoord(3, 3, 1), new TileCoord(4, 4, 1), "peas"));
        bed.Simulation.Commands.Enqueue(new PlaceFarmZoneCommand(new TileCoord(3, 3, 1), new TileCoord(4, 4, 1), "nothing"));
        bed.Simulation.ApplyCommandsNow();
        bed.World.Store<FarmZone>().Count.ShouldBe(0);

        var zone = Zone(bed, "wheat", new TileCoord(3, 3, 1), new TileCoord(4, 4, 1));
        bed.Simulation.Commands.Enqueue(new SetFarmZoneCropCommand(zone, "peas"));
        bed.Simulation.ApplyCommandsNow();
        bed.World.Get<FarmZone>(zone).Crop.ShouldBe("wheat");

        bed.Simulation.Progress.Learn("agriculture");
        bed.Simulation.Commands.Enqueue(new SetFarmZoneCropCommand(zone, "peas"));
        bed.Simulation.ApplyCommandsNow();
        bed.World.Get<FarmZone>(zone).Crop.ShouldBe("peas");
        bed.DrainEvents().OfType<FarmZonesChangedEvent>().ShouldNotBeEmpty();
    }

    [Fact]
    public void Cancelling_takes_cells_and_their_crops_from_a_zone_and_an_empty_zone_goes_away()
    {
        var bed = Field();
        var zone = Zone(bed, "wheat", new TileCoord(3, 3, 1), new TileCoord(5, 3, 1));
        FarmRules.Sow(bed.Simulation, bed.Simulation.Content.Get<CropDef>("wheat"), new TileCoord(3, 3, 1));
        FarmRules.Sow(bed.Simulation, bed.Simulation.Content.Get<CropDef>("wheat"), new TileCoord(5, 3, 1));

        bed.Designate(DesignationKind.Cancel, new TileCoord(3, 3, 1), new TileCoord(3, 3, 1));
        bed.Simulation.ApplyCommandsNow();

        bed.World.Get<FarmZone>(zone).Cells.Select(cell => cell.X).ShouldBe([4, 5]);
        Crops(bed).Select(crop => bed.World.Get<Position>(crop).Cell.X).ShouldBe([5]);

        bed.Designate(DesignationKind.Cancel, new TileCoord(0, 0, 1), new TileCoord(15, 15, 1));
        bed.Simulation.ApplyCommandsNow();
        bed.World.IsAlive(zone).ShouldBeFalse();
        Crops(bed).ShouldBeEmpty();
    }

    // Criterion 2.
    [Fact]
    public void A_farmer_sows_the_zone_it_ripens_in_ten_days_and_the_reaped_cells_are_sown_again()
    {
        var bed = Field();
        Zone(bed, "wheat", new TileCoord(5, 5, 1), new TileCoord(7, 5, 1));
        Farmer(bed, 2, 2);
        bed.AddItems("provisions", 50, 1, 14, 1);

        bed.Simulation.Run(400);
        var crops = Crops(bed);
        crops.Count.ShouldBe(3);
        crops.ShouldAllBe(crop => bed.World.Get<Crop>(crop).Days == 0);

        // Ten midnights of growth: the first is at tick 1080, so ripe at 1080 + 9 × 1440.
        bed.Simulation.Run(1_080 + (8 * TicksPerDay) - 400 + 10);
        crops.ShouldAllBe(crop => bed.World.Get<Crop>(crop).Days == 9);
        FarmRules.State(bed.Simulation, new TileCoord(5, 5, 1))!.State.ShouldBe(FarmCellState.Growing);
        FarmRules.State(bed.Simulation, new TileCoord(5, 5, 1))!.PercentGrown.ShouldBe(90);

        for (var tick = 0; tick < 4_000 && bed.CountItems("wheat") < 24; tick += 20)
        {
            bed.Simulation.Run(20);
        }

        // A farmer of the average level reaps 8 × (70 + 3 × 10) / 100 = 8 a cell, and sows the cells again.
        bed.CountItems("wheat").ShouldBe(24);
        for (var tick = 0; tick < 400 && Crops(bed).Count < 3; tick += 20)
        {
            bed.Simulation.Run(20);
        }

        Crops(bed).Count.ShouldBe(3);
        Crops(bed).ShouldAllBe(crop => bed.World.Get<Crop>(crop).Days <= 1);
    }

    [Fact]
    public void The_more_skilled_the_reaper_the_more_it_reaps_and_the_fraction_is_drawn()
    {
        var bed = Field();
        var wheat = bed.Simulation.Content.Get<CropDef>("wheat");

        var green = Enumerable.Range(0, 400).Select(_ => FarmRules.Yield(bed.Simulation, wheat, 0)).ToList();
        var average = Enumerable.Range(0, 100).Select(_ => FarmRules.Yield(bed.Simulation, wheat, 10)).ToList();
        var master = Enumerable.Range(0, 400).Select(_ => FarmRules.Yield(bed.Simulation, wheat, 20)).ToList();

        // 5.6 at level 0, 8 at 10 and 10.4 at 20.
        green.ShouldAllBe(units => units == 5 || units == 6);
        average.ShouldAllBe(units => units == 8);
        master.ShouldAllBe(units => units == 10 || units == 11);
        green.Average().ShouldBeInRange(5.45, 5.75);
        master.Average().ShouldBeInRange(10.25, 10.55);
    }

    // Criterion 3.
    [Fact]
    public void Nothing_is_sown_that_cannot_ripen_before_the_growing_seasons_end()
    {
        var bed = Field();
        var wheat = bed.Simulation.Content.Get<CropDef>("wheat");
        var cabbage = bed.Simulation.Content.Get<CropDef>("cabbage");

        // Autumn ends on day 44: on day 40 four days of growth are left, on day 35 nine.
        GoTo(bed, 40);
        FarmRules.HasTimeToMature(bed.Simulation, wheat).ShouldBeFalse();
        FarmRules.HasTimeToMature(bed.Simulation, cabbage).ShouldBeFalse();
        GoTo(bed, 35);
        FarmRules.HasTimeToMature(bed.Simulation, wheat).ShouldBeFalse();
        FarmRules.HasTimeToMature(bed.Simulation, cabbage).ShouldBeTrue();
        GoTo(bed, 34);
        FarmRules.HasTimeToMature(bed.Simulation, wheat).ShouldBeTrue();

        // Not in winter, and the first day of spring has the whole season ahead.
        GoTo(bed, 59);
        FarmRules.HasTimeToMature(bed.Simulation, cabbage).ShouldBeFalse();
        GoTo(bed, 60);
        FarmRules.HasTimeToMature(bed.Simulation, wheat).ShouldBeTrue();
    }

    [Fact]
    public void A_farmer_does_not_sow_wheat_on_day_40_but_sows_cabbage_on_day_35()
    {
        var late = Field();
        Zone(late, "wheat", new TileCoord(5, 5, 1), new TileCoord(7, 5, 1));
        Farmer(late, 2, 2);
        GoTo(late, 40);
        late.Simulation.Run(300);
        Crops(late).ShouldBeEmpty();

        var cabbage = Field();
        Zone(cabbage, "cabbage", new TileCoord(5, 5, 1), new TileCoord(7, 5, 1));
        Farmer(cabbage, 2, 2);
        GoTo(cabbage, 35);
        cabbage.Simulation.Run(300);
        Crops(cabbage).Count.ShouldBe(3);
    }

    // Criterion 4.
    [Fact]
    public void A_crop_sown_late_in_autumn_waits_through_winter_and_grows_again_in_spring()
    {
        var bed = Field();
        Zone(bed, "cabbage", new TileCoord(5, 5, 1), new TileCoord(5, 5, 1));
        FarmRules.Sow(bed.Simulation, bed.Simulation.Content.Get<CropDef>("cabbage"), new TileCoord(5, 5, 1));
        var crop = Crops(bed).Single();

        // The midnights of days 43 and 44 are of autumn: two days.
        GoTo(bed, 42);
        bed.Simulation.Run(2 * TicksPerDay);
        bed.World.Get<Crop>(crop).Days.ShouldBe(2);

        // Winter, days 45 to 59: nothing grows and nothing dies.
        bed.Simulation.Run(15 * TicksPerDay);
        bed.World.IsAlive(crop).ShouldBeTrue();
        bed.World.Get<Crop>(crop).Days.ShouldBe(2);

        // The first midnight of spring.
        bed.Simulation.Run(2 * TicksPerDay);
        bed.World.Get<Crop>(crop).Days.ShouldBeGreaterThan(2);
    }

    // Criterion 5.
    [Fact]
    public void A_farmer_with_farming_set_to_never_does_not_sow_and_the_zone_needs_no_free_will()
    {
        var never = Field();
        Zone(never, "wheat", new TileCoord(5, 5, 1), new TileCoord(6, 5, 1));
        var idle = Farmer(never, 2, 2);
        never.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(idle, "farming", WorkPriorityLevels.Never));
        never.Simulation.Run(600);
        Crops(never).ShouldBeEmpty();

        var willing = Field();
        willing.Simulation.FreeWill.ShouldBeFalse();
        Zone(willing, "wheat", new TileCoord(5, 5, 1), new TileCoord(6, 5, 1));
        Farmer(willing, 2, 2);
        willing.Simulation.Run(600);
        Crops(willing).Count.ShouldBe(2);
    }

    [Fact]
    public void An_adolescent_farms_but_a_child_does_not()
    {
        var bed = Field();
        Zone(bed, "wheat", new TileCoord(5, 5, 1), new TileCoord(6, 5, 1));
        var child = Farmer(bed, 2, 2);
        bed.World.Add(child, new Life { AgeDays = 8 * 60, Stage = "child" });

        bed.Simulation.Run(600);
        Crops(bed).ShouldBeEmpty();

        bed.World.Get<Life>(child).AgeDays = 15 * 60;
        bed.World.Get<Life>(child).Stage = "adolescent";
        bed.Simulation.Run(600);
        Crops(bed).Count.ShouldBe(2);
    }

    [Fact]
    public void The_job_says_which_crop_it_sows_and_which_it_reaps()
    {
        var bed = Field();
        Zone(bed, "wheat", new TileCoord(5, 5, 1), new TileCoord(5, 5, 1));
        var farmer = Farmer(bed, 2, 2);
        bed.Simulation.Run(2);

        var sowing = bed.JobOf(farmer)!;
        sowing.Kind.ShouldBe(JobKind.Sow);
        sowing.Crop.ShouldBe("wheat");
        JobKinds.ReportKey(sowing).ShouldBe("job.sow.report");

        // Ripe crop: the farmer reaps it before sowing anything else.
        bed.Simulation.Run(200);
        var crop = Crops(bed).Single();
        bed.World.Get<Crop>(crop).Days = 10;
        Job? reaping = null;
        for (var tick = 0; tick < 120 && reaping?.Kind != JobKind.Reap; tick++)
        {
            bed.Simulation.Run(1);
            reaping = bed.JobOf(farmer);
        }

        reaping.ShouldNotBeNull();
        reaping.Kind.ShouldBe(JobKind.Reap);
        reaping.Crop.ShouldBe("wheat");
    }

    [Fact]
    public void A_farmer_reaps_what_is_ripe_before_sowing_what_is_empty()
    {
        var bed = Field();
        Zone(bed, "wheat", new TileCoord(5, 5, 1), new TileCoord(9, 5, 1));
        FarmRules.Sow(bed.Simulation, bed.Simulation.Content.Get<CropDef>("wheat"), new TileCoord(9, 5, 1));
        bed.World.Get<Crop>(Crops(bed).Single()).Days = 10;
        var farmer = Farmer(bed, 5, 6);

        bed.Simulation.Run(3);

        // The empty cells at hand are nearer than the crop at the end of the row; the harvest still comes first.
        bed.JobOf(farmer)!.Kind.ShouldBe(JobKind.Reap);
    }

    private static EntityId RipeCrop(ColonyTestBed bed, int x, int y, string crop = "wheat")
    {
        FarmRules.Sow(bed.Simulation, bed.Simulation.Content.Get<CropDef>(crop), new TileCoord(x, y, 1));
        var sown = Crops(bed).Last();
        bed.World.Get<Crop>(sown).Days = 10;
        return sown;
    }

    private static void Reap(ColonyTestBed bed, TileCoord from, TileCoord to, DesignationKind kind = DesignationKind.Reap)
    {
        bed.Simulation.Commands.Enqueue(new DesignateCommand(kind, from, to));
        bed.Simulation.ApplyCommandsNow();
    }

    [Fact]
    public void Reaping_orders_the_ripe_crops_of_the_box_in_a_zone_or_not_and_ignores_the_unripe()
    {
        var bed = Field();
        Zone(bed, "wheat", new TileCoord(5, 5, 1), new TileCoord(6, 5, 1));
        var inZone = RipeCrop(bed, 5, 5);
        var outside = RipeCrop(bed, 10, 10);
        FarmRules.Sow(bed.Simulation, bed.Simulation.Content.Get<CropDef>("wheat"), new TileCoord(6, 5, 1));
        var unripe = Crops(bed).Last();

        Reap(bed, new TileCoord(12, 12, 1), new TileCoord(4, 4, 1));

        bed.World.Has<HarvestOrder>(inZone).ShouldBeTrue();
        bed.World.Has<HarvestOrder>(outside).ShouldBeTrue();
        bed.World.Has<HarvestOrder>(unripe).ShouldBeFalse();

        // A wild gathering order leaves the crops alone, and a reaping order the wild plants.
        var other = RipeCrop(bed, 3, 3);
        Reap(bed, new TileCoord(0, 0, 0), new TileCoord(15, 15, 3), DesignationKind.Harvest);
        bed.World.Has<HarvestOrder>(other).ShouldBeFalse();
    }

    [Fact]
    public void A_farmer_reaps_an_ordered_crop_outside_any_zone_with_the_same_yield()
    {
        var bed = Field();
        var crop = RipeCrop(bed, 10, 10);
        var farmer = Farmer(bed, 8, 10);
        bed.Simulation.Run(3);
        bed.JobOf(farmer)?.Kind.ShouldNotBe(JobKind.Reap);

        Reap(bed, new TileCoord(10, 10, 1), new TileCoord(10, 10, 1));
        for (var tick = 0; tick < 200 && bed.JobOf(farmer)?.Kind != JobKind.Reap; tick++)
        {
            bed.Simulation.Run(1);
        }

        bed.JobOf(farmer)!.Kind.ShouldBe(JobKind.Reap);
        bed.JobOf(farmer)!.Target.ShouldBe(crop);

        for (var tick = 0; tick < 600 && bed.World.IsAlive(crop); tick += 20)
        {
            bed.Simulation.Run(20);
        }

        bed.World.IsAlive(crop).ShouldBeFalse();
        bed.CountItems("wheat").ShouldBe(8);
    }

    [Fact]
    public void An_ordered_crop_comes_before_a_nearer_one_of_the_zone_and_the_gatherer_does_not_take_it()
    {
        var bed = Field();
        Zone(bed, "wheat", new TileCoord(5, 5, 1), new TileCoord(5, 5, 1));
        RipeCrop(bed, 5, 5);
        var far = RipeCrop(bed, 13, 5);
        Reap(bed, new TileCoord(13, 5, 1), new TileCoord(13, 5, 1));
        var farmer = Farmer(bed, 5, 6);

        bed.Simulation.Run(3);

        bed.JobOf(farmer)!.Kind.ShouldBe(JobKind.Reap);
        bed.JobOf(farmer)!.Target.ShouldBe(far);
    }

    [Fact]
    public void Cancelling_removes_the_reaping_order_and_the_job_ends()
    {
        var bed = Field();
        var crop = RipeCrop(bed, 10, 10);
        Reap(bed, new TileCoord(10, 10, 1), new TileCoord(10, 10, 1));
        var farmer = Farmer(bed, 8, 10);
        bed.Simulation.Run(3);
        bed.JobOf(farmer)!.Kind.ShouldBe(JobKind.Reap);

        Reap(bed, new TileCoord(10, 10, 1), new TileCoord(10, 10, 1), DesignationKind.Cancel);
        bed.World.Has<HarvestOrder>(crop).ShouldBeFalse();
        bed.Simulation.Run(3);
        bed.JobOf(farmer)?.Kind.ShouldNotBe(JobKind.Reap);
        bed.World.IsAlive(crop).ShouldBeTrue();
    }

    [Fact]
    public void The_run_with_a_reaping_order_is_deterministic()
    {
        ulong Run()
        {
            var bed = Field(seed: 7);
            RipeCrop(bed, 10, 10);
            RipeCrop(bed, 11, 10);
            Reap(bed, new TileCoord(10, 10, 1), new TileCoord(11, 10, 1));
            Farmer(bed, 8, 10);
            bed.Simulation.Run(300);
            return bed.Simulation.ComputeStateHash();
        }

        Run().ShouldBe(Run());
    }

    [Fact]
    public void Two_farmers_do_not_sow_the_same_cell()
    {
        var bed = Field();
        Zone(bed, "wheat", new TileCoord(5, 5, 1), new TileCoord(6, 5, 1));
        var first = Farmer(bed, 3, 3, name: "Elvira");
        var second = Farmer(bed, 3, 4, name: "Rodrigo");

        bed.Simulation.Run(2);

        bed.JobOf(first)!.Cell.ShouldNotBe(bed.JobOf(second)!.Cell);
    }

    // Criterion 7.
    [Fact]
    public void Berries_in_the_open_rot_in_sixty_days_and_the_player_is_told()
    {
        var bed = Field();
        bed.AddItems("berries", 20, 4, 4, 1);

        bed.Simulation.Run(59 * TicksPerDay);
        bed.CountItems("berries").ShouldBe(20);
        var fresh = bed.World.Get<ItemStack>(PileOf(bed, "berries")).FreshnessValue;
        fresh.ShouldBeInRange(1, 400);
        bed.DrainEvents().OfType<NotificationEvent>().ShouldNotContain(notice => notice.Text.Key == SpoilageSystem.SpoiledKey);

        bed.Simulation.Run(TicksPerDay);

        bed.CountItems("berries").ShouldBe(0);
        var notice = bed.DrainEvents().OfType<NotificationEvent>().Single(notice => notice.Text.Key == SpoilageSystem.SpoiledKey);
        notice.Text.Args["count"].ShouldBe(20);
        ((LocalizedText)notice.Text.Args["item"]).Key.ShouldBe("def.item.berries.name");
    }

    [Fact]
    public void Berries_under_a_roof_or_rock_last_twice_as_long()
    {
        // Level 2: a roof over row 3 from x 0 to 2, rock at x 3 on that row, and open air over row 5.
        var bed = new ColonyTestBed(
            FarmTests.Content,
            1,
            ColonyTestBed.Level(8, 8, '#'),
            ColonyTestBed.Level(8, 8, '.'),
            string.Join("\n", Enumerable.Range(0, 8).Select(row => row == 3 ? "...#...." : "        ")));
        bed.AddItems("berries", 20, 1, 3, 1);
        bed.AddItems("berries", 20, 3, 3, 1);
        bed.AddItems("berries", 20, 1, 5, 1);

        bed.Simulation.Run(60 * TicksPerDay);

        // The open cell is gone at day 60; the roofed one and the one with rock above it are not.
        bed.CountItemsAt("berries", new TileCoord(1, 5, 1)).ShouldBe(0);
        bed.CountItemsAt("berries", new TileCoord(1, 3, 1)).ShouldBe(20);
        bed.CountItemsAt("berries", new TileCoord(3, 3, 1)).ShouldBe(20);

        bed.Simulation.Run(59 * TicksPerDay);
        bed.CountItemsAt("berries", new TileCoord(1, 3, 1)).ShouldBe(20);
        bed.CountItemsAt("berries", new TileCoord(3, 3, 1)).ShouldBe(20);
        bed.Simulation.Run(TicksPerDay);
        bed.CountItemsAt("berries", new TileCoord(1, 3, 1)).ShouldBe(0);
        bed.CountItemsAt("berries", new TileCoord(3, 3, 1)).ShouldBe(0);
    }

    [Fact]
    public void Food_that_does_not_rot_and_small_losses_change_nothing_and_say_nothing()
    {
        var bed = Field();
        bed.AddItems("provisions", 10, 4, 4, 1);
        bed.AddItems("berries", 4, 6, 6, 1);

        bed.Simulation.Run(70 * TicksPerDay);

        bed.CountItems("provisions").ShouldBe(10);
        bed.World.Get<ItemStack>(PileOf(bed, "provisions")).Freshness.ShouldBeNull();
        bed.CountItems("berries").ShouldBe(0);

        // Four units are under the notice of five.
        bed.DrainEvents().OfType<NotificationEvent>().ShouldNotContain(notice => notice.Text.Key == SpoilageSystem.SpoiledKey);
    }

    // Criterion 8.
    [Fact]
    public void Two_piles_put_together_average_their_freshness_by_units()
    {
        var bed = Field();
        var cell = new TileCoord(4, 4, 1);
        ItemPiles.Drop(bed.Simulation, "wheat", 40, cell, freshness: 5_000);

        ItemPiles.Drop(bed.Simulation, "wheat", 40, cell);

        var pile = bed.World.Get<ItemStack>(PileOf(bed, "wheat"));
        pile.Count.ShouldBe(80);
        pile.Freshness.ShouldBe(7_500);
    }

    [Fact]
    public void A_load_in_the_hands_rots_too_and_is_lost_when_it_is_rotten()
    {
        var bed = Field();
        var colonist = bed.AddColonist(2, 2, 1);
        var other = bed.AddColonist(3, 2, 1);
        bed.World.Add(colonist, new Carried { Def = "cabbage", Count = 10, Freshness = 200 });
        bed.World.Add(other, new Carried { Def = "cabbage", Count = 10 });

        new SpoilageSystem().Tick(bed.Simulation);

        // A cabbage rots in 30 days: 334 a day.
        bed.World.Has<Carried>(colonist).ShouldBeFalse();
        bed.World.Get<Carried>(other).Freshness.ShouldBe(10_000 - 334);
    }

    [Fact]
    public void Cancelling_a_grove_takes_its_saplings_with_it()
    {
        var bed = Field();
        Zone(bed, "grove", new TileCoord(2, 2, 1), new TileCoord(7, 7, 1));
        Farmer(bed, 10, 10);
        bed.AddItems("provisions", 50, 15, 15, 1);
        bed.Simulation.Run(1_000);
        Saplings(bed).ShouldNotBeEmpty();

        bed.Designate(DesignationKind.Cancel, new TileCoord(0, 0, 1), new TileCoord(15, 15, 1));
        bed.Simulation.ApplyCommandsNow();

        Saplings(bed).ShouldBeEmpty();
    }

    [Fact]
    public void A_load_that_rots_on_the_way_ends_the_job_that_carried_it()
    {
        var bed = Field();
        var colonist = bed.AddColonist(2, 2, 1);
        bed.World.Add(colonist, new Carried { Def = "cabbage", Count = 10, Freshness = 200 });
        bed.World.Add(colonist, new Job { Kind = JobKind.Haul, Stage = JobStage.Carrying, Destination = new TileCoord(5, 5, 1) });

        new SpoilageSystem().Tick(bed.Simulation);

        bed.World.Has<Carried>(colonist).ShouldBeFalse();
        bed.World.Has<Job>(colonist).ShouldBeFalse();
    }

    // Criterion 6 (the choice of what to eat).
    [Fact]
    public void A_hungry_colonist_prefers_a_prepared_dish_to_nearer_food()
    {
        var bed = Field(40);
        bed.AddItems("berries", 20, 6, 2, 1);
        bed.AddItems("bread", 5, 16, 2, 1);
        var colonist = bed.AddColonist(2, 2, 1, hunger: 3_000);

        bed.Simulation.Run(1);

        EatenFrom(bed, colonist).ShouldBe("bread");
    }

    [Fact]
    public void Among_foods_that_are_not_dishes_the_less_fresh_comes_first_and_then_the_nearer()
    {
        var bed = Field(40);
        ItemPiles.Drop(bed.Simulation, "berries", 20, new TileCoord(6, 2, 1));
        ItemPiles.Drop(bed.Simulation, "cabbage", 20, new TileCoord(12, 2, 1), freshness: 4_000);
        var colonist = bed.AddColonist(2, 2, 1, hunger: 3_000);

        bed.Simulation.Run(1);
        EatenFrom(bed, colonist).ShouldBe("cabbage");

        // Equally fresh: the nearer.
        var same = Field(40);
        ItemPiles.Drop(same.Simulation, "berries", 20, new TileCoord(6, 2, 1));
        ItemPiles.Drop(same.Simulation, "cabbage", 20, new TileCoord(12, 2, 1));
        var other = same.AddColonist(2, 2, 1, hunger: 3_000);
        same.Simulation.Run(1);
        EatenFrom(same, other).ShouldBe("berries");
    }

    [Fact]
    public void The_dish_must_be_near_the_nearest_food_and_a_starving_colonist_eats_the_nearest()
    {
        var far = Field(60);
        far.AddItems("berries", 20, 6, 2, 1);
        far.AddItems("bread", 5, 40, 2, 1);
        var colonist = far.AddColonist(2, 2, 1, hunger: 3_000);
        far.Simulation.Run(1);
        EatenFrom(far, colonist).ShouldBe("berries");

        var starving = Field(40);
        starving.AddItems("berries", 20, 6, 2, 1);
        starving.AddItems("bread", 5, 16, 2, 1);
        var critical = starving.AddColonist(2, 2, 1, hunger: 1_000);
        starving.Simulation.Run(1);
        EatenFrom(starving, critical).ShouldBe("berries");
    }

    [Fact]
    public void Eating_a_dish_leaves_one_thought_that_does_not_add_up()
    {
        var bed = Field();
        bed.AddItems("bread", 10, 3, 2, 1);
        var colonist = bed.AddColonist(2, 2, 1, hunger: 3_000);

        bed.Simulation.Run(400);

        var thoughts = MoodRules.Thoughts(bed.Simulation, colonist).Where(thought => thought.Id == "ate_meal").ToList();
        thoughts.Count.ShouldBe(1);
        thoughts[0].Mood.ShouldBe(3);
        bed.World.Get<Mood>(colonist).Thoughts.Count(thought => thought.Thought == "ate_meal").ShouldBe(1);
    }

    // Criterion 9.
    [Fact]
    public void Wheat_flour_and_bread_count_for_the_food_reserve_of_a_couple()
    {
        var content = TestContent.WithDefs(Json(birth: "\"foodReserveDays\": 30"));
        var (bed, wife, _) = BirthTests.Couple(seed: 1, pace: "yearly", content: content, food: false);

        // 30 days for three mouths at 4800 a day are 432 000: wheat and flour are worth 800 each, bread 800.
        bed.AddItems("wheat", 100, 1, 13, 1);
        bed.AddItems("flour", 100, 3, 13, 1);
        bed.AddItems("bread", 100, 5, 13, 1);
        bed.Simulation.Run(TicksPerDay);
        bed.World.Has<Pregnancy>(wife).ShouldBeFalse();

        // 480 000 with what the couple eats in the meantime.
        bed.AddItems("wheat", 100, 7, 13, 1);
        bed.AddItems("flour", 100, 9, 13, 1);
        bed.AddItems("bread", 100, 11, 13, 1);
        bed.Simulation.Run(TicksPerDay);
        bed.World.Has<Pregnancy>(wife).ShouldBeTrue();
    }

    [Fact]
    public void A_field_of_a_crop_of_food_counts_as_plants_for_the_land_that_feeds_one_more()
    {
        var content = TestContent.WithDefs(Json(birth: "\"foodPlantsPerColonist\": 4, \"cropTilePlantEquivalent\": 1"));
        var (bed, wife, _) = BirthTests.Couple(seed: 1, pace: "yearly", content: content);
        ColonyTestBedSoil(bed);
        for (var x = 1; x < 12; x++)
        {
            bed.AddPlant("bush", x, 1, 1);
        }

        // Eleven bushes feed two colonists, not three; a grove is no food.
        Zone(bed, "grove", new TileCoord(1, 5, 1), new TileCoord(2, 5, 1));
        bed.Simulation.Run(TicksPerDay);
        bed.World.Has<Pregnancy>(wife).ShouldBeFalse();

        // One cell of wheat, sown or not, is a plant more.
        Zone(bed, "wheat", new TileCoord(1, 9, 1), new TileCoord(1, 9, 1));
        bed.Simulation.Run(TicksPerDay);
        bed.World.Has<Pregnancy>(wife).ShouldBeTrue();
    }

    private static void ColonyTestBedSoil(ColonyTestBed bed)
    {
        var soil = (ushort)bed.Simulation.Content.IndexOf<MaterialDef>("soil");
        for (var y = 0; y < bed.Map.Size.Height; y++)
        {
            for (var x = 0; x < bed.Map.Size.Width; x++)
            {
                bed.Map.SetTileQuietly(x, y, 1, TileShape.Floor, soil, TileFlags.None);
            }
        }
    }

    // Criterion 10.
    [Fact]
    public void A_grove_zone_plants_saplings_on_the_even_cells_only_once_and_they_grow_in_ten_days()
    {
        var bed = Field(20);
        Zone(bed, "grove", new TileCoord(2, 2, 1), new TileCoord(7, 7, 1));
        Farmer(bed, 10, 10);
        Farmer(bed, 11, 10, name: "Rodrigo");
        bed.AddItems("provisions", 50, 15, 15, 1);
        bed.AddItems("provisions", 50, 16, 15, 1);

        bed.Simulation.Run(2_000);

        var saplings = Saplings(bed);
        saplings.Count.ShouldBe(9);
        saplings.ShouldAllBe(sapling => bed.World.Get<Position>(sapling).X % 2 == 0 && bed.World.Get<Position>(sapling).Y % 2 == 0);
        bed.World.Get<Plant>(saplings[0]).Def.ShouldBe("tree");
        bed.Map.IsStandable(bed.World.Get<Position>(saplings[0]).Cell).ShouldBeTrue();

        // Nothing is sown again where there is a sapling or, later, a tree.
        bed.Simulation.Run(11 * TicksPerDay);
        Saplings(bed).ShouldBeEmpty();
        var trees = new List<EntityId>();
        bed.World.Query<Plant, Position>(trees);
        trees.Count.ShouldBe(9);
        trees.ShouldAllBe(tree => !bed.Map.IsStandable(bed.World.Get<Position>(tree).Cell));
    }

    // Criterion 11.
    [Fact]
    public void Nature_leaves_the_farm_zones_alone()
    {
        var content = TestContent.WithDefs(Json(seedChance: 10_000));
        var bed = Field(20, 3, content);
        bed.AddPlant("tree", 10, 10, 1);
        var zone = Zone(bed, "wheat", new TileCoord(11, 6, 1), new TileCoord(16, 13, 1));
        var inside = bed.World.Get<FarmZone>(zone).Cells.ToHashSet();
        inside.Remove(new TileCoord(10, 10, 1));

        for (var day = 0; day < 30; day++)
        {
            bed.Simulation.Run(TicksPerDay);
        }

        var plants = new List<EntityId>();
        bed.World.Query<Plant, Position>(plants);
        plants.Count.ShouldBeGreaterThan(5);
        plants.ShouldAllBe(plant => !inside.Contains(bed.World.Get<Position>(plant).Cell));
    }

    [Fact]
    public void A_stockpile_drawn_over_a_field_skips_its_cells_and_nothing_is_built_on_them()
    {
        var bed = Field();
        Zone(bed, "wheat", new TileCoord(3, 3, 1), new TileCoord(5, 5, 1));
        bed.Designate(DesignationKind.Stockpile, new TileCoord(4, 4, 1), new TileCoord(7, 7, 1));
        bed.Simulation.ApplyCommandsNow();
        var stockpiles = new List<EntityId>();
        bed.World.Query<Stockpile>(stockpiles);
        var cells = stockpiles.SelectMany(zone => bed.World.Get<Stockpile>(zone).Cells).ToList();
        cells.Count.ShouldBe(16 - 4);
        cells.ShouldAllBe(cell => cell.X > 5 || cell.Y > 5);
        Construction.CellsToPlan(bed.Simulation, bed.Simulation.Content.Get<BuildableDef>("table"), new TileCoord(9, 9, 1), new TileCoord(9, 9, 1)).Count.ShouldBe(1);

        var table = bed.Simulation.Content.Get<BuildableDef>("table");
        Construction.CellsToPlan(bed.Simulation, table, new TileCoord(3, 3, 1), new TileCoord(5, 5, 1)).ShouldBeEmpty();
    }

    [Fact]
    public void Queries_tell_the_zones_and_the_state_of_a_cell()
    {
        var bed = Field();
        var zone = Zone(bed, "wheat", new TileCoord(3, 3, 1), new TileCoord(4, 3, 1));
        FarmRules.Sow(bed.Simulation, bed.Simulation.Content.Get<CropDef>("wheat"), new TileCoord(3, 3, 1));
        bed.World.Get<Crop>(Crops(bed).Single()).Days = 10;
        FarmRules.Sow(bed.Simulation, bed.Simulation.Content.Get<CropDef>("wheat"), new TileCoord(4, 3, 1));

        FarmRules.Zones(bed.Simulation).ShouldBe([new FarmZoneInfo(zone, "wheat", 2, 1, 1)]);
        FarmRules.State(bed.Simulation, new TileCoord(3, 3, 1))!.State.ShouldBe(FarmCellState.Ripe);
        FarmRules.State(bed.Simulation, new TileCoord(4, 3, 1))!.State.ShouldBe(FarmCellState.Growing);
        FarmRules.State(bed.Simulation, new TileCoord(9, 9, 1)).ShouldBeNull();
        FarmRules.ZoneAt(bed.Simulation, new TileCoord(4, 3, 1)).ShouldBe(zone);
    }

    private static EntityId PileOf(ColonyTestBed bed, string def)
    {
        var piles = new List<EntityId>();
        bed.World.Query<ItemStack>(piles);
        return piles.First(pile => bed.World.Get<ItemStack>(pile).Def == def);
    }

    private static List<EntityId> Saplings(ColonyTestBed bed)
    {
        var saplings = new List<EntityId>();
        bed.World.Query<Sapling>(saplings);
        return saplings;
    }

    /// <summary>The item of the pile the colonist set off to eat from; "" if it is not going to eat.</summary>
    private static string EatenFrom(ColonyTestBed bed, EntityId colonist) =>
        bed.JobOf(colonist) is { Kind: JobKind.Eat } job ? bed.World.Get<ItemStack>(job.Target).Def : string.Empty;
}
