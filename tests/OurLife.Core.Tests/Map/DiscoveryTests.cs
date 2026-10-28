using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Core.Tests.Colony;

namespace OurLife.Core.Tests.Map;

/// <summary>
/// The fog of the underground (<c>docs/gdd/subsuelo.md</c>, "Niebla subterránea"): the rule of the
/// 27 cells, discovering by digging, and that nothing in the simulation depends on it.
/// </summary>
public class DiscoveryTests
{
    // The rock of this test has a vein of iron, whose ore takes longer to dig out than the rock.
    private static readonly DefDatabase OreContent = TestContent.WithDefs(ColonyTestBed.ContentJson.Replace(
        """{ "type": "Material", "id": "timber",""",
        """
        { "type": "Material", "id": "iron_vein", "kind": "ore", "color": "#8A4B3A", "mineWorkTicks": 200, "minedItem": "iron_ore", "minedItemChance": 10000 },
        { "type": "Item", "id": "iron_ore", "category": "stone", "kind": "raw", "visual": "item.iron_ore", "stackLimit": 25 },
        { "type": "Material", "id": "timber",
        """));

    /// <summary>Four levels of rock under a floor with open air above it: 9 × 9 cells.</summary>
    private static GameMap Block()
    {
        var map = AsciiMap.Parse(Rock(), Rock(), Rock(), Rock(), Floor(), Air());
        map.ComputeDiscovered();
        return map;
    }

    private static string Rock() => ColonyTestBed.Level(9, 9, '#');

    private static string Floor() => ColonyTestBed.Level(9, 9, '.');

    private static string Air() => ColonyTestBed.Level(9, 9, ' ');

    [Fact]
    public void A_cell_is_discovered_if_it_or_one_of_its_26_neighbours_is_not_a_wall()
    {
        var map = Block();

        // The air, the floor and the layer of rock just under the floor are in sight; deeper rock is hidden.
        map.IsDiscovered(4, 4, 5).ShouldBeTrue();
        map.IsDiscovered(4, 4, 4).ShouldBeTrue();
        map.IsDiscovered(4, 4, 3).ShouldBeTrue();
        map.IsDiscovered(4, 4, 2).ShouldBeFalse();
        map.IsDiscovered(4, 4, 0).ShouldBeFalse();

        // The border of the map does not count as open.
        map.IsDiscovered(0, 0, 0).ShouldBeFalse();
    }

    [Fact]
    public void Opening_a_cell_discovers_it_and_its_26_neighbours_and_marks_their_chunks_dirty()
    {
        var map = Block();
        map.DrainDirtyChunks([]);

        map.SetTile(4, 4, 2, TileShape.Floor, GameMap.NoMaterial, TileFlags.None);

        for (var z = 1; z <= 3; z++)
        {
            for (var y = 3; y <= 5; y++)
            {
                for (var x = 3; x <= 5; x++)
                {
                    map.IsDiscovered(x, y, z).ShouldBeTrue($"({x}, {y}, {z})");
                }
            }
        }

        map.IsDiscovered(4, 4, 0).ShouldBeFalse();
        map.IsDiscovered(2, 4, 1).ShouldBeFalse();
        var dirty = new List<ChunkKey>();
        map.DrainDirtyChunks(dirty);
        dirty.ShouldBe([new ChunkKey(0, 0, 2), new ChunkKey(0, 0, 3), new ChunkKey(0, 0, 1)], ignoreOrder: true);
    }

    [Fact]
    public void Building_a_wall_again_does_not_hide_the_cell()
    {
        var map = Block();
        map.SetTile(4, 4, 2, TileShape.Floor, GameMap.NoMaterial, TileFlags.None);

        map.SetTile(4, 4, 2, TileShape.Wall, GameMap.NoMaterial, TileFlags.None);
        map.SetFlags(4, 4, 1, TileFlags.None);

        map.IsDiscovered(4, 4, 2).ShouldBeTrue();
        map.IsDiscovered(4, 4, 1).ShouldBeTrue();
        map.IsDiscovered(3, 3, 3).ShouldBeTrue();
    }

    [Fact]
    public void The_discovered_cells_are_part_of_the_fingerprint()
    {
        var simulation = new Simulation(1, TestContent.Minimal);
        var map = AsciiMap.Parse(Rock(), Rock(), Rock(), Floor());
        simulation.AttachMap(map);
        var hidden = simulation.ComputeStateHash();

        map.ComputeDiscovered();

        simulation.ComputeStateHash().ShouldNotBe(hidden);
    }

    [Fact]
    public void Digging_stairs_and_channels_discover_around_the_cells_they_open()
    {
        var bed = BedWithRock();
        bed.Map.IsDiscovered(4, 4, 1).ShouldBeFalse();
        bed.Designate(DesignationKind.Stairs, new TileCoord(4, 4, 3), new TileCoord(4, 4, 3));
        bed.Simulation.Run(600);

        bed.Map.ShapeAt(4, 4, 2).ShouldBe(TileShape.StairUp);
        bed.Map.IsDiscovered(3, 3, 1).ShouldBeTrue();
        bed.Map.IsDiscovered(5, 5, 1).ShouldBeTrue();
        bed.Map.IsDiscovered(4, 4, 0).ShouldBeFalse();

        bed.Designate(DesignationKind.Channel, new TileCoord(7, 7, 3), new TileCoord(7, 7, 3));
        bed.Simulation.Run(600);

        bed.Map.ShapeAt(7, 7, 3).ShouldBe(TileShape.Empty);
        bed.Map.IsDiscovered(8, 8, 1).ShouldBeTrue();
        bed.Map.IsDiscovered(7, 7, 0).ShouldBeFalse();
    }

    [Fact]
    public void Digging_blind_through_hidden_cells_discovers_as_it_goes()
    {
        // A gallery entrance at (0, 4, 1); the rest of the level is rock, hidden but for what touches the entrance.
        var entrance = Rock().ToCharArray();
        entrance[(4 * 10) + 0] = '.';
        var bed = new ColonyTestBed(OreContent, 1, Rock(), new string(entrance), Rock(), Rock());
        bed.Map.ComputeDiscovered();
        bed.AddColonist(0, 4, 1);
        bed.Map.IsDiscovered(3, 4, 1).ShouldBeFalse();

        // The order is given over cells that are hidden: it is accepted and marks them.
        bed.Designate(DesignationKind.Mine, new TileCoord(1, 4, 1), new TileCoord(6, 4, 1));
        bed.Simulation.Tick();
        for (var x = 1; x <= 6; x++)
        {
            (bed.Map.FlagsAt(x, 4, 1) & TileFlags.MineDesignation).ShouldNotBe(TileFlags.None);
        }

        bed.Map.DrainDirtyChunks([]);
        for (var tick = 0; tick < 600 && bed.Map.ShapeAt(1, 4, 1) != TileShape.Floor; tick++)
        {
            bed.Simulation.Tick();
        }

        // The first cell is dug: its neighbours show, the ones further along the order still do not.
        bed.Map.ShapeAt(1, 4, 1).ShouldBe(TileShape.Floor);
        bed.Map.IsDiscovered(2, 4, 1).ShouldBeTrue();
        bed.Map.IsDiscovered(3, 4, 1).ShouldBeFalse();
        (bed.Map.FlagsAt(3, 4, 1) & TileFlags.MineDesignation).ShouldNotBe(TileFlags.None);
        var dirty = new List<ChunkKey>();
        bed.Map.DrainDirtyChunks(dirty);
        dirty.ShouldContain(new ChunkKey(0, 0, 1));

        bed.Simulation.Run(2_000);

        for (var x = 1; x <= 6; x++)
        {
            bed.Map.ShapeAt(x, 4, 1).ShouldBe(TileShape.Floor);
        }

        bed.Map.IsDiscovered(7, 4, 1).ShouldBeTrue();
        bed.Map.IsDiscovered(8, 4, 1).ShouldBeFalse();
    }

    [Fact]
    public void A_cell_of_ore_takes_200_work_ticks_and_always_leaves_one_iron_ore()
    {
        var bed = new ColonyTestBed(OreContent, 1, Rock(), ColonyTestBed.Level(9, 9, '.'));
        var vein = (ushort)OreContent.IndexOf<MaterialDef>("iron_vein");
        var rock = (ushort)OreContent.IndexOf<MaterialDef>("rock");
        bed.Map.SetTileQuietly(4, 4, 1, TileShape.Wall, vein, TileFlags.None);
        bed.Map.SetTileQuietly(2, 4, 1, TileShape.Wall, rock, TileFlags.None);
        bed.AddColonist(3, 4, 1);
        bed.Designate(DesignationKind.Mine, new TileCoord(2, 4, 1), new TileCoord(2, 4, 1));
        var rockTicks = TicksToDig(bed, new TileCoord(2, 4, 1));
        bed.Designate(DesignationKind.Mine, new TileCoord(4, 4, 1), new TileCoord(4, 4, 1));

        var oreTicks = TicksToDig(bed, new TileCoord(4, 4, 1));

        rockTicks.ShouldBeLessThan(60);
        oreTicks.ShouldBeInRange(200, 215);
        bed.CountItems("iron_ore").ShouldBe(1);
        bed.CountItems("stone").ShouldBe(1);
    }

    [Fact]
    public void Discovered_cells_do_not_change_where_creatures_can_go()
    {
        // The same map with the fog worked out and without it has the same regions of connectivity.
        var withFog = Block();
        var without = AsciiMap.Parse(Rock(), Rock(), Rock(), Rock(), Floor(), Air());

        Regions(Connectivity.Compute(withFog), withFog).ShouldBe(Regions(Connectivity.Compute(without), without));
    }

    [Fact]
    public void Discovering_on_digging_costs_far_less_than_a_hundredth_of_a_millisecond_per_cell()
    {
        var map = AsciiMap.Parse(Enumerable.Repeat(ColonyTestBed.Level(40, 40, '#'), 20).Append(ColonyTestBed.Level(40, 40, ' ')).ToArray());
        const int Digs = 8_000;

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        for (var i = 0; i < Digs; i++)
        {
            map.SetTile(i % 40, (i / 40) % 40, 1 + (i / 1600), TileShape.Floor, GameMap.NoMaterial, TileFlags.None);
        }

        var perDigMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds / Digs;
        perDigMs.ShouldBeLessThan(0.02);
    }

    /// <summary>Rock with a floor on level 3, a colonist in the open and rock below.</summary>
    private static ColonyTestBed BedWithRock()
    {
        var bed = new ColonyTestBed(OreContent, 1, Rock(), Rock(), Rock(), Floor());
        bed.Map.ComputeDiscovered();
        bed.AddColonist(0, 0, 3);
        return bed;
    }

    private static int TicksToDig(ColonyTestBed bed, TileCoord cell)
    {
        var ticks = 0;
        while (bed.Map.ShapeAt(cell) == TileShape.Wall && ticks < 1_000)
        {
            bed.Simulation.Tick();
            ticks++;
        }

        return ticks;
    }

    private static List<int> Regions(Connectivity connectivity, GameMap map)
    {
        var regions = new List<int>();
        for (var z = 0; z < map.Size.Depth; z++)
        {
            for (var y = 0; y < map.Size.Height; y++)
            {
                for (var x = 0; x < map.Size.Width; x++)
                {
                    regions.Add(connectivity.RegionAt(new TileCoord(x, y, z)));
                }
            }
        }

        return regions;
    }
}
