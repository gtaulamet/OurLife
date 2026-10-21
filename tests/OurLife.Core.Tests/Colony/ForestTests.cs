using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;

namespace OurLife.Core.Tests.Colony;

/// <summary>
/// The forest that comes back (L4b of IP3.5, "Bosque que vuelve" in <c>docs/gdd/libre-albedrio.md</c>).
/// The test trees seed every day and grow in two days, so a few days are enough.
/// </summary>
public class ForestTests
{
    private const int Size = 20;

    // The map is rock all through, so the rock is the soil; the floor is on level 1, four levels under the valley floor.
    private const string WorldGen = """
        { "type": "WorldGen", "id": "test_valley", "width": 32, "height": 32, "depth": 16,
          "valleyFloor": 5, "hillHeight": 4, "valleyHalfWidth": 4, "valleyMeander": 2,
          "terrainNoiseScale": 12, "terrainAmplitude": 2, "detailNoiseScale": 6, "forestNoiseScale": 8,
          "soilMaterial": "rock", "soilDepthMin": 1, "soilDepthMax": 2, "rockExposureElevation": 4, "rockExposureWobble": 1,
          "strata": [{ "material": "rock" }], "strataWobble": 1, "grassCoverage": 5000, "groveFactorMax": 2,
          "vegetation": [{ "plant": "tree", "chance": 100, "minElevation": -10, "maxElevation": 10 }] }
        """;

    private const string Tree = """{ "type": "Plant", "id": "tree", "visual": "plant.tree", "blocksMovement": true,""";

    private static readonly DefDatabase Content = TestContent.WithDefs(
        ColonyTestBed.ContentJson.Replace(
            """{ "type": "Plant", "id": "tree", "visual": "plant.tree", "blocksMovement": true,""",
            Tree + """ "regrowth": { "seedChanceBasisPoints": 10000, "seedRadius": 3, "growDays": 2, "maxCoverPerMille": 35 },""",
            StringComparison.Ordinal)
        .TrimEnd().TrimEnd(']') + ", " + WorldGen + "]");

    [Fact]
    public void A_cleared_valley_gets_its_forest_back_and_never_passes_the_cap()
    {
        var bed = Bed();
        var cap = Size * Size * 35 / 1000;
        bed.AddPlant("tree", 10, 10, 1);

        for (var day = 0; day < 40; day++)
        {
            RunDays(bed, 1);
            Cover(bed).ShouldBeLessThanOrEqualTo(cap);
        }

        Cover(bed).ShouldBe(cap);
        Saplings(bed).Count.ShouldBeLessThan(cap);
    }

    [Fact]
    public void A_sapling_blocks_nothing_and_cannot_be_ordered_to_be_felled_until_it_grows()
    {
        var bed = Bed();
        bed.AddPlant("tree", 10, 10, 1);
        RunDays(bed, 1);

        var sapling = Saplings(bed).ShouldHaveSingleItem();
        var cell = bed.World.Get<Position>(sapling).Cell;
        bed.Map.IsStandable(cell).ShouldBeTrue();
        bed.Designate(DesignationKind.Chop, cell, cell);
        bed.Simulation.Run(1);
        bed.World.Has<ChopOrder>(sapling).ShouldBeFalse();

        RunDays(bed, 2);

        bed.World.Has<Sapling>(sapling).ShouldBeFalse();
        bed.Map.IsStandable(cell).ShouldBeFalse();
    }

    [Fact]
    public void A_sapling_waits_while_something_stands_on_its_cell()
    {
        var bed = Bed();
        bed.AddPlant("tree", 10, 10, 1);
        RunDays(bed, 1);
        var sapling = Saplings(bed).ShouldHaveSingleItem();
        var cell = bed.World.Get<Position>(sapling).Cell;
        bed.AddItems("log", 1, cell.X, cell.Y, cell.Z);

        RunDays(bed, 2);

        bed.World.Has<Sapling>(sapling).ShouldBeTrue();
        bed.Map.IsStandable(cell).ShouldBeTrue();
    }

    [Fact]
    public void No_sapling_appears_on_a_cell_with_an_order_or_a_plant()
    {
        var bed = Bed();
        bed.AddPlant("tree", 10, 10, 1);
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (x != 10 || y != 10)
                {
                    bed.Map.SetFlags(x, y, 1, bed.Map.FlagsAt(x, y, 1) | TileFlags.StairsDesignation);
                }
            }
        }

        RunDays(bed, 5);

        Saplings(bed).ShouldBeEmpty();
        Cover(bed).ShouldBe(1);
    }

    [Fact]
    public void The_same_seed_gives_the_same_forest()
    {
        var first = Bed(7);
        var second = Bed(7);
        var other = Bed(8);
        foreach (var bed in new[] { first, second, other })
        {
            bed.AddPlant("tree", 10, 10, 1);
            RunDays(bed, 6);
        }

        first.Simulation.ComputeStateHash().ShouldBe(second.Simulation.ComputeStateHash());
        first.Simulation.ComputeStateHash().ShouldNotBe(other.Simulation.ComputeStateHash());
    }

    private static ColonyTestBed Bed(ulong seed = 1) => ColonyTestBed.Flat(Size, seed, Content);

    private static void RunDays(ColonyTestBed bed, int days) =>
        bed.Simulation.Run(days * bed.Simulation.Time.TicksPerDay);

    private static int Cover(ColonyTestBed bed)
    {
        var plants = new List<EntityId>();
        bed.World.Query<Plant>(plants);
        return plants.Count;
    }

    private static List<EntityId> Saplings(ColonyTestBed bed)
    {
        var saplings = new List<EntityId>();
        bed.World.Query<Sapling>(saplings);
        return saplings;
    }
}
