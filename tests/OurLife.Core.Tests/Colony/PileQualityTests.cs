using OurLife.Core.Colony;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;

namespace OurLife.Core.Tests.Colony;

/// <summary>
/// Piles with quality and freshness (<c>docs/gdd/oficios.md</c>, "Pilas con calidad y frescura"): a
/// pile is an item plus a quality, so piles of different quality never mix, and a pile that gets
/// more has the average freshness.
/// </summary>
public class PileQualityTests
{
    private static readonly TileCoord Here = new(3, 3, 1);

    [Fact]
    public void Piles_of_the_same_quality_add_up_and_piles_of_another_quality_stay_apart()
    {
        var bed = ColonyTestBed.Flat(10);

        ItemPiles.Drop(bed.Simulation, "plank", 10, Here);
        ItemPiles.Drop(bed.Simulation, "plank", 5, Here);
        ItemPiles.Drop(bed.Simulation, "plank", 4, Here, "good");
        ItemPiles.Drop(bed.Simulation, "plank", 3, Here, "good");
        ItemPiles.Drop(bed.Simulation, "plank", 2, Here, "poor");

        PilesAt(bed, "plank", Here).Select(stack => (stack.Quality, stack.Count)).ShouldBe([(null, 15), ("good", 7), ("poor", 2)]);
    }

    [Fact]
    public void The_default_quality_is_stored_as_none_even_when_it_is_named()
    {
        var bed = ColonyTestBed.Flat(10);

        ItemPiles.Drop(bed.Simulation, "plank", 4, Here, "normal");
        ItemPiles.Drop(bed.Simulation, "plank", 4, Here);

        var piles = PilesAt(bed, "plank", Here);
        piles.Count.ShouldBe(1);
        piles[0].Quality.ShouldBeNull();
        piles[0].Count.ShouldBe(8);
    }

    [Fact]
    public void A_pile_over_the_stack_limit_splits_and_every_part_keeps_its_quality()
    {
        var bed = ColonyTestBed.Flat(10);

        ItemPiles.Drop(bed.Simulation, "plank", 60, Here, "excellent");

        PilesAt(bed, "plank", Here).Select(stack => (stack.Quality, stack.Count)).ShouldBe([("excellent", 25), ("excellent", 25), ("excellent", 10)]);
    }

    [Fact]
    public void Joined_piles_have_the_average_freshness_by_units_rounded_down()
    {
        var bed = ColonyTestBed.Flat(10);

        ItemPiles.Drop(bed.Simulation, "berries", 10, Here);
        ItemPiles.Drop(bed.Simulation, "berries", 30, Here, freshness: 4_000);

        var pile = PilesAt(bed, "berries", Here).Single();
        pile.Count.ShouldBe(40);
        pile.Freshness.ShouldBe(5_500);
        pile.FreshnessValue.ShouldBe(5_500);

        // (1 x 10000 + 2 x 9999) / 3 = 9999.33: rounded down.
        ItemPiles.Drop(bed.Simulation, "berries", 1, new TileCoord(4, 4, 1));
        ItemPiles.Drop(bed.Simulation, "berries", 2, new TileCoord(4, 4, 1), freshness: 9_999);
        PilesAt(bed, "berries", new TileCoord(4, 4, 1)).Single().Freshness.ShouldBe(9_999);
    }

    [Fact]
    public void A_fresh_pile_has_no_freshness_written_and_one_above_the_maximum_counts_as_the_maximum()
    {
        var bed = ColonyTestBed.Flat(10);

        ItemPiles.Drop(bed.Simulation, "berries", 5, Here, freshness: ItemStack.FullFreshness);
        PilesAt(bed, "berries", Here).Single().Freshness.ShouldBeNull();

        ItemPiles.Drop(bed.Simulation, "berries", 5, Here, freshness: 9_000);
        PilesAt(bed, "berries", Here).Single().Freshness.ShouldBe(9_500);

        // 10 at 9500 and 5 more at the maximum: 145000 / 15 = 9666.67, rounded down.
        ItemPiles.Drop(bed.Simulation, "berries", 5, Here, freshness: 20_000);
        var pile = PilesAt(bed, "berries", Here).Single();
        pile.Count.ShouldBe(15);
        pile.Freshness.ShouldBe(9_666);
    }

    [Fact]
    public void An_item_that_does_not_rot_ignores_freshness_and_always_mixes()
    {
        var bed = ColonyTestBed.Flat(10);

        ItemPiles.Drop(bed.Simulation, "plank", 5, Here, freshness: 3_000);
        ItemPiles.Drop(bed.Simulation, "plank", 5, Here);

        var pile = PilesAt(bed, "plank", Here).Single();
        pile.Count.ShouldBe(10);
        pile.Freshness.ShouldBeNull();
    }

    [Fact]
    public void Taking_units_keeps_the_quality_and_freshness_of_what_is_left()
    {
        var bed = ColonyTestBed.Flat(10);
        var pile = ItemPiles.Drop(bed.Simulation, "berries", 10, Here, "good", 6_000);

        ItemPiles.Take(bed.Simulation, pile, 4);

        var stack = bed.World.Get<ItemStack>(pile);
        (stack.Count, stack.Quality, stack.Freshness).ShouldBe((6, "good", 6_000));
    }

    [Fact]
    public void A_stockpile_keeps_each_quality_of_an_item_on_a_cell_of_its_own()
    {
        var bed = ColonyTestBed.Flat(16);
        ItemPiles.Drop(bed.Simulation, "plank", 10, new TileCoord(2, 2, 1));
        ItemPiles.Drop(bed.Simulation, "plank", 8, new TileCoord(2, 4, 1), "good");
        ItemPiles.Drop(bed.Simulation, "plank", 6, new TileCoord(2, 6, 1), "poor");
        MarkStockpile(bed, new TileCoord(9, 9, 1), new TileCoord(11, 9, 1));
        bed.AddColonist(7, 7, 1);

        bed.Simulation.Run(1_500);

        var piles = PilesIn(bed, "plank", new TileCoord(9, 9, 1), new TileCoord(11, 9, 1));
        piles.Select(pile => pile.Stack.Quality).Order().ShouldBe([null, "good", "poor"], ignoreOrder: true);
        piles.Select(pile => pile.Cell).Distinct().Count().ShouldBe(3);
        piles.Single(pile => pile.Stack.Quality == "good").Stack.Count.ShouldBe(8);
        piles.Single(pile => pile.Stack.Quality == "poor").Stack.Count.ShouldBe(6);
        piles.Single(pile => pile.Stack.Quality is null).Stack.Count.ShouldBe(10);
        bed.World.Store<Carried>().Count.ShouldBe(0);
    }

    [Fact]
    public void A_cell_with_planks_of_one_quality_has_no_room_for_planks_of_another()
    {
        var bed = ColonyTestBed.Flat(16);
        ItemPiles.Drop(bed.Simulation, "plank", 10, new TileCoord(10, 10, 1));
        ItemPiles.Drop(bed.Simulation, "plank", 8, new TileCoord(2, 2, 1), "good");
        MarkStockpile(bed, new TileCoord(10, 10, 1), new TileCoord(10, 10, 1));
        bed.AddColonist(7, 7, 1);

        bed.Simulation.Run(1_000);

        // The only cell keeps the normal planks: the good ones stay where they were.
        PilesAt(bed, "plank", new TileCoord(10, 10, 1)).Select(stack => (stack.Quality, stack.Count)).ShouldBe([(null, 10)]);
        PilesAt(bed, "plank", new TileCoord(2, 2, 1)).Select(stack => (stack.Quality, stack.Count)).ShouldBe([("good", 8)]);
        Stockpiles.StoredItems(bed.Simulation).ShouldBe(10);
    }

    [Fact]
    public void Piles_of_the_same_quality_join_in_the_stockpile_and_the_hauler_carries_quality_and_freshness()
    {
        var bed = ColonyTestBed.Flat(16);
        ItemPiles.Drop(bed.Simulation, "berries", 20, new TileCoord(2, 2, 1), "good", 4_000);
        MarkStockpile(bed, new TileCoord(11, 11, 1), new TileCoord(11, 11, 1));
        var hauler = bed.AddColonist(5, 2, 1);

        Carried? seen = null;
        for (var tick = 0; tick < 600; tick++)
        {
            bed.Simulation.Tick();
            if (bed.World.TryGet<Carried>(hauler, out var carried))
            {
                seen ??= new Carried { Def = carried.Def, Count = carried.Count, Quality = carried.Quality, Freshness = carried.Freshness };
            }
            else if (seen is not null)
            {
                break;
            }
        }

        seen.ShouldNotBeNull();
        (seen.Def, seen.Count, seen.Quality, seen.Freshness).ShouldBe(("berries", 20, "good", 4_000));
        var stored = PilesAt(bed, "berries", new TileCoord(11, 11, 1)).Single();
        (stored.Count, stored.Quality, stored.Freshness).ShouldBe((20, "good", 4_000));
    }

    [Fact]
    public void A_colonist_who_drops_a_load_leaves_it_with_its_quality()
    {
        var bed = ColonyTestBed.Flat(20);
        ItemPiles.Drop(bed.Simulation, "plank", 12, new TileCoord(1, 1, 1), "excellent");
        ItemPiles.Drop(bed.Simulation, "provisions", 5, new TileCoord(18, 1, 1));
        var zone = MarkStockpile(bed, new TileCoord(18, 18, 1), new TileCoord(18, 18, 1));

        // The provisions stay where they are: the only cell waits for the planks.
        bed.Simulation.Commands.Enqueue(new SetStockpileFilterCommand(zone, "food", Allowed: false));
        var colonist = bed.AddColonist(2, 2, 1);
        for (var tick = 0; tick < 300 && !(bed.World.Has<Carried>(colonist) && bed.World.Get<Position>(colonist).Cell != new TileCoord(1, 1, 1)); tick++)
        {
            bed.Simulation.Tick();
        }

        bed.World.Has<Carried>(colonist).ShouldBeTrue();
        bed.World.Get<Carried>(colonist).Quality.ShouldBe("excellent");
        bed.Need(colonist, "hunger").Value = 1_000;
        bed.Simulation.Run(31);

        bed.World.Has<Carried>(colonist).ShouldBeFalse();
        PilesIn(bed, "plank", new TileCoord(0, 0, 1), new TileCoord(19, 19, 1)).ShouldAllBe(pile => pile.Stack.Quality == "excellent");
        bed.CountItems("plank").ShouldBe(12);
    }

    [Fact]
    public void Stocks_count_by_item_and_minimum_quality_loose_stored_and_carried()
    {
        var bed = ColonyTestBed.Flat(16);
        ItemPiles.Drop(bed.Simulation, "plank", 10, new TileCoord(2, 2, 1));
        ItemPiles.Drop(bed.Simulation, "plank", 8, new TileCoord(3, 2, 1), "good");
        ItemPiles.Drop(bed.Simulation, "plank", 6, new TileCoord(4, 2, 1), "poor");
        ItemPiles.Drop(bed.Simulation, "plank", 4, new TileCoord(5, 2, 1), "masterwork");
        var carrier = bed.AddColonist(8, 8, 1);
        bed.World.Add(carrier, new Carried { Def = "plank", Count = 3, Quality = "good" });

        Stocks.Count(bed.Simulation, "plank").ShouldBe(31);
        Stocks.Count(bed.Simulation, "plank", "poor").ShouldBe(31);
        Stocks.Count(bed.Simulation, "plank", "normal").ShouldBe(25);
        Stocks.Count(bed.Simulation, "plank", "good").ShouldBe(15);
        Stocks.Count(bed.Simulation, "plank", "excellent").ShouldBe(4);
        Stocks.Count(bed.Simulation, "stone").ShouldBe(0);
        Should.Throw<ArgumentException>(() => Stocks.Count(bed.Simulation, "plank", "legendary"));
    }

    [Fact]
    public void The_totals_give_one_line_per_item_and_quality_with_the_average_freshness()
    {
        var bed = ColonyTestBed.Flat(16);
        ItemPiles.Drop(bed.Simulation, "plank", 8, new TileCoord(3, 2, 1), "good");
        ItemPiles.Drop(bed.Simulation, "plank", 10, new TileCoord(2, 2, 1));
        ItemPiles.Drop(bed.Simulation, "berries", 10, new TileCoord(6, 2, 1));
        ItemPiles.Drop(bed.Simulation, "berries", 10, new TileCoord(7, 2, 1), freshness: 5_000);
        var carrier = bed.AddColonist(8, 8, 1);
        bed.World.Add(carrier, new Carried { Def = "plank", Count = 2, Quality = "good" });

        var totals = Stocks.Totals(bed.Simulation);

        totals.Select(line => (line.Item.Id, line.Quality, line.Count, line.Freshness)).ShouldBe(
        [
            ("berries", null, 20, 7_500),
            ("plank", null, 10, 10_000),
            ("plank", "good", 10, 10_000),
        ]);
    }

    [Fact]
    public void The_stock_by_category_counts_every_quality()
    {
        var bed = ColonyTestBed.Flat(16);
        ItemPiles.Drop(bed.Simulation, "plank", 8, new TileCoord(3, 2, 1), "good");
        ItemPiles.Drop(bed.Simulation, "log", 5, new TileCoord(4, 2, 1));

        ColonyStock.ByCategory(bed.Simulation).Single(line => line.Category.Id == "wood").Count.ShouldBe(13);
    }

    private static EntityId MarkStockpile(ColonyTestBed bed, TileCoord from, TileCoord to)
    {
        bed.Designate(DesignationKind.Stockpile, from, to);
        bed.Simulation.ApplyCommandsNow();
        var zones = new List<EntityId>();
        bed.World.Query<Stockpile>(zones);
        return zones.MaxBy(zone => (zone.Index * 1000) + zone.Generation);
    }

    private static List<ItemStack> PilesAt(ColonyTestBed bed, string item, TileCoord cell) =>
        [.. PilesIn(bed, item, cell, cell).Select(pile => pile.Stack)];

    private static List<(TileCoord Cell, ItemStack Stack)> PilesIn(ColonyTestBed bed, string item, TileCoord min, TileCoord max)
    {
        var piles = new List<EntityId>();
        bed.World.Query<ItemStack, Position>(piles);
        return [.. piles
            .Select(pile => (Cell: bed.World.Get<Position>(pile).Cell, Stack: bed.World.Get<ItemStack>(pile)))
            .Where(pile => pile.Stack.Def == item
                           && pile.Cell.X >= min.X && pile.Cell.X <= max.X && pile.Cell.Y >= min.Y && pile.Cell.Y <= max.Y && pile.Cell.Z >= min.Z && pile.Cell.Z <= max.Z)];
    }
}
