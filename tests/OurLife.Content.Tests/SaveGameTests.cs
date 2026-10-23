using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Save;
using OurLife.Core.Sim;
using OurLife.Core.WorldGen;

namespace OurLife.Content.Tests;

/// <summary>
/// Acceptance of H2.1 with the real content (<c>docs/gdd/guardado.md</c>): saving a generated
/// valley halfway and going on gives the same game as never stopping.
/// </summary>
public class SaveGameTests
{
    private static readonly SaveInfo Info = new("content-test", IsAutosave: false, "test", DateTimeOffset.UnixEpoch);

    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    public void Saving_halfway_and_going_on_gives_the_same_game_as_never_stopping(ulong seed)
    {
        var uninterrupted = NewColonyAtWork(seed);
        uninterrupted.Run(20_000);
        var arrival = uninterrupted.World.Get<Position>(Colonists(uninterrupted)[0]).Cell;
        uninterrupted.Commands.Enqueue(new DesignateCommand(DesignationKind.Chop, arrival with { X = arrival.X - 30 }, arrival with { X = arrival.X + 30 }));

        var output = new MemoryStream();
        SaveGame.Write(output, uninterrupted, Info);
        var loaded = SaveGame.Load(new MemoryStream(output.ToArray()), GameContent.Defs);
        ColonySystems.AddTo(loaded);

        loaded.ComputeStateHash().ShouldBe(uninterrupted.ComputeStateHash());
        uninterrupted.Run(20_000);
        loaded.Run(20_000);
        loaded.ComputeStateHash().ShouldBe(uninterrupted.ComputeStateHash());
    }

    [Theory]
    [InlineData(7UL)]
    [InlineData(42UL)]
    public void Saving_a_colony_with_piles_of_quality_and_freshness_halfway_gives_the_same_game_as_never_stopping(ulong seed)
    {
        var uninterrupted = NewColonyAtWork(seed);
        var arrival = uninterrupted.World.Get<Position>(Colonists(uninterrupted)[0]).Cell;

        // Piles of every quality and stale berries, loose near the stockpile: haulers lift, carry and store them as the game goes on.
        ItemPiles.Drop(uninterrupted, "plank", 12, arrival with { X = arrival.X - 4 }, "good");
        ItemPiles.Drop(uninterrupted, "plank", 7, arrival with { X = arrival.X - 3 }, "poor");
        ItemPiles.Drop(uninterrupted, "berries", 30, arrival with { X = arrival.X - 5, Y = arrival.Y + 1 }, freshness: 3_000);
        ItemPiles.Drop(uninterrupted, "berries", 30, arrival with { X = arrival.X - 5, Y = arrival.Y + 2 }, "good", 6_500);

        uninterrupted.Run(400);
        var output = new MemoryStream();
        SaveGame.Write(output, uninterrupted, Info);
        var loaded = SaveGame.Load(new MemoryStream(output.ToArray()), GameContent.Defs);
        ColonySystems.AddTo(loaded);

        loaded.ComputeStateHash().ShouldBe(uninterrupted.ComputeStateHash());
        uninterrupted.Run(3_000);
        loaded.Run(3_000);
        loaded.ComputeStateHash().ShouldBe(uninterrupted.ComputeStateHash());

        // Nothing was lost or turned into another quality on the way: the planks of the arrival are normal.
        Stocks.Count(loaded, "plank").ShouldBe(16 + 12 + 7);
        Stocks.Count(loaded, "plank", "good").ShouldBe(12);
        Stocks.Count(loaded, "plank", "normal").ShouldBe(16 + 12);
        Stocks.Count(loaded, "plank", "poor").ShouldBe(35);
        Stocks.Totals(loaded).Where(line => line.Item.Id == "plank").Select(line => (line.Quality, line.Count))
            .ShouldBe([("poor", 7), (null, 16), ("good", 12)]);
    }

    [Theory]
    [InlineData(7UL)]
    [InlineData(42UL)]
    public void Saving_a_colony_with_a_workshop_in_use_and_orders_halfway_gives_the_same_game_as_never_stopping(ulong seed)
    {
        var uninterrupted = NewColonyAtWork(seed);
        var arrival = uninterrupted.World.Get<Position>(Colonists(uninterrupted)[0]).Cell;

        // The arrival brings 16 planks: a carpenter's bench, a bed and a table of planks, and orders for planks and handles.
        var (from, to) = (new TileCoord(arrival.X + 1, arrival.Y - 6, arrival.Z), new TileCoord(arrival.X + 6, arrival.Y + 6, arrival.Z));
        var bench = GameContent.Defs.Get<BuildableDef>("carpenter_bench");
        var cells = Construction.CellsToPlan(uninterrupted, bench, from, to);
        cells.Count.ShouldBeGreaterThan(2);
        uninterrupted.Commands.Enqueue(new PlaceBlueprintCommand("carpenter_bench", "planks", cells[0], cells[0]));
        uninterrupted.Commands.Enqueue(new PlaceBlueprintCommand("bed", "planks", cells[^1], cells[^1]));
        uninterrupted.Commands.Enqueue(new CreateWorkOrderCommand("plank", WorkOrderMode.Count, 20));
        uninterrupted.Commands.Enqueue(new CreateWorkOrderCommand("handle", WorkOrderMode.Count, 4));
        uninterrupted.Commands.Enqueue(new CreateWorkOrderCommand("plank", WorkOrderMode.Keep, 30));
        uninterrupted.Commands.Enqueue(new CreateWorkOrderCommand("handle", WorkOrderMode.Forever, 0));
        uninterrupted.Commands.Enqueue(new SetResearchQueueCommand(["stonecutting", "agriculture"]));

        // Until the workshop holds delivered ingredients: the save catches a craft under way.
        var world = uninterrupted.World;
        var shops = new List<Core.Ecs.EntityId>();
        for (var tick = 0; tick < 10_000 && !shops.Any(shop => world.Get<Workshop>(shop).Inputs.Count > 0); tick += 10)
        {
            uninterrupted.Run(10);
            world.Query<Workshop>(shops);
        }

        shops.Count.ShouldBe(1);
        world.Get<Workshop>(shops[0]).Inputs.ShouldNotBeEmpty();
        world.Store<WorkOrder>().Count.ShouldBe(4);
        var output = new MemoryStream();
        SaveGame.Write(output, uninterrupted, Info);
        var loaded = SaveGame.Load(new MemoryStream(output.ToArray()), GameContent.Defs);
        ColonySystems.AddTo(loaded);

        loaded.ComputeStateHash().ShouldBe(uninterrupted.ComputeStateHash());
        uninterrupted.Run(10_000);
        loaded.Run(10_000);
        loaded.ComputeStateHash().ShouldBe(uninterrupted.ComputeStateHash());
        var orders = new List<Core.Ecs.EntityId>();
        loaded.World.Query<WorkOrder>(orders);
        orders.Select(order => loaded.World.Get<WorkOrder>(order).Done).Sum().ShouldBeGreaterThan(0);
        loaded.Progress.ResearchQueue.ShouldBe(uninterrupted.Progress.ResearchQueue);
        loaded.Progress.Points.Keys.ShouldBe(uninterrupted.Progress.Points.Keys);
    }

    [Fact]
    public void A_saved_valley_is_small()
    {
        var simulation = NewColonyAtWork(1);
        var output = new MemoryStream();

        SaveGame.Write(output, simulation, Info);

        output.Length.ShouldBeLessThan(1_000_000);
    }

    /// <summary>A new colony with orders to harvest and chop around its arrival, on every level, and a stockpile beside it.</summary>
    private static Simulation NewColonyAtWork(ulong seed)
    {
        var simulation = new Simulation(seed, GameContent.Defs);
        WorldGenerator.Generate(simulation, GameContent.Defs.All<WorldGenDef>()[0]);
        var colonists = ColonyStart.Settle(simulation, ColonyStart.TheOnlyScenario(GameContent.Defs), "Valdeolmos");
        ColonySystems.AddTo(simulation);

        var arrival = simulation.World.Get<Position>(colonists[0]).Cell;
        var top = simulation.Map.Size.Depth - 1;
        simulation.Commands.Enqueue(new DesignateCommand(
            DesignationKind.Harvest, new TileCoord(arrival.X - 20, arrival.Y - 20, 0), new TileCoord(arrival.X + 20, arrival.Y + 20, top)));
        simulation.Commands.Enqueue(new DesignateCommand(
            DesignationKind.Chop, new TileCoord(arrival.X - 8, arrival.Y - 8, 0), new TileCoord(arrival.X + 8, arrival.Y + 8, top)));
        simulation.Commands.Enqueue(new DesignateCommand(
            DesignationKind.Stairs, arrival with { X = arrival.X + 3 }, arrival with { X = arrival.X + 3 }));
        simulation.Commands.Enqueue(new DesignateCommand(
            DesignationKind.Stockpile, new TileCoord(arrival.X - 6, arrival.Y + 2, arrival.Z), new TileCoord(arrival.X - 2, arrival.Y + 5, arrival.Z)));
        return simulation;
    }

    private static List<Core.Ecs.EntityId> Colonists(Simulation simulation)
    {
        var colonists = new List<Core.Ecs.EntityId>();
        simulation.World.Query<Colonist>(colonists);
        return colonists;
    }
}
