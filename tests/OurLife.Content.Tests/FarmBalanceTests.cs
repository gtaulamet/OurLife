using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Core.WorldGen;

namespace OurLife.Content.Tests;

/// <summary>
/// The balance of O6 (criterion 12 of <c>docs/gdd/agricultura.md</c>): ten game years at the
/// default pace, with a player who keeps the bushes around the village ordered and, in the second
/// year, draws a field of wheat of 8 × 8, builds a kitchen and orders to keep bread. Nobody starves,
/// at least as many children are born as without the field, and the village grows past eight.
/// Slow: two games of ten years for each seed.
/// </summary>
public class FarmBalanceTests
{
    private const int GatherRadius = 25;

    private sealed record Decade(int Starved, int Born, int Population, int Bread, int Rotten);

    [Theory]
    [Trait("Category", "Slow")]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    public void In_ten_years_a_village_with_a_field_never_starves_has_as_many_children_and_passes_eight(ulong seed)
    {
        var without = Play(seed, field: false);
        var with = Play(seed, field: true);

        without.Starved.ShouldBe(0);
        with.Starved.ShouldBe(0, $"seed {seed}: {with}");
        with.Born.ShouldBeGreaterThanOrEqualTo(without.Born, $"seed {seed}: with {with}, without {without}");
        with.Population.ShouldBeGreaterThan(8, $"seed {seed}: with {with}, without {without}");
        with.Bread.ShouldBeGreaterThan(0);
    }

    private static Decade Play(ulong seed, bool field)
    {
        var simulation = new Simulation(seed, GameContent.Defs);
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
        var bread = 0;
        var rotten = 0;
        var farmed = false;
        var deaths = NeedsSystem.DeathKey(GameContent.Defs.Get<NeedDef>("hunger"));

        for (var day = 1; day <= 10 * simulation.Time.DaysPerYear; day++)
        {
            if (day % 2 == 0)
            {
                simulation.Commands.Enqueue(new DesignateCommand(
                    DesignationKind.Harvest,
                    new TileCoord(arrival.X - GatherRadius, arrival.Y - GatherRadius, 0),
                    new TileCoord(arrival.X + GatherRadius, arrival.Y + GatherRadius, top)));
            }

            if (field)
            {
                Learn(simulation, arrival, day);
                if (!farmed && day > simulation.Time.DaysPerYear && simulation.Progress.Knowledge.Contains("milling"))
                {
                    Farm(simulation, arrival);
                    farmed = true;
                }
            }

            simulation.Run(simulation.Time.TicksPerDay);
            events.Clear();
            simulation.Events.DrainTo(events);
            foreach (var notice in events.OfType<NotificationEvent>())
            {
                starved += notice.Text.Key == deaths ? 1 : 0;
                born += notice.Text.Key == BirthSystem.BirthKey ? 1 : 0;
                rotten += notice.Text.Key == SpoilageSystem.SpoiledKey ? 1 : 0;
            }

            bread = Math.Max(bread, Stocks.Count(simulation, "bread"));
        }

        return new Decade(starved, born, simulation.World.Store<Colonist>().Count, bread, rotten);
    }

    /// <summary>
    /// The first days: a scriptorium of planks, and a queue of research for what the field and the
    /// oven need, one at a time as each becomes available.
    /// </summary>
    private static void Learn(Simulation simulation, TileCoord arrival, int day)
    {
        if (day == 1)
        {
            var scriptorium = GameContent.Defs.Get<BuildableDef>("scriptorium");
            var spot = Construction.CellsToPlan(simulation, scriptorium, arrival with { X = arrival.X + 1, Y = arrival.Y - 5 }, arrival with { X = arrival.X + 6, Y = arrival.Y - 1 });
            simulation.Commands.Enqueue(new PlaceBlueprintCommand("scriptorium", "planks", spot[0], spot[0]));
        }

        if (simulation.Progress.ResearchQueue.Count == 0)
        {
            var next = new[] { "agriculture", "cereals", "milling" }.FirstOrDefault(knowledge => ResearchRules.State(simulation, knowledge) == KnowledgeState.Available);
            if (next is not null)
            {
                simulation.Commands.Enqueue(new SetResearchQueueCommand([next]));
            }
        }
    }

    /// <summary>
    /// What the player does once the village knows agriculture, cereals and milling: draws a field of 8 × 8 of wheat on the best soil
    /// near the village, builds a kitchen with stone it mined, and orders flour and bread.
    /// </summary>
    private static void Farm(Simulation simulation, TileCoord arrival)
    {
        TileCoord? best = null;
        var most = 0;
        for (var dy = -16; dy <= 8; dy += 2)
        {
            for (var dx = -16; dx <= 8; dx += 2)
            {
                var corner = new TileCoord(arrival.X + dx, arrival.Y + dy, arrival.Z);
                var cells = FarmRules.CellsToZone(simulation, corner, corner with { X = corner.X + 7, Y = corner.Y + 7 }).Count;
                if (cells > most)
                {
                    (best, most) = (corner, cells);
                }
            }
        }

        simulation.Commands.Enqueue(new PlaceFarmZoneCommand(best!.Value, best.Value with { X = best.Value.X + 7, Y = best.Value.Y + 7 }, "wheat"));

        var kitchen = GameContent.Defs.Get<BuildableDef>("kitchen");
        var spot = Construction.CellsToPlan(simulation, kitchen, arrival with { X = arrival.X + 1 }, arrival with { X = arrival.X + 6, Y = arrival.Y + 4 });
        ItemPiles.Drop(simulation, "stone", 8, arrival);
        simulation.Commands.Enqueue(new PlaceBlueprintCommand("kitchen", "masonry", spot[0], spot[0]));
        simulation.Commands.Enqueue(new CreateWorkOrderCommand("flour", WorkOrderMode.Keep, 12));
        simulation.Commands.Enqueue(new CreateWorkOrderCommand("bread", WorkOrderMode.Keep, 20));
    }
}
