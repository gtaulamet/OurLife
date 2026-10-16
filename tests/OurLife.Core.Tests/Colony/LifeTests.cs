using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Colony;

/// <summary>Age, stages of life and death of old age (H3.2, <c>docs/gdd/vida-y-generaciones.md</c>).</summary>
public class LifeTests
{
    private const int DaysPerYear = 60;

    [Fact]
    public void The_founders_arrive_as_adults_of_different_ages_with_surnames_and_the_chosen_pace()
    {
        var bed = ColonyTestBed.Flat(15);

        var colonists = ColonyStart.Settle(bed.Simulation, ColonyStart.TheOnlyScenario(bed.Simulation.Content), "Valdeolmos", "very_fast");

        bed.Simulation.LifePace.ShouldBe("very_fast");
        colonists.Select(colonist => LifeRules.AgeYears(bed.Simulation, colonist)!.Value).ShouldAllBe(years => years >= 18 && years <= 40);
        colonists.Select(colonist => bed.World.Get<Life>(colonist).AgeDays).Distinct().Count().ShouldBe(5);
        colonists.Select(colonist => LifeRules.Stage(bed.Simulation, colonist)!.Id).ShouldAllBe(stage => stage == "adult");
        colonists.Select(colonist => bed.World.Get<Colonist>(colonist).Surname).ShouldAllBe(surname => surname.Length > 0);
    }

    [Fact]
    public void Without_a_choice_the_colony_lives_at_the_default_pace()
    {
        var bed = ColonyTestBed.Flat(15);

        ColonyStart.Settle(bed.Simulation, ColonyStart.TheOnlyScenario(bed.Simulation.Content), "Valdeolmos");

        bed.Simulation.LifePace.ShouldBe("fast");
    }

    [Fact]
    public void An_unknown_pace_is_refused_before_anyone_arrives()
    {
        var bed = ColonyTestBed.Flat(15);

        Should.Throw<ArgumentException>(() => ColonyStart.Settle(bed.Simulation, ColonyStart.TheOnlyScenario(bed.Simulation.Content), "Valdeolmos", "eternal"));

        bed.Colonists().ShouldBeEmpty();
        bed.Simulation.LifePace.ShouldBeEmpty();
    }

    [Fact]
    public void A_child_becomes_a_teenager_at_midnight_and_starts_on_light_work_only()
    {
        var bed = ColonyTestBed.Flat(12);
        var colonist = bed.AddColonist(2, 2, 1);
        bed.World.Add(colonist, new Life { AgeDays = (13 * DaysPerYear) - 4, Stage = "child" });
        bed.AddPlant("tree", 6, 2, 1);
        bed.AddPlant("bush", 2, 6, 1);
        bed.Designate(DesignationKind.Chop, new TileCoord(0, 0, 1), new TileCoord(11, 11, 1));
        bed.Designate(DesignationKind.Harvest, new TileCoord(0, 0, 1), new TileCoord(11, 11, 1));

        var jobs = new HashSet<JobKind>();
        for (var tick = 0; tick < LifeSystem.MidnightOffset(bed.Simulation.Time); tick++)
        {
            bed.Simulation.Tick();
            jobs.Add(bed.JobOf(colonist)?.Kind ?? JobKind.Wander);
        }

        jobs.ShouldNotContain(JobKind.Chop);
        jobs.ShouldNotContain(JobKind.Harvest);
        bed.DrainEvents();

        bed.Simulation.Tick();

        bed.World.Get<Life>(colonist).AgeDays.ShouldBe(13 * DaysPerYear);
        LifeRules.Stage(bed.Simulation, colonist)!.Id.ShouldBe("adolescent");
        var events = bed.DrainEvents();
        events.ShouldContain(new EntityChangedEvent(colonist));
        var notice = events.OfType<NotificationEvent>().Single();
        notice.Text.Key.ShouldBe("event.life.stage.adolescent");
        notice.Text.Args["colonist"].ShouldBe("Elvira");

        jobs.Clear();
        for (var tick = 0; tick < 2 * bed.Simulation.Time.TicksPerDay / 3; tick++)
        {
            bed.Simulation.Tick();
            jobs.Add(bed.JobOf(colonist)?.Kind ?? JobKind.Wander);
        }

        jobs.ShouldContain(JobKind.Harvest);
        jobs.ShouldNotContain(JobKind.Chop);
    }

    [Fact]
    public void A_child_never_works_even_with_orders_and_priorities()
    {
        var bed = new ColonyTestBed(ColonyTestBed.Level(12, 12, '#'), "#...........\n" + ColonyTestBed.Level(12, 11, '.'));
        var colonist = bed.AddColonist(2, 2, 1);
        bed.World.Add(colonist, new Life { AgeDays = 5 * DaysPerYear, Stage = "child" });
        bed.AddPlant("tree", 6, 2, 1);
        bed.AddPlant("bush", 2, 6, 1);
        bed.AddItems("log", 3, 8, 8, 1);
        bed.Designate(DesignationKind.Mine, new TileCoord(0, 0, 1), new TileCoord(0, 0, 1));
        bed.Designate(DesignationKind.Chop, new TileCoord(0, 0, 1), new TileCoord(11, 11, 1));
        bed.Designate(DesignationKind.Harvest, new TileCoord(0, 0, 1), new TileCoord(11, 11, 1));
        bed.Designate(DesignationKind.Stockpile, new TileCoord(9, 9, 1), new TileCoord(10, 10, 1));
        foreach (var workType in bed.Simulation.Content.All<WorkTypeDef>())
        {
            bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, workType.Id, WorkPriorityLevels.Highest));
        }

        var jobs = new HashSet<JobKind>();
        for (var tick = 0; tick < bed.Simulation.Time.TicksPerDay; tick++)
        {
            bed.Simulation.Tick();
            jobs.Add(bed.JobOf(colonist)?.Kind ?? JobKind.Wander);
        }

        LifeRules.Stage(bed.Simulation, colonist)!.Id.ShouldBe("child");
        jobs.Intersect(WorkTypeDef.OrderJobs).ShouldBeEmpty();
    }

    [Fact]
    public void An_elder_digs_slower_than_an_adult()
    {
        static long TicksToDig(int? ageYears)
        {
            var bed = new ColonyTestBed(ColonyTestBed.Level(5, 3, '#'), "#....\n.....\n.....");
            var colonist = bed.AddColonist(1, 0, 1);
            if (ageYears is { } years)
            {
                bed.World.Add(colonist, new Life { AgeDays = years * DaysPerYear, Stage = years >= 60 ? "elder" : "adult" });
            }

            bed.Designate(DesignationKind.Mine, new TileCoord(0, 0, 1), new TileCoord(0, 0, 1));
            while (bed.Map.ShapeAt(new TileCoord(0, 0, 1)) == TileShape.Wall)
            {
                bed.Simulation.Tick();
                bed.Simulation.CurrentTick.ShouldBeLessThan(500);
            }

            return bed.Simulation.CurrentTick;
        }

        var adult = TicksToDig(30);

        TicksToDig(null).ShouldBe(adult);

        // 30 ticks of rock at 80 % take 37.
        TicksToDig(65).ShouldBe(adult + 7);
    }

    [Fact]
    public void A_baby_crawls_slowly_and_gets_hungry_slowly()
    {
        var bed = ColonyTestBed.Flat(10);
        var adult = bed.AddColonist(2, 2, 1);
        var baby = bed.AddColonist(5, 5, 1);
        bed.World.Add(baby, new Life { AgeDays = 100, Stage = "baby" });

        bed.Simulation.Run(60);

        // A step of two ticks: 2000 thousandths, at 1000 a tick for an adult and 250 for a baby.
        MovementSystem.StepCost(bed.Simulation, baby, new TileCoord(1, 1, 1), new TileCoord(2, 1, 1)).ShouldBe(2_000);
        MovementSystem.ProgressOf(bed.Simulation, adult).ShouldBe(1_000);
        MovementSystem.ProgressOf(bed.Simulation, baby).ShouldBe(250);
        bed.Need(adult, "hunger").Value.ShouldBe(10_000 - 200);
        bed.Need(baby, "hunger").Value.ShouldBe(10_000 - 80);
        bed.Need(baby, "rest").Value.ShouldBe(10_000 - 750);
    }

    [Fact]
    public void A_child_at_ninety_percent_walks_ten_cells_in_the_time_an_adult_walks_nine()
    {
        static long TicksToWalk(string stage)
        {
            var bed = ColonyTestBed.Flat(14);
            var colonist = bed.AddColonist(1, 1, 1);
            bed.World.Add(colonist, new Life { AgeDays = 8 * DaysPerYear, Stage = stage });
            bed.World.Add(colonist, new Job { Kind = JobKind.Wander, Cell = new TileCoord(11, 1, 1), UntilTick = long.MaxValue });
            bed.World.Get<Movement>(colonist).Path.AddRange(Enumerable.Range(2, 10).Select(x => new TileCoord(x, 1, 1)));
            while (bed.World.Get<Movement>(colonist).IsMoving)
            {
                bed.Simulation.Tick();
            }

            return bed.Simulation.CurrentTick;
        }

        // Ten steps of two ticks: 20 ticks for an adult, 2000 / 900 a step for a child, 22.2 ticks in all.
        TicksToWalk("adult").ShouldBe(20);
        TicksToWalk("child").ShouldBe(23);
    }

    [Fact]
    public void A_child_who_plays_is_not_idle()
    {
        var bed = ColonyTestBed.Flat(12);
        var adult = bed.AddColonist(2, 2, 1, name: "Urraca");
        var child = bed.AddColonist(8, 8, 1);
        bed.World.Add(child, new Life { AgeDays = 6 * DaysPerYear, Stage = "child" });

        bed.Simulation.Run(4 * 60);

        var idle = bed.DrainEvents().OfType<NotificationEvent>().Where(notice => notice.Text.Key == ColonyAlerts.IdleEventKey).ToList();
        idle.Single().Text.Args["colonist"].ShouldBe("Urraca");
        ColonyAlerts.Active(bed.Simulation).Select(alert => alert.Colonist).ShouldBe(new[] { adult });
    }

    [Fact]
    public void Reaching_a_stage_that_does_not_do_a_job_drops_it()
    {
        var content = TestContent.WithDefs(ColonyTestBed.ContentJson.TrimEnd().TrimEnd(']')
            + """, { "type": "LifeStage", "id": "frail", "fromYears": 85, "work": "none" }]""");
        var (simulation, colonist) = Lonely(content, seed: 1);
        var life = simulation.World.Get<Life>(colonist);
        (life.AgeDays, life.Stage) = ((85 * DaysPerYear) - 8, "elder");
        simulation.World.Add(colonist, new Movement());
        simulation.World.Add(colonist, new Job { Kind = JobKind.Mine, Stage = JobStage.Working });

        simulation.Run(LifeSystem.MidnightOffset(simulation.Time) + 1);

        LifeRules.Stage(simulation, colonist)!.Id.ShouldBe("frail");
        simulation.World.Has<Job>(colonist).ShouldBeFalse();
    }

    [Fact]
    public void A_stage_that_does_the_job_keeps_it_and_going_back_a_stage_is_not_told()
    {
        var (simulation, colonist) = Lonely(ColonyTestBed.Content, seed: 1);
        var life = simulation.World.Get<Life>(colonist);
        (life.AgeDays, life.Stage) = (30 * DaysPerYear, "elder");
        simulation.World.Add(colonist, new Movement());
        simulation.World.Add(colonist, new Job { Kind = JobKind.Mine, Stage = JobStage.Working });

        simulation.Run(LifeSystem.MidnightOffset(simulation.Time) + 1);

        LifeRules.Stage(simulation, colonist)!.Id.ShouldBe("adult");
        simulation.World.Has<Job>(colonist).ShouldBeTrue();
        var events = new List<ISimEvent>();
        simulation.Events.DrainTo(events);
        events.ShouldContain(new EntityChangedEvent(colonist));
        events.OfType<NotificationEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void An_elder_gathers_to_eat_as_fast_as_an_adult()
    {
        static long TicksToGather(string stage)
        {
            var bed = ColonyTestBed.Flat(10);
            var colonist = bed.AddColonist(2, 2, 1, hunger: 1_000);
            bed.World.Add(colonist, new Life { AgeDays = 70 * DaysPerYear, Stage = stage });
            var bush = bed.AddPlant("bush", 3, 2, 1);
            while (!bed.World.Has<Harvested>(bush))
            {
                bed.Simulation.Tick();
                bed.Simulation.CurrentTick.ShouldBeLessThan(500);
            }

            return bed.Simulation.CurrentTick;
        }

        TicksToGather("elder").ShouldBe(TicksToGather("adult"));
    }

    [Fact]
    public void At_very_fast_a_colonist_of_59_grows_old_and_dies_of_old_age_before_101()
    {
        var (simulation, colonist) = Lonely(ColonyTestBed.Content, seed: 3);
        var events = new List<ISimEvent>();

        var days = RunUntilGone(simulation, colonist, events);

        days.ShouldBeInRange(0, (101 - 59) * DaysPerYear / 8);
        var notices = events.OfType<NotificationEvent>().Select(notice => notice.Text).ToList();
        notices.ShouldContain(text => text.Key == "event.life.stage.elder");
        var death = notices.Single(text => text.Key == LifeSystem.OldAgeDeathKey);
        death.Args["colonist"].ShouldBe("Elvira");
        ((int)death.Args["years"]).ShouldBeInRange(60, 100);
        events.OfType<ColonyLostEvent>().Count().ShouldBe(1);
    }

    [Fact]
    public void With_no_chance_in_the_table_nobody_dies_of_old_age()
    {
        var json = ColonyTestBed.ContentJson
            .Replace("\"percentPerYear\": 100 }", "\"percentPerYear\": 0 }", StringComparison.Ordinal)
            .Replace("\"percentPerYear\": 40 }", "\"percentPerYear\": 0 }", StringComparison.Ordinal)
            .Replace("\"percentPerYear\": 15 }", "\"percentPerYear\": 0 }", StringComparison.Ordinal)
            .Replace("\"percentPerYear\": 4 }", "\"percentPerYear\": 0 }", StringComparison.Ordinal)
            .Replace("\"percentPerYear\": 1 }", "\"percentPerYear\": 0 }", StringComparison.Ordinal);
        json.ShouldNotContain("\"percentPerYear\": 1");
        var (simulation, colonist) = Lonely(TestContent.WithDefs(json), seed: 3);

        RunUntilGone(simulation, colonist, []).ShouldBe(-1);

        LifeRules.AgeYears(simulation, colonist)!.Value.ShouldBeGreaterThanOrEqualTo(101);
    }

    [Fact]
    public void The_same_seed_gives_the_same_deaths()
    {
        static List<long> DeathTicks(ulong seed)
        {
            var (simulation, _) = Lonely(ColonyTestBed.Content, seed);
            for (var i = 0; i < 9; i++)
            {
                AddElder(simulation, $"Elder {i}");
            }

            var deaths = new List<long>();
            var events = new List<ISimEvent>();
            while (simulation.World.Store<Colonist>().Count > 0 && simulation.CurrentTick < 500L * simulation.Time.TicksPerDay)
            {
                simulation.Run(simulation.Time.TicksPerDay);
                events.Clear();
                simulation.Events.DrainTo(events);
                deaths.AddRange(events.OfType<NotificationEvent>().Where(notice => notice.Text.Key == LifeSystem.OldAgeDeathKey).Select(notice => notice.Tick));
            }

            return deaths;
        }

        var deaths = DeathTicks(11);

        deaths.Count.ShouldBe(10);
        deaths.Distinct().Count().ShouldBeGreaterThan(1);
        DeathTicks(11).ShouldBe(deaths);
    }

    [Theory]
    [InlineData(59, 1, 0)]
    [InlineData(60, 1, 166)]
    [InlineData(65, 8, 3_333)]
    [InlineData(95, 4, 46_666)]
    [InlineData(100, 1, LifeRules.Certain)]
    [InlineData(104, 1, LifeRules.Certain)]
    public void The_chance_of_dying_of_old_age_follows_the_table(int years, int lifeDaysPerDay, int perMillion)
    {
        var rules = LifeRules.From(ColonyTestBed.Content);

        // At 65, half way from 1 % to 4 %: 2.5 % a year, times 8 days of life over 60.
        rules.OldAgeChance(years * DaysPerYear, lifeDaysPerDay, DaysPerYear).ShouldBe(perMillion);
    }

    [Fact]
    public void The_stage_of_an_age_is_the_last_one_it_reached()
    {
        var rules = LifeRules.From(ColonyTestBed.Content);

        rules.StageAt(0, DaysPerYear).Id.ShouldBe("baby");
        rules.StageAt((3 * DaysPerYear) - 1, DaysPerYear).Id.ShouldBe("baby");
        rules.StageAt(3 * DaysPerYear, DaysPerYear).Id.ShouldBe("child");
        rules.StageAt(17 * DaysPerYear, DaysPerYear).Id.ShouldBe("adolescent");
        rules.StageAt(18 * DaysPerYear, DaysPerYear).Id.ShouldBe("adult");
        rules.StageAt(99 * DaysPerYear, DaysPerYear).Id.ShouldBe("elder");
    }

    [Theory]
    [InlineData("""{ "type": "LifeStage", "id": "teen", "fromYears": 13, "work": "light" }""", "start at 13 years")]
    [InlineData("""{ "type": "LifePace", "id": "slow", "lifeDaysPerDay": 1, "default": true }""", "must be the default, found 2")]
    public void Clashing_content_of_life_is_refused(string extra, string fragment)
    {
        var content = TestContent.WithDefs(ColonyTestBed.ContentJson.TrimEnd().TrimEnd(']') + ", " + extra + "]");

        Should.Throw<InvalidOperationException>(() => LifeRules.From(content)).Message.ShouldContain(fragment);
    }

    /// <summary>A simulation with only the life system, at very fast, and one colonist of 59 who never gets hungry.</summary>
    private static (Simulation Simulation, EntityId Colonist) Lonely(DefDatabase content, ulong seed)
    {
        var simulation = new Simulation(seed, content) { LifePace = "very_fast" };
        simulation.AddSystem(
            new LifeSystem(LifeRules.From(content), ColonyRules.From(content)), simulation.Time.TicksPerDay, LifeSystem.MidnightOffset(simulation.Time));
        return (simulation, AddElder(simulation, "Elvira"));
    }

    private static EntityId AddElder(Simulation simulation, string name)
    {
        var colonist = simulation.World.CreateEntity();
        simulation.World.Add(colonist, new Position());
        simulation.World.Add(colonist, new Colonist { Name = name, Gender = Gender.Female, Creature = "human" });
        simulation.World.Add(colonist, new Life { AgeDays = 59 * DaysPerYear, Stage = "adult" });
        return colonist;
    }

    /// <summary>Runs day by day until the colonist is gone or 101 years old: the days it lived, or -1 if it did not die.</summary>
    private static int RunUntilGone(Simulation simulation, EntityId colonist, List<ISimEvent> events)
    {
        for (var day = 0; day < 400; day++)
        {
            simulation.Run(simulation.Time.TicksPerDay);
            simulation.Events.DrainTo(events);
            if (!simulation.World.IsAlive(colonist))
            {
                return day;
            }

            if (LifeRules.AgeYears(simulation, colonist) >= 101)
            {
                return -1;
            }
        }

        throw new InvalidOperationException("The colonist neither died nor reached 101.");
    }
}
