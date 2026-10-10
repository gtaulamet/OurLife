using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Map;

/// <summary>
/// The connectivity a simulation keeps up to date in place (<see cref="Connectivity.OpenWays"/>)
/// always groups the cells exactly as computing it again would.
/// </summary>
public class IncrementalConnectivityTests
{
    private const int Size = 12;
    private const int Depth = 4;

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void Opening_ways_one_by_one_gives_the_same_regions_as_computing_again(ulong seed)
    {
        var random = new Pcg32(seed, 7);
        var simulation = new Simulation(seed, TestContent.Minimal);
        var map = RandomCaves(random);
        simulation.AttachMap(map);
        _ = simulation.Connectivity;

        for (var step = 0; step < 200; step++)
        {
            OpenSomething(map, random);
            SamePartition(simulation.Connectivity, Connectivity.Compute(map));
        }
    }

    [Fact]
    public void A_change_that_closes_a_way_computes_again()
    {
        var simulation = new Simulation(1, TestContent.Minimal);
        var map = AsciiMap.Parse("###", "...");
        simulation.AttachMap(map);
        simulation.Connectivity.AreConnected(new TileCoord(0, 0, 1), new TileCoord(2, 0, 1)).ShouldBeTrue();

        map.SetFlags(1, 0, 1, TileFlags.Blocked);

        simulation.Connectivity.AreConnected(new TileCoord(0, 0, 1), new TileCoord(2, 0, 1)).ShouldBeFalse();
    }

    private static GameMap RandomCaves(Pcg32 random)
    {
        var map = new GameMap(new MapSize(Size, Size, Depth), []);
        for (var z = 0; z < Depth; z++)
        {
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var shape = z == 0 || random.Chance(6000) ? TileShape.Wall : random.Chance(1500) ? TileShape.Ramp : TileShape.Floor;
                    var flags = shape == TileShape.Floor && random.Chance(1500) ? TileFlags.Blocked : TileFlags.None;
                    map.SetTileQuietly(x, y, z, shape, GameMap.NoMaterial, flags);
                }
            }
        }

        return map;
    }

    /// <summary>Digs a wall, frees a blocked cell or digs stairs down, like the colonists do.</summary>
    private static void OpenSomething(GameMap map, Pcg32 random)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var x = random.NextInt(Size);
            var y = random.NextInt(Size);
            var z = random.NextInt(1, Depth);
            var shape = map.ShapeAt(x, y, z);
            var flags = map.FlagsAt(x, y, z);
            switch (random.NextInt(3))
            {
                case 0 when shape == TileShape.Wall:
                    map.SetTile(x, y, z, TileShape.Floor, GameMap.NoMaterial, flags);
                    return;
                case 1 when (flags & TileFlags.Blocked) != 0:
                    map.SetFlags(x, y, z, flags & ~TileFlags.Blocked);
                    return;
                case 2 when shape == TileShape.Floor && (flags & TileFlags.Blocked) == 0 && map.ShapeAt(x, y, z - 1) == TileShape.Wall:
                    map.SetTile(x, y, z, TileShape.StairDown, GameMap.NoMaterial, flags);
                    map.SetTile(x, y, z - 1, TileShape.StairUp, GameMap.NoMaterial, TileFlags.None);
                    return;
            }
        }
    }

    /// <summary>Region numbers may differ; which cells share a region may not.</summary>
    private static void SamePartition(Connectivity kept, Connectivity fresh)
    {
        var keptToFresh = new Dictionary<int, int>();
        var freshToKept = new Dictionary<int, int>();
        for (var z = 0; z < Depth; z++)
        {
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var a = kept.RegionAt(x, y, z);
                    var b = fresh.RegionAt(x, y, z);
                    keptToFresh.TryAdd(a, b);
                    freshToKept.TryAdd(b, a);
                    keptToFresh[a].ShouldBe(b, $"({x}, {y}, {z})");
                    freshToKept[b].ShouldBe(a, $"({x}, {y}, {z})");
                }
            }
        }

        kept.RegionCount.ShouldBe(fresh.RegionCount);
    }
}
