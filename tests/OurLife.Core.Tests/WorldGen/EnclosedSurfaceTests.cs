using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Tests.Colony;
using OurLife.Core.WorldGen;

namespace OurLife.Core.Tests.WorldGen;

/// <summary>Trees never close off part of the surface (see <c>docs/gdd/mapa-y-generacion.md</c>).</summary>
public class EnclosedSurfaceTests
{
    [Fact]
    public void A_ring_of_trees_loses_the_tree_nearest_the_cell_it_closes_off()
    {
        var bed = ColonyTestBed.Flat(7);
        foreach (var (dx, dy) in MapNavigation.Directions)
        {
            bed.AddPlant("tree", 3 + dx, 3 + dy, 1);
        }

        Connectivity.Compute(bed.Map).SurfaceRegions(bed.Map).Count.ShouldBe(2);

        WorldGenerator.FreeEnclosedSurface(bed.Simulation, bed.Map);

        Connectivity.Compute(bed.Map).SurfaceRegions(bed.Map).Count.ShouldBe(1);
        var trees = new List<EntityId>();
        bed.World.Query<Plant>(trees);
        trees.Count.ShouldBe(7);
        bed.Map.IsStandable(3, 2, 1).ShouldBeTrue();
    }

    [Fact]
    public void Trees_inside_the_closed_off_pocket_are_kept()
    {
        var bed = ColonyTestBed.Flat(9);
        bed.AddPlant("tree", 3, 3, 1);
        bed.AddPlant("tree", 5, 5, 1);
        AddRing(bed, centre: 4, radius: 2);

        WorldGenerator.FreeEnclosedSurface(bed.Simulation, bed.Map);

        Connectivity.Compute(bed.Map).SurfaceRegions(bed.Map).Count.ShouldBe(1);
        bed.Map.IsStandable(3, 3, 1).ShouldBeFalse();
        bed.Map.IsStandable(5, 5, 1).ShouldBeFalse();
        CountTrees(bed).ShouldBe(2 + 16 - 1);
    }

    [Fact]
    public void A_ring_two_trees_thick_loses_one_tree_of_each_layer()
    {
        var bed = ColonyTestBed.Flat(11);
        AddRing(bed, centre: 5, radius: 1);
        AddRing(bed, centre: 5, radius: 2);

        WorldGenerator.FreeEnclosedSurface(bed.Simulation, bed.Map);

        Connectivity.Compute(bed.Map).SurfaceRegions(bed.Map).Count.ShouldBe(1);
        CountTrees(bed).ShouldBe(8 + 16 - 2);
    }

    [Fact]
    public void A_tree_at_the_top_of_a_ramp_is_the_way_out_of_a_plateau()
    {
        // Low ground on level 1 with a ramp up to a plateau on level 2. A row of trees at the top
        // of the ramp closes off the rest of the plateau: the only way out goes down the ramp.
        const string row1 = ".../###", row2 = "____...";
        var bed = new ColonyTestBed(
            ColonyTestBed.Level(7, 3, '#'),
            string.Join("\n", row1, row1, row1),
            string.Join("\n", row2, row2, row2));
        for (var y = 0; y < 3; y++)
        {
            bed.AddPlant("tree", 4, y, 2);
        }

        Connectivity.Compute(bed.Map).SurfaceRegions(bed.Map).Count.ShouldBe(2);

        WorldGenerator.FreeEnclosedSurface(bed.Simulation, bed.Map);

        Connectivity.Compute(bed.Map).SurfaceRegions(bed.Map).Count.ShouldBe(1);
        CountTrees(bed).ShouldBe(2);
    }

    [Fact]
    public void A_connected_surface_keeps_every_tree()
    {
        var bed = ColonyTestBed.Flat(7);
        bed.AddPlant("tree", 2, 2, 1);
        bed.AddPlant("tree", 4, 4, 1);

        WorldGenerator.FreeEnclosedSurface(bed.Simulation, bed.Map);

        CountTrees(bed).ShouldBe(2);
    }

    /// <summary>Trees on every cell at a Chebyshev distance of <paramref name="radius"/> from the centre, on level 1.</summary>
    private static void AddRing(ColonyTestBed bed, int centre, int radius)
    {
        for (var y = centre - radius; y <= centre + radius; y++)
        {
            for (var x = centre - radius; x <= centre + radius; x++)
            {
                if (Math.Max(Math.Abs(x - centre), Math.Abs(y - centre)) == radius)
                {
                    bed.AddPlant("tree", x, y, 1);
                }
            }
        }
    }

    private static int CountTrees(ColonyTestBed bed)
    {
        var trees = new List<EntityId>();
        bed.World.Query<Plant>(trees);
        return trees.Count;
    }
}
