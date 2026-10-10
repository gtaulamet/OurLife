using OurLife.Core.Map;
using OurLife.Core.Pathfinding;
using OurLife.Core.Tests.Map;

namespace OurLife.Core.Tests.Pathfinding;

public class PathFinderTests
{
    private readonly PathFinder _finder = new();
    private readonly List<TileCoord> _path = [];

    [Fact]
    public void A_straight_corridor_is_walked_cell_by_cell()
    {
        var map = AsciiMap.Parse("#####", ".....");

        _finder.TryFind(map, new TileCoord(0, 0, 1), [new TileCoord(4, 0, 1)], _path).ShouldBeTrue();

        _path.ShouldBe([new(1, 0, 1), new(2, 0, 1), new(3, 0, 1), new(4, 0, 1)]);
    }

    [Fact]
    public void An_open_field_is_crossed_diagonally()
    {
        var map = AsciiMap.Parse(ColonyLevel(5, '#'), ColonyLevel(5, '.'));

        _finder.TryFind(map, new TileCoord(0, 0, 1), [new TileCoord(4, 4, 1)], _path).ShouldBeTrue();

        _path.ShouldBe([new(1, 1, 1), new(2, 2, 1), new(3, 3, 1), new(4, 4, 1)]);
    }

    [Fact]
    public void Walls_are_walked_around()
    {
        var map = AsciiMap.Parse(
            "###\n###\n###",
            ".#.\n.#.\n...");

        _finder.TryFind(map, new TileCoord(0, 0, 1), [new TileCoord(2, 0, 1)], _path).ShouldBeTrue();

        _path.ShouldNotContain(cell => map.ShapeAt(cell) == TileShape.Wall);
        _path[^1].ShouldBe(new TileCoord(2, 0, 1));
        _path.ShouldContain(new TileCoord(1, 2, 1));
    }

    [Fact]
    public void A_blocked_cell_is_walked_around_like_a_wall()
    {
        var map = AsciiMap.Parse("###\n###\n###", "...\n...\n...");
        map.SetFlags(1, 0, 1, TileFlags.Blocked);
        map.SetFlags(1, 1, 1, TileFlags.Blocked);

        _finder.TryFind(map, new TileCoord(0, 0, 1), [new TileCoord(2, 0, 1)], _path).ShouldBeTrue();

        _path.ShouldNotContain(new TileCoord(1, 0, 1));
        _path.ShouldNotContain(new TileCoord(1, 1, 1));
        _path.ShouldContain(new TileCoord(1, 2, 1));
    }

    [Fact]
    public void Ramps_and_stairs_change_level()
    {
        // A ramp climbs from level 1 to level 2 on the left; stairs go back down on the right.
        var map = AsciiMap.Parse(
            "#####",
            "./##<",
            "  ..>");

        _finder.TryFind(map, new TileCoord(0, 0, 1), [new TileCoord(4, 0, 1)], _path).ShouldBeTrue();

        _path.ShouldBe([new(1, 0, 1), new(2, 0, 2), new(3, 0, 2), new(4, 0, 2), new(4, 0, 1)]);
    }

    [Fact]
    public void An_unreachable_goal_fails_and_leaves_the_path_empty()
    {
        var map = AsciiMap.Parse("#####", "..#..");

        _finder.TryFind(map, new TileCoord(0, 0, 1), [new TileCoord(4, 0, 1)], _path).ShouldBeFalse();

        _path.ShouldBeEmpty();
    }

    [Fact]
    public void Starting_on_a_goal_needs_no_steps()
    {
        var map = AsciiMap.Parse("###", "...");

        _finder.TryFind(map, new TileCoord(1, 0, 1), [new TileCoord(2, 0, 1), new TileCoord(1, 0, 1)], _path).ShouldBeTrue();

        _path.ShouldBeEmpty();
    }

    [Fact]
    public void The_nearest_of_several_goals_is_reached()
    {
        var map = AsciiMap.Parse("#########", ".........");

        _finder.TryFind(map, new TileCoord(3, 0, 1), [new TileCoord(0, 0, 1), new TileCoord(8, 0, 1)], _path).ShouldBeTrue();

        _path[^1].ShouldBe(new TileCoord(0, 0, 1));
    }

    [Fact]
    public void The_estimate_never_exceeds_the_real_cost()
    {
        PathFinder.Estimate(new TileCoord(0, 0, 0), new TileCoord(3, 1, 0)).ShouldBe(34);
        PathFinder.Estimate(new TileCoord(0, 0, 0), new TileCoord(1, 0, 5)).ShouldBe(50);
        PathFinder.StepCost(new TileCoord(0, 0, 0), new TileCoord(1, 1, 0)).ShouldBe(PathFinder.DiagonalCost);
        PathFinder.StepCost(new TileCoord(0, 0, 0), new TileCoord(1, 0, 1)).ShouldBe(PathFinder.StraightCost);
    }

    [Fact]
    public void A_finder_serves_several_maps_one_after_another()
    {
        var small = AsciiMap.Parse("###", "...");
        var large = AsciiMap.Parse("#####", ".....");

        _finder.TryFind(small, new TileCoord(0, 0, 1), [new TileCoord(2, 0, 1)], _path).ShouldBeTrue();
        _finder.TryFind(large, new TileCoord(0, 0, 1), [new TileCoord(4, 0, 1)], _path).ShouldBeTrue();

        _path.Count.ShouldBe(4);
    }

    private static string ColonyLevel(int size, char symbol) =>
        string.Join("\n", Enumerable.Repeat(new string(symbol, size), size));
}
