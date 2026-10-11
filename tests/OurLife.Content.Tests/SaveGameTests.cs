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

    [Fact]
    public void A_saved_valley_is_small()
    {
        var simulation = NewColonyAtWork(1);
        var output = new MemoryStream();

        SaveGame.Write(output, simulation, Info);

        output.Length.ShouldBeLessThan(1_000_000);
    }

    /// <summary>A new colony with orders to harvest and chop around its arrival, on every level.</summary>
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
        return simulation;
    }

    private static List<Core.Ecs.EntityId> Colonists(Simulation simulation)
    {
        var colonists = new List<Core.Ecs.EntityId>();
        simulation.World.Query<Colonist>(colonists);
        return colonists;
    }
}
