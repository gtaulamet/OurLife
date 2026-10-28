using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Colony;

/// <summary>
/// The chain of O8 (<c>docs/gdd/oficios.md</c>): logs to charcoal in the kiln, ore and charcoal to an
/// ingot in the bloomery, ingot and handle to an axe in the forge, and the axe in the hands of a
/// woodcutter. The test content has the numbers of the recipes of the game content.
/// </summary>
public class MetalsTests
{
    private const string Extras = """
        { "type": "WorkType", "id": "smelting", "order": 5, "jobs": [], "workshop": true, "skill": true },
        { "type": "WorkType", "id": "smithing", "order": 6, "jobs": [], "workshop": true, "skill": true },
        { "type": "Knowledge", "id": "charcoal_burning" },
        { "type": "Knowledge", "id": "metallurgy", "requires": ["charcoal_burning"] },
        { "type": "Knowledge", "id": "ironwork", "requires": ["metallurgy"] },
        { "type": "Item", "id": "handle", "category": "misc", "kind": "raw", "visual": "item.handle", "stackLimit": 50 },
        { "type": "Item", "id": "iron_ore", "category": "misc", "kind": "raw", "visual": "item.iron_ore", "stackLimit": 25 },
        { "type": "Item", "id": "charcoal", "category": "misc", "kind": "raw", "visual": "item.charcoal", "stackLimit": 50 },
        { "type": "Item", "id": "iron_ingot", "category": "misc", "kind": "raw", "visual": "item.iron_ingot", "stackLimit": 25 },
        { "type": "Item", "id": "iron_axe", "category": "misc", "kind": "raw", "visual": "item.iron_axe", "stackLimit": 1,
          "tool": { "workType": "woodcutting", "speedPercent": 30 } },
        { "type": "Buildable", "id": "charcoal_kiln", "requires": ["charcoal_burning"], "shape": "furniture", "materials": ["timber"], "cost": 2,
          "workTicks": 20, "visual": "furniture.charcoal_kiln", "beauty": -2, "workstation": { "workType": "smelting" } },
        { "type": "Buildable", "id": "bloomery", "requires": ["metallurgy"], "shape": "furniture", "materials": ["timber"], "cost": 2,
          "workTicks": 20, "visual": "furniture.bloomery", "beauty": -4, "workstation": { "workType": "smelting" } },
        { "type": "Buildable", "id": "forge", "requires": ["ironwork"], "shape": "furniture", "materials": ["timber"], "cost": 2,
          "workTicks": 20, "visual": "furniture.forge", "beauty": -4, "workstation": { "workType": "smithing" } },
        { "type": "Recipe", "id": "charcoal", "requires": ["charcoal_burning"], "workshop": "charcoal_kiln",
          "inputs": [{ "item": "log", "count": 4 }], "outputs": [{ "item": "charcoal", "count": 2 }], "workTicks": 240 },
        { "type": "Recipe", "id": "iron_ingot", "requires": ["metallurgy"], "workshop": "bloomery",
          "inputs": [{ "item": "iron_ore", "count": 2 }, { "item": "charcoal", "count": 2 }], "outputs": [{ "item": "iron_ingot", "count": 1 }], "workTicks": 300 },
        { "type": "Recipe", "id": "iron_axe", "requires": ["ironwork"], "workshop": "forge", "quality": true,
          "inputs": [{ "item": "iron_ingot", "count": 1 }, { "item": "handle", "count": 1 }], "outputs": [{ "item": "iron_axe", "count": 1 }], "workTicks": 360 }
        """;

    private static readonly Lazy<DefDatabase> ContentLazy = new(() =>
        TestContent.WithDefs(string.Concat(ColonyTestBed.ContentJson.AsSpan(0, ColonyTestBed.ContentJson.LastIndexOf(']')), ",", Extras, "]")));

    private static DefDatabase Content => ContentLazy.Value;

    private static ColonyTestBed Village(params string[] knowledge)
    {
        var bed = ColonyTestBed.Flat(16, seed: 3, Content);
        foreach (var known in knowledge)
        {
            bed.Simulation.Progress.Learn(known);
        }

        bed.Designate(DesignationKind.Stockpile, new TileCoord(1, 1, 1), new TileCoord(3, 3, 1));
        bed.Simulation.ApplyCommandsNow();
        return bed;
    }

    private static void Build(ColonyTestBed bed, string buildable, TileCoord cell)
    {
        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand(buildable, "timber", cell, cell));
        bed.Simulation.ApplyCommandsNow();
        var blueprints = new List<EntityId>();
        bed.World.Query<Blueprint, Position>(blueprints);
        Construction.Finish(bed.Simulation, blueprints.Single(entity => bed.World.Get<Position>(entity).Cell == cell), cell);
    }

    private static EntityId Order(ColonyTestBed bed, string recipe, int quantity)
    {
        bed.Simulation.Commands.Enqueue(new CreateWorkOrderCommand(recipe, WorkOrderMode.Count, quantity));
        bed.Simulation.ApplyCommandsNow();
        var orders = new List<EntityId>();
        bed.World.Query<WorkOrder>(orders);
        return orders[^1];
    }

    private static EntityId Smith(ColonyTestBed bed, int level = 0)
    {
        var smith = bed.AddColonist(6, 6, 1, name: "Smith");
        bed.World.Add(smith, new Skills
        {
            Values =
            [
                new SkillValue { WorkType = "smelting", Level = level },
                new SkillValue { WorkType = "smithing", Level = level },
                new SkillValue { WorkType = "woodcutting", Level = 0 },
            ],
        });
        return smith;
    }

    /// <summary>Every unit of an item anywhere: on the ground, in hands, delivered to workshops and equipped.</summary>
    private static int Everywhere(ColonyTestBed bed, string item)
    {
        var holders = new List<EntityId>();
        bed.World.Query<Workshop>(holders);
        var inShops = holders.Sum(shop => bed.World.Get<Workshop>(shop).Inputs.Where(stack => stack.Def == item).Sum(stack => stack.Count));
        bed.World.Query<Equipment>(holders);
        var equipped = holders.Sum(holder => bed.World.Get<Equipment>(holder).Tools.Where(stack => stack.Def == item).Sum(stack => stack.Count));
        return Stocks.Count(bed.Simulation, item) + inShops + equipped;
    }

    [Fact]
    public void Logs_and_ore_become_charcoal_an_ingot_and_an_axe_that_a_woodcutter_then_equips()
    {
        var bed = Village("charcoal_burning", "metallurgy", "ironwork");
        Build(bed, "charcoal_kiln", new TileCoord(8, 8, 1));
        Build(bed, "bloomery", new TileCoord(10, 8, 1));
        Build(bed, "forge", new TileCoord(12, 8, 1));
        bed.AddItems("log", 4, 2, 2, 1);
        bed.AddItems("iron_ore", 2, 3, 2, 1);
        bed.AddItems("handle", 1, 2, 3, 1);
        var smith = Smith(bed);
        var charcoal = Order(bed, "charcoal", 2);
        var ingot = Order(bed, "iron_ingot", 1);
        var axe = Order(bed, "iron_axe", 1);

        bed.Simulation.Run(4_000);

        foreach (var order in new[] { charcoal, ingot, axe })
        {
            WorkOrderRules.Status(bed.Simulation, order).ShouldBe(WorkOrderStatus.Done);
        }

        // Everything was used up: the log, the ore, the charcoal, the ingot and the handle are gone and one axe is left.
        foreach (var gone in new[] { "log", "iron_ore", "charcoal", "iron_ingot", "handle" })
        {
            Everywhere(bed, gone).ShouldBe(0, gone);
        }

        Everywhere(bed, "iron_axe").ShouldBe(1);
        var skills = bed.World.Get<Skills>(smith);
        (skills.Find("smelting")!.Level * 100 + skills.Find("smelting")!.Experience).ShouldBeGreaterThan(0);
        (skills.Find("smithing")!.Level * 100 + skills.Find("smithing")!.Experience).ShouldBeGreaterThan(0);

        // The axe is in a stockpile, so the next woodcutting fetches it and keeps it.
        Tools.Of(bed.Simulation, smith).ShouldBeEmpty();
        bed.AddPlant("tree", 9, 12, 1);
        bed.Designate(DesignationKind.Chop, new TileCoord(9, 12, 1), new TileCoord(9, 12, 1));
        bed.Simulation.Run(600);

        Tools.Of(bed.Simulation, smith).Select(tool => tool.Def).ShouldBe(["iron_axe"]);
        bed.CountItems("iron_axe").ShouldBe(0);
        bed.CountItems("log").ShouldBe(4);
    }

    [Fact]
    public void Each_step_of_the_chain_waits_for_its_knowledge_and_the_forge_cannot_even_be_planned_without_it()
    {
        var bed = Village("charcoal_burning");
        Build(bed, "charcoal_kiln", new TileCoord(8, 8, 1));
        var ingot = Order(bed, "iron_ingot", 1);
        var axe = Order(bed, "iron_axe", 1);

        WorkOrderRules.Status(bed.Simulation, ingot).ShouldBe(WorkOrderStatus.Locked);
        WorkOrderRules.Status(bed.Simulation, axe).ShouldBe(WorkOrderStatus.Locked);
        bed.Simulation.Unlocks.Missing(Content.Get<RecipeDef>("iron_axe"), bed.Simulation.Progress).ShouldBe(["ironwork"]);

        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand("forge", "timber", new TileCoord(12, 8, 1), new TileCoord(12, 8, 1)));
        bed.Simulation.ApplyCommandsNow();
        var blueprints = new List<EntityId>();
        bed.World.Query<Blueprint>(blueprints);
        blueprints.ShouldBeEmpty();
    }

    [Fact]
    public void The_same_seed_gives_the_same_ingot_and_axe_and_the_same_state()
    {
        string Play()
        {
            var bed = Village("charcoal_burning", "metallurgy", "ironwork");
            Build(bed, "bloomery", new TileCoord(10, 8, 1));
            Build(bed, "forge", new TileCoord(12, 8, 1));
            bed.AddItems("iron_ore", 2, 3, 2, 1);
            bed.AddItems("charcoal", 2, 3, 3, 1);
            bed.AddItems("handle", 1, 2, 3, 1);
            Smith(bed, level: 12);
            Order(bed, "iron_ingot", 1);
            Order(bed, "iron_axe", 1);
            bed.Simulation.Run(3_000);
            var stacks = new List<EntityId>();
            bed.World.Query<ItemStack>(stacks);
            var axe = bed.World.Get<ItemStack>(stacks.Single(pile => bed.World.Get<ItemStack>(pile).Def == "iron_axe"));
            return $"{axe.Quality ?? "normal"}:{bed.Simulation.ComputeStateHash()}";
        }

        Play().ShouldBe(Play());
    }
}
