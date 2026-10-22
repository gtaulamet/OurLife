using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Core.Text;

namespace OurLife.Core.Colony;

/// <summary>
/// Brings the colonists of a <see cref="ScenarioDef"/> to the generated map, with what they carry
/// (see "Llegada" in <c>docs/gdd/colonos-e-ia.md</c> and <c>docs/gdd/vida-y-generaciones.md</c>).
/// Draws from the random stream <c>colony.start</c>.
/// </summary>
public static class ColonyStart
{
    private const string MigrationStream = "life.migration";

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
    /// Creates the colonists and their items, names the village with the player's
    /// <paramref name="colonyName"/> and keeps the pace of life the player chose, or the default
    /// one; returns the colonists by ascending id. With two or more colonists, the first two are a
    /// woman and a man, in either order, so the colony can have children, and so is each founding
    /// couple, two by two; the rest are drawn.
    /// </summary>
    /// <exception cref="ArgumentException">The content has no LifePace def <paramref name="lifePace"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The map is missing, the colony already arrived, there is nowhere to stand, or the content
    /// lacks the rules of life, personality, skills or relations (see <see cref="LifeRules.From"/>,
    /// <see cref="PersonalityRules.From"/>, <see cref="SkillRules.From"/> and <see cref="SocialRules.From"/>).
    /// </exception>
    public static IReadOnlyList<EntityId> Settle(Simulation simulation, ScenarioDef scenario, string colonyName, string? lifePace = null)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(colonyName);
        if (lifePace is not null && !simulation.Content.TryGet<LifePaceDef>(lifePace, out _))
        {
            throw new ArgumentException($"There is no LifePace def '{lifePace}'.", nameof(lifePace));
        }

        if (simulation.World.Store<Colonist>().Count > 0)
        {
            throw new InvalidOperationException("The colonists already arrived.");
        }

        var life = LifeRules.From(simulation.Content);
        var personalities = PersonalityRules.From(simulation.Content);
        var skills = SkillRules.From(simulation.Content);
        var social = SocialRules.From(simulation.Content);
        var arrival = FindArrival(simulation);
        var creature = simulation.Content.Get<CreatureDef>(scenario.Creature);
        var random = simulation.Random.Get("colony.start");
        var spots = SpotsAround(simulation.Map, arrival);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        var usedSurnames = new HashSet<string>(StringComparer.Ordinal);

        // The couples come first, two by two; without any, the first two are still a woman and a man.
        var couples = scenario.FoundingCouple?.Count ?? 0;
        var colonists = new List<EntityId>();
        for (var i = 0; i < scenario.Colonists; i++)
        {
            var colonist = simulation.World.CreateEntity();
            var spot = spots[i % spots.Count];
            var gender = i % 2 == 1 && i < 2 * Math.Max(couples, 1)
                ? Opposite(simulation.World.Get<Colonist>(colonists[i - 1]).Gender)
                : random.NextBool() ? Gender.Female : Gender.Male;
            simulation.World.Add(colonist, new Position { X = spot.X, Y = spot.Y, Z = spot.Z });
            var name = PickName(simulation.Content, creature, gender, random, usedNames);
            var couple = i < 2 * couples ? scenario.FoundingCouple : null;
            var founder = couple is null
                ? FounderLife(simulation, scenario.FounderMinYears, scenario.FounderMaxYears, life, random)
                : FounderLife(simulation, couple.MinYears, couple.MaxYears, life, random);
            simulation.World.Add(colonist, new Colonist
            {
                Name = name,
                Surname = PickSurname(simulation.Content, creature, random, usedSurnames),
                Gender = gender,
                Creature = creature.Id,
            });
            simulation.World.Add(colonist, founder);
            simulation.World.Add(colonist, personalities.Draw(random, toOtherSex: couple is not null));
            simulation.World.Add(colonist, skills.Founder(scenario, random));
            simulation.World.Add(colonist, StartingNeeds(simulation.Content, creature, scenario, random));
            simulation.World.Add(colonist, new Movement());
            simulation.Events.Emit(new EntitySpawnedEvent(colonist));
            colonists.Add(colonist);
        }

        social.Introduce(simulation, colonists, scenario.FoundingCouple, random);

        foreach (var item in scenario.Items)
        {
            ItemPiles.Drop(simulation, item.Item, item.Count, arrival);
        }

        // Named once the colony is there: a failure above leaves the simulation as it was.
        simulation.ColonyName = colonyName;
        simulation.LifePace = lifePace ?? life.DefaultPace.Id;
        simulation.VillageCenter = arrival;
        simulation.Events.Emit(new NotificationEvent(
            simulation.CurrentTick,
            LocalizedText.Of("event.colony.arrived").With("count", colonists.Count),
            NotificationSeverity.Info));
        return colonists;
    }

    /// <summary>
    /// Gives the colonists of a game saved before IP3 (format 1) what H3.2 added, as if they had
    /// arrived with it: an age in the range of the founders and a surname, drawn from the random
    /// stream <c>life.migration</c> by ascending id. The pace is the default one (ADR-013).
    /// </summary>
    internal static void MigrateLives(Simulation simulation)
    {
        var life = LifeRules.From(simulation.Content);
        var scenario = TheOnlyScenario(simulation.Content);
        var random = simulation.Random.Get(MigrationStream);
        var world = simulation.World;
        var usedSurnames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var colonist in ColonistsOf(world))
        {
            var person = world.Get<Colonist>(colonist);
            world.Add(colonist, FounderLife(simulation, scenario.FounderMinYears, scenario.FounderMaxYears, life, random));
            person.Surname = PickSurname(simulation.Content, simulation.Content.Get<CreatureDef>(person.Creature), random, usedSurnames);
        }

        simulation.LifePace = life.DefaultPace.Id;
    }

    /// <summary>
    /// Gives the colonists of a game saved before H3.3 (format 2) a personality drawn as on
    /// arrival, from the random stream <c>life.migration</c> by ascending id (ADR-013).
    /// </summary>
    internal static void MigratePersonalities(Simulation simulation)
    {
        var personalities = PersonalityRules.From(simulation.Content);
        var random = simulation.Random.Get(MigrationStream);
        foreach (var colonist in ColonistsOf(simulation.World))
        {
            simulation.World.Add(colonist, personalities.Draw(random));
        }
    }

    /// <summary>
    /// Gives the colonists of a game saved before H3.4 (format 3) skills drawn as on arrival, from
    /// the random stream <c>life.migration</c> by ascending id (ADR-013).
    /// </summary>
    internal static void MigrateSkills(Simulation simulation)
    {
        var skills = SkillRules.From(simulation.Content);
        var scenario = TheOnlyScenario(simulation.Content);
        var random = simulation.Random.Get(MigrationStream);
        foreach (var colonist in ColonistsOf(simulation.World))
        {
            simulation.World.Add(colonist, skills.Founder(scenario, random));
        }
    }

    /// <summary>
    /// Gives the colonists of a game saved before H3.7 (format 6) or before the trades of IP4
    /// (format 11) the skills the content added since, like childcare and the seven of the trades,
    /// each drawn between the levels of the founders (without the bonus of one skill, which they
    /// already got on arrival), from the random stream <c>life.migration</c> by ascending id and
    /// work type (ADR-013). Their work priorities are the default one: only the others are saved.
    /// </summary>
    internal static void MigrateNewSkills(Simulation simulation)
    {
        var skills = SkillRules.From(simulation.Content);
        var scenario = TheOnlyScenario(simulation.Content);
        var random = simulation.Random.Get(MigrationStream);
        foreach (var colonist in ColonistsOf(simulation.World))
        {
            if (!simulation.World.TryGet<Skills>(colonist, out var own))
            {
                continue;
            }

            foreach (var type in skills.WorkTypes.Where(type => own.Find(type.Id) is null))
            {
                var at = own.Values.FindIndex(value => string.CompareOrdinal(value.WorkType, type.Id) > 0);
                own.Values.Insert(at < 0 ? own.Values.Count : at, new SkillValue
                {
                    WorkType = type.Id,
                    Level = random.NextInt(scenario.FounderSkillMin, scenario.FounderSkillMax + 1),
                });
            }
        }
    }

    /// <summary>
    /// Gives a game saved before IP3.5 (format 7) the centre of its village, as the arrival is not
    /// kept: the cell of the colonist nearest the middle of them all (the median on each axis), so
    /// one far away at work does not move it; the lowest id among equals. None in a game without
    /// colonists (ADR-013).
    /// </summary>
    internal static void MigrateVillageCenter(Simulation simulation)
    {
        var cells = new List<TileCoord>();
        foreach (var colonist in ColonistsOf(simulation.World))
        {
            if (simulation.World.TryGet<Position>(colonist, out var position))
            {
                cells.Add(position.Cell);
            }
        }

        if (cells.Count == 0)
        {
            return;
        }

        var middleX = cells.Select(cell => cell.X).Order().ElementAt((cells.Count - 1) / 2);
        var middleY = cells.Select(cell => cell.Y).Order().ElementAt((cells.Count - 1) / 2);
        simulation.VillageCenter = cells.MinBy(cell => ((long)(cell.X - middleX) * (cell.X - middleX)) + ((long)(cell.Y - middleY) * (cell.Y - middleY)));
    }

    private static List<EntityId> ColonistsOf(World world)
    {
        var colonists = new List<EntityId>();
        world.Query<Colonist>(colonists);
        return colonists;
    }

    /// <summary>
    /// Gives the colonists of a game saved before H3.5 (format 4) what founders think of each
    /// other, drawn from the random stream <c>life.migration</c>. Nobody arrives as a couple (ADR-013).
    /// </summary>
    internal static void MigrateRelations(Simulation simulation)
    {
        var random = simulation.Random.Get(MigrationStream);
        SocialRules.From(simulation.Content).Introduce(simulation, ColonistsOf(simulation.World), couple: null, random);
    }

    /// <summary>A founder of any day of life between the youngest age and the end of the oldest year.</summary>
    private static Life FounderLife(Simulation simulation, int minYears, int maxYears, LifeRules life, Pcg32 random)
    {
        var daysPerYear = simulation.Time.DaysPerYear;
        var youngest = minYears * daysPerYear;
        var oldest = ((maxYears + 1) * daysPerYear) - 1;
        return life.LifeAt(random.NextInt(youngest, oldest + 1), daysPerYear);
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

    private static Gender Opposite(Gender gender) => gender == Gender.Female ? Gender.Male : Gender.Female;

    /// <summary>A name of the creature's list for the sex that is not in <paramref name="used"/>, or any of them once all are; added to it.</summary>
    internal static string PickName(DefDatabase content, CreatureDef creature, Gender gender, Pcg32 random, HashSet<string> used)
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

    /// <summary>A surname of the list nobody of this arrival has, while there are some left; empty if the list has none.</summary>
    private static string PickSurname(DefDatabase content, CreatureDef creature, Pcg32 random, HashSet<string> used)
    {
        if (creature.NameList is null || content.Get<NameListDef>(creature.NameList).Surnames is not { Count: > 0 } surnames)
        {
            return string.Empty;
        }

        var free = surnames.Where(surname => !used.Contains(surname)).ToArray();
        var surname = free.Length > 0 ? free[random.NextInt(free.Length)] : surnames[random.NextInt(surnames.Count)];
        used.Add(surname);
        return surname;
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
