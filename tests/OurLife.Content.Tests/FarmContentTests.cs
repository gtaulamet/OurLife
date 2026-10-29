using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Save;
using OurLife.Core.Sim;
using OurLife.Core.WorldGen;

namespace OurLife.Content.Tests;

/// <summary>The real content of O6 (<c>docs/gdd/agricultura.md</c>): crops, spoilage, and criteria 13 and 15.</summary>
[Collection(TimingCollection.Name)]
public class FarmContentTests
{
    [Fact]
    public void The_crops_are_the_five_of_the_spec_and_need_knowledge_that_exists()
    {
        var crops = GameContent.Defs.All<CropDef>().ToDictionary(crop => crop.Id);

        crops.Keys.Order().ShouldBe(["cabbage", "oak_grove", "peas", "pine_grove", "wheat"]);
        crops["wheat"].Requires.ShouldBe(["cereals"]);
        crops["peas"].Requires.ShouldBe(["agriculture"]);
        crops["cabbage"].Requires.ShouldBe(["agriculture"]);
        crops["oak_grove"].Requires.ShouldBe(["silviculture"]);
        (crops["wheat"].Count, crops["wheat"].GrowDays, crops["wheat"].SowTicks, crops["wheat"].ReapTicks).ShouldBe((8, 10, 40, 60));
        (crops["peas"].Count, crops["peas"].GrowDays).ShouldBe((6, 8));
        (crops["cabbage"].Count, crops["cabbage"].GrowDays).ShouldBe((5, 8));
        crops.Values.Where(crop => crop.Item is not null).ShouldAllBe(crop => crop.GrowSeasons.SequenceEqual(new[] { "spring", "summer", "autumn" }));
        foreach (var crop in crops.Values)
        {
            crop.Era.ShouldBe("medieval");
            foreach (var knowledge in crop.Requires) { GameContent.Defs.Get<KnowledgeDef>(knowledge).ShouldNotBeNull(); }
        }

        // The trees a grove plants spread by themselves, so their saplings grow up.
        foreach (var grove in crops.Values.Where(crop => crop.IsTree))
        {
            GameContent.Defs.Get<PlantDef>(grove.Tree!).Regrowth.ShouldNotBeNull();
            grove.Spacing.ShouldBe(2);
        }
    }

    [Fact]
    public void Spoilage_and_the_food_of_the_spec_are_in_the_content()
    {
        var config = GameContent.Defs.All<SpoilageConfigDef>().ShouldHaveSingleItem();
        (config.CoveredPercent, config.RotNoticeMinUnits).ShouldBe((50, 5));
        var items = GameContent.Defs.All<ItemDef>().ToDictionary(item => item.Id);
        new[] { ("berries", 60), ("cabbage", 30), ("peas", 90), ("wheat", 200), ("flour", 90), ("bread", 20), ("pottage", 6), ("pickled_cabbage", 150), ("provisions", 0) }
            .ShouldAllBe(entry => items[entry.Item1].SpoilDays == entry.Item2);
        GameContent.Defs.All<AiConfigDef>().Single().EatChoiceRadius.ShouldBe(15);
        MoodRules.ConfigOf(GameContent.Defs)!.MealThought.ShouldBe("ate_meal");
    }

    // Criterion 13.
    [Fact]
    public void Saving_a_colony_with_a_field_half_grown_gives_the_same_game_as_never_stopping()
    {
        var uninterrupted = NewColony(7);
        var arrival = uninterrupted.World.Get<Position>(Colonists(uninterrupted)[0]).Cell;
        var cells = FarmRules.CellsToZone(uninterrupted, arrival with { X = arrival.X + 2, Y = arrival.Y - 12 }, arrival with { X = arrival.X + 9, Y = arrival.Y - 5 });
        cells.Count.ShouldBeGreaterThan(10);
        var world = uninterrupted.World;
        var zone = world.CreateEntity();
        world.Add(zone, new FarmZone { Crop = "wheat", Cells = [.. cells] });
        foreach (var (cell, index) in cells.Select((cell, index) => (cell, index)).Where(entry => entry.index % 2 == 0))
        {
            var crop = world.CreateEntity();
            world.Add(crop, new Position { X = cell.X, Y = cell.Y, Z = cell.Z });
            world.Add(crop, new Crop { Def = "wheat", Days = 1 + (index % 8) });
        }

        ItemPiles.Drop(uninterrupted, "berries", 40, arrival with { X = arrival.X - 7 }, freshness: 4_000);
        uninterrupted.Run(2_000);

        var output = new MemoryStream();
        SaveGame.Write(output, uninterrupted, new SaveInfo("content-test", IsAutosave: false, "test", DateTimeOffset.UnixEpoch));
        var loaded = SaveGame.Load(new MemoryStream(output.ToArray()), GameContent.Defs);
        ColonySystems.AddTo(loaded);

        loaded.ComputeStateHash().ShouldBe(uninterrupted.ComputeStateHash());
        uninterrupted.Run(6_000);
        loaded.Run(6_000);
        loaded.ComputeStateHash().ShouldBe(uninterrupted.ComputeStateHash());
        loaded.World.Store<Crop>().Count.ShouldBeGreaterThan(0);
        loaded.World.Store<FarmZone>().Count.ShouldBe(1);
    }

    // Criterion 15.
    [Fact]
    public void A_field_of_300_cells_and_200_piles_with_freshness_cost_less_than_a_tenth_of_a_millisecond_a_tick()
    {
        var simulation = NewColony(1);
        var arrival = simulation.World.Get<Position>(Colonists(simulation)[0]).Cell;
        var world = simulation.World;
        var cells = new List<TileCoord>();
        for (var y = arrival.Y - 30; y < arrival.Y - 5 && cells.Count < 300; y++)
        {
            for (var x = arrival.X - 15; x < arrival.X + 15 && cells.Count < 300; x++)
            {
                cells.AddRange(FarmRules.CellsToZone(simulation, new TileCoord(x, y, arrival.Z), new TileCoord(x, y, arrival.Z)));
            }
        }

        var zone = world.CreateEntity();
        world.Add(zone, new FarmZone { Crop = "wheat", Cells = [.. cells.Take(300)] });
        foreach (var cell in cells.Take(300))
        {
            var crop = world.CreateEntity();
            world.Add(crop, new Position { X = cell.X, Y = cell.Y, Z = cell.Z });
            world.Add(crop, new Crop { Def = "wheat", Days = 3 });
        }

        for (var pile = 0; pile < 200; pile++)
        {
            ItemPiles.Drop(simulation, "berries", 10, arrival with { X = arrival.X - 20 + (pile % 20), Y = arrival.Y + 3 + (pile / 20) }, freshness: 9_000 - (pile * 10));
        }

        simulation.Run(500);
        // Six rounds (and the warm-up one) of a whole game day each, so every round crosses a midnight and carries the daily sweeps
        // and the growth of the crops; the cheapest counts, so a busy machine does not fail what the code does not cost.
        var ticks = simulation.Time.TicksPerDay;
        var best = Timing.BestRoundMs(6, _ => simulation.Run(ticks)) / ticks;

        best.ShouldBeLessThan(0.1);
    }

    private static Simulation NewColony(ulong seed)
    {
        var simulation = new Simulation(seed, GameContent.Defs);
        WorldGenerator.Generate(simulation, GameContent.Defs.All<WorldGenDef>()[0]);
        ColonyStart.Settle(simulation, ColonyStart.TheOnlyScenario(GameContent.Defs), "Valdeolmos");
        ColonySystems.AddTo(simulation);
        return simulation;
    }

    private static List<Core.Ecs.EntityId> Colonists(Simulation simulation)
    {
        var colonists = new List<Core.Ecs.EntityId>();
        simulation.World.Query<Colonist>(colonists);
        return colonists;
    }
}
