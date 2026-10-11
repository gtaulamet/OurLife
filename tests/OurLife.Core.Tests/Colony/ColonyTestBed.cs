using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Core.Tests.Map;

namespace OurLife.Core.Tests.Colony;

/// <summary>
/// A small world for colony scenarios: test content with the numbers of the IP1 content, a map
/// built from text (every solid cell is <c>rock</c>), the colony systems, and helpers to place
/// colonists, plants and items. Rock always leaves a stone when dug, so tests can count them.
/// </summary>
internal sealed class ColonyTestBed
{
    public const string ContentJson = """
        [
          { "type": "Material", "id": "rock", "kind": "stone", "color": "#8C7F7D", "mineWorkTicks": 30,
            "minedItem": "stone", "minedItemChance": 10000 },
          { "type": "Item", "id": "stone", "kind": "raw", "visual": "item.stone", "stackLimit": 25 },
          { "type": "Item", "id": "log", "kind": "raw", "visual": "item.log", "stackLimit": 25 },
          { "type": "Item", "id": "berries", "kind": "food", "visual": "item.berries", "stackLimit": 75, "nutrition": 400 },
          { "type": "Item", "id": "provisions", "kind": "food", "visual": "item.provisions", "stackLimit": 50, "nutrition": 2500 },
          { "type": "Plant", "id": "tree", "visual": "plant.tree", "blocksMovement": true,
            "chop": { "item": "log", "count": 4, "workTicks": 60 } },
          { "type": "Plant", "id": "bush", "visual": "plant.bush",
            "harvest": { "item": "berries", "count": 8, "workTicks": 30 }, "regrowDays": 4, "dormantSeasons": ["winter"] },
          { "type": "Plant", "id": "fruit_tree", "visual": "plant.fruit_tree", "blocksMovement": true,
            "harvest": { "item": "berries", "count": 8, "workTicks": 30 }, "regrowDays": 4 },
          { "type": "Need", "id": "hunger", "max": 10000, "decayPerHour": 200, "lowThreshold": 4000,
            "criticalThreshold": 1500, "deathAfterHoursAtZero": 72, "satisfiedBy": "eating" },
          { "type": "Need", "id": "rest", "max": 10000, "decayPerHour": 600, "lowThreshold": 3000,
            "criticalThreshold": 1000, "satisfiedBy": "sleeping", "recoveryPerHour": 900 },
          { "type": "NameList", "id": "test_names", "male": ["Rodrigo", "Sancho", "Pedro"], "female": ["Elvira", "Urraca", "Jimena"] },
          { "type": "Creature", "id": "human", "visual": "creature.human", "moveTicksPerTile": 2,
            "needs": ["hunger", "rest"], "nameList": "test_names" },
          { "type": "WorkType", "id": "mining", "order": 0, "jobs": ["mine", "dig_stairs"] },
          { "type": "WorkType", "id": "woodcutting", "order": 1, "jobs": ["chop"] },
          { "type": "WorkType", "id": "gathering", "order": 2, "jobs": ["harvest"] },
          { "type": "AiConfig", "id": "default", "eatTicksPerUnit": 20, "wanderRadius": 6,
            "wanderWaitMin": 60, "wanderWaitMax": 300, "unreachableCooldownTicks": 600, "recheckTicks": 30 },
          { "type": "Scenario", "id": "start", "creature": "human", "colonists": 5, "startNeedsMin": 7000,
            "items": [{ "item": "provisions", "count": 30 }] }
        ]
        """;

    private static readonly Lazy<DefDatabase> ContentLazy = new(() => TestContent.WithDefs(ContentJson));

    public ColonyTestBed(params string[] levels)
        : this(1, levels)
    {
    }

    public ColonyTestBed(ulong seed, params string[] levels)
    {
        Simulation = new Simulation(seed, Content);
        Map = AsciiMap.ParseWith(Content.All<MaterialDef>(), levels);
        Simulation.AttachMap(Map);
        ColonySystems.AddTo(Simulation);
    }

    public static DefDatabase Content => ContentLazy.Value;

    public Simulation Simulation { get; }

    public GameMap Map { get; }

    public World World => Simulation.World;

    /// <summary>A level of <paramref name="width"/> × <paramref name="height"/> cells, all the same symbol.</summary>
    public static string Level(int width, int height, char symbol) =>
        string.Join("\n", Enumerable.Repeat(new string(symbol, width), height));

    /// <summary>Rock below and an open floor of <paramref name="size"/> × <paramref name="size"/> on level 1.</summary>
    public static ColonyTestBed Flat(int size, ulong seed = 1) =>
        new(seed, Level(size, size, '#'), Level(size, size, '.'));

    public EntityId AddColonist(int x, int y, int z, int hunger = 10_000, int rest = 10_000, string name = "Elvira")
    {
        var colonist = World.CreateEntity();
        World.Add(colonist, new Position { X = x, Y = y, Z = z });
        World.Add(colonist, new Colonist { Name = name, Gender = Gender.Female, Creature = "human" });
        World.Add(colonist, new Needs
        {
            Values =
            [
                new NeedState { Need = "hunger", Value = hunger },
                new NeedState { Need = "rest", Value = rest },
            ],
        });
        World.Add(colonist, new Movement());
        return colonist;
    }

    public EntityId AddPlant(string def, int x, int y, int z)
    {
        var plant = World.CreateEntity();
        World.Add(plant, new Position { X = x, Y = y, Z = z });
        World.Add(plant, new Plant { Def = def });
        if (Content.Get<PlantDef>(def).BlocksMovement)
        {
            Map.SetFlags(x, y, z, Map.FlagsAt(x, y, z) | TileFlags.Blocked);
        }

        return plant;
    }

    public void AddItems(string def, int count, int x, int y, int z) =>
        ItemPiles.Drop(Simulation, def, count, new TileCoord(x, y, z));

    /// <summary>Applies an order at the start of the next tick.</summary>
    public void Designate(DesignationKind kind, TileCoord from, TileCoord to) =>
        Simulation.Commands.Enqueue(new DesignateCommand(kind, from, to));

    public NeedState Need(EntityId colonist, string need) => World.Get<Needs>(colonist).Find(need)!;

    public Job? JobOf(EntityId colonist) => World.TryGet<Job>(colonist, out var job) ? job : null;

    /// <summary>Total units of an item lying anywhere.</summary>
    public int CountItems(string def)
    {
        var piles = new List<EntityId>();
        World.Query<ItemStack>(piles);
        return piles.Select(World.Get<ItemStack>).Where(stack => stack.Def == def).Sum(stack => stack.Count);
    }

    public List<EntityId> Colonists()
    {
        var colonists = new List<EntityId>();
        World.Query<Colonist>(colonists);
        return colonists;
    }

    public List<ISimEvent> DrainEvents()
    {
        var events = new List<ISimEvent>();
        Simulation.Events.DrainTo(events);
        return events;
    }
}
