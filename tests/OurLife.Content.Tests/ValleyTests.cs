using System.Diagnostics;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Core.WorldGen;

namespace OurLife.Content.Tests;

/// <summary>The real valley of IP1 meets the criteria of <c>docs/gdd/mapa-y-generacion.md</c>.</summary>
public class ValleyTests
{
    private const int BushesFiveColonistsNeed = 30;

    [Fact]
    public void The_content_has_exactly_one_world_to_generate()
    {
        // The game generates the only WorldGen def until IP7 brings the choice of site.
        GameContent.Defs.All<WorldGenDef>().Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(42)]
    [InlineData(20261009)]
    public void The_valley_has_one_connected_surface_and_enough_berry_bushes(ulong seed)
    {
        var simulation = Generate(seed);
        var map = simulation.Map;
        var surfaceRegions = Connectivity.Compute(map).SurfaceRegions(map);

        surfaceRegions.Count.ShouldBe(1);
        surfaceRegions.ShouldNotContain(Connectivity.NoRegion);
        CountPlants(simulation, "berry_bush").ShouldBeGreaterThanOrEqualTo(BushesFiveColonistsNeed);
        CountPlants(simulation, "oak").ShouldBeGreaterThan(0);
        CountPlants(simulation, "pine").ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Digging_down_from_the_valley_floor_crosses_every_stratum()
    {
        var simulation = Generate(1);
        var map = simulation.Map;
        var settings = GameContent.Defs.All<WorldGenDef>()[0];
        var found = new HashSet<string>();
        for (var y = 0; y < map.Size.Height; y++)
        {
            for (var x = 0; x < map.Size.Width; x++)
            {
                if (map.SurfaceZ(x, y) <= settings.ValleyFloor)
                {
                    for (var z = 0; z < map.SurfaceZ(x, y); z++)
                    {
                        found.Add(map.MaterialAt(x, y, z)!.Id);
                    }
                }
            }
        }

        found.ShouldBe(settings.Strata.Select(stratum => stratum.Material).Append(settings.SoilMaterial), ignoreOrder: true);
    }

    [Fact]
    public void The_valley_is_generated_in_less_than_two_seconds()
    {
        Generate(1); // warms up the JIT, as a running game already has
        var stopwatch = Stopwatch.StartNew();

        Generate(2);

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    private static Simulation Generate(ulong seed)
    {
        var simulation = new Simulation(seed, GameContent.Defs);
        WorldGenerator.Generate(simulation, GameContent.Defs.All<WorldGenDef>()[0]);
        return simulation;
    }

    private static int CountPlants(Simulation simulation, string def)
    {
        var plants = new List<EntityId>();
        simulation.World.Query<Plant>(plants);
        return plants.Count(plant => simulation.World.Get<Plant>(plant).Def == def);
    }
}
