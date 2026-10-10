using OurLife.Core.Colony;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Tests.Map;

namespace OurLife.Core.Tests.Colony;

public class WorkSitesTests
{
    [Fact]
    public void A_wall_is_not_dug_across_a_diagonal_closed_by_walls()
    {
        var map = AsciiMap.Parse("""
            .#
            ##
            """);
        var sites = new List<TileCoord>();

        WorkSites.Find(map, JobKind.Mine, new TileCoord(1, 1, 0), sites);

        sites.ShouldBeEmpty();
        WorkSites.IsSite(map, JobKind.Mine, new TileCoord(1, 1, 0), new TileCoord(0, 0, 0)).ShouldBeFalse();
    }

    [Fact]
    public void A_wall_is_dug_across_an_open_diagonal_and_from_its_sides()
    {
        var map = AsciiMap.Parse("""
            ..
            .#
            """);
        var sites = new List<TileCoord>();

        WorkSites.Find(map, JobKind.Mine, new TileCoord(1, 1, 0), sites);

        sites.ShouldBe(new[] { new TileCoord(1, 0, 0), new TileCoord(0, 1, 0), new TileCoord(0, 0, 0) });
        WorkSites.IsSite(map, JobKind.Mine, new TileCoord(1, 1, 0), new TileCoord(0, 0, 0)).ShouldBeTrue();
    }
}
