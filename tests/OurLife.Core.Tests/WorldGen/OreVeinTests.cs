using OurLife.Core.Defs;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Core.WorldGen;

namespace OurLife.Core.Tests.WorldGen;

/// <summary>
/// Veins of ore on a small test valley (<c>docs/gdd/subsuelo.md</c>); the real valley, with seeds 1, 7
/// and 42, is checked in OurLife.Content.Tests.
/// </summary>
public class OreVeinTests
{
    private const string Veins = """{ "type": "OreVein", "id": "iron", "material": "iron_vein", "hosts": ["sandstone", "limestone"], "veins": { "min": 6, "max": 9 }, "cells": { "min": 10, "max": 24 }, "minFit": 6, "attempts": 200, "outcrop": { "veins": 1, "radius": 22 } }""";

    private static readonly DefDatabase Content = ContentWith(Veins);

    /// <summary>The same valley and the same materials, but no veins: the rock as the strata laid it.</summary>
    private static readonly DefDatabase Bare = ContentWith(
        """{ "type": "OreVein", "id": "iron", "material": "iron_vein", "hosts": ["sandstone", "limestone"], "veins": { "min": 0, "max": 0 }, "cells": { "min": 10, "max": 24 }, "minFit": 6, "attempts": 200, "outcrop": { "veins": 0, "radius": 22 } }""");

    private static DefDatabase ContentWith(string oreVein) => TestContent.WithDefs($$"""
        [
          { "type": "ItemCategory", "id": "ore" },
          { "type": "Item", "id": "log", "category": "wood", "kind": "raw", "visual": "item.log", "stackLimit": 25 },
          { "type": "Item", "id": "berries", "category": "food", "kind": "food", "visual": "item.berries", "stackLimit": 75, "nutrition": 400 },
          { "type": "Item", "id": "iron_ore", "category": "ore", "kind": "raw", "visual": "item.iron_ore", "stackLimit": 25 },
          { "type": "Material", "id": "soil", "kind": "soil", "color": "#7A5A3A", "mineWorkTicks": 30 },
          { "type": "Material", "id": "granite", "kind": "stone", "color": "#8C7F7D", "mineWorkTicks": 180 },
          { "type": "Material", "id": "limestone", "kind": "stone", "color": "#C8BFA8", "mineWorkTicks": 120 },
          { "type": "Material", "id": "sandstone", "kind": "stone", "color": "#C9A26B", "mineWorkTicks": 100 },
          { "type": "Material", "id": "iron_vein", "kind": "ore", "color": "#8A4B3A", "mineWorkTicks": 200, "minedItem": "iron_ore", "minedItemChance": 10000 },
          { "type": "Plant", "id": "oak", "visual": "plant.oak", "blocksMovement": true,
            "chop": { "item": "log", "count": 4, "workTicks": 240 } },
          { "type": "Plant", "id": "berry_bush", "visual": "plant.berry_bush",
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
              { "plant": "oak", "chance": 500, "minElevation": -5, "maxElevation": 3 }
            ]
          },
          {{oreVein}}
        ]
        """);

    private static Simulation Generate(ulong seed, DefDatabase? content = null)
    {
        var defs = content ?? Content;
        var simulation = new Simulation(seed, defs);
        WorldGenerator.Generate(simulation, defs.Get<WorldGenDef>("test_valley"));
        return simulation;
    }

    private static List<int> OreCells(GameMap map, DefDatabase content)
    {
        var vein = content.IndexOf<MaterialDef>("iron_vein");
        var cells = new List<int>();
        for (var index = 0; index < map.Size.CellCount; index++)
        {
            var cell = map.Size.CoordOf(index);
            if (map.MaterialIndexAt(cell.X, cell.Y, cell.Z) == vein)
            {
                cells.Add(index);
            }
        }

        return cells;
    }

    /// <summary>Groups ore cells that touch face to face: each group is one vein, or a few that grew into each other.</summary>
    private static List<List<int>> Groups(GameMap map, List<int> cells)
    {
        var left = cells.ToList();
        var groups = new List<List<int>>();
        while (left.Count > 0)
        {
            var group = new List<int> { left[0] };
            left.RemoveAt(0);
            for (var next = 0; next < group.Count; next++)
            {
                var cell = map.Size.CoordOf(group[next]);
                foreach (var (dx, dy, dz) in new[] { (-1, 0, 0), (1, 0, 0), (0, -1, 0), (0, 1, 0), (0, 0, -1), (0, 0, 1) })
                {
                    if (map.Size.Contains(cell.X + dx, cell.Y + dy, cell.Z + dz))
                    {
                        var neighbour = map.Size.IndexOf(cell.X + dx, cell.Y + dy, cell.Z + dz);
                        if (left.Remove(neighbour))
                        {
                            group.Add(neighbour);
                        }
                    }
                }
            }

            groups.Add(group);
        }

        return groups;
    }

    [Fact]
    public void The_same_seed_gives_the_same_veins_cell_by_cell_and_another_seed_others()
    {
        var first = Generate(7);
        var second = Generate(7);

        second.ComputeStateHash().ShouldBe(first.ComputeStateHash());
        OreCells(second.Map, Content).ShouldBe(OreCells(first.Map, Content));
        OreCells(Generate(8).Map, Content).ShouldNotBe(OreCells(first.Map, Content));
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    public void There_are_six_to_nine_veins_of_ten_to_twenty_four_cells_in_sandstone_or_limestone_only(ulong seed)
    {
        var map = Generate(seed).Map;
        var bare = Generate(seed, Bare).Map;
        var cells = OreCells(map, Content);
        var groups = Groups(map, cells);

        OreCells(bare, Bare).ShouldBeEmpty();
        groups.Count.ShouldBeInRange(6, 9);
        cells.Count.ShouldBeInRange(6 * 6, 9 * 24);
        groups.ShouldAllBe(group => group.Count >= 6);
        var hosts = new[] { Bare.IndexOf<MaterialDef>("sandstone"), Bare.IndexOf<MaterialDef>("limestone") };
        for (var index = 0; index < map.Size.CellCount; index++)
        {
            var cell = map.Size.CoordOf(index);
            var (ore, rock) = (map.MaterialIndexAt(cell.X, cell.Y, cell.Z), bare.MaterialIndexAt(cell.X, cell.Y, cell.Z));
            if (ore == Content.IndexOf<MaterialDef>("iron_vein"))
            {
                // Only a wall of the host rocks becomes ore: never soil, granite or a floor.
                map.ShapeAt(cell).ShouldBe(TileShape.Wall);
                hosts.ShouldContain(rock);
            }
            else
            {
                ore.ShouldBe(rock);
            }
        }
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    public void At_least_one_vein_has_a_discovered_cell_near_the_middle_of_the_valley(ulong seed)
    {
        var map = Generate(seed).Map;
        var (cx, cy) = OreVeins.ValleyCenter(map.Size);

        var seen = OreCells(map, Content).Select(map.Size.CoordOf)
            .Where(cell => map.IsDiscovered(cell) && (((cell.X - cx) * (cell.X - cx)) + ((cell.Y - cy) * (cell.Y - cy))) <= 22 * 22)
            .ToList();

        seen.ShouldNotBeEmpty();
    }

    [Fact]
    public void A_valley_without_rock_in_sight_cannot_hide_ore_where_it_can_be_found()
    {
        // With a radius of 1 around the middle, only flat soil is in sight: a generation error.
        var content = ContentWith(Veins.Replace("\"radius\": 22", "\"radius\": 1"));

        var error = Should.Throw<InvalidOperationException>(() => Generate(7, content));

        error.Message.ShouldContain("no rock of sandstone, limestone is in sight");
    }

    [Fact]
    public void A_vein_material_that_is_not_ore_cannot_be_generated_and_leaves_the_simulation_untouched()
    {
        var content = ContentWith(Veins.Replace("\"material\": \"iron_vein\"", "\"material\": \"granite\""));
        var simulation = new Simulation(7, content);
        var before = simulation.ComputeStateHash();

        var error = Should.Throw<ArgumentException>(() => WorldGenerator.Generate(simulation, content.Get<WorldGenDef>("test_valley")));

        error.Message.ShouldContain("is not of kind ore");
        simulation.HasMap.ShouldBeFalse();
        simulation.ComputeStateHash().ShouldBe(before);
    }

    [Fact]
    public void Most_of_the_rock_starts_hidden_and_the_surface_and_the_layer_under_it_do_not()
    {
        var map = Generate(7).Map;
        int rock = 0, hidden = 0;
        for (var y = 0; y < map.Size.Height; y++)
        {
            for (var x = 0; x < map.Size.Width; x++)
            {
                var top = map.SurfaceZ(x, y);
                map.IsDiscovered(x, y, top).ShouldBeTrue();
                map.IsDiscovered(x, y, top - 1).ShouldBeTrue();
                for (var z = 0; z < top; z++)
                {
                    rock++;
                    hidden += map.IsDiscovered(x, y, z) ? 0 : 1;
                }
            }
        }

        (hidden * 100 / rock).ShouldBeGreaterThan(70);
    }

    [Fact]
    public void The_fog_does_not_change_the_regions_of_connectivity()
    {
        var map = Generate(7).Map;
        var fogless = new GameMap(map.Size, map.Materials);
        for (var index = 0; index < map.Size.CellCount; index++)
        {
            var cell = map.Size.CoordOf(index);
            fogless.SetTileQuietly(cell.X, cell.Y, cell.Z, map.ShapeAt(cell), map.MaterialIndexAt(cell.X, cell.Y, cell.Z), map.FlagsAt(cell) & ~TileFlags.Discovered);
        }

        var (with, without) = (Connectivity.Compute(map), Connectivity.Compute(fogless));

        for (var index = 0; index < map.Size.CellCount; index++)
        {
            with.RegionAt(map.Size.CoordOf(index)).ShouldBe(without.RegionAt(map.Size.CoordOf(index)));
        }
    }

    [Fact]
    public void An_old_map_gets_veins_only_in_hidden_rock_and_the_same_ones_every_time()
    {
        var old = Generate(7, Bare);
        var content = Content;

        Simulation Migrated()
        {
            // The materials are the same in both contents, so the old map is read as it is.
            var simulation = new Simulation(7, content);
            simulation.AttachMap(CopyOf(old.Map));
            OreVeins.Migrate(simulation);
            return simulation;
        }

        var (first, second) = (Migrated(), Migrated());
        var cells = OreCells(first.Map, content);

        second.ComputeStateHash().ShouldBe(first.ComputeStateHash());
        Groups(first.Map, cells).Count.ShouldBeInRange(6, 9);
        cells.Select(first.Map.Size.CoordOf).ShouldAllBe(cell => !first.Map.IsDiscovered(cell));
        OreCells(old.Map, Bare).ShouldBeEmpty();
    }

    [Fact]
    public void A_map_with_no_hidden_rock_gets_no_veins_when_migrated()
    {
        var content = Content;
        var simulation = new Simulation(7, content);
        var map = new GameMap(new MapSize(8, 8, 4), content.All<MaterialDef>());
        simulation.AttachMap(map);

        OreVeins.Migrate(simulation);

        OreCells(map, content).ShouldBeEmpty();
    }

    private static GameMap CopyOf(GameMap map)
    {
        var copy = new GameMap(map.Size, map.Materials);
        for (var index = 0; index < map.Size.CellCount; index++)
        {
            var cell = map.Size.CoordOf(index);
            copy.SetTileQuietly(cell.X, cell.Y, cell.Z, map.ShapeAt(cell), map.MaterialIndexAt(cell.X, cell.Y, cell.Z), map.FlagsAt(cell));
        }

        return copy;
    }
}
