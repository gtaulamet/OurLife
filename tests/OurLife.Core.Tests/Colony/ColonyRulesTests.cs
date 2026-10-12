using OurLife.Core.Colony;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;

namespace OurLife.Core.Tests.Colony;

/// <summary>Arrival, orders and needs, one rule at a time.</summary>
public class ColonyRulesTests
{
    [Fact]
    public void The_colonists_arrive_at_the_centre_with_their_provisions()
    {
        var bed = ColonyTestBed.Flat(15);

        var colonists = ColonyStart.Settle(bed.Simulation, ColonyStart.TheOnlyScenario(bed.Simulation.Content), "Valdeolmos");

        colonists.Count.ShouldBe(5);
        bed.World.Get<Position>(colonists[0]).Cell.ShouldBe(new TileCoord(7, 7, 1));
        colonists.Select(colonist => bed.World.Get<Position>(colonist).Cell).Distinct().Count().ShouldBe(5);
        colonists.Select(colonist => bed.World.Get<Colonist>(colonist).Name).Distinct().Count().ShouldBe(5);
        foreach (var colonist in colonists)
        {
            bed.World.Get<Needs>(colonist).Values.Select(need => need.Value).ShouldAllBe(value => value >= 7000 && value <= 10_000);
        }

        var piles = new List<EntityId>();
        bed.World.Query<ItemStack>(piles);
        bed.World.Get<ItemStack>(piles.Single()).Count.ShouldBe(30);
        bed.World.Get<Position>(piles.Single()).Cell.ShouldBe(new TileCoord(7, 7, 1));
    }

    [Fact]
    public void The_arrival_avoids_plants_and_is_the_same_with_the_same_seed()
    {
        static (string, TileCoord) Arrive(ulong seed)
        {
            var bed = ColonyTestBed.Flat(15, seed);
            bed.AddPlant("bush", 7, 7, 1);
            var colonists = ColonyStart.Settle(bed.Simulation, ColonyStart.TheOnlyScenario(bed.Simulation.Content), "Valdeolmos");
            return (bed.World.Get<Colonist>(colonists[0]).Name, bed.World.Get<Position>(colonists[0]).Cell);
        }

        var (name, cell) = Arrive(3);

        cell.ShouldNotBe(new TileCoord(7, 7, 1));
        Arrive(3).ShouldBe((name, cell));
    }

    [Fact]
    public void The_colony_arrives_only_once()
    {
        var bed = ColonyTestBed.Flat(10);
        var scenario = ColonyStart.TheOnlyScenario(bed.Simulation.Content);
        ColonyStart.Settle(bed.Simulation, scenario, "Valdeolmos");

        Should.Throw<InvalidOperationException>(() => ColonyStart.Settle(bed.Simulation, scenario, "Valdeolmos"));
    }

    [Fact]
    public void Mining_marks_only_walls_and_stairs_only_where_they_can_be_dug()
    {
        var bed = new ColonyTestBed("####\n####", "#..#\n#.. ", "    \n    ");
        bed.Designate(DesignationKind.Mine, new TileCoord(0, 0, 1), new TileCoord(3, 1, 1));
        bed.Designate(DesignationKind.Stairs, new TileCoord(0, 0, 0), new TileCoord(3, 1, 2));

        bed.Simulation.Tick();

        (bed.Map.FlagsAt(0, 0, 1) & TileFlags.MineDesignation).ShouldNotBe(TileFlags.None);
        (bed.Map.FlagsAt(1, 0, 1) & TileFlags.MineDesignation).ShouldBe(TileFlags.None);
        (bed.Map.FlagsAt(1, 0, 1) & TileFlags.StairsDesignation).ShouldNotBe(TileFlags.None);
        (bed.Map.FlagsAt(0, 0, 0) & TileFlags.StairsDesignation).ShouldBe(TileFlags.None);
        (bed.Map.FlagsAt(3, 1, 1) & TileFlags.StairsDesignation).ShouldBe(TileFlags.None);
        (bed.Map.FlagsAt(0, 0, 2) & TileFlags.StairsDesignation).ShouldBe(TileFlags.None);
        bed.DrainEvents().OfType<DesignationsChangedEvent>().Count().ShouldBe(2);
    }

    [Fact]
    public void Plants_get_only_the_orders_their_def_allows_and_cancel_removes_every_order()
    {
        var bed = ColonyTestBed.Flat(6);
        var tree = bed.AddPlant("tree", 1, 1, 1);
        var bush = bed.AddPlant("bush", 2, 2, 1);
        bed.Designate(DesignationKind.Chop, new TileCoord(0, 0, 1), new TileCoord(5, 5, 1));
        bed.Designate(DesignationKind.Harvest, new TileCoord(0, 0, 1), new TileCoord(5, 5, 1));
        bed.Designate(DesignationKind.Mine, new TileCoord(0, 0, 0), new TileCoord(5, 5, 0));
        bed.Simulation.Commands.Enqueue(new DesignateCommand(DesignationKind.Cancel, new TileCoord(0, 0, 1), new TileCoord(5, 5, 1)));

        bed.Simulation.Commands.Count.ShouldBe(4);
        bed.Simulation.Tick();

        bed.World.Has<ChopOrder>(tree).ShouldBeFalse();
        bed.World.Has<ChopOrder>(bush).ShouldBeFalse();
        bed.World.Has<HarvestOrder>(bush).ShouldBeFalse();
        (bed.Map.FlagsAt(3, 3, 0) & TileFlags.MineDesignation).ShouldNotBe(TileFlags.None);
    }

    [Fact]
    public void Orders_outside_the_map_are_clamped_or_ignored()
    {
        var bed = ColonyTestBed.Flat(4);
        bed.Designate(DesignationKind.Mine, new TileCoord(-5, -5, 0), new TileCoord(1, 1, 0));
        bed.Designate(DesignationKind.Mine, new TileCoord(10, 10, 0), new TileCoord(20, 20, 0));

        bed.Simulation.Tick();

        (bed.Map.FlagsAt(0, 0, 0) & TileFlags.MineDesignation).ShouldNotBe(TileFlags.None);
        (bed.Map.FlagsAt(2, 2, 0) & TileFlags.MineDesignation).ShouldBe(TileFlags.None);
        bed.DrainEvents().OfType<DesignationsChangedEvent>().Count().ShouldBe(1);
    }

    [Fact]
    public void Needs_drop_by_their_hourly_rate_without_losing_fractions()
    {
        var bed = ColonyTestBed.Flat(6);
        var colonist = bed.AddColonist(2, 2, 1);

        bed.Simulation.Run(7);
        bed.Need(colonist, "hunger").Value.ShouldBe(10_000 - 23);

        bed.Simulation.Run(53);
        bed.Need(colonist, "hunger").Value.ShouldBe(10_000 - 200);
        bed.Need(colonist, "rest").Value.ShouldBe(10_000 - 500);
    }

    [Fact]
    public void An_order_nobody_can_reach_waits_untouched()
    {
        var bed = ColonyTestBed.Flat(6);
        bed.Designate(DesignationKind.Mine, new TileCoord(4, 4, 0), new TileCoord(4, 4, 0));
        bed.Simulation.Tick();

        // The wall below the floor cannot be reached from level 1, so nobody takes the order.
        bed.AddColonist(1, 1, 1);
        bed.Simulation.Run(10);

        (bed.Map.FlagsAt(4, 4, 0) & TileFlags.MineDesignation).ShouldNotBe(TileFlags.None);
        bed.Map.ShapeAt(4, 4, 0).ShouldBe(TileShape.Wall);
    }

    [Fact]
    public void Job_texts_follow_the_key_convention()
    {
        foreach (var kind in Enum.GetValues<JobKind>())
        {
            Core.Text.LocalizedText.IsValidKey(JobKinds.ReportKey(kind)).ShouldBeTrue();
        }

        JobKinds.ReportKey(JobKind.DigStairs).ShouldBe("job.dig_stairs.report");
    }
}
