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

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    public void The_colonists_of_the_real_scenario_are_alive_and_fed_after_three_days(ulong seed)
    {
        var simulation = new Simulation(seed, GameContent.Defs);
        WorldGenerator.Generate(simulation, GameContent.Defs.All<WorldGenDef>()[0]);
        var colonists = ColonyStart.Settle(simulation, ColonyStart.TheOnlyScenario(GameContent.Defs));
        ColonySystems.AddTo(simulation);

        simulation.Run(3 * TicksPerDay);

        var hunger = GameContent.Defs.Get<NeedDef>("hunger");
        foreach (var colonist in colonists)
        {
            simulation.World.IsAlive(colonist).ShouldBeTrue();
            simulation.World.Get<Needs>(colonist).Find(hunger.Id)!.Value.ShouldBeGreaterThan(hunger.CriticalThreshold);
        }
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
        var colonists = ColonyStart.Settle(simulation, ColonyStart.TheOnlyScenario(GameContent.Defs));
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
