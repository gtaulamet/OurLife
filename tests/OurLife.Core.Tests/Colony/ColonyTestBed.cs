using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Core.Tests.Map;

namespace OurLife.Core.Tests.Colony;

/// <summary>
/// A small world for colony scenarios: test content with the numbers of the game content, a map
/// built from text (every solid cell is <c>rock</c>), the colony systems, and helpers to place
/// colonists, plants and items. Rock always leaves a stone when dug, so tests can count them.
/// </summary>
internal sealed class ColonyTestBed
{
    public const string ContentJson = """
        [
          { "type": "Material", "id": "rock", "kind": "stone", "color": "#8C7F7D", "mineWorkTicks": 30,
            "minedItem": "stone", "minedItemChance": 10000 },
          { "type": "Material", "id": "timber", "kind": "wood", "color": "#9A6B40", "mineWorkTicks": 30, "builtFrom": "log" },
          { "type": "Material", "id": "masonry", "kind": "stone", "color": "#9E9A92", "mineWorkTicks": 30, "builtFrom": "stone" },
          { "type": "Buildable", "id": "wall", "shape": "wall", "materials": ["timber", "masonry"], "cost": 2, "workTicks": 40, "outline": true },
          { "type": "Buildable", "id": "floor", "shape": "floor", "materials": ["timber", "masonry"], "cost": 1, "workTicks": 20 },
          { "type": "Buildable", "id": "door", "shape": "door", "materials": ["timber"], "cost": 3, "workTicks": 40 },
          { "type": "Buildable", "id": "ramp", "shape": "ramp", "materials": ["timber", "masonry"], "cost": 2, "workTicks": 30 },
          { "type": "Buildable", "id": "stairs", "shape": "stairs", "materials": ["timber", "masonry"], "cost": 3, "workTicks": 40 },
          { "type": "Buildable", "id": "bed", "shape": "furniture", "materials": ["timber"], "cost": 4, "workTicks": 40,
            "visual": "furniture.bed", "restRecoveryPercent": 150 },
          { "type": "Buildable", "id": "table", "shape": "furniture", "materials": ["timber"], "cost": 3, "workTicks": 30, "visual": "furniture.table" },
          { "type": "Buildable", "id": "chest", "shape": "furniture", "materials": ["timber"], "cost": 4, "workTicks": 30,
            "visual": "furniture.chest", "storage": true },
          { "type": "Item", "id": "stone", "category": "stone", "kind": "raw", "visual": "item.stone", "stackLimit": 25 },
          { "type": "Item", "id": "log", "category": "wood", "kind": "raw", "visual": "item.log", "stackLimit": 25 },
          { "type": "Item", "id": "berries", "category": "food", "kind": "food", "visual": "item.berries", "stackLimit": 75, "nutrition": 400 },
          { "type": "Item", "id": "provisions", "category": "food", "kind": "food", "visual": "item.provisions", "stackLimit": 50, "nutrition": 2500 },
          { "type": "Plant", "id": "tree", "visual": "plant.tree", "blocksMovement": true,
            "chop": { "item": "log", "count": 4, "workTicks": 60 } },
          { "type": "Plant", "id": "bush", "visual": "plant.bush",
            "harvest": { "item": "berries", "count": 8, "workTicks": 30 }, "regrowDays": 4, "dormantSeasons": ["winter"] },
          { "type": "Plant", "id": "fruit_tree", "visual": "plant.fruit_tree", "blocksMovement": true,
            "harvest": { "item": "berries", "count": 8, "workTicks": 30 }, "regrowDays": 4 },
          { "type": "Need", "id": "hunger", "max": 10000, "decayPerHour": 200, "lowThreshold": 4000,
            "criticalThreshold": 1500, "deathAfterHoursAtZero": 72, "satisfiedBy": "eating", "alertWhenCritical": true },
          { "type": "Need", "id": "rest", "max": 10000, "decayPerHour": 500, "lowThreshold": 3000,
            "criticalThreshold": 1000, "satisfiedBy": "sleeping", "recoveryPerHour": 1200, "nightThreshold": 10000 },
          { "type": "NameList", "id": "test_names", "male": ["Rodrigo", "Sancho", "Pedro", "Godric", "Hugh"],
            "female": ["Elvira", "Urraca", "Jimena", "Edith", "Agnes"], "surnames": ["Pérez", "Smith", "de Lara", "Miller", "Núñez"] },
          { "type": "Creature", "id": "human", "visual": "creature.human", "moveTicksPerTile": 2,
            "needs": ["hunger", "rest"], "nameList": "test_names" },
          { "type": "WorkType", "id": "mining", "order": 0, "jobs": ["mine", "dig_stairs", "channel"], "skill": true },
          { "type": "WorkType", "id": "woodcutting", "order": 1, "jobs": ["chop"], "skill": true },
          { "type": "WorkType", "id": "gathering", "order": 2, "jobs": ["harvest"], "light": true, "skill": true },
          { "type": "WorkType", "id": "construction", "order": 3, "jobs": ["deliver", "build", "deconstruct"], "skill": true },
          { "type": "WorkType", "id": "hauling", "order": 4, "jobs": ["haul"], "light": true, "skill": true },
          { "type": "SkillConfig", "id": "default", "maxLevel": 20, "averageLevel": 10, "experienceBase": 600, "experiencePerLevel": 150, "speedBase": 80,
            "speedPerLevel": 2, "teachingMinLevels": 3, "teachingBase": 100, "teachingPerLevel": 30 },
          { "type": "AiConfig", "id": "default", "eatTicksPerUnit": 20, "wanderRadius": 6,
            "wanderWaitMin": 60, "wanderWaitMax": 300, "unreachableCooldownTicks": 600, "recheckTicks": 30,
            "nightStartHour": 21, "nightEndHour": 6, "deconstructRefundPercent": 50, "defaultWorkPriority": 3, "idleAlertHours": 2 },
          { "type": "LifePace", "id": "normal", "lifeDaysPerDay": 1 },
          { "type": "LifePace", "id": "fast", "lifeDaysPerDay": 4, "default": true },
          { "type": "LifePace", "id": "very_fast", "lifeDaysPerDay": 8 },
          { "type": "LifeStage", "id": "baby", "fromYears": 0, "work": "none", "interacts": false, "moveSpeedPercent": 25,
            "needsPercent": { "hunger": 40, "rest": 150 } },
          { "type": "LifeStage", "id": "child", "fromYears": 3, "work": "none", "moveSpeedPercent": 90, "traitDriftPercent": 200,
            "needsPercent": { "hunger": 70, "rest": 110 } },
          { "type": "LifeStage", "id": "adolescent", "fromYears": 13, "work": "light", "workSpeedPercent": 80, "traitDriftPercent": 200 },
          { "type": "LifeStage", "id": "adult", "fromYears": 18, "work": "all" },
          { "type": "LifeStage", "id": "elder", "fromYears": 60, "work": "all", "workSpeedPercent": 80, "moveSpeedPercent": 80,
            "needsPercent": { "hunger": 90 } },
          { "type": "LifeConfig", "id": "default", "oldAge": [
            { "years": 60, "percentPerYear": 1 }, { "years": 70, "percentPerYear": 4 }, { "years": 80, "percentPerYear": 15 },
            { "years": 90, "percentPerYear": 40 }, { "years": 100, "percentPerYear": 100 } ] },
          { "type": "Trait", "id": "sociability", "interactionChance": { "low": 0, "high": 200 } }, { "type": "Trait", "id": "kindness" },
          { "type": "Trait", "id": "diligence", "workSpeed": { "low": 90, "high": 110 }, "needsBeforeWorkBelow": 25 },
          { "type": "Trait", "id": "curiosity" }, { "type": "Trait", "id": "intelligence", "experience": { "low": 75, "high": 125 } }, { "type": "Trait", "id": "patience" },
          { "type": "PersonalityConfig", "id": "default", "rolls": 3, "inheritedPercent": 50, "maxDriftPerDay": 3,
            "orientations": { "opposite": 80, "same": 8, "both": 12 } },
          { "type": "Interaction", "id": "chat", "initiatorThought": "chatted", "receiverThought": "chatted", "desire": "befriend", "initiatorStages": ["child", "adolescent", "adult", "elder"],
            "receiverStages": ["child", "adolescent", "adult", "elder"], "weight": 60, "initiatorTraits": { "sociability": 2 },
            "opinion": { "min": 1, "max": 3 }, "backfire": { "trait": "patience", "below": 30, "chancePercent": 30, "opinion": { "min": -3, "max": -1 } } },
          { "type": "Interaction", "id": "joke", "initiatorThought": "laughed", "receiverThought": "laughed", "initiatorStages": ["child", "adolescent", "adult", "elder"],
            "receiverStages": ["child", "adolescent", "adult", "elder"], "weight": 20, "initiatorTraits": { "sociability": 2 }, "minOpinion": 10,
            "opinion": { "min": 2, "max": 5 }, "backfire": { "trait": "patience", "below": 30, "chancePercent": 20, "opinion": { "min": -2, "max": -2 } } },
          { "type": "Interaction", "id": "teach", "desire": "learnFrom", "initiatorStages": ["adolescent", "adult", "elder"], "receiverStages": ["adolescent", "adult", "elder"],
            "weight": 30, "receiverTraits": { "curiosity": 2 }, "teaches": true, "receiverOpinion": { "min": 2, "max": 2 } },
          { "type": "Interaction", "id": "argue", "initiatorThought": "argued", "receiverThought": "argued", "desire": "reconcile", "initiatorStages": ["adolescent", "adult", "elder"], "receiverStages": ["adolescent", "adult", "elder"],
            "weight": 5, "initiatorTraits": { "patience": -6, "kindness": -4 }, "maxOpinion": 20, "opinion": { "min": -8, "max": -3 },
            "drift": [{ "who": "both", "trait": "patience", "points": -1, "chancePercent": 20 }] },
          { "type": "Interaction", "id": "comfort", "receiverThought": "comforted", "initiatorStages": ["adolescent", "adult", "elder"], "receiverStages": ["adolescent", "adult", "elder"],
            "weight": 25, "initiatorTraits": { "kindness": 3 }, "receiverMoodBelow": 30, "receiverOpinion": { "min": 3, "max": 6 },
            "drift": [{ "who": "initiator", "trait": "kindness", "points": 1, "chancePercent": 20 }] },
          { "type": "Interaction", "id": "flirt", "rejectedThought": "rebuffed", "initiatorStages": ["adult", "elder"], "receiverStages": ["adult", "elder"], "weight": 15,
            "minOpinion": 20, "romantic": true, "romanceMinOpinion": 0, "romance": { "min": 10, "max": 20 }, "opinion": { "min": 2, "max": 4 } },
          { "type": "Interaction", "id": "play", "initiatorThought": "played", "receiverThought": "played", "initiatorStages": ["child"], "receiverStages": ["child", "adolescent"], "familyReceivers": true,
            "weight": 80, "opinion": { "min": 2, "max": 4 }, "drift": [{ "who": "initiator", "trait": "sociability", "points": 1, "chancePercent": 20 }] },
          { "type": "SocialConfig", "id": "default", "checkTicks": 30, "radius": 4, "pairCooldownHours": 2, "startChancePercent": 20,
            "closeWeightPercent": 200, "friendOpinion": 40, "rivalOpinion": -40, "retellMargin": 10, "coupleOpinion": 50, "coupleRomance": 100, "breakupBelow": 0,
            "forgetPerDay": 1, "familyFloor": 30, "founderOpinion": { "min": -10, "max": 20 }, "widowYears": 1, "recentInteractions": 5 },
          {"type": "Thought", "id": "hungry", "mood": -6, "durationHours": 0, "need": "hunger", "below": "low"},
          {"type": "Thought", "id": "starving", "mood": -15, "durationHours": 0, "need": "hunger", "below": "critical", "replaces": "hungry"},
          {"type": "Thought", "id": "tired", "mood": -4, "durationHours": 0, "need": "rest", "below": "low"},
          {"type": "Thought", "id": "slept_in_bed", "mood": 3, "durationHours": 12},
          {"type": "Thought", "id": "slept_on_floor", "mood": -3, "durationHours": 12},
          {"type": "Thought", "id": "chatted", "mood": 2, "durationHours": 6, "stackLimit": 3},
          {"type": "Thought", "id": "laughed", "mood": 3, "durationHours": 6, "stackLimit": 2},
          {"type": "Thought", "id": "played", "mood": 4, "durationHours": 6, "stackLimit": 2},
          {"type": "Thought", "id": "comforted", "mood": 6, "durationHours": 12},
          {"type": "Thought", "id": "argued", "mood": -5, "durationHours": 12, "stackLimit": 2},
          {"type": "Thought", "id": "rebuffed", "mood": -3, "durationHours": 12},
          {"type": "Thought", "id": "learned", "mood": 3, "durationHours": 24, "stackLimit": 2},
          {"type": "Thought", "id": "new_partner", "mood": 10, "durationHours": 72},
          {"type": "Thought", "id": "breakup", "mood": -10, "durationHours": 72},
          {"type": "Thought", "id": "death_partner", "mood": -20, "durationHours": 240},
          {"type": "Thought", "id": "death_child", "mood": -25, "durationHours": 240, "stackLimit": 2},
          {"type": "Thought", "id": "death_parent", "mood": -15, "durationHours": 168, "stackLimit": 2},
          {"type": "Thought", "id": "death_friend", "mood": -10, "durationHours": 120, "stackLimit": 3},
          {"type": "Thought", "id": "death_colonist", "mood": -3, "durationHours": 48, "stackLimit": 3},
          {"type": "Thought", "id": "desire_fulfilled", "mood": 8, "durationHours": 72, "stackLimit": 2},
          {"type": "Thought", "id": "desire_frustrated", "mood": -6, "durationHours": 72, "stackLimit": 2},
          {"type": "Crisis", "id": "refuse_work", "weight": 10, "traits": {"diligence": -2}, "durationHours": 6, "behavior": "refuseWork", "drift": [{"trait": "patience", "points": -2}]},
          {"type": "Crisis", "id": "isolate", "weight": 10, "traits": {"sociability": -2}, "durationHours": 6, "behavior": "isolate", "drift": [{"trait": "patience", "points": -2}]},
          {"type": "Desire", "id": "learn_from", "kind": "learnFrom", "chancePercent": 30, "scaledBy": "curiosity", "minLevels": 5},
          {"type": "Desire", "id": "befriend", "kind": "befriend", "chancePercent": 20, "scaledBy": "sociability", "minOpinion": 20, "maxOpinion": 39},
          {"type": "Desire", "id": "find_partner", "kind": "findPartner", "chancePercent": 30, "stages": ["adult"]},
          {"type": "Desire", "id": "own_bed", "kind": "ownBed", "chancePercent": 50, "nights": 2},
          {"type": "Desire", "id": "reconcile", "kind": "reconcile", "chancePercent": 40, "scaledBy": "kindness", "minOpinion": 20},
          {"type": "MoodConfig", "id": "default", "base": 50, "workSpeed": [{"upTo": 14, "percent": 80}, {"upTo": 29, "percent": 90}, {"upTo": 70, "percent": 100}, {"upTo": 85, "percent": 105}, {"upTo": 100, "percent": 110}], "crisisBelow": 15, "crisisChancePercent": 10, "maxDesires": 2, "daysBetweenDesires": 1, "desireExpireDays": 10, "grief": {"trait": "sociability", "points": -2}, "sleptInBed": "slept_in_bed", "sleptOnFloor": "slept_on_floor", "learned": "learned", "newPartner": "new_partner", "breakup": "breakup", "deathPartner": "death_partner", "deathChild": "death_child", "deathParent": "death_parent", "deathFriend": "death_friend", "deathColonist": "death_colonist", "desireFulfilled": "desire_fulfilled", "desireFrustrated": "desire_frustrated"},
          { "type": "Scenario", "id": "start", "creature": "human", "colonists": 5, "startNeedsMin": 7000,
            "founderMinYears": 18, "founderMaxYears": 40, "founderSkillMin": 3, "founderSkillMax": 10, "founderSkillBonus": 4,
            "foundingCouple": { "minYears": 20, "maxYears": 30, "opinion": 70, "romance": 100 },
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
        Map = AsciiMap.ParseFilled(Content.All<MaterialDef>(), (ushort)Content.IndexOf<MaterialDef>("rock"), levels);
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

    /// <summary>Units of an item lying on one cell.</summary>
    public int CountItemsAt(string def, TileCoord cell)
    {
        var piles = new List<EntityId>();
        World.Query<ItemStack, Position>(piles);
        return piles.Where(pile => World.Get<Position>(pile).Cell == cell)
            .Select(World.Get<ItemStack>).Where(stack => stack.Def == def).Sum(stack => stack.Count);
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
