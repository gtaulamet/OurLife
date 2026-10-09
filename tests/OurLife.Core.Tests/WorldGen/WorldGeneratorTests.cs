using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Core.WorldGen;

namespace OurLife.Core.Tests.WorldGen;

/// <summary>
/// Generation on a small test valley; the real valley is checked in OurLife.Content.Tests.
/// See the acceptance criteria in <c>docs/gdd/mapa-y-generacion.md</c>.
/// </summary>
public class WorldGeneratorTests
{
    private static readonly DefDatabase Content = TestContent.WithDefs("""
        [
          { "type": "Item", "id": "log", "kind": "raw", "sprite": "item.log", "stackLimit": 25 },
          { "type": "Item", "id": "berries", "kind": "food", "sprite": "item.berries", "stackLimit": 75, "nutrition": 400 },
          { "type": "Material", "id": "soil", "kind": "soil", "color": "#7A5A3A", "mineWorkTicks": 30 },
          { "type": "Material", "id": "granite", "kind": "stone", "color": "#8C7F7D", "mineWorkTicks": 180 },
          { "type": "Material", "id": "limestone", "kind": "stone", "color": "#C8BFA8", "mineWorkTicks": 120 },
          { "type": "Material", "id": "sandstone", "kind": "stone", "color": "#C9A26B", "mineWorkTicks": 100 },
          { "type": "Plant", "id": "oak", "sprite": "plant.oak", "blocksMovement": true,
            "chop": { "item": "log", "count": 4, "workTicks": 240 } },
          { "type": "Plant", "id": "pine", "sprite": "plant.pine", "blocksMovement": true,
            "chop": { "item": "log", "count": 3, "workTicks": 180 } },
          { "type": "Plant", "id": "berry_bush", "sprite": "plant.berry_bush",
            "harvest": { "item": "berries", "count": 8, "workTicks": 60 }, "regrowDays": 4 },
          {
            "type": "WorldGen", "id": "test_valley",
            "width": 48, "height": 40, "depth": 24,
            "valleyFloor": 10, "hillHeight": 7, "valleyHalfWidth": 6, "valleyMeander": 4,
            "terrainNoiseScale": 16, "terrainAmplitude": 3, "detailNoiseScale": 6, "forestNoiseScale": 8,
            "soilMaterial": "soil", "soilDepthMin": 2, "soilDepthMax": 5, "rockExposureElevation": 6, "rockExposureWobble": 2,
            "strata": [
              { "material": "granite", "thickness": 4 },
              { "material": "limestone", "thickness": 3 },
              { "material": "sandstone" }
            ],
            "strataWobble": 1, "grassCoverage": 8000, "groveFactorMax": 2,
            "vegetation": [
              { "plant": "berry_bush", "chance": 300, "minElevation": -5, "maxElevation": 2 },
              { "plant": "oak", "chance": 500, "minElevation": -5, "maxElevation": 3 },
              { "plant": "pine", "chance": 600, "minElevation": 2, "maxElevation": 20 }
            ]
          }
        ]
        """);

    private static readonly WorldGenDef Settings = Content.Get<WorldGenDef>("test_valley");

    [Fact]
    public void The_same_seed_generates_the_same_world()
    {
        var first = Generate(7);
        var second = Generate(7);

        second.ComputeStateHash().ShouldBe(first.ComputeStateHash());
        second.Map.ShapeBytes.SequenceEqual(first.Map.ShapeBytes).ShouldBeTrue();
        second.Map.MaterialBytes.SequenceEqual(first.Map.MaterialBytes).ShouldBeTrue();
    }

    [Fact]
    public void Another_seed_generates_another_world()
    {
        Generate(8).Map.ShapeBytes.SequenceEqual(Generate(7).Map.ShapeBytes).ShouldBeFalse();
    }

    [Fact]
    public void Every_cell_of_the_map_is_part_of_the_state_fingerprint()
    {
        var original = Generate(7);
        var before = original.ComputeStateHash();
        var map = original.Map;
        var top = map.SurfaceZ(3, 3);

        map.SetTile(3, 3, top, TileShape.Wall, map.MaterialIndexAt(3, 3, top), TileFlags.None);

        original.ComputeStateHash().ShouldNotBe(before);
    }

    [Fact]
    public void Grass_is_the_same_with_the_same_seed()
    {
        Generate(7).Map.FlagBytes.SequenceEqual(Generate(7).Map.FlagBytes).ShouldBeTrue();
    }

    [Fact]
    public void Settings_from_other_content_fail_without_changing_the_simulation()
    {
        var otherContent = TestContent.WithDefs("""
            [
              { "type": "Material", "id": "clay", "kind": "soil", "color": "#7A5A3A", "mineWorkTicks": 30 },
              { "type": "Material", "id": "granite", "kind": "stone", "color": "#8C7F7D", "mineWorkTicks": 180 },
              { "type": "Plant", "id": "bush", "sprite": "plant.bush" },
            """ + Defs.WorldGenDefTests.WorldGen("\"soilMaterial\": \"clay\"") + "]");
        var simulation = new Simulation(7, Content);
        var before = simulation.ComputeStateHash();

        var error = Should.Throw<ArgumentException>(() => WorldGenerator.Generate(simulation, otherContent.Get<WorldGenDef>("small")));

        error.Message.ShouldContain("soilMaterial points to 'clay'");
        simulation.HasMap.ShouldBeFalse();
        simulation.ComputeStateHash().ShouldBe(before);
    }

    [Fact]
    public void Generating_a_second_map_fails_without_changing_the_simulation()
    {
        var simulation = Generate(7);
        var before = simulation.ComputeStateHash();

        Should.Throw<InvalidOperationException>(() => WorldGenerator.Generate(simulation, Settings));

        simulation.ComputeStateHash().ShouldBe(before);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Every_column_is_rock_then_surface_then_air(ulong seed)
    {
        var map = Generate(seed).Map;

        ForEachColumn(map, (x, y, top) =>
        {
            for (var z = 0; z < top; z++)
            {
                map.ShapeAt(x, y, z).ShouldBe(TileShape.Wall);
            }

            map.ShapeAt(x, y, top).ShouldBeOneOf(TileShape.Floor, TileShape.Ramp);
            map.MaterialIndexAt(x, y, top).ShouldBe(map.MaterialIndexAt(x, y, top - 1));
            for (var z = top + 1; z < map.Size.Depth; z++)
            {
                map.ShapeAt(x, y, z).ShouldBe(TileShape.Empty);
            }
        });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Neighbouring_columns_differ_by_one_level_at_most_and_each_step_has_a_ramp(ulong seed)
    {
        var map = Generate(seed).Map;

        ForEachColumn(map, (x, y, top) =>
        {
            var higherNeighbour = false;
            foreach (var (nx, ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
            {
                if (nx < 0 || ny < 0 || nx >= map.Size.Width || ny >= map.Size.Height)
                {
                    continue;
                }

                var other = map.SurfaceZ(nx, ny);
                Math.Abs(other - top).ShouldBeLessThanOrEqualTo(1);
                higherNeighbour |= other == top + 1;
            }

            map.ShapeAt(x, y, top).ShouldBe(higherNeighbour ? TileShape.Ramp : TileShape.Floor);
        });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Soil_lies_on_strata_in_order(ulong seed)
    {
        var map = Generate(seed).Map;
        var soil = Content.IndexOf<MaterialDef>("soil");
        var strataOrder = Settings.Strata.Select(stratum => Content.IndexOf<MaterialDef>(stratum.Material)).ToList();
        var exposedRock = 0;

        ForEachColumn(map, (x, y, top) =>
        {
            var soilDepth = 0;
            while (soilDepth < top && map.MaterialIndexAt(x, y, top - 1 - soilDepth) == soil)
            {
                soilDepth++;
            }

            if (soilDepth == 0)
            {
                exposedRock++;
            }
            else
            {
                soilDepth.ShouldBeInRange(Settings.SoilDepthMin, Settings.SoilDepthMax);
            }

            var previousStratum = 0;
            for (var z = 0; z < top - soilDepth; z++)
            {
                var stratum = strataOrder.IndexOf(map.MaterialIndexAt(x, y, z));
                stratum.ShouldBeGreaterThanOrEqualTo(previousStratum, $"stratum order broken at ({x}, {y}, {z})");
                previousStratum = stratum;
            }
        });

        exposedRock.ShouldBeGreaterThan(0, "the hills should show bare rock");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void The_whole_surface_is_one_region(ulong seed)
    {
        var map = Generate(seed).Map;

        var regions = Connectivity.Compute(map).SurfaceRegions(map);

        regions.Count.ShouldBe(1);
        regions.ShouldNotContain(Connectivity.NoRegion);
    }

    [Fact]
    public void Plants_grow_one_per_cell_on_soil_floors_within_their_elevations()
    {
        var simulation = Generate(5);
        var map = simulation.Map;
        var soil = Content.IndexOf<MaterialDef>("soil");
        var plants = new List<EntityId>();
        simulation.World.Query<Plant, Position>(plants);

        plants.Count.ShouldBeGreaterThan(0);
        plants.Select(plant => simulation.World.Get<Position>(plant).Cell).Distinct().Count().ShouldBe(plants.Count);
        foreach (var entity in plants)
        {
            var position = simulation.World.Get<Position>(entity);
            var rule = Settings.Vegetation.Single(r => r.Plant == simulation.World.Get<Plant>(entity).Def);
            map.ShapeAt(position.X, position.Y, position.Z).ShouldBe(TileShape.Floor);
            ((int)map.MaterialIndexAt(position.X, position.Y, position.Z)).ShouldBe(soil);
            (position.Z - Settings.ValleyFloor).ShouldBeInRange(rule.MinElevation, rule.MaxElevation);
        }

        var kinds = plants.Select(plant => simulation.World.Get<Plant>(plant).Def).Distinct();
        kinds.ShouldBe(new[] { "berry_bush", "oak", "pine" }, ignoreOrder: true);
    }

    [Fact]
    public void Grass_grows_only_on_soil_and_covers_most_of_it()
    {
        var map = Generate(6).Map;
        var soil = Content.IndexOf<MaterialDef>("soil");
        int soilFloors = 0, grassy = 0;

        ForEachColumn(map, (x, y, top) =>
        {
            var hasGrass = map.FlagsAt(x, y, top).HasFlag(TileFlags.Grass);
            if (map.MaterialIndexAt(x, y, top) == soil)
            {
                soilFloors++;
                grassy += hasGrass ? 1 : 0;
            }
            else
            {
                hasGrass.ShouldBeFalse();
            }
        });

        ((double)grassy / soilFloors).ShouldBeInRange(0.6, 0.95);
    }

    private static Simulation Generate(ulong seed)
    {
        var simulation = new Simulation(seed, Content);
        WorldGenerator.Generate(simulation, Settings);
        return simulation;
    }

    private static void ForEachColumn(GameMap map, Action<int, int, int> check)
    {
        for (var y = 0; y < map.Size.Height; y++)
        {
            for (var x = 0; x < map.Size.Width; x++)
            {
                check(x, y, map.SurfaceZ(x, y));
            }
        }
    }
}
