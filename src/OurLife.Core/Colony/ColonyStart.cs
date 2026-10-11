using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Core.Text;

namespace OurLife.Core.Colony;

/// <summary>
/// Brings the colonists of a <see cref="ScenarioDef"/> to the generated map, with what they carry
/// (see "Llegada" in <c>docs/gdd/colonos-e-ia.md</c>). Draws from the random stream <c>colony.start</c>.
/// </summary>
public static class ColonyStart
{
    /// <summary>The only scenario of the content, until IP7 brings the choice of site.</summary>
    /// <exception cref="InvalidOperationException">The content has no Scenario def, or more than one.</exception>
    public static ScenarioDef TheOnlyScenario(DefDatabase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var scenarios = content.All<ScenarioDef>();
        return scenarios.Count == 1
            ? scenarios[0]
            : throw new InvalidOperationException($"The content must have exactly one Scenario def until IP7, found {scenarios.Count}.");
    }

    /// <summary>
    /// Creates the colonists and their items and names the village with the player's
    /// <paramref name="colonyName"/>; returns the colonists by ascending id.
    /// </summary>
    /// <exception cref="InvalidOperationException">The map is missing, the colony already arrived, or there is nowhere to stand.</exception>
    public static IReadOnlyList<EntityId> Settle(Simulation simulation, ScenarioDef scenario, string colonyName)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(colonyName);
        if (simulation.World.Store<Colonist>().Count > 0)
        {
            throw new InvalidOperationException("The colonists already arrived.");
        }

        var arrival = FindArrival(simulation);
        var creature = simulation.Content.Get<CreatureDef>(scenario.Creature);
        var random = simulation.Random.Get("colony.start");
        var spots = SpotsAround(simulation.Map, arrival);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        var colonists = new List<EntityId>();
        for (var i = 0; i < scenario.Colonists; i++)
        {
            var colonist = simulation.World.CreateEntity();
            var spot = spots[i % spots.Count];
            var gender = random.NextBool() ? Gender.Female : Gender.Male;
            simulation.World.Add(colonist, new Position { X = spot.X, Y = spot.Y, Z = spot.Z });
            simulation.World.Add(colonist, new Colonist
            {
                Name = PickName(simulation.Content, creature, gender, random, usedNames),
                Gender = gender,
                Creature = creature.Id,
            });
            simulation.World.Add(colonist, StartingNeeds(simulation.Content, creature, scenario, random));
            simulation.World.Add(colonist, new Movement());
            simulation.Events.Emit(new EntitySpawnedEvent(colonist));
            colonists.Add(colonist);
        }

        foreach (var item in scenario.Items)
        {
            ItemPiles.Drop(simulation, item.Item, item.Count, arrival);
        }

        // Named once the colony is there: a failure above leaves the simulation as it was.
        simulation.ColonyName = colonyName;
        simulation.Events.Emit(new NotificationEvent(
            simulation.CurrentTick,
            LocalizedText.Of("event.colony.arrived").With("count", colonists.Count),
            NotificationSeverity.Info));
        return colonists;
    }

    /// <summary>
    /// The flat floor of the surface nearest the centre of the map, without a plant and in the
    /// largest region of the surface. Ties go to the lowest cell index.
    /// </summary>
    private static TileCoord FindArrival(Simulation simulation)
    {
        var map = simulation.Map;
        var connectivity = simulation.Connectivity;
        var plantCells = Designations.PlantCells(simulation);

        var mainRegion = connectivity.MainSurfaceRegion(map);

        TileCoord? best = null;
        var bestDistance = long.MaxValue;
        for (var y = 0; y < map.Size.Height; y++)
        {
            for (var x = 0; x < map.Size.Width; x++)
            {
                var cell = new TileCoord(x, y, map.SurfaceZ(x, y));
                if (cell.Z < 0 || map.ShapeAt(cell) != TileShape.Floor || !map.IsStandable(cell)
                    || plantCells.Contains(cell) || connectivity.RegionAt(cell) != mainRegion)
                {
                    continue;
                }

                // Doubled coordinates keep the centre of an even map exact.
                long dx = (2 * x) + 1 - map.Size.Width;
                long dy = (2 * y) + 1 - map.Size.Height;
                var distance = (dx * dx) + (dy * dy);
                if (distance < bestDistance)
                {
                    best = cell;
                    bestDistance = distance;
                }
            }
        }

        return best ?? throw new InvalidOperationException("The map has no flat floor on its surface for the colonists to arrive.");
    }

    private static List<TileCoord> SpotsAround(GameMap map, TileCoord arrival)
    {
        var spots = new List<TileCoord>();
        foreach (var (dx, dy) in MapNavigation.Directions.Prepend((0, 0)))
        {
            var cell = new TileCoord(arrival.X + dx, arrival.Y + dy, arrival.Z);
            if (map.IsStandable(cell))
            {
                spots.Add(cell);
            }
        }

        return spots;
    }

    private static string PickName(DefDatabase content, CreatureDef creature, Gender gender, Pcg32 random, HashSet<string> used)
    {
        if (creature.NameList is null)
        {
            return string.Empty;
        }

        var list = content.Get<NameListDef>(creature.NameList);
        var names = gender == Gender.Female ? list.Female : list.Male;
        var free = names.Where(name => !used.Contains(name)).ToArray();
        var name = free.Length > 0 ? free[random.NextInt(free.Length)] : names[random.NextInt(names.Count)];
        used.Add(name);
        return name;
    }

    private static Needs StartingNeeds(DefDatabase content, CreatureDef creature, ScenarioDef scenario, Pcg32 random)
    {
        var needs = new Needs();
        foreach (var id in creature.Needs)
        {
            var need = content.Get<NeedDef>(id);
            var min = Math.Min(scenario.StartNeedsMin, need.Max);
            needs.Values.Add(new NeedState { Need = id, Value = random.NextInt(min, need.Max + 1) });
        }

        return needs;
    }
}
