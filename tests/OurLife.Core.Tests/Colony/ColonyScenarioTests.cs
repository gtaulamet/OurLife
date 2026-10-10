using OurLife.Core.Colony;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Colony;

/// <summary>Scenarios of the acceptance criteria in <c>docs/gdd/colonos-e-ia.md</c>.</summary>
public class ColonyScenarioTests
{
    private const int TicksPerDay = 24 * 60;

    [Fact]
    public void Five_colonists_with_berry_bushes_are_all_alive_and_fed_after_three_days()
    {
        var bed = ColonyTestBed.Flat(24);
        for (var y = 2; y < 22; y += 3)
        {
            for (var x = 2; x < 22; x += 3)
            {
                bed.AddPlant("bush", x, y, 1);
            }
        }

        var colonists = Enumerable.Range(0, 5).Select(i => bed.AddColonist(10 + i, 12, 1, hunger: 2000)).ToList();

        bed.Simulation.Run(3 * TicksPerDay);

        bed.Colonists().ShouldBe(colonists);
        foreach (var colonist in colonists)
        {
            bed.Need(colonist, "hunger").Value.ShouldBeGreaterThan(1500);
        }
    }

    [Fact]
    public void A_designated_block_of_rock_is_dug_out()
    {
        var floor = ColonyTestBed.Level(10, 10, '.').ToCharArray();
        for (var y = 3; y < 7; y++)
        {
            for (var x = 3; x < 7; x++)
            {
                floor[(y * 11) + x] = '#';
            }
        }

        var bed = new ColonyTestBed(ColonyTestBed.Level(10, 10, '#'), new string(floor));
        bed.AddColonist(0, 0, 1);
        bed.AddColonist(9, 9, 1);
        bed.Designate(DesignationKind.Mine, new TileCoord(3, 3, 1), new TileCoord(6, 6, 1));

        bed.Simulation.Run(2000);

        for (var y = 3; y < 7; y++)
        {
            for (var x = 3; x < 7; x++)
            {
                bed.Map.ShapeAt(x, y, 1).ShouldBe(TileShape.Floor);
                (bed.Map.FlagsAt(x, y, 1) & TileFlags.MineDesignation).ShouldBe(TileFlags.None);
            }
        }

        bed.CountItems("stone").ShouldBe(16);

        // One redraw for the order, and one for each cell whose order was done.
        bed.DrainEvents().OfType<DesignationsChangedEvent>().Count().ShouldBe(17);
    }

    [Fact]
    public void Stairs_and_digging_the_wall_below_them_are_never_done_at_once()
    {
        // Level 0 has a gallery that reaches the wall under the stairs; level 1 is open ground.
        var bed = new ColonyTestBed(
            "#####\n#####\n#####\n#####\n....#",
            ColonyTestBed.Level(5, 5, '.'));
        var above = bed.AddColonist(0, 0, 1);
        var below = bed.AddColonist(0, 4, 0);
        bed.Designate(DesignationKind.Stairs, new TileCoord(4, 4, 1), new TileCoord(4, 4, 1));
        bed.Designate(DesignationKind.Mine, new TileCoord(4, 4, 0), new TileCoord(4, 4, 0));

        for (var tick = 0; tick < 300; tick++)
        {
            bed.Simulation.Tick();
            var digging = bed.JobOf(above)?.Kind == JobKind.DigStairs && bed.JobOf(below)?.Kind == JobKind.Mine;
            digging.ShouldBeFalse();
        }

        bed.Map.ShapeAt(4, 4, 1).ShouldBe(TileShape.StairDown);
        bed.Map.ShapeAt(4, 4, 0).ShouldBe(TileShape.StairUp);
    }

    [Fact]
    public void Two_colonists_never_work_on_the_same_tree()
    {
        var bed = ColonyTestBed.Flat(10);
        var tree = bed.AddPlant("tree", 5, 5, 1);
        var first = bed.AddColonist(1, 1, 1);
        var second = bed.AddColonist(8, 8, 1);
        bed.Designate(DesignationKind.Chop, new TileCoord(5, 5, 1), new TileCoord(5, 5, 1));

        for (var tick = 0; tick < 600 && bed.World.IsAlive(tree); tick++)
        {
            bed.Simulation.Tick();
            new[] { first, second }.Count(colonist => bed.JobOf(colonist)?.Target == tree).ShouldBeLessThanOrEqualTo(1);
        }

        bed.World.IsAlive(tree).ShouldBeFalse();
        bed.CountItems("log").ShouldBe(4);
    }

    [Fact]
    public void Chopping_a_tree_opens_its_cell_to_walkers()
    {
        // A corridor one cell wide, closed by a tree.
        var bed = new ColonyTestBed("#######", ".......");
        bed.AddPlant("tree", 3, 0, 1);
        bed.AddColonist(0, 0, 1);
        bed.Simulation.Connectivity.AreConnected(new TileCoord(0, 0, 1), new TileCoord(6, 0, 1)).ShouldBeFalse();
        bed.Designate(DesignationKind.Chop, new TileCoord(3, 0, 1), new TileCoord(3, 0, 1));

        bed.Simulation.Run(300);

        bed.Map.IsStandable(3, 0, 1).ShouldBeTrue();
        bed.Simulation.Connectivity.AreConnected(new TileCoord(0, 0, 1), new TileCoord(6, 0, 1)).ShouldBeTrue();
    }

    [Fact]
    public void Stairs_dug_down_lead_to_the_level_below()
    {
        var bed = ColonyTestBed.Flat(8);
        var colonist = bed.AddColonist(1, 1, 1);
        bed.Designate(DesignationKind.Stairs, new TileCoord(5, 5, 1), new TileCoord(5, 5, 1));

        bed.Simulation.Run(300);

        bed.Map.ShapeAt(5, 5, 1).ShouldBe(TileShape.StairDown);
        bed.Map.ShapeAt(5, 5, 0).ShouldBe(TileShape.StairUp);
        bed.Simulation.Connectivity.AreConnected(bed.World.Get<Position>(colonist).Cell, new TileCoord(5, 5, 0)).ShouldBeTrue();
        bed.CountItems("stone").ShouldBe(1);
    }

    [Fact]
    public void Stairs_ordered_on_two_levels_make_one_stairwell_whatever_is_dug_first()
    {
        var bed = new ColonyTestBed(ColonyTestBed.Level(6, 6, '#'), ColonyTestBed.Level(6, 6, '.'), ColonyTestBed.Level(6, 6, '.'));
        bed.AddColonist(1, 1, 2);
        bed.Designate(DesignationKind.Stairs, new TileCoord(3, 3, 1), new TileCoord(3, 3, 2));

        bed.Simulation.Run(600);

        bed.Map.ShapeAt(3, 3, 2).ShouldBe(TileShape.StairDown);
        bed.Map.ShapeAt(3, 3, 1).ShouldBe(TileShape.StairUpDown);
        bed.Map.ShapeAt(3, 3, 0).ShouldBe(TileShape.StairUp);
    }

    [Fact]
    public void A_colonist_who_gathers_to_eat_eats_before_going_back_to_work()
    {
        var bed = ColonyTestBed.Flat(10);
        var bush = bed.AddPlant("bush", 3, 3, 1);
        bed.AddPlant("tree", 8, 8, 1);
        var colonist = bed.AddColonist(1, 1, 1, hunger: 1000);
        bed.Designate(DesignationKind.Chop, new TileCoord(8, 8, 1), new TileCoord(8, 8, 1));

        for (var tick = 0; tick < 200 && !bed.World.Has<Harvested>(bush); tick++)
        {
            bed.Simulation.Tick();
        }

        bed.World.Has<Harvested>(bush).ShouldBeTrue();
        bed.JobOf(colonist)!.Kind.ShouldBe(JobKind.Eat);
    }

    [Fact]
    public void A_colonist_without_food_starves_after_three_days_at_zero_and_the_colony_is_lost()
    {
        var bed = ColonyTestBed.Flat(6);
        var colonist = bed.AddColonist(2, 2, 1, hunger: 0);

        bed.Simulation.Run((72 * 60) - 1);
        bed.World.IsAlive(colonist).ShouldBeTrue();
        bed.DrainEvents();
        bed.Simulation.Tick();

        bed.World.IsAlive(colonist).ShouldBeFalse();
        var events = bed.DrainEvents();
        var death = events.OfType<NotificationEvent>().First();
        death.Text.Key.ShouldBe("event.need.hunger.death");
        death.Text.Args["colonist"].ShouldBe("Elvira");
        death.Text.Args["colonist_gender"].ShouldBe(Gender.Female);
        events.OfType<NotificationEvent>().Select(e => e.Text.Key).ShouldContain("event.colony.lost");
        events.OfType<ColonyLostEvent>().Count().ShouldBe(1);
        events.OfType<EntityRemovedEvent>().Single().Entity.ShouldBe(colonist);
    }

    [Fact]
    public void A_death_is_not_the_end_while_someone_lives()
    {
        var bed = ColonyTestBed.Flat(6);
        bed.AddColonist(2, 2, 1, hunger: 0);
        bed.AddColonist(3, 3, 1);

        bed.Simulation.Run(72 * 60);

        bed.Colonists().Count.ShouldBe(1);
        bed.DrainEvents().OfType<ColonyLostEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void A_critical_need_interrupts_work()
    {
        var bed = new ColonyTestBed(
            ColonyTestBed.Level(12, 3, '#'),
            "...#########\n............\n............");
        var colonist = bed.AddColonist(1, 1, 1, hunger: 1600);
        bed.AddItems("provisions", 5, 0, 2, 1);
        bed.Designate(DesignationKind.Mine, new TileCoord(3, 0, 1), new TileCoord(11, 0, 1));

        bed.Simulation.Run(5);
        bed.JobOf(colonist)!.Kind.ShouldBe(JobKind.Mine);

        bed.Simulation.Run(100);

        bed.Need(colonist, "hunger").Value.ShouldBeGreaterThan(4000);
        bed.CountItems("provisions").ShouldBeLessThan(5);
    }

    [Fact]
    public void A_hungry_colonist_without_piles_gathers_a_bush_and_eats_its_berries()
    {
        var bed = ColonyTestBed.Flat(8);
        var bush = bed.AddPlant("bush", 6, 6, 1);
        var colonist = bed.AddColonist(1, 1, 1, hunger: 1000);

        bed.Simulation.Run(300);

        bed.World.Has<Harvested>(bush).ShouldBeTrue();
        bed.Need(colonist, "hunger").Value.ShouldBeGreaterThan(2500);
        bed.CountItems("berries").ShouldBe(0);
    }

    [Fact]
    public void An_exhausted_colonist_sleeps_until_rested()
    {
        var bed = ColonyTestBed.Flat(6);
        var colonist = bed.AddColonist(2, 2, 1, rest: 500);

        bed.Simulation.Run(3);
        bed.JobOf(colonist)!.Kind.ShouldBe(JobKind.Sleep);
        var cell = bed.World.Get<Position>(colonist).Cell;

        bed.Simulation.Run(11 * 60);

        bed.Need(colonist, "rest").Value.ShouldBeGreaterThan(9000);
        bed.JobOf(colonist)?.Kind.ShouldNotBe(JobKind.Sleep);
        cell.ShouldBe(new TileCoord(2, 2, 1));
    }

    [Fact]
    public void A_gathered_bush_bears_fruit_again_after_its_regrowth_days()
    {
        var bed = ColonyTestBed.Flat(6);
        var bush = bed.AddPlant("bush", 4, 4, 1);
        bed.AddColonist(1, 1, 1);

        // Provisions, so the colonist never gathers the bush to eat.
        bed.AddItems("provisions", 50, 0, 0, 1);
        bed.Designate(DesignationKind.Harvest, new TileCoord(4, 4, 1), new TileCoord(4, 4, 1));

        bed.Simulation.Run(200);
        bed.World.Has<Harvested>(bush).ShouldBeTrue();
        bed.World.Has<HarvestOrder>(bush).ShouldBeFalse();
        bed.CountItems("berries").ShouldBe(8);

        bed.Simulation.Run((4 * TicksPerDay) + 1);

        bed.World.Has<Harvested>(bush).ShouldBeFalse();
    }

    [Fact]
    public void A_tree_that_blocks_its_cell_drops_its_fruit_where_the_gatherer_stands()
    {
        var bed = ColonyTestBed.Flat(6);
        bed.AddPlant("fruit_tree", 4, 4, 1);
        bed.AddColonist(1, 1, 1);
        bed.AddItems("provisions", 50, 0, 0, 1);
        bed.Designate(DesignationKind.Harvest, new TileCoord(4, 4, 1), new TileCoord(4, 4, 1));

        bed.Simulation.Run(200);

        var piles = new List<EntityId>();
        bed.World.Query<ItemStack, Position>(piles);
        var berries = piles.Single(pile => bed.World.Get<ItemStack>(pile).Def == "berries");
        bed.World.Get<ItemStack>(berries).Count.ShouldBe(8);
        bed.Map.IsStandable(bed.World.Get<Position>(berries).Cell).ShouldBeTrue();
    }

    [Fact]
    public void Same_seed_and_orders_give_the_same_state()
    {
        static ulong Play(ulong seed)
        {
            var bed = ColonyTestBed.Flat(16, seed);
            bed.AddPlant("tree", 8, 8, 1);
            bed.AddPlant("bush", 3, 12, 1);
            bed.AddColonist(2, 2, 1, hunger: 3000);
            bed.AddColonist(12, 3, 1, rest: 2500);
            bed.AddItems("provisions", 4, 5, 5, 1);
            bed.Designate(DesignationKind.Chop, new TileCoord(0, 0, 1), new TileCoord(15, 15, 1));
            bed.Designate(DesignationKind.Stairs, new TileCoord(10, 10, 1), new TileCoord(11, 10, 1));
            bed.Simulation.Run(3000);
            return bed.Simulation.ComputeStateHash();
        }

        Play(7).ShouldBe(Play(7));
    }

    [Fact]
    public void Orders_applied_while_paused_give_the_same_state_as_on_the_next_tick()
    {
        static ColonyTestBed Prepare()
        {
            var bed = ColonyTestBed.Flat(12, 3);
            bed.AddPlant("tree", 8, 8, 1);
            bed.AddColonist(2, 2, 1);
            bed.Simulation.Run(50);
            bed.Designate(DesignationKind.Chop, new TileCoord(0, 0, 1), new TileCoord(11, 11, 1));
            return bed;
        }

        var paused = Prepare();
        paused.Simulation.ApplyCommandsNow();
        paused.Simulation.Commands.Count.ShouldBe(0);
        paused.Simulation.Run(500);

        var running = Prepare();
        running.Simulation.Run(500);

        paused.Simulation.ComputeStateHash().ShouldBe(running.Simulation.ComputeStateHash());
    }

    [Fact]
    public void Colonists_without_anything_to_do_wander_on_their_level()
    {
        var bed = ColonyTestBed.Flat(30);
        var colonist = bed.AddColonist(15, 15, 1);
        var moved = false;

        for (var tick = 0; tick < 600; tick++)
        {
            bed.Simulation.Tick();
            var cell = bed.World.Get<Position>(colonist).Cell;
            bed.JobOf(colonist)?.Kind.ShouldBe(JobKind.Wander);
            cell.Z.ShouldBe(1);
            moved |= cell != new TileCoord(15, 15, 1);
        }

        moved.ShouldBeTrue();
    }
}
