using OurLife.Core.Map;

namespace OurLife.Core.Tests.Map;

public class MapRaycastTests
{
    // Level 0 is rock; level 1 has a floor with a wall in the middle; level 2 is air with a wall on top of that one.
    private static readonly GameMap Map = AsciiMap.Parse(
        """
        ###
        ###
        ###
        """,
        """
        ...
        .#.
        ...
        """,
        """
        ___
        _#_
        ___
        """);

    [Fact]
    public void A_ray_straight_down_hits_the_floor_cell()
    {
        Hit((0.5, 0.5, 10), (0, 0, -1), maxLevel: 2).ShouldBe(new TileCoord(0, 0, 1));
    }

    [Fact]
    public void A_ray_straight_down_onto_a_column_hits_its_top()
    {
        Hit((1.5, 1.5, 10), (0, 0, -1), maxLevel: 2).ShouldBe(new TileCoord(1, 1, 2));
    }

    [Fact]
    public void Levels_above_the_view_are_skipped()
    {
        Hit((1.5, 1.5, 10), (0, 0, -1), maxLevel: 1).ShouldBe(new TileCoord(1, 1, 1));
        Hit((0.5, 0.5, 10), (0, 0, -1), maxLevel: 0).ShouldBe(new TileCoord(0, 0, 0));
    }

    [Fact]
    public void Levels_below_the_deepest_drawn_are_skipped()
    {
        MapRaycast.TryFirstHit(Map, (1.5, 1.5, 10), (0, 0, -1), maxLevel: 2, out var cell, minLevel: 2).ShouldBeTrue();
        cell.ShouldBe(new TileCoord(1, 1, 2));
        MapRaycast.TryFirstHit(Map, (0.5, 0.5, 10), (0, 0, -1), maxLevel: 2, out _, minLevel: 2).ShouldBeFalse();
    }

    [Fact]
    public void A_level_ray_hits_a_wall_from_the_side()
    {
        Hit((-5, 1.5, 2.5), (1, 0, 0), maxLevel: 2).ShouldBe(new TileCoord(1, 1, 2));
    }

    [Fact]
    public void A_slanted_ray_hits_the_first_solid_cell_on_its_way()
    {
        // From high above the corner (0, 0), down towards the far corner: it meets the column first.
        Hit((0.2, 0.2, 2.9), (1, 1, -0.6), maxLevel: 2).ShouldBe(new TileCoord(1, 1, 2));
    }

    [Fact]
    public void A_slanted_ray_hits_the_floor_where_it_reaches_the_ground()
    {
        // Like the camera, about 55° down, aimed at (1.3, 0.5) on the floor of level 1. It crosses
        // the top of the floor cell (0, 0) first, but the floor is on the bottom of the cell.
        var direction = (1.0, 0.0, -1.43);
        var origin = (1.3 - 5, 0.5, 1 + (5 * 1.43));
        Hit(origin, direction, maxLevel: 2).ShouldBe(new TileCoord(1, 0, 1));
    }

    [Theory]
    [InlineData(10, 10, 10, 1, 0, 0)]
    [InlineData(1.5, 1.5, 10, 0, 0, 1)]
    [InlineData(1.5, 1.5, 10, 0, 0, 0)]
    public void A_ray_that_misses_the_map_finds_nothing(double x, double y, double z, double dx, double dy, double dz)
    {
        MapRaycast.TryFirstHit(Map, (x, y, z), (dx, dy, dz), maxLevel: 2, out _).ShouldBeFalse();
    }

    private static TileCoord Hit((double, double, double) origin, (double, double, double) direction, int maxLevel)
    {
        MapRaycast.TryFirstHit(Map, origin, direction, maxLevel, out var cell).ShouldBeTrue();
        return cell;
    }
}
