using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Save;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Colony;

/// <summary>
/// Workshops, recipes and the craft job (<c>docs/gdd/oficios.md</c>, criteria 3, 4, 5, 6, 8 and 9).
/// The test content adds a carpenter's bench, a kitchen and a forge to the usual one.
/// </summary>
public class CraftTests
{
    private const string Extras = """
        { "type": "WorkType", "id": "carpentry", "order": 5, "jobs": [], "workshop": true, "skill": true },
        { "type": "WorkType", "id": "cooking", "order": 6, "jobs": [], "workshop": true, "light": true, "skill": true },
        { "type": "WorkType", "id": "smithing", "order": 7, "jobs": [], "workshop": true, "skill": true },
        { "type": "Knowledge", "id": "carpentry" },
        { "type": "Knowledge", "id": "cooking" },
        { "type": "Item", "id": "handle", "category": "misc", "kind": "raw", "visual": "item.handle", "stackLimit": 50 },
        { "type": "Item", "id": "iron_axe", "category": "misc", "kind": "raw", "visual": "item.iron_axe", "stackLimit": 1,
          "tool": { "workType": "woodcutting", "speedPercent": 30 } },
        { "type": "Buildable", "id": "carpenter_bench", "requires": ["carpentry"], "shape": "furniture", "materials": ["timber"], "cost": 2,
          "workTicks": 20, "visual": "furniture.carpenter_bench", "beauty": -1, "workstation": { "workType": "carpentry" } },
        { "type": "Buildable", "id": "kitchen", "requires": ["cooking"], "shape": "furniture", "materials": ["timber"], "cost": 2,
          "workTicks": 20, "visual": "furniture.kitchen", "workstation": { "workType": "cooking" } },
        { "type": "Buildable", "id": "forge", "shape": "furniture", "materials": ["timber"], "cost": 2,
          "workTicks": 20, "visual": "furniture.forge", "workstation": { "workType": "smithing" } },
        { "type": "Recipe", "id": "plank", "requires": ["carpentry"], "workshop": "carpenter_bench",
          "inputs": [{ "item": "log", "count": 1 }], "outputs": [{ "item": "plank", "count": 2 }], "workTicks": 90 },
        { "type": "Recipe", "id": "handle", "requires": ["carpentry"], "workshop": "carpenter_bench", "order": 1,
          "inputs": [{ "item": "plank", "count": 1 }], "outputs": [{ "item": "handle", "count": 2 }], "workTicks": 60 },
        { "type": "Recipe", "id": "fine_handle", "requires": ["carpentry", "cooking"], "workshop": "carpenter_bench", "order": 2,
          "inputs": [{ "item": "plank", "count": 1 }], "outputs": [{ "item": "handle", "count": 2 }], "workTicks": 60 },
        { "type": "Recipe", "id": "dried_berries", "requires": ["cooking"], "workshop": "kitchen",
          "inputs": [{ "item": "berries", "count": 3 }], "outputs": [{ "item": "provisions", "count": 1 }], "workTicks": 60 },
        { "type": "Recipe", "id": "axe", "workshop": "forge", "quality": true,
          "inputs": [{ "item": "handle", "count": 1 }], "outputs": [{ "item": "iron_axe", "count": 1 }], "workTicks": 60 }
        """;

    private static readonly Lazy<DefDatabase> ContentLazy = new(() =>
        TestContent.WithDefs(string.Concat(ColonyTestBed.ContentJson.AsSpan(0, ColonyTestBed.ContentJson.LastIndexOf(']')), ",", Extras, "]")));

    private static DefDatabase Content => ContentLazy.Value;

    private static readonly TileCoord BenchCell = new(8, 8, 1);

    /// <summary>A flat floor of 16 × 16 with a stockpile in its corner; the village knows carpentry and cooking.</summary>
    private static ColonyTestBed Village(ulong seed = 1)
    {
        var bed = ColonyTestBed.Flat(16, seed, Content);
        bed.Simulation.Progress.Learn("carpentry");
        bed.Simulation.Progress.Learn("cooking");
        bed.Designate(DesignationKind.Stockpile, new TileCoord(1, 1, 1), new TileCoord(3, 3, 1));
        bed.Simulation.ApplyCommandsNow();
        return bed;
    }

    /// <summary>Plans a workshop and finishes it at once, as FurnitureTests does with furniture.</summary>
    private static EntityId Build(ColonyTestBed bed, string buildable, TileCoord cell)
    {
        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand(buildable, "timber", cell, cell));
        bed.Simulation.ApplyCommandsNow();
        var blueprints = new List<EntityId>();
        bed.World.Query<Blueprint, Position>(blueprints);
        Construction.Finish(bed.Simulation, blueprints.Single(entity => bed.World.Get<Position>(entity).Cell == cell), cell);
        var workshops = new List<EntityId>();
        bed.World.Query<Workshop, Position>(workshops);
        return workshops.Single(entity => bed.World.Get<Position>(entity).Cell == cell);
    }

    private static EntityId Order(ColonyTestBed bed, string recipe, int quantity)
    {
        bed.Simulation.Commands.Enqueue(new CreateWorkOrderCommand(recipe, WorkOrderMode.Count, quantity));
        bed.Simulation.ApplyCommandsNow();
        var orders = new List<EntityId>();
        bed.World.Query<WorkOrder>(orders);
        return orders[^1];
    }

    private static EntityId Worker(ColonyTestBed bed, int x, int y, string name, int level = 0, string trade = "carpentry")
    {
        var colonist = bed.AddColonist(x, y, 1, name: name);
        bed.World.Add(colonist, new Skills { Values = [new SkillValue { WorkType = trade, Level = level }] });
        return colonist;
    }

    /// <summary>Every unit of an item anywhere: on the ground, in hands and delivered to the workshops.</summary>
    private static int Everywhere(ColonyTestBed bed, string item)
    {
        var inShops = new List<EntityId>();
        bed.World.Query<Workshop>(inShops);
        return Stocks.Count(bed.Simulation, item)
               + inShops.Sum(shop => bed.World.Get<Workshop>(shop).Inputs.Where(stack => stack.Def == item).Sum(stack => stack.Count));
    }

    [Fact]
    public void An_order_for_four_planks_fetches_the_logs_makes_the_planks_and_the_hauler_stores_them()
    {
        var bed = Village();
        Build(bed, "carpenter_bench", BenchCell);
        bed.AddItems("log", 2, 2, 2, 1);
        var carpenter = Worker(bed, 6, 6, "Carpenter");
        var order = Order(bed, "plank", 4);

        bed.Simulation.Run(900);

        bed.CountItems("plank").ShouldBe(4);
        Everywhere(bed, "log").ShouldBe(0);
        bed.World.Get<WorkOrder>(order).Done.ShouldBe(4);
        WorkOrderRules.Status(bed.Simulation, order).ShouldBe(WorkOrderStatus.Done);
        Stockpiles.StoredItems(bed.Simulation).ShouldBe(4);

        // The carpenter learns the trade by working: 1 point per tick of work.
        var skill = bed.World.Get<Skills>(carpenter).Find("carpentry")!;
        (skill.Level * 100 + skill.Experience).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void With_one_log_the_orders_for_two_planks_and_two_handles_end_with_one_plank_and_two_handles()
    {
        var bed = Village();
        Build(bed, "carpenter_bench", BenchCell);
        bed.AddItems("log", 1, 2, 2, 1);
        Worker(bed, 6, 6, "Carpenter");
        var planks = Order(bed, "plank", 2);
        var handles = Order(bed, "handle", 2);

        bed.Simulation.Run(1_500);

        bed.CountItems("plank").ShouldBe(1);
        bed.CountItems("handle").ShouldBe(2);
        Everywhere(bed, "log").ShouldBe(0);
        WorkOrderRules.Status(bed.Simulation, planks).ShouldBe(WorkOrderStatus.Done);
        WorkOrderRules.Status(bed.Simulation, handles).ShouldBe(WorkOrderStatus.Done);
    }

    [Fact]
    public void Only_one_colonist_at_a_time_uses_a_workshop_and_the_other_takes_something_else()
    {
        var bed = Village();
        var shop = Build(bed, "carpenter_bench", BenchCell);
        bed.AddItems("log", 2, 2, 2, 1);
        var first = Worker(bed, 6, 6, "First");
        var second = Worker(bed, 7, 7, "Second");
        Order(bed, "plank", 4);

        var crafted = 0;
        for (var tick = 0; tick < 1_200; tick++)
        {
            bed.Simulation.Tick();
            var crafters = new[] { first, second }.Where(colonist => bed.JobOf(colonist) is { Kind: JobKind.Craft }).ToList();
            crafters.Count.ShouldBeLessThanOrEqualTo(1);
            crafters.ForEach(colonist => bed.JobOf(colonist)!.Recipient.ShouldBe(shop));
            crafted += crafters.Count;
        }

        crafted.ShouldBeGreaterThan(0);
        bed.CountItems("plank").ShouldBe(4);
    }

    [Fact]
    public void A_recipe_without_its_knowledge_is_locked_and_a_workshop_without_it_is_not_planned_until_it_is_learned()
    {
        var bed = ColonyTestBed.Flat(16, 1, Content);
        Worker(bed, 6, 6, "Carpenter");
        bed.AddItems("log", 2, 2, 2, 1);
        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand("carpenter_bench", "timber", BenchCell, BenchCell));
        bed.Simulation.ApplyCommandsNow();
        bed.World.Store<Blueprint>().Count.ShouldBe(0);
        var order = Order(bed, "plank", 2);

        WorkOrderRules.Status(bed.Simulation, order).ShouldBe(WorkOrderStatus.Locked);
        WorkOrderRules.MissingKnowledge(bed.Simulation, order).ShouldBe(["carpentry"]);
        bed.Simulation.Unlocks.Missing(Content.Get<BuildableDef>("carpenter_bench"), bed.Simulation.Progress).ShouldBe(["carpentry"]);

        bed.Simulation.Progress.Learn("carpentry");
        WorkOrderRules.Status(bed.Simulation, order).ShouldBe(WorkOrderStatus.NoWorkshop);
        Build(bed, "carpenter_bench", BenchCell);
        WorkOrderRules.Status(bed.Simulation, order).ShouldBe(WorkOrderStatus.Running);
        bed.Simulation.Run(600);
        bed.CountItems("plank").ShouldBe(2);
    }

    [Fact]
    public void A_locked_order_is_not_made_even_if_the_workshop_stands()
    {
        var bed = ColonyTestBed.Flat(16, 1, Content);
        bed.Simulation.Progress.Learn("carpentry");
        Build(bed, "carpenter_bench", BenchCell);
        bed.AddItems("plank", 2, 2, 2, 1);
        Worker(bed, 6, 6, "Carpenter");
        var order = Order(bed, "fine_handle", 2);

        bed.Simulation.Run(600);

        // The recipe also needs cooking, which this village lacks.
        bed.CountItems("handle").ShouldBe(0);
        bed.CountItems("plank").ShouldBe(2);
        WorkOrderRules.Status(bed.Simulation, order).ShouldBe(WorkOrderStatus.Locked);
        WorkOrderRules.MissingKnowledge(bed.Simulation, order).ShouldBe(["cooking"]);

        bed.Simulation.Progress.Learn("cooking");
        bed.Simulation.Run(600);
        bed.CountItems("handle").ShouldBe(2);
    }

    [Fact]
    public void A_colonist_who_never_does_the_trade_does_not_take_the_order()
    {
        var bed = Village();
        Build(bed, "carpenter_bench", BenchCell);
        bed.AddItems("log", 2, 2, 2, 1);
        var colonist = Worker(bed, 6, 6, "Carpenter");
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "carpentry", WorkPriorityLevels.Never));
        var order = Order(bed, "plank", 2);

        bed.Simulation.Run(600);

        bed.CountItems("plank").ShouldBe(0);
        bed.CountItems("log").ShouldBe(2);
        WorkOrderRules.Status(bed.Simulation, order).ShouldBe(WorkOrderStatus.NoWorker);
    }

    [Fact]
    public void An_adolescent_can_cook_but_not_forge()
    {
        var bed = Village();
        Build(bed, "kitchen", new TileCoord(8, 8, 1));
        Build(bed, "forge", new TileCoord(10, 8, 1));
        bed.AddItems("berries", 3, 2, 2, 1);
        bed.AddItems("handle", 1, 3, 3, 1);
        var adolescent = Worker(bed, 6, 6, "Teenager", trade: "cooking");
        bed.World.Add(adolescent, new Life { AgeDays = 15 * 60, Stage = "adolescent" });
        Order(bed, "dried_berries", 1);
        var axes = Order(bed, "axe", 1);

        bed.Simulation.Run(800);

        bed.CountItems("provisions").ShouldBe(1);
        bed.CountItems("berries").ShouldBe(0);
        bed.CountItems("handle").ShouldBe(1);
        bed.CountItems("iron_axe").ShouldBe(0);
        WorkOrderRules.Status(bed.Simulation, axes).ShouldBe(WorkOrderStatus.NoWorker);

        // An adult smith makes the tool, with a quality that a roll gives: never a poor one at level 20.
        Worker(bed, 7, 7, "Smith", level: 20, trade: "smithing");
        bed.Simulation.Run(800);
        bed.CountItems("iron_axe").ShouldBe(1);
        var axe = new List<EntityId>();
        bed.World.Query<ItemStack>(axe);
        axe.Select(bed.World.Get<ItemStack>).Single(stack => stack.Def == "iron_axe").Quality.ShouldNotBe("poor");
    }

    [Fact]
    public void A_colonist_in_critical_hunger_leaves_the_craft_and_the_ingredients_stay_in_the_workshop_for_the_next()
    {
        var bed = Village();
        var shop = Build(bed, "carpenter_bench", BenchCell);
        bed.AddItems("log", 1, 2, 2, 1);
        bed.AddItems("berries", 5, 3, 3, 1);
        var first = Worker(bed, 6, 6, "First");
        Order(bed, "plank", 2);

        var working = false;
        for (var tick = 0; tick < 400 && !working; tick++)
        {
            bed.Simulation.Tick();
            working = bed.JobOf(first) is { Kind: JobKind.Craft, Stage: JobStage.Working } job && job.Target.IsNone
                      && bed.World.Get<Workshop>(shop).Inputs.Count == 1;
        }

        working.ShouldBeTrue();
        bed.Need(first, "hunger").Value = 1_000;
        for (var tick = 0; tick < 100 && bed.JobOf(first)?.Kind != JobKind.Eat; tick++)
        {
            bed.Simulation.Tick();
        }

        bed.JobOf(first)!.Kind.ShouldBe(JobKind.Eat);
        var delivered = bed.World.Get<Workshop>(shop).Inputs.Single();
        (delivered.Def, delivered.Count).ShouldBe(("log", 1));
        Everywhere(bed, "log").ShouldBe(1);

        // Another colonist goes on with the delivered log; nothing is lost or made twice.
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(first, "carpentry", WorkPriorityLevels.Never));
        Worker(bed, 7, 7, "Second");
        bed.Simulation.Run(800);

        bed.CountItems("plank").ShouldBe(2);
        Everywhere(bed, "log").ShouldBe(0);
    }

    [Fact]
    public void Taking_away_an_order_lets_a_craft_under_way_finish_and_drops_the_ingredients_of_a_workshop_nobody_uses()
    {
        var bed = Village();
        var shop = Build(bed, "carpenter_bench", BenchCell);
        Crafting.Deliver(bed.Simulation, shop, "log", 2, null, null);
        var idle = Order(bed, "plank", 4);

        bed.Simulation.Commands.Enqueue(new RemoveWorkOrderCommand(idle));
        bed.Simulation.ApplyCommandsNow();

        bed.World.Store<WorkOrder>().Count.ShouldBe(0);
        bed.World.Get<Workshop>(shop).Inputs.ShouldBeEmpty();
        bed.CountItemsAt("log", BenchCell).ShouldBe(2);

        var busy = Order(bed, "plank", 4);
        Worker(bed, 6, 6, "Carpenter");
        bed.Simulation.Run(60);
        bed.JobOf(bed.Colonists().Single())!.Kind.ShouldBe(JobKind.Craft);
        bed.Simulation.Commands.Enqueue(new RemoveWorkOrderCommand(busy));
        bed.Simulation.Run(600);

        // The batch under way ends; no new one starts for an order that is gone.
        bed.CountItems("plank").ShouldBe(2);
        bed.CountItems("log").ShouldBe(1);
        bed.World.Store<WorkOrder>().Count.ShouldBe(0);
    }

    [Fact]
    public void Creating_an_order_ignores_what_makes_no_sense()
    {
        var bed = Village();
        var shop = Build(bed, "kitchen", new TileCoord(8, 8, 1));

        bed.Simulation.Commands.Enqueue(new CreateWorkOrderCommand("nothing", WorkOrderMode.Count, 2));
        bed.Simulation.Commands.Enqueue(new CreateWorkOrderCommand("plank", WorkOrderMode.Count, 0));
        bed.Simulation.Commands.Enqueue(new CreateWorkOrderCommand("plank", WorkOrderMode.Count, 2, shop));
        bed.Simulation.Commands.Enqueue(new CreateWorkOrderCommand("dried_berries", WorkOrderMode.Count, 2, shop));
        bed.Simulation.Commands.Enqueue(new RemoveWorkOrderCommand(new EntityId(500, 1)));
        bed.Simulation.ApplyCommandsNow();

        var orders = new List<EntityId>();
        bed.World.Query<WorkOrder>(orders);
        orders.Count.ShouldBe(1);
        var order = bed.World.Get<WorkOrder>(orders[0]);
        (order.Recipe, order.Quantity, order.Done, order.Workshop).ShouldBe(("dried_berries", 2, 0, shop));
    }

    [Fact]
    public void Logs_beyond_the_fetch_radius_of_every_workshop_do_not_make_an_order_run()
    {
        var bed = ColonyTestBed.Flat(80, 1, Content);
        bed.Simulation.Progress.Learn("carpentry");
        Build(bed, "carpenter_bench", BenchCell);
        Worker(bed, 6, 6, "Carpenter");
        bed.AddItems("log", 2, 70, 70, 1);
        var order = Order(bed, "plank", 2);

        WorkOrderRules.Status(bed.Simulation, order).ShouldBe(WorkOrderStatus.WaitingInputs);

        bed.AddItems("log", 2, 12, 12, 1);
        WorkOrderRules.Status(bed.Simulation, order).ShouldBe(WorkOrderStatus.Running);
    }

    [Fact]
    public void An_order_for_one_workshop_goes_away_with_it()
    {
        var bed = Village();
        var shop = Build(bed, "carpenter_bench", BenchCell);
        bed.Simulation.Commands.Enqueue(new CreateWorkOrderCommand("plank", WorkOrderMode.Count, 2, shop));
        bed.Simulation.Commands.Enqueue(new CreateWorkOrderCommand("plank", WorkOrderMode.Count, 3));
        bed.Simulation.ApplyCommandsNow();
        bed.World.Store<WorkOrder>().Count.ShouldBe(2);
        Worker(bed, 6, 6, "Carpenter");
        bed.Designate(DesignationKind.Deconstruct, BenchCell, BenchCell);

        bed.Simulation.Run(400);

        bed.World.Store<Workshop>().Count.ShouldBe(0);
        bed.World.Store<WorkOrder>().Count.ShouldBe(1);
    }

    [Fact]
    public void A_craft_is_work_of_the_workshop_for_the_rules_of_the_colony()
    {
        var rules = ColonyRules.From(Content);

        rules.WorkTypeOf(new Job { Kind = JobKind.Craft, Recipe = "plank" })!.Id.ShouldBe("carpentry");
        rules.WorkTypeOf(new Job { Kind = JobKind.Craft }).ShouldBeNull();
    }

    [Fact]
    public void Taking_down_a_workshop_leaves_its_ingredients_on_the_ground()
    {
        var bed = Village();
        var shop = Build(bed, "carpenter_bench", BenchCell);
        Crafting.Deliver(bed.Simulation, shop, "log", 3, null, null);
        Worker(bed, 6, 6, "Carpenter");
        bed.Designate(DesignationKind.Deconstruct, BenchCell, BenchCell);

        bed.Simulation.Run(400);

        bed.World.Store<Workshop>().Count.ShouldBe(0);

        // The three delivered, and the one that taking it down gives back (half of its cost of two logs).
        bed.CountItems("log").ShouldBe(4);
    }

    [Fact]
    public void Ingredients_nobody_uses_for_a_day_fall_to_the_ground()
    {
        var bed = Village();
        var shop = Build(bed, "carpenter_bench", BenchCell);
        Crafting.Deliver(bed.Simulation, shop, "log", 2, null, null);

        bed.Simulation.Run(bed.Simulation.Time.TicksPerDay - 60);
        bed.World.Get<Workshop>(shop).Inputs.Count.ShouldBe(1);
        bed.Simulation.Run(2 * 60);

        bed.World.Get<Workshop>(shop).Inputs.ShouldBeEmpty();
        bed.CountItems("log").ShouldBe(2);
    }

    [Fact]
    public void Furniture_gets_the_quality_the_builder_rolls_with_the_skill_of_construction()
    {
        var bed = ColonyTestBed.Flat(16, 3, Content);
        var builder = bed.AddColonist(0, 0, 1);
        bed.World.Add(builder, new Skills { Values = [new SkillValue { WorkType = "construction", Level = 20 }] });
        var qualities = new List<string?>();
        for (var i = 0; i < 40; i++)
        {
            var cell = new TileCoord(i % 16, 4 + (i / 16), 1);
            bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand("table", "timber", cell, cell));
            bed.Simulation.ApplyCommandsNow();
            var blueprints = new List<EntityId>();
            bed.World.Query<Blueprint, Position>(blueprints);
            Construction.Finish(bed.Simulation, blueprints.Single(entity => bed.World.Get<Position>(entity).Cell == cell), cell, builder);
            var pieces = new List<EntityId>();
            bed.World.Query<Furniture, Position>(pieces);
            qualities.Add(bed.World.Get<Furniture>(pieces.Single(entity => bed.World.Get<Position>(entity).Cell == cell)).Quality);
        }

        // At level 20 the score is 60 to 159: nothing poor, 40 % normal (not written), and some good or better.
        qualities.ShouldNotContain("poor");
        qualities.ShouldContain((string?)null);
        qualities.Count(quality => quality is "good" or "excellent" or "masterwork").ShouldBeGreaterThan(10);

        // A workshop has no quality.
        bed.Simulation.Progress.Learn("carpentry");
        var shop = Build(bed, "carpenter_bench", BenchCell);
        bed.World.Get<Furniture>(shop).Quality.ShouldBeNull();
    }

    [Fact]
    public void A_bed_recovers_rest_by_its_comfort_and_a_bed_of_timber_without_quality_is_as_before()
    {
        var bed = ColonyTestBed.Flat(16, 1, Content);
        var (plain, fine) = (new TileCoord(3, 3, 1), new TileCoord(12, 12, 1));
        foreach (var cell in new[] { plain, fine })
        {
            bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand("bed", "timber", cell, cell));
            bed.Simulation.ApplyCommandsNow();
        }

        var blueprints = new List<EntityId>();
        bed.World.Query<Blueprint, Position>(blueprints);
        blueprints.ForEach(blueprint => Construction.Finish(bed.Simulation, blueprint, bed.World.Get<Position>(blueprint).Cell));
        var beds = new List<EntityId>();
        bed.World.Query<Bed, Position>(beds);
        var fineBed = beds.Single(entity => bed.World.Get<Position>(entity).Cell == fine);
        bed.World.Get<Furniture>(fineBed).Quality = "masterwork";
        bed.World.Get<Furniture>(beds.Single(entity => bed.World.Get<Position>(entity).Cell == plain)).Quality.ShouldBeNull();

        bed.Simulation.Run(900);
        var onPlain = bed.AddColonist(4, 3, 1, rest: 2_000, name: "Plain");
        var onFine = bed.AddColonist(11, 12, 1, rest: 2_000, name: "Fine");
        bed.Simulation.Run(90);

        bed.World.Get<Position>(onPlain).Cell.ShouldBe(plain);
        bed.World.Get<Position>(onFine).Cell.ShouldBe(fine);

        // 150 % of recovery in the plain bed; 150 % times 130 % (masterwork) = 195 % in the other.
        var plainGain = bed.Need(onPlain, "rest").Value - 2_000;
        var fineGain = bed.Need(onFine, "rest").Value - 2_000;
        plainGain.ShouldBeGreaterThan(0);
        fineGain.ShouldBeGreaterThan(plainGain);
    }

    [Fact]
    public void A_game_saved_halfway_through_a_craft_goes_on_as_if_never_stopped()
    {
        var uninterrupted = Village(5);
        var shop = Build(uninterrupted, "carpenter_bench", BenchCell);
        uninterrupted.AddItems("log", 3, 2, 2, 1);
        Worker(uninterrupted, 6, 6, "Carpenter");
        Order(uninterrupted, "plank", 6);
        Order(uninterrupted, "handle", 2);
        for (var tick = 0; tick < 600 && !(uninterrupted.World.Get<Workshop>(shop).Inputs.Count > 0 && uninterrupted.JobOf(uninterrupted.Colonists()[0])?.Kind == JobKind.Craft); tick++)
        {
            uninterrupted.Simulation.Tick();
        }

        uninterrupted.World.Get<Workshop>(shop).Inputs.ShouldNotBeEmpty();
        var output = new MemoryStream();
        SaveGame.Write(output, uninterrupted.Simulation, new SaveInfo("craft", IsAutosave: false, "test", DateTimeOffset.UnixEpoch));
        var loaded = SaveGame.Load(new MemoryStream(output.ToArray()), Content);
        ColonySystems.AddTo(loaded);

        loaded.ComputeStateHash().ShouldBe(uninterrupted.Simulation.ComputeStateHash());
        loaded.World.Get<Workshop>(shop).Inputs.Select(stack => (stack.Def, stack.Count))
            .ShouldBe(uninterrupted.World.Get<Workshop>(shop).Inputs.Select(stack => (stack.Def, stack.Count)));
        uninterrupted.Simulation.Run(1_500);
        loaded.Run(1_500);
        loaded.ComputeStateHash().ShouldBe(uninterrupted.Simulation.ComputeStateHash());
        uninterrupted.CountItems("handle").ShouldBe(2);
    }

    [Fact]
    public void The_same_seed_and_orders_give_the_same_state()
    {
        ulong Run()
        {
            var bed = Village(9);
            Build(bed, "carpenter_bench", BenchCell);
            bed.AddItems("log", 4, 2, 2, 1);
            Worker(bed, 6, 6, "One");
            Worker(bed, 7, 7, "Two");
            Order(bed, "plank", 8);
            Order(bed, "handle", 4);
            bed.Simulation.Run(2_000);
            return bed.Simulation.ComputeStateHash();
        }

        Run().ShouldBe(Run());
    }
}
