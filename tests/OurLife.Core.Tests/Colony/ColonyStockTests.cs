using OurLife.Core.Colony;
using OurLife.Core.Entities;
using OurLife.Core.Map;

namespace OurLife.Core.Tests.Colony;

/// <summary>The stock of the colony that the HUD shows (<c>docs/gdd/indicadores.md</c>).</summary>
public class ColonyStockTests
{
    [Fact]
    public void The_stock_counts_every_pile_stored_or_not_and_what_is_carried_by_category()
    {
        var bed = ColonyTestBed.Flat(10);
        bed.AddItems("berries", 12, 1, 1, 1);
        bed.AddItems("provisions", 30, 2, 1, 1);
        bed.AddItems("log", 4, 5, 5, 1);
        bed.Designate(DesignationKind.Stockpile, new TileCoord(7, 7, 1), new TileCoord(8, 8, 1));
        bed.Simulation.Tick();
        bed.AddItems("log", 3, 7, 7, 1);
        var hauler = bed.AddColonist(3, 3, 1);
        bed.World.Add(hauler, new Carried { Def = "stone", Count = 2 });

        var stock = ColonyStock.ByCategory(bed.Simulation);

        // Without an order, the categories of the test content go by id.
        stock.Select(line => (line.Category.Id, line.Count)).ShouldBe(new[] { ("food", 42), ("misc", 0), ("stone", 2), ("wood", 7) });
    }

    [Fact]
    public void Categories_go_in_their_order()
    {
        var content = TestContent.Load(
            ("defs/base.json", $"[{TestContent.TimeConfigJson}, {TestContent.FirstEraJson}]"),
            ("defs/items.json", """
                [{ "type": "ItemCategory", "id": "food", "order": 0 }, { "type": "ItemCategory", "id": "wood", "order": 1 },
                 { "type": "ItemCategory", "id": "stone", "order": 2 }]
                """));

        ColonyStock.ByCategory(new Core.Sim.Simulation(1, content)).Select(line => line.Category.Id).ShouldBe(new[] { "food", "wood", "stone" });
    }
}
