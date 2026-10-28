using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Save;
using OurLife.Core.Sim;
using OurLife.Core.WorldGen;
using OurLife.Tests.Shared;

namespace OurLife.Content.Tests;

/// <summary>
/// Veins of ore and the fog of the real valley (<c>docs/gdd/subsuelo.md</c>), with the seeds 1, 7
/// and 42, and the migration of a game saved before them.
/// </summary>
public class SubsoilTests
{
    private const int OutcropRadius = 40;

    /// <summary>The same content with no veins at all: the rock as the strata laid it.</summary>
    private static readonly Lazy<DefDatabase> BareLazy = new(() => DefLoader.Load(new NoVeinsSource(GameContent.Source)));

    [Fact]
    public void The_iron_vein_of_the_content_is_dug_slowly_and_always_leaves_ore()
    {
        var iron = GameContent.Defs.Get<MaterialDef>("iron_vein");

        iron.Kind.ShouldBe(MaterialKind.Ore);
        iron.MineWorkTicks.ShouldBe(200);
        iron.MinedItem.ShouldBe("iron_ore");
        iron.MinedItemChance.ShouldBe(10_000);
        iron.IsBuilt.ShouldBeFalse();
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    public void The_same_seed_gives_the_same_veins_and_another_seed_others(ulong seed)
    {
        var first = Veins(Generate(seed).Map);

        Veins(Generate(seed).Map).ShouldBe(first);
        Veins(Generate(seed + 1).Map).ShouldNotBe(first);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    public void There_are_six_to_nine_veins_of_ten_to_twenty_four_cells_and_only_in_sandstone_or_limestone(ulong seed)
    {
        var map = Generate(seed).Map;
        var cells = Veins(map);
        var groups = Groups(map, cells);

        groups.Count.ShouldBeInRange(6, 9);
        groups.ShouldAllBe(group => group.Count >= 6);
        cells.Count.ShouldBeInRange(6 * 10, 9 * 24);

        // The map without veins has the same everything but the ore cells, which were sandstone or limestone walls.
        var without = Generate(seed, BareLazy.Value).Map;
        Veins(without).ShouldBeEmpty();
        var hosts = new[] { "sandstone", "limestone" };
        foreach (var index in cells)
        {
            var cell = map.Size.CoordOf(index);
            map.ShapeAt(cell).ShouldBe(TileShape.Wall);
            hosts.ShouldContain(without.MaterialAt(cell)!.Id);
        }

        for (var index = 0; index < map.Size.CellCount; index++)
        {
            var cell = map.Size.CoordOf(index);
            map.ShapeAt(cell).ShouldBe(without.ShapeAt(cell));
            if (!cells.Contains(index))
            {
                map.MaterialAt(cell)?.Id.ShouldBe(without.MaterialAt(cell)?.Id);
            }
        }
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    public void A_vein_shows_within_forty_cells_of_the_middle_of_the_valley(ulong seed)
    {
        var map = Generate(seed).Map;
        var (cx, cy) = OreVeins.ValleyCenter(map.Size);

        var seen = Veins(map).Select(map.Size.CoordOf)
            .Where(cell => map.IsDiscovered(cell) && ((cell.X - cx) * (cell.X - cx)) + ((cell.Y - cy) * (cell.Y - cy)) <= OutcropRadius * OutcropRadius)
            .ToList();

        seen.ShouldNotBeEmpty();
    }

    [Fact]
    public void Many_other_seeds_also_have_a_vein_in_sight_so_generation_never_fails()
    {
        foreach (var seed in new ulong[] { 2, 3, 5, 11, 13, 100, 2026, 20261009 })
        {
            var map = Generate(seed).Map;
            var (cx, cy) = OreVeins.ValleyCenter(map.Size);

            Veins(map).Select(map.Size.CoordOf)
                .Any(cell => map.IsDiscovered(cell) && ((cell.X - cx) * (cell.X - cx)) + ((cell.Y - cy) * (cell.Y - cy)) <= OutcropRadius * OutcropRadius)
                .ShouldBeTrue($"seed {seed}");
        }
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    public void Two_layers_under_the_flat_floor_of_the_valley_are_hidden_and_the_layer_under_it_is_not(ulong seed)
    {
        var map = Generate(seed).Map;
        var checkedColumns = 0;
        int rock = 0, hidden = 0;
        for (var y = 1; y < map.Size.Height - 1; y++)
        {
            for (var x = 1; x < map.Size.Width - 1; x++)
            {
                var top = map.SurfaceZ(x, y);
                map.IsDiscovered(x, y, top).ShouldBeTrue();
                map.IsDiscovered(x, y, top - 1).ShouldBeTrue();
                var flat = true;
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        flat &= map.SurfaceZ(x + dx, y + dy) >= top;
                    }
                }

                if (flat)
                {
                    map.IsDiscovered(x, y, top - 2).ShouldBeFalse($"({x}, {y}, {top - 2})");
                    checkedColumns++;
                }

                for (var z = 0; z < top; z++)
                {
                    rock++;
                    hidden += map.IsDiscovered(x, y, z) ? 0 : 1;
                }
            }
        }

        checkedColumns.ShouldBeGreaterThan(1_000);
        (hidden * 100 / rock).ShouldBeGreaterThan(70);
    }

    [Fact]
    public void The_hills_show_their_walls_and_their_peaks()
    {
        var map = Generate(1).Map;
        var settings = GameContent.Defs.All<WorldGenDef>()[0];

        // The highest column is a peak: its surface and the rock down its exposed sides are in sight.
        var (peakX, peakY, peakZ) = (0, 0, -1);
        for (var y = 0; y < map.Size.Height; y++)
        {
            for (var x = 0; x < map.Size.Width; x++)
            {
                if (map.SurfaceZ(x, y) > peakZ)
                {
                    (peakX, peakY, peakZ) = (x, y, map.SurfaceZ(x, y));
                }
            }
        }

        peakZ.ShouldBeGreaterThan(settings.ValleyFloor + 5);
        map.IsDiscovered(peakX, peakY, peakZ).ShouldBeTrue();
        map.IsDiscovered(peakX, peakY, peakZ - 1).ShouldBeTrue();
    }

    [Theory]
    [InlineData(7UL)]
    [InlineData(42UL)]
    public void A_colonist_digs_a_vein_in_sight_and_gets_one_iron_ore_for_it(ulong seed)
    {
        var simulation = ColonyDiggingTheOutcrop(seed, out var cell);

        var ticks = 0;
        for (; ticks < 60_000 && simulation.Map.ShapeAt(cell) == TileShape.Wall; ticks += 50)
        {
            simulation.Run(50);
        }

        simulation.Map.ShapeAt(cell).ShouldBe(TileShape.Floor);
        simulation.Map.IsDiscovered(cell).ShouldBeTrue();
        Stocks.Count(simulation, "iron_ore").ShouldBe(1);
    }

    [Fact]
    public void A_game_of_format_16_gets_the_fog_and_its_veins_drawn_in_hidden_rock_and_always_the_same()
    {
        var (first, second) = (LoadFixture("o7.olsave", out var header), LoadFixture("o7.olsave", out _));
        var map = first.Map;
        var cells = Veins(map);

        header.FormatVersion.ShouldBe(16);
        second.ComputeStateHash().ShouldBe(first.ComputeStateHash());
        Groups(map, cells).Count.ShouldBeInRange(6, 9);
        cells.Select(map.Size.CoordOf).ShouldAllBe(cell => !map.IsDiscovered(cell));

        // What the colonists had dug and its surroundings are in sight; the rock further in is not.
        var hidden = Enumerable.Range(0, map.Size.CellCount).Count(index => !map.IsDiscovered(map.Size.CoordOf(index)));
        hidden.ShouldBeGreaterThan(map.Size.CellCount / 4);
        var colonists = new List<OurLife.Core.Ecs.EntityId>();
        first.World.Query<Colonist>(colonists);
        colonists.ShouldAllBe(colonist => map.IsDiscovered(first.World.Get<Position>(colonist).Cell));

        first.Run(3_000);
        second.Run(3_000);
        second.ComputeStateHash().ShouldBe(first.ComputeStateHash());
    }

    /// <summary>
    /// A new colony with an order to dig the vein cell in sight that is nearest to the arrival (one
    /// with a floor beside it), settled and ready to run.
    /// </summary>
    internal static Simulation ColonyDiggingTheOutcrop(ulong seed, out TileCoord cell)
    {
        var simulation = new Simulation(seed, GameContent.Defs);
        WorldGenerator.Generate(simulation, GameContent.Defs.All<WorldGenDef>()[0]);
        var colonists = ColonyStart.Settle(simulation, ColonyStart.TheOnlyScenario(GameContent.Defs), "Valdeolmos");
        ColonySystems.AddTo(simulation);
        var map = simulation.Map;
        var arrival = simulation.World.Get<Position>(colonists[0]).Cell;

        var best = long.MaxValue;
        cell = default;
        foreach (var index in Veins(map))
        {
            var candidate = map.Size.CoordOf(index);
            var beside = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }
                .Any(step => map.IsStandable(candidate.X + step.Item1, candidate.Y + step.Item2, candidate.Z));
            var distance = ((long)(candidate.X - arrival.X) * (candidate.X - arrival.X)) + ((long)(candidate.Y - arrival.Y) * (candidate.Y - arrival.Y))
                + ((long)(candidate.Z - arrival.Z) * (candidate.Z - arrival.Z));
            if (map.IsDiscovered(candidate) && beside && distance < best)
            {
                (best, cell) = (distance, candidate);
            }
        }

        best.ShouldNotBe(long.MaxValue, "no vein cell in sight has a floor beside it");
        simulation.Commands.Enqueue(new DesignateCommand(DesignationKind.Mine, cell, cell));
        return simulation;
    }

    private static Simulation Generate(ulong seed, DefDatabase? content = null)
    {
        var defs = content ?? GameContent.Defs;
        var simulation = new Simulation(seed, defs);
        WorldGenerator.Generate(simulation, defs.All<WorldGenDef>()[0]);
        return simulation;
    }

    /// <summary>Indexes of the cells of ore, ascending.</summary>
    internal static List<int> Veins(GameMap map)
    {
        var cells = new List<int>();
        for (var index = 0; index < map.Size.CellCount; index++)
        {
            if (map.MaterialAt(map.Size.CoordOf(index))?.Kind == MaterialKind.Ore)
            {
                cells.Add(index);
            }
        }

        return cells;
    }

    /// <summary>Cells that touch face to face: each group is a vein, or a few that grew into each other.</summary>
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
                    if (map.Size.Contains(cell.X + dx, cell.Y + dy, cell.Z + dz) && left.Remove(map.Size.IndexOf(cell.X + dx, cell.Y + dy, cell.Z + dz)))
                    {
                        group.Add(map.Size.IndexOf(cell.X + dx, cell.Y + dy, cell.Z + dz));
                    }
                }
            }

            groups.Add(group);
        }

        return groups;
    }

    private static Simulation LoadFixture(string file, out SaveHeader header)
    {
        using var stream = File.OpenRead(RepoPaths.Combine("tests", "OurLife.Content.Tests", "Fixtures", file));
        var simulation = SaveGame.Load(stream, GameContent.Defs, out header);
        ColonySystems.AddTo(simulation);
        return simulation;
    }

    /// <summary>The content of the game with the veins of ore turned down to none.</summary>
    private sealed class NoVeinsSource(IContentSource inner) : IContentSource
    {
        public IReadOnlyList<string> ListFiles(string directory, string extension) => inner.ListFiles(directory, extension);

        public IReadOnlyList<string> ListDirectories(string directory) => inner.ListDirectories(directory);

        public string ReadText(string path)
        {
            var text = inner.ReadText(path);
            return path.EndsWith("worldgen.json", StringComparison.Ordinal)
                ? text.Replace("\"veins\": { \"min\": 6, \"max\": 9 }", "\"veins\": { \"min\": 0, \"max\": 0 }")
                    .Replace("\"outcrop\": { \"veins\": 1,", "\"outcrop\": { \"veins\": 0,")
                : text;
        }
    }
}
