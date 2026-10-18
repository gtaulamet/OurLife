using OurLife.Core.Colony;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Colony;

/// <summary>Feeding babies, cradles, crawling and play (H3.7, "Crianza" in <c>docs/gdd/vida-y-generaciones.md</c>).</summary>
public class ChildcareTests
{
    private const int DaysPerYear = 60;

    [Fact]
    public void A_hungry_baby_is_fed_by_its_free_mother_though_another_colonist_is_nearer()
    {
        var bed = ColonyTestBed.Flat(15);
        bed.AddColonist(2, 3, 1, name: "Urraca");
        var mother = bed.AddColonist(12, 12, 1);
        var baby = Baby(bed, mother, 3, 3, hunger: 3_000);
        bed.AddItems("provisions", 10, 1, 1, 1);

        var feeder = RunUntilFed(bed, baby);

        feeder.ShouldBe(mother);
        bed.Need(baby, "hunger").Value.ShouldBeGreaterThan(10_000 - 1_250);
        bed.CountItems("provisions").ShouldBe(7);
        bed.World.Has<Carried>(mother).ShouldBeFalse();
    }

    [Fact]
    public void Without_a_free_parent_the_nearest_colonist_feeds_the_baby()
    {
        var bed = ColonyTestBed.Flat(15);
        var neighbour = bed.AddColonist(2, 3, 1, name: "Urraca");
        var mother = bed.AddColonist(12, 12, 1);
        var baby = Baby(bed, mother, 3, 3, hunger: 3_000);
        bed.AddItems("provisions", 10, 1, 1, 1);

        // The mother sleeps a long night.
        bed.Need(mother, "rest").Value = 0;
        bed.World.Add(mother, new Job { Kind = JobKind.Sleep, Stage = JobStage.Working, Cell = new TileCoord(12, 12, 1) });

        RunUntilFed(bed, baby).ShouldBe(neighbour);
    }

    [Fact]
    public void A_baby_never_eats_on_its_own()
    {
        var bed = ColonyTestBed.Flat(10);
        var baby = Baby(bed, mother: EntityId.None, 3, 3, hunger: 1_000);
        bed.AddItems("provisions", 10, 3, 3, 1);

        bed.Simulation.Run(600);

        bed.CountItems("provisions").ShouldBe(10);
        bed.Need(baby, "hunger").Value.ShouldBeLessThan(1_000);
    }

    [Fact]
    public void A_baby_crawls_near_its_mother()
    {
        var bed = ColonyTestBed.Flat(20);
        // The mother stays where she is, with nothing else to do, until the night.
        var mother = bed.AddColonist(10, 10, 1);
        bed.World.Add(mother, new Job { Kind = JobKind.Wander, Stage = JobStage.Working, Cell = new TileCoord(10, 10, 1), UntilTick = long.MaxValue });
        var baby = Baby(bed, mother, 10, 10, hunger: 10_000);
        var moved = false;

        for (var tick = 0; tick < 800; tick++)
        {
            bed.Simulation.Tick();
            var cell = bed.World.Get<Position>(baby).Cell;
            Math.Max(Math.Abs(cell.X - 10), Math.Abs(cell.Y - 10)).ShouldBeLessThanOrEqualTo(3);
            moved |= cell != new TileCoord(10, 10, 1);
        }

        moved.ShouldBeTrue();
    }

    [Fact]
    public void A_sleepy_baby_takes_the_cradle_and_an_adult_the_bed()
    {
        var bed = ColonyTestBed.Flat(12);
        Furnish(bed, "cradle", new TileCoord(2, 2, 1));
        Furnish(bed, "bed", new TileCoord(9, 9, 1));
        var adult = bed.AddColonist(3, 3, 1, rest: 500, name: "Urraca");
        var baby = Baby(bed, adult, 8, 8, hunger: 10_000);
        bed.Need(baby, "rest").Value = 500;

        bed.Simulation.Run(400);

        OwnerOf(bed, "cradle").ShouldBe(baby);
        OwnerOf(bed, "bed").ShouldBe(adult);
        Beds.HasBed(bed.Simulation, baby).ShouldBeTrue();
    }

    [Fact]
    public void A_child_who_grew_out_of_the_cradle_leaves_it_free()
    {
        var bed = ColonyTestBed.Flat(12);
        Furnish(bed, "cradle", new TileCoord(2, 2, 1));
        var child = Baby(bed, EntityId.None, 8, 8, hunger: 10_000);
        bed.World.Get<Bed>(BedOf(bed, "cradle")).Owner = child;
        bed.World.Get<Life>(child).Stage = "child";
        bed.World.Get<Life>(child).AgeDays = 3 * DaysPerYear;

        Beds.HasBed(bed.Simulation, child).ShouldBeFalse();
        Beds.IsTaken(bed.Simulation, BedOf(bed, "cradle")).ShouldBeFalse();
    }

    [Fact]
    public void A_child_goes_to_play_with_another_child_and_they_play()
    {
        var bed = ColonyTestBed.Flat(16, seed: 2);
        var a = Child(bed, 1, 1, "Sancho");
        var b = Child(bed, 13, 13, "Jimena");
        var events = new List<ISimEvent>();
        var went = false;

        for (var tick = 0; tick < 2_000 && !events.OfType<InteractionEvent>().Any(e => e.Interaction == "play"); tick++)
        {
            bed.Simulation.Tick();
            bed.Simulation.Events.DrainTo(events);
            went |= (bed.JobOf(a) is { Kind: JobKind.Play } job && job.Target == b) || (bed.JobOf(b) is { Kind: JobKind.Play } other && other.Target == a);
        }

        went.ShouldBeTrue();
        events.OfType<InteractionEvent>().ShouldContain(e => e.Interaction == "play");
    }

    [Fact]
    public void A_child_does_not_go_to_play_with_a_stranger()
    {
        var bed = ColonyTestBed.Flat(16, seed: 2);
        var child = Child(bed, 1, 1, "Sancho");
        bed.AddColonist(13, 13, 1, name: "Urraca");

        for (var tick = 0; tick < 600; tick++)
        {
            bed.Simulation.Tick();
            bed.JobOf(child)?.Kind.ShouldNotBe(JobKind.Play);
        }
    }

    [Fact]
    public void Nobody_goes_to_eat_the_units_a_feeder_is_taking_to_a_baby()
    {
        var bed = ColonyTestBed.Flat(15);
        var mother = bed.AddColonist(12, 12, 1);
        var baby = Baby(bed, mother, 12, 11, hunger: 0);
        var hungry = bed.AddColonist(6, 6, 1, hunger: 1_000, name: "Urraca");
        bed.AddItems("provisions", 3, 1, 1, 1);

        for (var tick = 0; tick < 400; tick++)
        {
            bed.Simulation.Tick();
            if (bed.JobOf(mother) is { Kind: JobKind.FeedBaby, Stage: JobStage.Moving } feeding)
            {
                (bed.JobOf(hungry) is { Kind: JobKind.Eat } eating && eating.Target == feeding.Target).ShouldBeFalse();
            }
        }

        bed.Need(baby, "hunger").Value.ShouldBeGreaterThan(5_000);
    }

    [Fact]
    public void A_child_at_play_leaves_it_to_eat_when_hungry()
    {
        var bed = ColonyTestBed.Flat(15);
        var child = Child(bed, 3, 3, "Sancho");
        var playmate = Child(bed, 4, 4, "Jimena");
        bed.AddItems("provisions", 5, 6, 6, 1);
        bed.World.Add(child, new Job { Kind = JobKind.Play, Stage = JobStage.Working, Target = playmate, Cell = new TileCoord(4, 4, 1), UntilTick = long.MaxValue });
        bed.Need(child, "hunger").Value = 3_000;

        bed.Simulation.Run(60);

        bed.JobOf(child)?.Kind.ShouldBe(JobKind.Eat);
    }

    [Fact]
    public void A_hungry_colonist_still_eats_from_a_pile_a_feeder_takes_a_few_units_from()
    {
        var bed = ColonyTestBed.Flat(15);
        var feeder = bed.AddColonist(2, 2, 1, name: "Urraca");
        var hungry = bed.AddColonist(4, 4, 1, hunger: 1_000);
        Baby(bed, EntityId.None, 12, 12, hunger: 3_900);
        bed.AddItems("provisions", 10, 1, 1, 1);

        bed.Simulation.Tick();

        bed.JobOf(feeder).ShouldNotBeNull().Kind.ShouldBe(JobKind.FeedBaby);
        bed.JobOf(hungry).ShouldNotBeNull().Kind.ShouldBe(JobKind.Eat);
        bed.JobOf(hungry)!.Target.ShouldBe(bed.JobOf(feeder)!.Target);
    }

    [Fact]
    public void A_baby_is_not_left_aside_when_the_food_is_only_held_by_another_feeder()
    {
        var bed = ColonyTestBed.Flat(15);
        var first = bed.AddColonist(2, 2, 1, name: "Urraca");
        var second = bed.AddColonist(3, 2, 1, name: "Jimena");
        var babies = new[] { Baby(bed, EntityId.None, 6, 6, hunger: 3_000), Baby(bed, EntityId.None, 7, 7, hunger: 3_000) };
        bed.AddItems("provisions", 3, 1, 1, 1);

        bed.Simulation.Tick();

        bed.JobOf(first).ShouldNotBeNull().Kind.ShouldBe(JobKind.FeedBaby);
        bed.World.Get<Colonist>(second).Unreachable.ShouldBeEmpty();

        // More food comes: the second feeder goes at once, without waiting out a cooldown.
        bed.AddItems("provisions", 5, 13, 13, 1);
        bed.Simulation.Run(bed.Simulation.Content.All<OurLife.Core.Defs.AiConfigDef>()[0].RecheckTicks + 1);

        bed.JobOf(second).ShouldNotBeNull().Kind.ShouldBe(JobKind.FeedBaby);
        babies.ShouldContain(bed.JobOf(second)!.Recipient);
    }

    [Fact]
    public void A_baby_in_a_crisis_still_crawls_near_its_mother()
    {
        var bed = ColonyTestBed.Flat(25);
        var mother = bed.AddColonist(12, 12, 1);
        bed.World.Add(mother, new Job { Kind = JobKind.Wander, Stage = JobStage.Working, Cell = new TileCoord(12, 12, 1), UntilTick = long.MaxValue });
        var baby = Baby(bed, mother, 12, 12, hunger: 10_000);
        bed.World.Add(baby, new Crisis { Kind = "isolate", UntilTick = long.MaxValue });

        for (var tick = 0; tick < 800; tick++)
        {
            bed.Simulation.Tick();
            var cell = bed.World.Get<Position>(baby).Cell;
            Math.Max(Math.Abs(cell.X - 12), Math.Abs(cell.Y - 12)).ShouldBeLessThanOrEqualTo(3);
        }
    }

    [Fact]
    public void A_feeder_holds_only_the_units_nobody_else_holds()
    {
        var bed = ColonyTestBed.Flat(15);
        bed.AddColonist(4, 4, 1, hunger: 1_000);
        var feeder = bed.AddColonist(2, 2, 1, name: "Urraca");
        Baby(bed, EntityId.None, 12, 12, hunger: 0);
        bed.AddItems("provisions", 3, 1, 1, 1);

        bed.Simulation.Tick();

        bed.JobOf(feeder).ShouldNotBeNull().Kind.ShouldBe(JobKind.FeedBaby);
        bed.JobOf(feeder)!.Count.ShouldBe(2);
    }

    [Fact]
    public void A_parent_with_other_work_first_leaves_the_baby_to_the_others()
    {
        // Mining comes first for the mother, so she would look for it before the baby.
        var bed = ColonyTestBed.Flat(15);
        var neighbour = bed.AddColonist(2, 3, 1, name: "Urraca");
        var mother = bed.AddColonist(12, 12, 1);
        var baby = Baby(bed, mother, 3, 3, hunger: 3_000);
        bed.AddItems("provisions", 10, 1, 1, 1);
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(mother, "mining", 1));

        RunUntilFed(bed, baby).ShouldBe(neighbour);
    }

    [Fact]
    public void A_child_does_not_go_to_play_with_family_in_a_crisis()
    {
        var bed = ColonyTestBed.Flat(16, seed: 2);
        var father = bed.AddColonist(13, 13, 1, name: "Rodrigo");
        bed.World.Add(father, new Crisis { Kind = "isolate", UntilTick = long.MaxValue });
        var child = Child(bed, 1, 1, "Sancho");
        bed.World.Add(child, new Family { Father = Relationships.RelativeOf(bed.Simulation, father) });

        for (var tick = 0; tick < 600; tick++)
        {
            bed.Simulation.Tick();
            bed.JobOf(child)?.Kind.ShouldNotBe(JobKind.Play);
        }
    }

    [Fact]
    public void A_baby_far_from_its_mother_crawls_where_it_is()
    {
        var bed = ColonyTestBed.Flat(25);
        var mother = bed.AddColonist(2, 2, 1);
        bed.World.Add(mother, new Job { Kind = JobKind.Wander, Stage = JobStage.Working, Cell = new TileCoord(2, 2, 1), UntilTick = long.MaxValue });
        var baby = Baby(bed, mother, 21, 21, hunger: 10_000);
        Job? last = null;
        var crawls = 0;

        // Each crawl goes to a cell near where the baby is when it sets off, not towards its mother.
        for (var tick = 0; tick < 1_500; tick++)
        {
            var from = bed.World.Get<Position>(baby).Cell;
            bed.Simulation.Tick();
            if (bed.JobOf(baby) is { Kind: JobKind.Wander } job && !ReferenceEquals(job, last))
            {
                Math.Max(Math.Abs(job.Cell.X - from.X), Math.Abs(job.Cell.Y - from.Y)).ShouldBeLessThanOrEqualTo(3);
                (last, crawls) = (job, crawls + 1);
            }
        }

        crawls.ShouldBeGreaterThan(1);
    }

    /// <summary>A baby of a few months, the child of the mother if there is one, with no traits.</summary>
    private static EntityId Baby(ColonyTestBed bed, EntityId mother, int x, int y, int hunger)
    {
        var baby = bed.AddColonist(x, y, 1, hunger: hunger, name: "Hugh");
        bed.World.Get<Colonist>(baby).Gender = Gender.Male;
        bed.World.Add(baby, new Life { AgeDays = 20, Stage = "baby" });
        if (!mother.IsNone)
        {
            bed.World.Add(baby, new Family { Mother = Relationships.RelativeOf(bed.Simulation, mother) });
            bed.World.Add(mother, new Family { Children = [Relationships.RelativeOf(bed.Simulation, baby)] });
        }

        return baby;
    }

    private static EntityId Child(ColonyTestBed bed, int x, int y, string name)
    {
        var child = bed.AddColonist(x, y, 1, name: name);
        bed.World.Add(child, new Life { AgeDays = 6 * DaysPerYear, Stage = "child" });
        return child;
    }

    /// <summary>Runs until the baby is fed or a day passes: whoever took the food to it, or none.</summary>
    private static EntityId RunUntilFed(ColonyTestBed bed, EntityId baby)
    {
        var feeder = EntityId.None;
        for (var tick = 0; tick < 1_440 && bed.Need(baby, "hunger").Value < 8_000; tick++)
        {
            bed.Simulation.Tick();
            foreach (var colonist in bed.Colonists())
            {
                if (bed.JobOf(colonist) is { Kind: JobKind.FeedBaby } job && job.Recipient == baby)
                {
                    feeder = colonist;
                }
            }
        }

        return feeder;
    }

    private static void Furnish(ColonyTestBed bed, string buildable, TileCoord cell)
    {
        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand(buildable, "timber", cell, cell));
        bed.Simulation.ApplyCommandsNow();
        var blueprints = new List<EntityId>();
        bed.World.Query<Blueprint, Position>(blueprints);
        Construction.Finish(bed.Simulation, blueprints.Single(entity => bed.World.Get<Position>(entity).Cell == cell), cell);
    }

    private static EntityId OwnerOf(ColonyTestBed bed, string buildable) => bed.World.Get<Bed>(BedOf(bed, buildable)).Owner;

    private static EntityId BedOf(ColonyTestBed bed, string buildable)
    {
        var beds = new List<EntityId>();
        bed.World.Query<Bed>(beds);
        return beds.Single(entity => bed.World.Get<Furniture>(entity).Buildable == buildable);
    }
}
