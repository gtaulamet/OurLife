using OurLife.Core.Map;

namespace OurLife.Core.Tests.Map;

/// <summary>Movement rules and regions, on small maps written in ASCII (see <see cref="AsciiMap"/>).</summary>
public class ConnectivityTests
{
    [Fact]
    public void An_open_floor_is_one_region()
    {
        var connectivity = Connectivity.Compute(AsciiMap.Parse("""
            ...
            ...
            """));

        connectivity.RegionCount.ShouldBe(1);
        connectivity.SizeOf(1).ShouldBe(6);
    }

    [Fact]
    public void A_wall_splits_the_floor_and_walls_have_no_region()
    {
        var connectivity = Connectivity.Compute(AsciiMap.Parse("""
            .#.
            .#.
            """));

        connectivity.RegionCount.ShouldBe(2);
        connectivity.RegionAt(1, 0, 0).ShouldBe(Connectivity.NoRegion);
        connectivity.AreConnected(new TileCoord(0, 0, 0), new TileCoord(2, 0, 0)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(3, 0, 0)]
    [InlineData(0, 2, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(0, 0, 1)]
    public void Cells_outside_the_map_have_no_region(int x, int y, int z)
    {
        var connectivity = Connectivity.Compute(AsciiMap.Parse("""
            ...
            ...
            """));

        connectivity.RegionAt(x, y, z).ShouldBe(Connectivity.NoRegion);
        connectivity.AreConnected(new TileCoord(x, y, z), new TileCoord(0, 0, 0)).ShouldBeFalse();
    }

    [Fact]
    public void Diagonal_steps_cannot_cut_a_wall_corner()
    {
        var connectivity = Connectivity.Compute(AsciiMap.Parse("""
            .#
            #.
            """));

        connectivity.AreConnected(new TileCoord(0, 0, 0), new TileCoord(1, 1, 0)).ShouldBeFalse();
    }

    [Fact]
    public void Diagonal_steps_pass_between_open_cells()
    {
        var connectivity = Connectivity.Compute(AsciiMap.Parse("""
            ._
            _.
            """));

        connectivity.AreConnected(new TileCoord(0, 0, 0), new TileCoord(1, 1, 0)).ShouldBeTrue();
    }

    [Fact]
    public void A_ramp_leads_to_the_level_above()
    {
        var map = AsciiMap.Parse(
            """
            /#
            """,
            """
            _.
            """);

        Connectivity.Compute(map).AreConnected(new TileCoord(0, 0, 0), new TileCoord(1, 0, 1)).ShouldBeTrue();
    }

    [Fact]
    public void A_ramp_with_a_wall_above_it_leads_nowhere()
    {
        var map = AsciiMap.Parse(
            """
            /#
            """,
            """
            #.
            """);

        Connectivity.Compute(map).AreConnected(new TileCoord(0, 0, 0), new TileCoord(1, 0, 1)).ShouldBeFalse();
    }

    [Fact]
    public void A_floor_next_to_a_higher_floor_needs_a_ramp()
    {
        var map = AsciiMap.Parse(
            """
            .#
            """,
            """
            _.
            """);

        Connectivity.Compute(map).AreConnected(new TileCoord(0, 0, 0), new TileCoord(1, 0, 1)).ShouldBeFalse();
    }

    [Theory]
    [InlineData('<', '>', true)]
    [InlineData('X', 'X', true)]
    [InlineData('<', 'X', true)]
    [InlineData('X', '>', true)]
    [InlineData('<', '<', false)]
    [InlineData('>', '>', false)]
    [InlineData('.', '>', false)]
    public void Stairs_connect_levels_only_when_they_match(char lower, char upper, bool connected)
    {
        var map = AsciiMap.Parse(lower.ToString(), upper.ToString());

        Connectivity.Compute(map).AreConnected(new TileCoord(0, 0, 0), new TileCoord(0, 0, 1)).ShouldBe(connected);
    }

    [Fact]
    public void Every_movement_rule_is_symmetric()
    {
        var map = AsciiMap.Parse(
            """
            ##/#.
            #X.<.
            /..#.
            """,
            """
            _.__.
            _X_>.
            ._.#_
            """);
        Span<TileCoord> neighbours = stackalloc TileCoord[MapNavigation.MaxNeighbours];
        Span<TileCoord> back = stackalloc TileCoord[MapNavigation.MaxNeighbours];

        for (var index = 0; index < map.Size.CellCount; index++)
        {
            var cell = map.Size.CoordOf(index);
            var count = MapNavigation.GetNeighbours(map, cell.X, cell.Y, cell.Z, neighbours);
            for (var i = 0; i < count; i++)
            {
                var next = neighbours[i];
                var backCount = MapNavigation.GetNeighbours(map, next.X, next.Y, next.Z, back);
                back[..backCount].Contains(cell).ShouldBeTrue($"{next} should lead back to {cell}");
            }
        }
    }
}
