using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
using OurLife.Core.WorldGen;

namespace OurLife.Content.Tests;

/// <summary>The real content runs a colony (see <c>docs/gdd/colonos-e-ia.md</c>), with every text the colony builds at run time.</summary>
public class ColonyTests
{
    private const int TicksPerDay = 24 * 60;

    [Fact]
    public void The_content_has_the_rules_and_the_only_scenario_of_the_colony()
    {
        Should.NotThrow(() => ColonyRules.From(GameContent.Defs));
        ColonyStart.TheOnlyScenario(GameContent.Defs).Colonists.ShouldBe(5);
    }

    [Fact]
    public void The_content_has_the_rules_of_life_with_rapido_by_default()
    {
        var life = LifeRules.From(GameContent.Defs);

        life.DefaultPace.Id.ShouldBe("fast");
        life.Stages.Select(stage => stage.Id).ShouldBe(new[] { "baby", "child", "adolescent", "adult", "elder" });
        var scenario = ColonyStart.TheOnlyScenario(GameContent.Defs);
        life.StageAt(scenario.FounderMinYears * GameContent.Defs.Time.DaysPerYear, GameContent.Defs.Time.DaysPerYear).Id.ShouldBe("adult");
        GameContent.Defs.Get<NameListDef>("medieval_names").Surnames.Count.ShouldBeGreaterThanOrEqualTo(scenario.Colonists);
    }

    [Fact]
    public void Every_message_of_life_is_in_every_language()
    {
        var stages = LifeRules.From(GameContent.Defs).Stages.Where(stage => stage.FromYears > 0).Select(LifeSystem.GrownKey);
        foreach (var key in stages.Append(LifeSystem.OldAgeDeathKey))
        {
            foreach (var catalog in GameContent.Catalogs.Values)
            {
                catalog.Messages.ContainsKey(key).ShouldBeTrue($"{catalog.Locale}: missing {key}");
                catalog.Messages[key].ArgumentNames.ShouldContain("colonist");
            }
        }
    }

    [Fact]
    public void The_name_of_every_stage_of_life_agrees_with_the_colonist_in_every_language()
    {
        foreach (var stage in GameContent.Defs.All<LifeStageDef>())
        {
            foreach (var catalog in GameContent.Catalogs.Values)
            {
                foreach (var gender in new[] { "female", "male" })
                {
                    var name = catalog.Format(catalog.Messages[stage.NameKey!], new Dictionary<string, object?> { ["gender"] = gender });
                    name.ShouldNotContain("⟦", customMessage: $"{catalog.Locale}: {stage.NameKey} for {gender}");
                    name.ShouldNotBeEmpty();
                }
            }
        }

        var spanish = GameContent.Spanish;
        spanish.Format(spanish.Messages["def.life_stage.elder.name"], new Dictionary<string, object?> { ["gender"] = "female" }).ShouldBe("anciana");
    }

    [Fact]
    public void The_content_has_the_six_traits_of_the_spec_and_diligence_sets_the_speed()
    {
        var personality = PersonalityRules.From(GameContent.Defs);

        personality.Traits.Select(trait => trait.Id).ShouldBe(new[] { "curiosity", "diligence", "intelligence", "kindness", "patience", "sociability" });
        personality.Traits.Where(trait => trait.WorkSpeed is not null).Select(trait => trait.Id).ShouldBe(new[] { "diligence" });
        personality.Config.MaxDriftPerDay.ShouldBe(3);
    }

    [Fact]
    public void The_hud_shows_food_wood_and_stone_in_that_order()
    {
        var simulation = new Simulation(1, GameContent.Defs);

        ColonyStock.ByCategory(simulation).Select(line => line.Category.Id).ShouldBe(new[] { "food", "wood", "stone" });
    }

    [Fact]
    public void Every_job_has_its_report_in_every_language()
    {
        foreach (var kind in Enum.GetValues<JobKind>())
        {
            foreach (var catalog in GameContent.Catalogs.Values)
            {
                catalog.Messages.ContainsKey(JobKinds.ReportKey(kind)).ShouldBeTrue($"{catalog.Locale}: missing {JobKinds.ReportKey(kind)}");
            }
        }
    }

    [Fact]
    public void Every_need_that_kills_has_its_death_message_in_every_language()
    {
        foreach (var need in GameContent.Defs.All<NeedDef>().Where(need => need.DeathAfterHoursAtZero is not null))
        {
            foreach (var catalog in GameContent.Catalogs.Values)
            {
                var key = NeedsSystem.DeathKey(need);
                catalog.Messages.ContainsKey(key).ShouldBeTrue($"{catalog.Locale}: missing {key}");
                catalog.Messages[key].ArgumentNames.ShouldContain("colonist");
            }
        }
    }

    [Fact]
    public void Every_alert_has_its_texts_in_every_language()
    {
        var keys = GameContent.Defs.All<NeedDef>()
            .Where(need => need.AlertWhenCritical)
            .SelectMany(need => new[] { ColonyAlerts.CriticalEventKey(need), ColonyAlerts.CriticalAlertKey(need) })
            .Append(ColonyAlerts.IdleEventKey)
            .Append(ColonyAlerts.IdleAlertKey)
            .ToList();
        keys.Count.ShouldBeGreaterThan(2);
        foreach (var key in keys)
        {
            foreach (var catalog in GameContent.Catalogs.Values)
            {
                catalog.Messages.ContainsKey(key).ShouldBeTrue($"{catalog.Locale}: missing {key}");
                catalog.Messages[key].ArgumentNames.ShouldContain("colonist");
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    public void The_colonists_of_the_real_scenario_are_alive_and_fed_after_three_days(ulong seed)
    {
        var simulation = new Simulation(seed, GameContent.Defs);
        WorldGenerator.Generate(simulation, GameContent.Defs.All<WorldGenDef>()[0]);
        var colonists = ColonyStart.Settle(simulation, ColonyStart.TheOnlyScenario(GameContent.Defs), "Valdeolmos");
        ColonySystems.AddTo(simulation);

        simulation.Run(3 * TicksPerDay);

        var hunger = GameContent.Defs.Get<NeedDef>("hunger");
        foreach (var colonist in colonists)
        {
            simulation.World.IsAlive(colonist).ShouldBeTrue();
            simulation.World.Get<Needs>(colonist).Find(hunger.Id)!.Value.ShouldBeGreaterThan(hunger.CriticalThreshold);
        }
    }

    // H2.2: colonists prefer to sleep at night (docs/gdd/dia-y-noche.md).
    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    public void The_village_sleeps_at_night_and_is_awake_at_noon(ulong seed)
    {
        var (simulation, colonists) = NewColony(seed);

        for (var day = 0; day < 3; day++)
        {
            RunUntilHour(simulation, 2);
            Sleeping(simulation, colonists).ShouldBe(colonists.Count, $"asleep at 02:00 of day {day + 2}");
            RunUntilHour(simulation, 12);
            Sleeping(simulation, colonists).ShouldBe(0, $"asleep at noon of day {day + 2}");
        }
    }

    private static int Sleeping(Simulation simulation, IReadOnlyList<Core.Ecs.EntityId> colonists) =>
        colonists.Count(colonist => simulation.World.TryGet<Job>(colonist, out var job) && job.Kind == JobKind.Sleep);

    /// <summary>Runs whole hours until the clock shows <paramref name="hour"/>:00; the game starts on the hour.</summary>
    private static void RunUntilHour(Simulation simulation, int hour)
    {
        do
        {
            simulation.Run(60);
        }
        while (simulation.CurrentDate.Hour != hour);
    }

    // Balance of IP1 (H1.8): berries do not grow back in winter, so winter is the threat.
    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    public void A_colony_that_never_gathers_starves_in_its_first_winter(ulong seed)
    {
        var (simulation, colonists) = NewColony(seed);

        RunUntil(simulation, year: 1201, season: "spring");

        colonists.Count(simulation.World.IsAlive).ShouldBeLessThan(colonists.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    public void A_colony_that_gathers_in_autumn_lives_through_the_winter(ulong seed)
    {
        var (simulation, colonists) = NewColony(seed);
        var arrival = simulation.World.Get<Position>(colonists[0]).Cell;
        RunUntil(simulation, year: 1200, season: "autumn");

        // A player who orders the bushes around the village gathered every two days of autumn.
        while (simulation.CurrentDate.SeasonId == "autumn")
        {
            simulation.Commands.Enqueue(new DesignateCommand(
                DesignationKind.Harvest,
                new(arrival.X - GatherRadius, arrival.Y - GatherRadius, 0),
                new(arrival.X + GatherRadius, arrival.Y + GatherRadius, simulation.Map.Size.Depth - 1)));
            simulation.Run(2 * TicksPerDay);
        }

        RunUntil(simulation, year: 1201, season: "spring");

        colonists.ShouldAllBe(colonist => simulation.World.IsAlive(colonist));
    }

    private const int GatherRadius = 25;

    private static (Simulation Simulation, IReadOnlyList<Core.Ecs.EntityId> Colonists) NewColony(ulong seed)
    {
        var simulation = new Simulation(seed, GameContent.Defs);
        WorldGenerator.Generate(simulation, GameContent.Defs.All<WorldGenDef>()[0]);
        var colonists = ColonyStart.Settle(simulation, ColonyStart.TheOnlyScenario(GameContent.Defs), "Valdeolmos");
        ColonySystems.AddTo(simulation);
        return (simulation, colonists);
    }

    private static void RunUntil(Simulation simulation, int year, string season)
    {
        while (simulation.CurrentDate.Year < year || simulation.CurrentDate.SeasonId != season)
        {
            simulation.Run(60);
        }
    }
}
