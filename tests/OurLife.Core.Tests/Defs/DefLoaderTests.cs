using OurLife.Core.Defs;
using OurLife.Core.Entities;

namespace OurLife.Core.Tests.Defs;

public class DefLoaderTests
{
    private const string SampleDefs = """
        [
          { "type": "Item", "id": "stone", "category": "stone", "kind": "raw", "visual": "item.stone", "stackLimit": 20 },
          { "type": "Item", "id": "berries", "category": "food", "kind": "food", "visual": "item.berries", "stackLimit": 50, "nutrition": 800 },
          { "type": "Material", "id": "granite", "kind": "stone", "color": "#8A7F7A",
            "mineWorkTicks": 120, "minedItem": "stone", "minedItemChance": 2500 },
          { "type": "Material", "id": "soil", "kind": "soil", "color": "#6B4F2A", "mineWorkTicks": 40 },
          { "type": "Plant", "id": "berry_bush", "visual": "plant.berry_bush",
            "harvest": { "item": "berries", "count": 5, "workTicks": 60 }, "regrowDays": 4 },
          { "type": "Need", "id": "hunger", "max": 10000, "decayPerHour": 400,
            "lowThreshold": 4000, "criticalThreshold": 1500, "deathAfterHoursAtZero": 72, "satisfiedBy": "eating" },
          { "type": "Need", "id": "rest", "max": 10000, "decayPerHour": 600,
            "lowThreshold": 3000, "criticalThreshold": 1000, "satisfiedBy": "sleeping", "recoveryPerHour": 900 },
          { "type": "NameList", "id": "test_names", "male": ["Rodrigo"], "female": ["Elvira"], "surnames": ["Pérez"] },
          { "type": "Creature", "id": "human", "visual": "creature.human", "moveTicksPerTile": 2,
            "needs": ["hunger", "rest"], "nameList": "test_names" },
          { "type": "WorkType", "id": "gathering", "order": 2, "jobs": ["harvest"], "light": true, "skill": true },
          { "type": "SkillConfig", "id": "default", "maxLevel": 20, "averageLevel": 10, "experienceBase": 600, "experiencePerLevel": 150, "speedBase": 80,
            "speedPerLevel": 2, "teachingMinLevels": 3, "teachingBase": 100, "teachingPerLevel": 30 },
          { "type": "Scenario", "id": "start", "creature": "human", "colonists": 3, "startNeedsMin": 7000,
            "founderMinYears": 18, "founderMaxYears": 40, "founderSkillMin": 3, "founderSkillMax": 10, "founderSkillBonus": 4, "items": [{ "item": "berries", "count": 10 }] },
          { "type": "LifePace", "id": "fast", "lifeDaysPerDay": 4, "default": true },
          { "type": "LifeStage", "id": "child", "fromYears": 3, "work": "none", "moveSpeedPercent": 90,
            "needsPercent": { "hunger": 70 } },
          { "type": "LifeConfig", "id": "default", "oldAge": [{ "years": 60, "percentPerYear": 1 }, { "years": 100, "percentPerYear": 100 }] },
          { "type": "Trait", "id": "diligence", "workSpeed": { "low": 90, "high": 110 }, "needsBeforeWorkBelow": 25 },
          { "type": "PersonalityConfig", "id": "default", "rolls": 3, "inheritedPercent": 50, "maxDriftPerDay": 3,
            "orientations": { "opposite": 80, "same": 8, "both": 12 } },
          { "type": "AiConfig", "id": "default", "eatTicksPerUnit": 20, "wanderRadius": 6,
            "wanderWaitMin": 60, "wanderWaitMax": 300, "unreachableCooldownTicks": 600, "recheckTicks": 30,
            "nightStartHour": 21, "nightEndHour": 6, "deconstructRefundPercent": 50, "defaultWorkPriority": 3, "idleAlertHours": 2, "craftFetchRadius": 30 }
        ]
        """;

    [Fact]
    public void Every_def_type_loads_with_its_values()
    {
        var defs = TestContent.WithDefs(SampleDefs);

        defs.Get<ItemDef>("berries").Kind.ShouldBe(ItemKind.Food);
        defs.Get<ItemDef>("berries").Nutrition.ShouldBe(800);
        defs.Get<MaterialDef>("granite").MinedItem.ShouldBe("stone");
        defs.Get<MaterialDef>("soil").Kind.ShouldBe(MaterialKind.Soil);
        defs.Get<PlantDef>("berry_bush").Harvest!.Count.ShouldBe(5);
        defs.Get<NeedDef>("hunger").DeathAfterHoursAtZero.ShouldBe(72);
        defs.Get<NeedDef>("rest").SatisfiedBy.ShouldBe(NeedSatisfaction.Sleeping);
        defs.Get<NeedDef>("rest").RecoveryPerHour.ShouldBe(900);
        defs.Get<CreatureDef>("human").Needs.ShouldBe(new[] { "hunger", "rest" });
        defs.Get<WorkTypeDef>("gathering").JobKindsOf().ShouldBe(new[] { JobKind.Harvest });
        defs.Get<ScenarioDef>("start").Items.Single().Count.ShouldBe(10);
        defs.Get<AiConfigDef>("default").WanderWaitMax.ShouldBe(300);
        defs.Get<WorkTypeDef>("gathering").NameKey.ShouldBe("def.work_type.gathering.name");
        defs.Get<NameListDef>("test_names").Female.ShouldBe(new[] { "Elvira" });
        defs.Get<NameListDef>("test_names").Surnames.ShouldBe(new[] { "Pérez" });
        defs.Get<WorkTypeDef>("gathering").Light.ShouldBeTrue();
        defs.Get<ScenarioDef>("start").FounderMaxYears.ShouldBe(40);
        defs.Get<LifePaceDef>("fast").LifeDaysPerDay.ShouldBe(4);
        defs.Get<LifePaceDef>("fast").Default.ShouldBeTrue();
        defs.Get<LifeStageDef>("child").Work.ShouldBe(StageWork.None);
        defs.Get<LifeStageDef>("child").NeedPercent("hunger").ShouldBe(70);
        defs.Get<LifeStageDef>("child").NeedPercent("rest").ShouldBe(100);
        defs.Get<LifeStageDef>("child").WorkSpeedPercent.ShouldBe(100);
        defs.Get<LifeStageDef>("child").NameKey.ShouldBe("def.life_stage.child.name");
        defs.Get<LifeConfigDef>("default").OldAge[1].PercentPerYear.ShouldBe(100);
        defs.Get<TraitDef>("diligence").WorkSpeed!.At(50).ShouldBe(100);
        defs.Get<TraitDef>("diligence").NeedsBeforeWorkBelow.ShouldBe(25);
        defs.Get<TraitDef>("diligence").NameKey.ShouldBe("def.trait.diligence.name");
        defs.Get<PersonalityConfigDef>("default").Orientations.Both.ShouldBe(12);
        defs.Get<WorkTypeDef>("gathering").Skill.ShouldBeTrue();
        defs.Get<SkillConfigDef>("default").ExperienceFor(7).ShouldBe(1_650);
        defs.Get<SkillConfigDef>("default").SpeedAt(20).ShouldBe(120);
        defs.Get<MaterialDef>("granite").SourceFile.ShouldBe("defs/extra.json");
    }

    [Fact]
    public void Defs_of_a_type_are_sorted_by_id_which_gives_their_index()
    {
        var defs = TestContent.WithDefs(SampleDefs);

        defs.All<MaterialDef>().Select(material => material.Id).ShouldBe(new[] { "granite", "soil" });
        defs.IndexOf<MaterialDef>("soil").ShouldBe(1);
    }

    [Fact]
    public void Name_keys_follow_the_localization_convention()
    {
        var defs = TestContent.WithDefs(SampleDefs);

        defs.Get<PlantDef>("berry_bush").NameKey.ShouldBe("def.plant.berry_bush.name");
        defs.Get<NameListDef>("test_names").NameKey.ShouldBeNull();
    }

    [Theory]
    [InlineData("""[{ "type": "Dragon", "id": "smaug" }]""", "unknown def type 'Dragon'")]
    [InlineData("""[{ "id": "no_type" }]""", "needs a \"type\"")]
    [InlineData("""[{ "type": "Item", "id": "log", "category": "wood", "kind": "raw", "visual": "item.log", "stackLimit": 10, "stacklimit": 3 }]""", "stacklimit")]
    [InlineData("""[{ "type": "Item", "id": "log", "category": "wood", "kind": "raw", "visual": "item.log" }]""", "stackLimit")]
    [InlineData("""[{ "type": "Item", "id": "log", "category": "wood", "kind": "magic", "visual": "item.log", "stackLimit": 10 }]""", "Item")]
    [InlineData("""{ "type": "Item" }""", "JSON array")]
    [InlineData("""[ { "type": "Item", ] """, "invalid JSON")]
    [InlineData("""[ /* A comment */ { "type": "Knowledge", "id": "writing" } ]""", "invalid JSON")]
    [InlineData("""[ { "type": "Knowledge", "id": "writing" }, ]""", "invalid JSON")]
    [InlineData("""[{ "type": "Knowledge", "id": null }]""", "null in 'id'")]
    [InlineData("""[{ "type": "Item", "id": "log", "category": "wood", "kind": "raw", "visual": "item.log", "stackLimit": 10, "requires": null }]""", "null in 'requires'")]
    [InlineData("""[{ "type": "Item", "id": "log", "category": "wood", "kind": "raw", "visual": "item.log", "stackLimit": 10, "requires": [null] }]""", "null in 'requires[0]'")]
    [InlineData("""[{ "type": "Plant", "id": "bush", "visual": "plant.bush", "harvest": { "item": null, "count": 1, "workTicks": 1 } }]""", "null in 'harvest.item'")]
    public void Malformed_defs_are_rejected_with_a_clear_message(string json, string expectedFragment)
    {
        var error = Should.Throw<DefValidationException>(() => TestContent.WithDefs(json));

        error.Message.ShouldContain(expectedFragment);
    }

    [Theory]
    [InlineData("""[{ "type": "Material", "id": "granite", "kind": "stone", "color": "#8A7F7A", "mineWorkTicks": 120, "minedItem": "boulder", "minedItemChance": 100 }]""", "minedItem points to 'boulder'")]
    [InlineData("""[{ "type": "Item", "id": "log", "category": "wood", "kind": "raw", "visual": "item.log", "stackLimit": 10, "era": "space_age" }]""", "era points to 'space_age'")]
    [InlineData("""[{ "type": "Item", "id": "log", "category": "wood", "kind": "raw", "visual": "item.log", "stackLimit": 10, "requires": ["sawmills"] }]""", "requires points to 'sawmills'")]
    [InlineData("""[{ "type": "Material", "id": "granite", "kind": "stone", "color": "grey", "mineWorkTicks": 120 }]""", "#RRGGBB")]
    [InlineData("""[{ "type": "Material", "id": "granite", "kind": "stone", "color": "#8A7F7A\n", "mineWorkTicks": 120 }]""", "#RRGGBB")]
    [InlineData("""[{ "type": "Need", "id": "rest", "max": 10000, "decayPerHour": 600, "lowThreshold": 1000, "criticalThreshold": 3000, "satisfiedBy": "eating" }]""", "thresholds")]
    [InlineData("""[{ "type": "Need", "id": "rest", "max": 10000, "decayPerHour": 600, "lowThreshold": 3000, "criticalThreshold": 1000, "satisfiedBy": "sleeping" }]""", "recoveryPerHour")]
    [InlineData("""[{ "type": "Need", "id": "hunger", "max": 10000, "decayPerHour": 200, "lowThreshold": 3000, "criticalThreshold": 1000, "satisfiedBy": "eating", "recoveryPerHour": 5 }]""", "only needs satisfied by sleeping")]
    [InlineData("""[{ "type": "WorkType", "id": "cooking", "order": 0, "jobs": ["cook"] }]""", "'cook' is not a job")]
    [InlineData("""[{ "type": "WorkType", "id": "mining", "order": 0, "jobs": ["mine", "mine"] }]""", "jobs cannot repeat")]
    [InlineData("""[{ "type": "Scenario", "id": "start", "creature": "dwarf", "colonists": 5, "startNeedsMin": 0, "founderMinYears": 18, "founderMaxYears": 40, "founderSkillMin": 3, "founderSkillMax": 10, "founderSkillBonus": 4 }]""", "creature points to 'dwarf'")]
    [InlineData("""[{ "type": "AiConfig", "id": "ai", "eatTicksPerUnit": 20, "wanderRadius": 6, "wanderWaitMin": 300, "wanderWaitMax": 60, "unreachableCooldownTicks": 600, "recheckTicks": 30, "nightStartHour": 21, "nightEndHour": 6, "deconstructRefundPercent": 50, "defaultWorkPriority": 3, "idleAlertHours": 2, "craftFetchRadius": 30 }]""", "wander waits")]
    [InlineData("""[{ "type": "AiConfig", "id": "ai", "eatTicksPerUnit": 20, "wanderRadius": 6, "wanderWaitMin": 60, "wanderWaitMax": 300, "unreachableCooldownTicks": 600, "recheckTicks": 30, "nightStartHour": 21, "nightEndHour": 21, "deconstructRefundPercent": 50, "defaultWorkPriority": 3, "idleAlertHours": 2, "craftFetchRadius": 30 }]""", "the night must last")]
    [InlineData("""[{ "type": "AiConfig", "id": "ai", "eatTicksPerUnit": 20, "wanderRadius": 6, "wanderWaitMin": 60, "wanderWaitMax": 300, "unreachableCooldownTicks": 600, "recheckTicks": 30, "nightStartHour": 24, "nightEndHour": 6, "deconstructRefundPercent": 50, "defaultWorkPriority": 3, "idleAlertHours": 2, "craftFetchRadius": 30 }]""", "hours from 0 to 23")]
    [InlineData("""[{ "type": "Need", "id": "hunger", "max": 100, "decayPerHour": 1, "lowThreshold": 40, "criticalThreshold": 10, "satisfiedBy": "eating", "nightThreshold": 50 }]""", "only needs satisfied by sleeping have nightThreshold")]
    [InlineData("""[{ "type": "Need", "id": "rest", "max": 100, "decayPerHour": 1, "lowThreshold": 40, "criticalThreshold": 10, "satisfiedBy": "sleeping", "recoveryPerHour": 5, "nightThreshold": 20 }]""", "lowThreshold <= nightThreshold <= max")]
    [InlineData("""[{ "type": "Item", "id": "Log", "category": "wood", "kind": "raw", "visual": "item.log", "stackLimit": 10 }]""", "invalid id")]
    [InlineData("""[{ "type": "Era", "id": "second_age", "order": 0 }]""", "share order 0")]
    [InlineData("""[{ "type": "TimeConfig", "id": "other_time", "ticksPerSecond": 10, "minutesPerTick": 1, "daysPerSeason": 15, "seasons": ["spring"], "startYear": 1200, "startHour": 6 }]""", "exactly one TimeConfig")]
    [InlineData("""[{ "type": "Item", "id": "berries", "category": "food", "kind": "food", "visual": "item.berries", "stackLimit": 50, "nutrition": 800 }, { "type": "Plant", "id": "bush", "visual": "plant.bush", "harvest": { "item": "berries", "count": 5, "workTicks": 60 }, "regrowDays": 4, "dormantSeasons": ["monsoon"] }]""", "no such season")]
    [InlineData("""[{ "type": "Item", "id": "berries", "category": "food", "kind": "food", "visual": "item.berries", "stackLimit": 50, "nutrition": 800 }, { "type": "Plant", "id": "bush", "visual": "plant.bush", "harvest": { "item": "berries", "count": 5, "workTicks": 60 }, "regrowDays": 4, "dormantSeasons": ["winter", "winter"] }]""", "repeats a season")]
    [InlineData("""[{ "type": "Plant", "id": "oak", "visual": "plant.oak", "dormantSeasons": ["winter"] }]""", "only makes sense for a plant that can be gathered")]
    [InlineData("""[{ "type": "Item", "id": "berries", "category": "food", "kind": "food", "visual": "item.berries", "stackLimit": 50, "nutrition": 800 }, { "type": "Plant", "id": "bush", "visual": "plant.bush", "harvest": { "item": "berries", "count": 5, "workTicks": 60 }, "regrowDays": 4, "dormantSeasons": ["spring", "summer", "autumn", "winter"] }]""", "lists every season")]
    [InlineData("""[{ "type": "AiConfig", "id": "ai", "eatTicksPerUnit": 20, "wanderRadius": 6, "wanderWaitMin": 60, "wanderWaitMax": 300, "unreachableCooldownTicks": 600, "recheckTicks": 30, "nightStartHour": 21, "nightEndHour": 6, "deconstructRefundPercent": 120, "defaultWorkPriority": 3, "idleAlertHours": 2, "craftFetchRadius": 30 }]""", "deconstructRefundPercent must be between")]
    [InlineData("""[{ "type": "Item", "id": "log", "category": "wood", "kind": "raw", "visual": "item.log", "stackLimit": 10 }, { "type": "Material", "id": "timber", "kind": "wood", "color": "#9A6B40", "mineWorkTicks": 1, "builtFrom": "log", "minedItem": "log", "minedItemChance": 100 }]""", "cannot have a minedItem")]
    [InlineData("""[{ "type": "Item", "id": "log", "category": "wood", "kind": "raw", "visual": "item.log", "stackLimit": 10 }, { "type": "Material", "id": "timber", "kind": "wood", "color": "#9A6B40", "mineWorkTicks": 1, "builtFrom": "log" }, { "type": "Material", "id": "planks", "kind": "wood", "color": "#9A6B40", "mineWorkTicks": 1, "builtFrom": "log" }]""", "are all built from 'log'")]
    [InlineData("""[{ "type": "Material", "id": "granite", "kind": "stone", "color": "#8A7F7A", "mineWorkTicks": 120 }, { "type": "Buildable", "id": "wall", "shape": "wall", "materials": ["granite"], "cost": 2, "workTicks": 10 }]""", "which has no builtFrom")]
    [InlineData("""[{ "type": "Buildable", "id": "wall", "shape": "wall", "materials": ["adobe"], "cost": 2, "workTicks": 10 }]""", "materials points to 'adobe'")]
    [InlineData("""[{ "type": "Buildable", "id": "wall", "shape": "wall", "materials": [], "cost": 0, "workTicks": 10 }]""", "cost must be at least 1")]
    [InlineData("""[{ "type": "Item", "id": "log", "category": "wood", "kind": "raw", "visual": "item.log", "stackLimit": 10 }, { "type": "Material", "id": "timber", "kind": "wood", "color": "#9A6B40", "mineWorkTicks": 1, "builtFrom": "log" }, { "type": "Buildable", "id": "bed", "shape": "furniture", "materials": ["timber"], "cost": 2, "workTicks": 10 }]""", "furniture needs a visual")]
    [InlineData("""[{ "type": "Item", "id": "log", "category": "wood", "kind": "raw", "visual": "item.log", "stackLimit": 10 }, { "type": "Material", "id": "timber", "kind": "wood", "color": "#9A6B40", "mineWorkTicks": 1, "builtFrom": "log" }, { "type": "Buildable", "id": "wall", "shape": "wall", "materials": ["timber"], "cost": 2, "workTicks": 10, "storage": true }]""", "only furniture can be a bed")]
    [InlineData("""[{ "type": "Item", "id": "log", "category": "wood", "kind": "raw", "visual": "item.log", "stackLimit": 10 }, { "type": "Material", "id": "timber", "kind": "wood", "color": "#9A6B40", "mineWorkTicks": 1, "builtFrom": "log" }, { "type": "Buildable", "id": "bed", "shape": "furniture", "materials": ["timber"], "cost": 2, "workTicks": 10, "visual": "furniture.bed", "restRecoveryPercent": 150, "storage": true }]""", "a bed or a chest, not both")]
    [InlineData("""[{ "type": "LifePace", "id": "frozen", "lifeDaysPerDay": 0 }]""", "lifeDaysPerDay must be positive")]
    [InlineData("""[{ "type": "LifeStage", "id": "baby", "fromYears": 0, "work": "none", "needsPercent": { "thirst": 50 } }]""", "needsPercent points to 'thirst'")]
    [InlineData("""[{ "type": "LifeStage", "id": "baby", "fromYears": 0, "work": "none", "moveSpeedPercent": 0 }]""", "moveSpeedPercent must be positive")]
    [InlineData("""[{ "type": "LifeConfig", "id": "life", "oldAge": [{ "years": 80, "percentPerYear": 10 }, { "years": 70, "percentPerYear": 20 }] }]""", "must go up in years")]
    [InlineData("""[{ "type": "LifeConfig", "id": "life", "oldAge": [{ "years": 80, "percentPerYear": 120 }] }]""", "percentPerYear between 0 and 100")]
    [InlineData("""[{ "type": "Scenario", "id": "start", "creature": "human", "colonists": 5, "startNeedsMin": 0, "founderMinYears": 40, "founderMaxYears": 18, "founderSkillMin": 3, "founderSkillMax": 10, "founderSkillBonus": 4 }]""", "founderMinYears <= founderMaxYears")]
    [InlineData("""[{ "type": "Scenario", "id": "start", "creature": "human", "colonists": 5, "startNeedsMin": 0, "founderMinYears": 18, "founderMaxYears": 40, "founderSkillMin": 3, "founderSkillMax": 10, "founderSkillBonus": 4, "foundingCouple": { "count": 3, "minYears": 20, "maxYears": 30, "opinion": 70, "romance": 100 } }]""", "two colonists for each")]
    [InlineData("""[{ "type": "LifeConfig", "id": "life", "oldAge": [{ "years": 60, "percentPerYear": 1 }], "birth": { "femaleFertileYears": { "min": 18, "max": 45 }, "maleFertileYears": { "min": 18, "max": 60 }, "conceptionChancePerYear": 40, "pregnancyLifeDays": 45, "lateFromLifeDays": 30, "lateWorkSpeedPercent": 90, "yearsBetweenChildren": 1, "parentOpinion": 40, "siblingOpinion": 30, "foodPlantsPerColonist": -1 } }]""", "foodPlantsPerColonist zero or more")]
    [InlineData("""[{ "type": "Trait", "id": "grit", "workSpeed": { "low": 0, "high": 110 } }]""", "workSpeed must be positive")]
    [InlineData("""[{ "type": "Trait", "id": "grit", "needsBeforeWorkBelow": 120 }]""", "needsBeforeWorkBelow must be between")]
    [InlineData("""[{ "type": "PersonalityConfig", "id": "p", "rolls": 0, "inheritedPercent": 50, "maxDriftPerDay": 3, "orientations": { "opposite": 1, "same": 0, "both": 0 } }]""", "rolls must be positive")]
    [InlineData("""[{ "type": "PersonalityConfig", "id": "p", "rolls": 3, "inheritedPercent": 50, "maxDriftPerDay": 3, "orientations": { "opposite": 0, "same": 5, "both": 0 } }]""", "opposite or both above zero")]
    [InlineData("""[{ "type": "SkillConfig", "id": "s", "maxLevel": 20, "averageLevel": 10, "experienceBase": 600, "experiencePerLevel": 150, "speedBase": 0, "speedPerLevel": 2, "teachingMinLevels": 3, "teachingBase": 100, "teachingPerLevel": 30 }]""", "speedBase must be positive")]
    [InlineData("""[{ "type": "SkillConfig", "id": "s", "maxLevel": 20, "averageLevel": 10, "experienceBase": 600, "experiencePerLevel": 150, "speedBase": 80, "speedPerLevel": 2, "teachingMinLevels": 0, "teachingBase": 100, "teachingPerLevel": 30 }]""", "teachingMinLevels must be at least 1")]
    [InlineData("""[{ "type": "Interaction", "id": "flirt", "initiatorStages": ["adult"], "receiverStages": ["adult"], "weight": 15, "romantic": true }]""", "needs romance")]
    [InlineData("""[{ "type": "Interaction", "id": "chat", "initiatorStages": ["toddler"], "receiverStages": ["toddler"], "weight": 60 }]""", "stages points to 'toddler'")]
    [InlineData("""[{ "type": "Interaction", "id": "chat", "initiatorStages": [], "receiverStages": [], "weight": 60, "opinion": { "min": 3, "max": 1 } }]""", "min <= max")]
    [InlineData("""[{ "type": "PersonalityConfig", "id": "p", "rolls": 3, "inheritedPercent": 50, "maxDriftPerDay": 3, "orientations": { "opposite": 1, "same": 0, "both": 0 }, "words": [{ "upTo": 50, "word": "low" }] }]""", "words need bands going up to 100")]
    [InlineData("""[{ "type": "Item", "id": "bread", "category": "food", "kind": "food", "visual": "item.bread", "stackLimit": 50, "nutrition": 800, "spoilDays": -1 }]""", "spoilDays must be zero or positive")]
    [InlineData("""[{ "type": "Item", "id": "plank", "category": "wood", "kind": "raw", "visual": "item.plank", "stackLimit": 25, "meal": true }]""", "only food can be a meal")]
    [InlineData("""[{ "type": "Item", "id": "bread", "category": "food", "kind": "food", "visual": "item.bread", "stackLimit": 50, "nutrition": 800, "reserveValue": 800 }]""", "only for what is not food")]
    [InlineData("""[{ "type": "Item", "id": "iron_axe", "category": "misc", "kind": "raw", "visual": "item.iron_axe", "stackLimit": 5, "tool": { "workType": "woodcutting", "speedPercent": 30 } }, { "type": "WorkType", "id": "woodcutting", "order": 0, "jobs": ["chop"] }]""", "a stackLimit of 1")]
    [InlineData("""[{ "type": "Item", "id": "iron_axe", "category": "misc", "kind": "raw", "visual": "item.iron_axe", "stackLimit": 1, "tool": { "workType": "woodcutting", "speedPercent": 0 } }, { "type": "WorkType", "id": "woodcutting", "order": 0, "jobs": ["chop"] }]""", "positive speedPercent")]
    [InlineData("""[{ "type": "Item", "id": "iron_axe", "category": "misc", "kind": "raw", "visual": "item.iron_axe", "stackLimit": 1, "tool": { "workType": "juggling", "speedPercent": 30 } }]""", "tool.workType points to 'juggling'")]
    [InlineData("""[{ "type": "Quality", "id": "poor", "order": 0, "below": 0, "valuePercent": 75, "comfortPercent": 90 }]""", "below must be positive")]
    [InlineData("""[{ "type": "Quality", "id": "poor", "order": 0, "below": 30, "valuePercent": 0, "comfortPercent": 90 }]""", "valuePercent and comfortPercent must be positive")]
    [InlineData("""[{ "type": "QualityConfig", "id": "q", "defaultQuality": "normal", "die": 100, "perLevel": 3 }]""", "defaultQuality points to 'normal'")]
    [InlineData("""[{ "type": "Quality", "id": "normal", "order": 1, "below": 100, "valuePercent": 100, "comfortPercent": 100 }, { "type": "QualityConfig", "id": "q", "defaultQuality": "normal", "die": 0, "perLevel": 3 }]""", "die must be at least 1")]
    public void Invalid_content_is_rejected_with_a_clear_message(string json, string expectedFragment)
    {
        var error = Should.Throw<DefValidationException>(() => TestContent.WithDefs(json));

        error.Message.ShouldContain(expectedFragment);
    }

    [Fact]
    public void A_work_type_of_the_workshops_has_no_jobs_of_its_own()
    {
        var defs = TestContent.WithDefs("""[{ "type": "WorkType", "id": "carpentry", "order": 6, "jobs": [], "workshop": true, "skill": true }]""");

        defs.Get<WorkTypeDef>("carpentry").Jobs.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""[{ "type": "WorkType", "id": "mining", "order": 1, "jobs": [] }]""", "jobs cannot be empty")]
    [InlineData("""[{ "type": "WorkType", "id": "carpentry", "order": 6, "jobs": ["chop"], "workshop": true }]""", "has no jobs")]
    public void A_work_type_without_jobs_must_be_of_a_workshop_and_the_other_way_round(string json, string expectedFragment)
    {
        var error = Should.Throw<DefValidationException>(() => TestContent.WithDefs(json));

        error.Message.ShouldContain(expectedFragment);
    }

    [Fact]
    public void A_tool_names_the_work_it_speeds_up()
    {
        var defs = TestContent.WithDefs("""
            [
              { "type": "WorkType", "id": "woodcutting", "order": 2, "jobs": ["chop"], "skill": true },
              { "type": "Item", "id": "iron_axe", "category": "misc", "kind": "raw", "visual": "item.iron_axe", "stackLimit": 1, "tool": { "workType": "woodcutting", "speedPercent": 30 } },
              { "type": "Item", "id": "wheat", "category": "misc", "kind": "raw", "visual": "item.wheat", "stackLimit": 75, "spoilDays": 200, "reserveValue": 800 }
            ]
            """);

        defs.Get<ItemDef>("iron_axe").Tool.ShouldNotBeNull().SpeedPercent.ShouldBe(30);
        defs.Get<ItemDef>("wheat").ReserveValue.ShouldBe(800);
        defs.Get<ItemDef>("wheat").SpoilDays.ShouldBe(200);
    }

    [Fact]
    public void A_duplicate_id_names_both_files()
    {
        var error = Should.Throw<DefValidationException>(() => TestContent.Load(
            ("defs/a.json", $"[{TestContent.TimeConfigJson}, {TestContent.FirstEraJson}]"),
            ("defs/b.json", """[{ "type": "Era", "id": "test_age", "order": 1 }]""")));

        error.Message.ShouldContain("defs/b.json");
        error.Message.ShouldContain("already defined in defs/a.json");
    }

    [Fact]
    public void The_same_id_can_be_used_by_different_def_types()
    {
        var defs = TestContent.WithDefs("""
            [
              { "type": "Item", "id": "oak", "category": "misc", "kind": "raw", "visual": "item.oak", "stackLimit": 10 },
              { "type": "Plant", "id": "oak", "visual": "plant.oak", "blocksMovement": true }
            ]
            """);

        defs.Get<ItemDef>("oak").ShouldNotBeNull();
        defs.Get<PlantDef>("oak").BlocksMovement.ShouldBeTrue();
    }

    [Fact]
    public void Every_problem_is_reported_at_once()
    {
        var error = Should.Throw<DefValidationException>(() => TestContent.WithDefs("""
            [
              { "type": "Item", "id": "log", "category": "wood", "kind": "raw", "visual": "item.log", "stackLimit": 0 },
              { "type": "Material", "id": "granite", "kind": "stone", "color": "#8A7F7A", "mineWorkTicks": 0, "minedItem": "boulder", "minedItemChance": 100 }
            ]
            """));

        error.Errors.Count.ShouldBe(3);
    }

    [Fact]
    public void Content_without_eras_is_rejected()
    {
        var error = Should.Throw<DefValidationException>(() =>
            TestContent.Load(("defs/time.json", $"[{TestContent.TimeConfigJson}]")));

        error.Message.ShouldContain("at least one Era");
    }
}
