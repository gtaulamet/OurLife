using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Core.WorldGen;

namespace OurLife.Content.Tests;

/// <summary>
/// The balance of H3.10 (see "Balance de población y comida" in <c>docs/gdd/vida-y-generaciones.md</c>):
/// a long game at the default pace, with a player who keeps the bushes around the village ordered,
/// keeps its colonists fed through every winter. Slow: ten game years.
/// </summary>
public class BalanceTests
{
    private const int GatherRadius = 25;

    [Fact]
    [Trait("Category", "Slow")]
    public void In_ten_years_at_the_default_pace_a_village_that_gathers_never_starves()
    {
        var simulation = new Simulation(1, GameContent.Defs);
        WorldGenerator.Generate(simulation, GameContent.Defs.All<WorldGenDef>()[0]);
        var colonists = ColonyStart.Settle(simulation, ColonyStart.TheOnlyScenario(GameContent.Defs), "Valdeolmos");
        ColonySystems.AddTo(simulation);
        var arrival = simulation.World.Get<Position>(colonists[0]).Cell;
        var top = simulation.Map.Size.Depth - 1;
        simulation.Commands.Enqueue(new DesignateCommand(
            DesignationKind.Stockpile, arrival with { X = arrival.X - 6, Y = arrival.Y + 2 }, arrival with { X = arrival.X - 2, Y = arrival.Y + 5 }));
        var events = new List<ISimEvent>();
        var starved = 0;
        var born = 0;

        for (var day = 1; day <= 10 * simulation.Time.DaysPerYear; day++)
        {
            // The order of a bush ends when it is gathered: a player orders them again.
            if (day % 2 == 0)
            {
                simulation.Commands.Enqueue(new DesignateCommand(
                    DesignationKind.Harvest,
                    new TileCoord(arrival.X - GatherRadius, arrival.Y - GatherRadius, 0),
                    new TileCoord(arrival.X + GatherRadius, arrival.Y + GatherRadius, top)));
            }

            simulation.Run(simulation.Time.TicksPerDay);
            events.Clear();
            simulation.Events.DrainTo(events);
            starved += events.OfType<NotificationEvent>().Count(notice => notice.Text.Key == NeedsSystem.DeathKey(GameContent.Defs.Get<NeedDef>("hunger")));
            born += events.OfType<NotificationEvent>().Count(notice => notice.Text.Key == BirthSystem.BirthKey);
        }

        starved.ShouldBe(0);
        born.ShouldBeGreaterThan(0);
        simulation.World.Store<Colonist>().Count.ShouldBeGreaterThanOrEqualTo(5);
    }
}
