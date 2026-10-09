using OurLife.Core.Defs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.WorldGen;

/// <summary>
/// Generates the map of a new game from a <see cref="WorldGenDef"/>: relief, soil, strata, ramps,
/// grass and plants (see <c>docs/gdd/mapa-y-generacion.md</c>). Every layer draws from its own
/// random stream, so the same seed and content always give the same map.
/// </summary>
public static class WorldGenerator
{
    // Calibration of the noise, not balance: the spread of GradientNoise is measured for 3 octaves.
    private const int Octaves = 3;

    // Offset between the noise samples of two strata boundaries, so they wave independently.
    private const double StrataNoiseOffset = 37.0;

    /// <summary>Builds the map, attaches it to the simulation and adds the plants as entities.</summary>
    public static GameMap Generate(Simulation simulation, WorldGenDef settings)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(settings);
        // Everything that can fail is checked before the first random stream is touched, so a
        // failed call leaves the simulation exactly as it was.
        if (simulation.HasMap)
        {
            throw new InvalidOperationException("The simulation already has a map.");
        }

        var problems = settings.ProblemsWith(simulation.Content).ToList();
        if (problems.Count > 0)
        {
            throw new ArgumentException(
                $"WorldGen '{settings.Id}' cannot be generated: {string.Join(" ", problems)}", nameof(settings));
        }

        var content = simulation.Content;
        var map = new GameMap(new MapSize(settings.Width, settings.Height, settings.Depth), content.All<MaterialDef>());
        var surface = BuildSurface(simulation.Random, settings);
        FillColumns(map, surface, simulation.Random, settings, content);
        PlantVegetation(simulation, map, surface, settings, content);
        simulation.AttachMap(map);
        return map;
    }

    /// <summary>Surface level of every column, indexed <c>x + y·Width</c>.</summary>
    private static int[] BuildSurface(RngStreams random, WorldGenDef settings)
    {
        int width = settings.Width, height = settings.Height;
        var terrain = new GradientNoise(random.Get("worldgen.terrain"));
        var meander = new GradientNoise(random.Get("worldgen.meander"));
        double scale = settings.TerrainNoiseScale;
        var minLevel = settings.SoilDepthMax + 1;
        var maxLevel = settings.Depth - 2;
        var halfWidth = width / 2.0;
        var hillRun = Math.Max(1.0, halfWidth - settings.ValleyHalfWidth);

        var surface = new int[width * height];
        for (var y = 0; y < height; y++)
        {
            var axis = halfWidth + (meander.Spread(y / scale, 0.5, Octaves) * settings.ValleyMeander);
            for (var x = 0; x < width; x++)
            {
                var distance = Math.Abs(x + 0.5 - axis);
                var t = Math.Clamp((distance - settings.ValleyHalfWidth) / hillRun, 0.0, 1.0);
                var hill = t * t * (3 - (2 * t)) * settings.HillHeight;
                var relief = terrain.Spread(x / scale, y / scale, Octaves) * settings.TerrainAmplitude;
                var level = settings.ValleyFloor + (int)Math.Floor(hill + relief + 0.5);
                surface[x + (y * width)] = Math.Clamp(level, minLevel, maxLevel);
            }
        }

        LimitSlopes(surface, width, height);
        return surface;
    }

    /// <summary>
    /// Lowers every column so that it is at most one level above its north, south, east and west
    /// neighbours: the city-block distance transform, exact in two passes. Lowering never goes
    /// under a neighbour's level, so the minimum level still holds.
    /// </summary>
    private static void LimitSlopes(int[] surface, int width, int height)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = x + (y * width);
                if (x > 0)
                {
                    surface[i] = Math.Min(surface[i], surface[i - 1] + 1);
                }

                if (y > 0)
                {
                    surface[i] = Math.Min(surface[i], surface[i - width] + 1);
                }
            }
        }

        for (var y = height - 1; y >= 0; y--)
        {
            for (var x = width - 1; x >= 0; x--)
            {
                var i = x + (y * width);
                if (x < width - 1)
                {
                    surface[i] = Math.Min(surface[i], surface[i + 1] + 1);
                }

                if (y < height - 1)
                {
                    surface[i] = Math.Min(surface[i], surface[i + width] + 1);
                }
            }
        }
    }

    private static void FillColumns(GameMap map, int[] surface, RngStreams random, WorldGenDef settings, DefDatabase content)
    {
        int width = settings.Width, height = settings.Height;
        double scale = settings.DetailNoiseScale;
        var soilNoise = new GradientNoise(random.Get("worldgen.soil"));
        var rockNoise = new GradientNoise(random.Get("worldgen.rock"));
        var strataNoise = new GradientNoise(random.Get("worldgen.strata"));
        var grassNoise = new GradientNoise(random.Get("worldgen.grass"));

        var soil = (ushort)content.IndexOf<MaterialDef>(settings.SoilMaterial);
        var strata = settings.Strata.Select(stratum => (ushort)content.IndexOf<MaterialDef>(stratum.Material)).ToArray();
        var boundaries = new int[strata.Length - 1];
        var soilRange = settings.SoilDepthMax - settings.SoilDepthMin + 1;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var top = surface[x + (y * width)];
                double sx = x / scale, sy = y / scale;

                var soilDepth = settings.SoilDepthMin + Math.Min(soilRange - 1, (int)(soilNoise.Spread01(sx, sy, Octaves) * soilRange));
                var elevation = top - settings.ValleyFloor;
                var rockEdge = (int)Math.Floor((rockNoise.Spread(sx, sy, Octaves) * settings.RockExposureWobble) + 0.5);
                if (elevation + rockEdge >= settings.RockExposureElevation)
                {
                    soilDepth = 0;
                }

                var level = 0;
                for (var b = 0; b < boundaries.Length; b++)
                {
                    level += settings.Strata[b].Thickness;
                    var wobble = strataNoise.Spread(sx + (b * StrataNoiseOffset), sy, Octaves) * settings.StrataWobble;
                    boundaries[b] = level + (int)Math.Floor(wobble + 0.5);
                }

                for (var z = 0; z < top; z++)
                {
                    var material = z >= top - soilDepth ? soil : strata[StratumAt(z, boundaries)];
                    map.SetTileQuietly(x, y, z, TileShape.Wall, material, TileFlags.None);
                }

                var floor = map.MaterialIndexAt(x, y, top - 1);
                var shape = HasHigherNeighbour(surface, width, height, x, y) ? TileShape.Ramp : TileShape.Floor;
                var grass = floor == soil && HasGrass(grassNoise.Spread01(sx, sy, Octaves), settings.GrassCoverage);
                map.SetTileQuietly(x, y, top, shape, floor, grass ? TileFlags.Grass : TileFlags.None);
            }
        }
    }

    /// <summary>
    /// Grass where the noise is under the coverage. The noise is not uniform, so the share is close
    /// to the coverage rather than exact; 0 and 10 000 still mean none and all.
    /// </summary>
    private static bool HasGrass(double noise01, int coverage) =>
        coverage >= 10_000 || noise01 * 10_000 < coverage;

    private static int StratumAt(int z, int[] boundaries)
    {
        var stratum = 0;
        while (stratum < boundaries.Length && z >= boundaries[stratum])
        {
            stratum++;
        }

        return stratum;
    }

    private static bool HasHigherNeighbour(int[] surface, int width, int height, int x, int y)
    {
        var level = surface[x + (y * width)] + 1;
        return (x > 0 && surface[x - 1 + (y * width)] == level)
               || (x < width - 1 && surface[x + 1 + (y * width)] == level)
               || (y > 0 && surface[x + ((y - 1) * width)] == level)
               || (y < height - 1 && surface[x + ((y + 1) * width)] == level);
    }

    private static void PlantVegetation(Simulation simulation, GameMap map, int[] surface, WorldGenDef settings, DefDatabase content)
    {
        var random = simulation.Random.Get("worldgen.vegetation");
        var forest = new GradientNoise(simulation.Random.Get("worldgen.forest"));
        double scale = settings.ForestNoiseScale;
        var soil = content.IndexOf<MaterialDef>(settings.SoilMaterial);

        for (var y = 0; y < settings.Height; y++)
        {
            for (var x = 0; x < settings.Width; x++)
            {
                var z = surface[x + (y * settings.Width)];
                if (map.ShapeAt(x, y, z) != TileShape.Floor || map.MaterialIndexAt(x, y, z) != soil)
                {
                    continue;
                }

                var elevation = z - settings.ValleyFloor;
                var groveFactor = forest.Spread01(x / scale, y / scale, Octaves) * settings.GroveFactorMax;
                foreach (var rule in settings.Vegetation)
                {
                    if (elevation < rule.MinElevation || elevation > rule.MaxElevation)
                    {
                        continue;
                    }

                    if (random.Chance((int)(rule.Chance * groveFactor)))
                    {
                        var plant = simulation.World.CreateEntity();
                        simulation.World.Add(plant, new Position { X = x, Y = y, Z = z });
                        simulation.World.Add(plant, new Plant { Def = rule.Plant });
                        break;
                    }
                }
            }
        }
    }
}
