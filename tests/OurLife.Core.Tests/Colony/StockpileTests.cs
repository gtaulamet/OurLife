using OurLife.Core.Colony;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;

namespace OurLife.Core.Tests.Colony;

/// <summary>Stockpiles and hauling (<c>docs/gdd/almacenes.md</c>). Tick 0 is 06:00; the night starts at tick 900.</summary>
public class StockpileTests
{
    private const int Recheck = 30;

    [Fact]
    public void Loose_logs_end_up_piled_in_a_wood_stockpile()
    {
        var bed = ColonyTestBed.Flat(16);
        bed.AddItems("log", 60, 2, 2, 1);
        bed.AddItems("log", 7, 3, 12, 1);
        var zone = MarkStockpile(bed, new TileCoord(10, 10, 1), new TileCoord(12, 12, 1));
        bed.Simulation.Commands.Enqueue(new SetStockpileFilterCommand(zone, "food", Allowed: false));
        bed.AddColonist(6, 6, 1);

        bed.Simulation.Run(800);

        var piles = Piles(bed, "log");
        piles.Sum(pile => pile.Count).ShouldBe(67);
        piles.ShouldAllBe(pile => InBox(pile.Cell, new TileCoord(10, 10, 1), new TileCoord(12, 12, 1)));
        piles.GroupBy(pile => pile.Cell).ShouldAllBe(cell => cell.Count() == 1);
        piles.ShouldAllBe(pile => pile.Count <= 25);
        bed.World.Store<Carried>().Count.ShouldBe(0);
    }

    [Fact]
    public void A_stockpile_that_forbids_food_gets_no_berries_until_food_is_allowed()
    {
        var bed = ColonyTestBed.Flat(16);
        bed.AddItems("berries", 20, 2, 2, 1);
        var zone = MarkStockpile(bed, new TileCoord(10, 10, 1), new TileCoord(11, 11, 1));
        bed.Simulation.Commands.Enqueue(new SetStockpileFilterCommand(zone, "food", Allowed: false));
        bed.AddColonist(6, 6, 1);

        bed.Simulation.Run(300);
        Piles(bed, "berries").ShouldAllBe(pile => pile.Cell == new TileCoord(2, 2, 1));

        bed.Simulation.Commands.Enqueue(new SetStockpileFilterCommand(zone, "food", Allowed: true));
        bed.Simulation.Run(300);
        Piles(bed, "berries").ShouldAllBe(pile => InBox(pile.Cell, new TileCoord(10, 10, 1), new TileCoord(11, 11, 1)));
    }

    [Fact]
    public void A_hauler_who_gets_hungry_drops_the_load_and_someone_stores_it_later()
    {
        var bed = ColonyTestBed.Flat(20);
        bed.AddItems("log", 20, 1, 1, 1);
        bed.AddItems("provisions", 5, 18, 1, 1);
        var zone = MarkStockpile(bed, new TileCoord(18, 18, 1), new TileCoord(18, 18, 1));

        // The provisions stay where they are, so the only cell waits for the logs.
        bed.Simulation.Commands.Enqueue(new SetStockpileFilterCommand(zone, "food", Allowed: false));
        var colonist = bed.AddColonist(2, 2, 1);

        // Until the colonist carries the logs, a few steps away from the pile.
        for (var tick = 0; tick < 300 && !(bed.World.Has<Carried>(colonist) && bed.World.Get<Position>(colonist).Cell != new TileCoord(1, 1, 1)); tick++)
        {
            bed.Simulation.Tick();
        }

        bed.World.Has<Carried>(colonist).ShouldBeTrue();
        bed.Need(colonist, "hunger").Value = 1000;
        bed.Simulation.Run(Recheck + 1);

        bed.World.Has<Carried>(colonist).ShouldBeFalse();
        bed.JobOf(colonist)!.Kind.ShouldBe(JobKind.Eat);
        Piles(bed, "log").ShouldContain(pile => pile.Cell != new TileCoord(18, 18, 1));

        bed.Simulation.Run(600);
        Piles(bed, "log").ShouldAllBe(pile => pile.Cell == new TileCoord(18, 18, 1));
        Piles(bed, "log").Sum(pile => pile.Count).ShouldBe(20);
    }

    [Fact]
    public void Two_haulers_never_share_a_pile_or_a_destination()
    {
        var bed = ColonyTestBed.Flat(16);
        bed.AddItems("log", 10, 2, 2, 1);
        bed.AddItems("stone", 10, 2, 13, 1);
        bed.AddItems("log", 10, 13, 2, 1);
        MarkStockpile(bed, new TileCoord(8, 8, 1), new TileCoord(9, 8, 1));
        var colonists = new[] { bed.AddColonist(7, 7, 1), bed.AddColonist(9, 9, 1) };

        for (var tick = 0; tick < 600; tick++)
        {
            bed.Simulation.Tick();
            var hauls = colonists.Select(bed.JobOf).OfType<Job>().Where(job => job.Kind == JobKind.Haul).ToList();
            if (hauls.Count == 2)
            {
                hauls[0].Destination.ShouldNotBe(hauls[1].Destination);
                if (hauls.All(job => job.Stage != JobStage.Carrying))
                {
                    hauls[0].Target.ShouldNotBe(hauls[1].Target);
                }
            }
        }

        // Two cells, one pile each: two of the three piles found room.
        Piles(bed, "log").Concat(Piles(bed, "stone")).Count(pile => pile.Cell.Y == 8 && pile.Cell.X is 8 or 9).ShouldBe(2);
    }

    [Fact]
    public void Nobody_goes_to_eat_from_a_pile_a_hauler_is_about_to_lift()
    {
        var bed = ColonyTestBed.Flat(16);
        bed.AddItems("berries", 10, 2, 2, 1);
        MarkStockpile(bed, new TileCoord(13, 13, 1), new TileCoord(13, 13, 1));
        var hauler = bed.AddColonist(9, 2, 1, name: "Hauler");
        var eater = bed.AddColonist(2, 5, 1, hunger: 1000, name: "Eater");
        bed.Simulation.Tick();
        bed.JobOf(hauler)!.Kind.ShouldBe(JobKind.Haul);
        var pile = bed.JobOf(hauler)!.Target;

        for (var tick = 0; tick < 300 && bed.World.IsAlive(pile); tick++)
        {
            bed.Simulation.Tick();
            if (bed.JobOf(eater) is { Kind: JobKind.Eat } meal)
            {
                meal.Target.ShouldNotBe(pile);
            }
        }

        bed.World.IsAlive(pile).ShouldBeFalse();
    }

    [Fact]
    public void Piles_with_nowhere_to_go_do_not_keep_a_hauler_from_a_farther_one_that_has()
    {
        // The only cell for food is walled in: berries have room somewhere, but not within reach.
        var floor = ColonyTestBed.Level(16, 16, '.').ToCharArray();
        foreach (var (x, y) in new[] { (13, 13), (14, 13), (15, 13), (13, 14), (15, 14), (13, 15), (14, 15), (15, 15) })
        {
            floor[(y * 17) + x] = '#';
        }

        var bed = new ColonyTestBed(ColonyTestBed.Level(16, 16, '#'), new string(floor));
        var pantry = MarkStockpile(bed, new TileCoord(14, 14, 1), new TileCoord(14, 14, 1));
        var shed = MarkStockpile(bed, new TileCoord(8, 8, 1), new TileCoord(8, 8, 1));
        foreach (var category in new[] { "misc", "stone", "wood" })
        {
            bed.Simulation.Commands.Enqueue(new SetStockpileFilterCommand(pantry, category, Allowed: false));
        }

        bed.Simulation.Commands.Enqueue(new SetStockpileFilterCommand(shed, "food", Allowed: false));
        bed.Simulation.ApplyCommandsNow();
        bed.AddItems("berries", 3, 2, 1, 1);
        bed.AddItems("berries", 3, 1, 2, 1);
        bed.AddItems("berries", 3, 2, 2, 1);
        bed.AddItems("log", 5, 6, 6, 1);
        var colonist = bed.AddColonist(1, 1, 1);

        bed.Simulation.Tick();

        var job = bed.JobOf(colonist);
        job.ShouldNotBeNull();
        job.Kind.ShouldBe(JobKind.Haul);
        job.Cell.ShouldBe(new TileCoord(6, 6, 1));
        job.Destination.ShouldBe(new TileCoord(8, 8, 1));
    }

    [Fact]
    public void A_second_pile_on_a_stockpile_cell_is_loose_and_moves_to_a_cell_of_its_own()
    {
        // Like a load that fell on a stockpile cell when its carrier got hungry.
        var bed = ColonyTestBed.Flat(16);
        MarkStockpile(bed, new TileCoord(8, 8, 1), new TileCoord(9, 8, 1));
        bed.AddItems("log", 10, 8, 8, 1);
        bed.AddItems("stone", 5, 8, 8, 1);
        bed.AddColonist(4, 4, 1);

        bed.Simulation.Run(300);

        Piles(bed, "log").ShouldBe([(new TileCoord(8, 8, 1), 10)]);
        Piles(bed, "stone").ShouldBe([(new TileCoord(9, 8, 1), 5)]);
    }

    [Fact]
    public void Cancel_takes_cells_out_and_a_zone_without_cells_is_gone()
    {
        var bed = ColonyTestBed.Flat(16);
        var zone = MarkStockpile(bed, new TileCoord(4, 4, 1), new TileCoord(6, 4, 1));
        bed.AddItems("log", 5, 4, 4, 1);

        bed.Designate(DesignationKind.Cancel, new TileCoord(4, 4, 1), new TileCoord(4, 4, 1));
        bed.Simulation.Tick();
        bed.World.Get<Stockpile>(zone).Cells.ShouldBe([new TileCoord(5, 4, 1), new TileCoord(6, 4, 1)]);
        Stockpiles.ZoneAt(bed.Simulation, new TileCoord(4, 4, 1)).ShouldBe(EntityId.None);

        bed.Designate(DesignationKind.Cancel, new TileCoord(0, 0, 1), new TileCoord(15, 15, 1));
        bed.Simulation.Tick();
        bed.World.IsAlive(zone).ShouldBeFalse();
    }

    [Fact]
    public void A_new_stockpile_skips_cells_of_other_zones_and_cells_nobody_can_stand_on()
    {
        var floor = ColonyTestBed.Level(8, 8, '.').ToCharArray();
        floor[(2 * 9) + 2] = '#';
        var bed = new ColonyTestBed(ColonyTestBed.Level(8, 8, '#'), new string(floor));
        var first = MarkStockpile(bed, new TileCoord(0, 0, 1), new TileCoord(1, 1, 1));

        var second = MarkStockpile(bed, new TileCoord(1, 1, 1), new TileCoord(2, 2, 1));

        bed.World.Get<Stockpile>(first).Cells.Count.ShouldBe(4);
        bed.World.Get<Stockpile>(second).Cells.ShouldBe([new TileCoord(2, 1, 1), new TileCoord(1, 2, 1)]);
        bed.World.Get<Stockpile>(second).Categories.ShouldBe(["food", "misc", "stone", "wood"]);
    }

    /// <summary>Marks a stockpile at once and returns its zone, the newest one.</summary>
    private static EntityId MarkStockpile(ColonyTestBed bed, TileCoord from, TileCoord to)
    {
        bed.Designate(DesignationKind.Stockpile, from, to);
        bed.Simulation.ApplyCommandsNow();
        var zones = new List<EntityId>();
        bed.World.Query<Stockpile>(zones);
        return zones.MaxBy(zone => zone.Index * 1000 + zone.Generation);
    }

    private static List<(TileCoord Cell, int Count)> Piles(ColonyTestBed bed, string item)
    {
        var piles = new List<EntityId>();
        bed.World.Query<ItemStack, Position>(piles);
        return [.. piles.Where(pile => bed.World.Get<ItemStack>(pile).Def == item)
            .Select(pile => (bed.World.Get<Position>(pile).Cell, bed.World.Get<ItemStack>(pile).Count))];
    }

    private static bool InBox(TileCoord cell, TileCoord min, TileCoord max) =>
        cell.X >= min.X && cell.X <= max.X && cell.Y >= min.Y && cell.Y <= max.Y && cell.Z >= min.Z && cell.Z <= max.Z;
}
