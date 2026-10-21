using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// The forest comes back (see "Bosque que vuelve" in <c>docs/gdd/libre-albedrio.md</c>). Each midnight a
/// sapling that has had its days becomes an adult tree, and each adult tree of a species with
/// <see cref="PlantDef.Regrowth"/> may leave a sapling on a free soil cell near it, in the band of
/// the valley where the species grows, until the adults and saplings of the species take their
/// share of the cells where it can grow. A sapling blocks nothing; its cell is blocked when it
/// becomes an adult, and never while someone or something stands on it.
/// </summary>
public sealed class ForestSystem : ISimSystem
{
    public const string RandomStream = "nature.forest";

    private const TileFlags Unfit = TileFlags.Blocked | TileFlags.Furniture | GameMap.OrderFlags;

    private readonly List<EntityId> _plants = [];
    private readonly List<EntityId> _scratch = [];
    private readonly List<int> _candidates = [];

    public void Tick(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var settings = simulation.Content.All<WorldGenDef>().FirstOrDefault();
        if (settings is null)
        {
            return;
        }

        var species = Species(simulation.Content, settings);
        if (species.Count == 0)
        {
            return;
        }

        simulation.World.Query<Plant, Position>(_plants);
        GrowUp(simulation, species);
        Seed(simulation, settings, species);
    }

    private static Dictionary<string, PlantDef> Species(DefDatabase content, WorldGenDef settings)
    {
        var species = new Dictionary<string, PlantDef>(StringComparer.Ordinal);
        foreach (var plant in content.All<PlantDef>())
        {
            if (plant.Regrowth is not null && settings.Vegetation.Any(rule => rule.Plant == plant.Id))
            {
                species.Add(plant.Id, plant);
            }
        }

        return species;
    }

    /// <summary>Saplings past their days become trees, if nothing stands on their cell and no order is on it.</summary>
    private void GrowUp(Simulation simulation, Dictionary<string, PlantDef> species)
    {
        var world = simulation.World;
        var map = simulation.Map;
        HashSet<int>? taken = null;
        foreach (var plant in _plants)
        {
            if (!world.TryGet<Sapling>(plant, out var sapling) || simulation.CurrentTick < sapling.GrowsAtTick
                || !species.ContainsKey(world.Get<Plant>(plant).Def))
            {
                continue;
            }

            var cell = world.Get<Position>(plant).Cell;
            taken ??= TakenCells(simulation, skipPlants: true);
            if (taken.Contains(map.Size.IndexOf(cell.X, cell.Y, cell.Z)) || (map.FlagsAt(cell) & GameMap.OrderFlags) != 0)
            {
                continue;
            }

            world.Remove<Sapling>(plant);
            map.SetFlags(cell.X, cell.Y, cell.Z, map.FlagsAt(cell) | TileFlags.Blocked);
            simulation.Events.Emit(new EntityChangedEvent(plant));
        }
    }

    private void Seed(Simulation simulation, WorldGenDef settings, Dictionary<string, PlantDef> species)
    {
        var world = simulation.World;
        var map = simulation.Map;
        var random = simulation.Random.Get(RandomStream);
        var season = simulation.CurrentDate.SeasonId;
        var soil = simulation.Content.IndexOf<MaterialDef>(settings.SoilMaterial);
        var covers = new Dictionary<string, int>(StringComparer.Ordinal);
        var limits = new Dictionary<string, (int Cap, VegetationRule Band)>(StringComparer.Ordinal);
        var adults = new List<EntityId>();
        foreach (var plant in _plants)
        {
            var id = world.Get<Plant>(plant).Def;
            if (!species.ContainsKey(id))
            {
                continue;
            }

            covers[id] = covers.GetValueOrDefault(id) + 1;
            if (!world.Has<Sapling>(plant))
            {
                adults.Add(plant);
            }
        }

        HashSet<int>? taken = null;
        foreach (var tree in adults)
        {
            var id = world.Get<Plant>(tree).Def;
            var regrowth = species[id].Regrowth!;
            if (regrowth.NoSeedSeasons.Contains(season) || !random.Chance(regrowth.SeedChanceBasisPoints))
            {
                continue;
            }

            if (!limits.TryGetValue(id, out var limit))
            {
                var band = settings.Vegetation.First(rule => rule.Plant == id);
                limit = ((int)((long)GrowthCells(map, settings, band, soil) * regrowth.MaxCoverPerMille / 1000), band);
                limits[id] = limit;
            }

            if (covers.GetValueOrDefault(id) >= limit.Cap)
            {
                continue;
            }

            taken ??= TakenCells(simulation, skipPlants: false);
            FreeCellsAround(map, settings, limit.Band, soil, taken, world.Get<Position>(tree).Cell, regrowth.SeedRadius);
            if (_candidates.Count == 0)
            {
                continue;
            }

            var chosen = _candidates[random.NextInt(_candidates.Count)];
            var cell = map.Size.CoordOf(chosen);
            var sapling = world.CreateEntity();
            world.Add(sapling, new Position { X = cell.X, Y = cell.Y, Z = cell.Z });
            world.Add(sapling, new Plant { Def = id });
            world.Add(sapling, new Sapling { GrowsAtTick = simulation.CurrentTick + ((long)regrowth.GrowDays * simulation.Time.TicksPerDay) });
            simulation.Events.Emit(new EntitySpawnedEvent(sapling));
            taken.Add(chosen);
            covers[id] = covers.GetValueOrDefault(id) + 1;
        }
    }

    /// <summary>Soil floors under open sky in the band of the valley where the rule plants: the cells the species can take.</summary>
    private static int GrowthCells(GameMap map, WorldGenDef settings, VegetationRule band, int soil)
    {
        var count = 0;
        for (var y = 0; y < map.Size.Height; y++)
        {
            for (var x = 0; x < map.Size.Width; x++)
            {
                count += CanGrowOn(map, settings, band, soil, x, y, out _) ? 1 : 0;
            }
        }

        return count;
    }

    private static bool CanGrowOn(GameMap map, WorldGenDef settings, VegetationRule band, int soil, int x, int y, out int z)
    {
        z = map.SurfaceZ(x, y);
        var elevation = z - settings.ValleyFloor;
        return z >= 0 && elevation >= band.MinElevation && elevation <= band.MaxElevation
               && map.ShapeAt(x, y, z) == TileShape.Floor && map.MaterialIndexAt(x, y, z) == soil;
    }

    private void FreeCellsAround(GameMap map, WorldGenDef settings, VegetationRule band, int soil, HashSet<int> taken, TileCoord origin, int radius)
    {
        _candidates.Clear();
        for (var y = origin.Y - radius; y <= origin.Y + radius; y++)
        {
            for (var x = origin.X - radius; x <= origin.X + radius; x++)
            {
                if (x < 0 || y < 0 || x >= map.Size.Width || y >= map.Size.Height
                    || !CanGrowOn(map, settings, band, soil, x, y, out var z) || (map.FlagsAt(x, y, z) & Unfit) != 0)
                {
                    continue;
                }

                var index = map.Size.IndexOf(x, y, z);
                if (!taken.Contains(index))
                {
                    _candidates.Add(index);
                }
            }
        }
    }

    /// <summary>
    /// The cells where a sapling would be in the way or in danger: stockpiles, blueprints, furniture
    /// and everything with a position, leaving out the plants if <paramref name="skipPlants"/>.
    /// </summary>
    private HashSet<int> TakenCells(Simulation simulation, bool skipPlants)
    {
        var world = simulation.World;
        var size = simulation.Map.Size;
        var taken = Stockpiles.TakenCells(simulation);
        foreach (var cell in Construction.OccupiedCells(simulation))
        {
            taken.Add(size.IndexOf(cell.X, cell.Y, cell.Z));
        }

        world.Query<Position>(_scratch);
        foreach (var entity in _scratch)
        {
            if (!skipPlants || !world.Has<Plant>(entity))
            {
                var cell = world.Get<Position>(entity).Cell;
                taken.Add(size.IndexOf(cell.X, cell.Y, cell.Z));
            }
        }

        return taken;
    }
}
