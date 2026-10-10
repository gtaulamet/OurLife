using OurLife.Core.Defs;

namespace OurLife.Core.Tests.Defs;

public class DefLoaderTests
{
    private const string SampleDefs = """
        [
          { "type": "Item", "id": "stone", "kind": "raw", "visual": "item.stone", "stackLimit": 20 },
          { "type": "Item", "id": "berries", "kind": "food", "visual": "item.berries", "stackLimit": 50, "nutrition": 800 },
          { "type": "Material", "id": "granite", "kind": "stone", "color": "#8A7F7A",
            "mineWorkTicks": 120, "minedItem": "stone", "minedItemChance": 2500 },
          { "type": "Material", "id": "soil", "kind": "soil", "color": "#6B4F2A", "mineWorkTicks": 40 },
          { "type": "Plant", "id": "berry_bush", "visual": "plant.berry_bush",
            "harvest": { "item": "berries", "count": 5, "workTicks": 60 }, "regrowDays": 4 },
          { "type": "Need", "id": "hunger", "max": 10000, "decayPerHour": 400,
            "lowThreshold": 4000, "criticalThreshold": 1500, "deathAfterHoursAtZero": 72 },
          { "type": "NameList", "id": "test_names", "male": ["Rodrigo"], "female": ["Elvira"] },
          { "type": "Creature", "id": "human", "visual": "creature.human", "moveTicksPerTile": 2,
            "needs": ["hunger"], "nameList": "test_names" }
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
        defs.Get<CreatureDef>("human").Needs.ShouldBe(new[] { "hunger" });
        defs.Get<NameListDef>("test_names").Female.ShouldBe(new[] { "Elvira" });
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
    [InlineData("""[{ "type": "Item", "id": "log", "kind": "raw", "visual": "item.log", "stackLimit": 10, "stacklimit": 3 }]""", "stacklimit")]
    [InlineData("""[{ "type": "Item", "id": "log", "kind": "raw", "visual": "item.log" }]""", "stackLimit")]
    [InlineData("""[{ "type": "Item", "id": "log", "kind": "magic", "visual": "item.log", "stackLimit": 10 }]""", "Item")]
    [InlineData("""{ "type": "Item" }""", "JSON array")]
    [InlineData("""[ { "type": "Item", ] """, "invalid JSON")]
    [InlineData("""[ /* A comment */ { "type": "Knowledge", "id": "writing" } ]""", "invalid JSON")]
    [InlineData("""[ { "type": "Knowledge", "id": "writing" }, ]""", "invalid JSON")]
    [InlineData("""[{ "type": "Knowledge", "id": null }]""", "null in 'id'")]
    [InlineData("""[{ "type": "Item", "id": "log", "kind": "raw", "visual": "item.log", "stackLimit": 10, "requires": null }]""", "null in 'requires'")]
    [InlineData("""[{ "type": "Item", "id": "log", "kind": "raw", "visual": "item.log", "stackLimit": 10, "requires": [null] }]""", "null in 'requires[0]'")]
    [InlineData("""[{ "type": "Plant", "id": "bush", "visual": "plant.bush", "harvest": { "item": null, "count": 1, "workTicks": 1 } }]""", "null in 'harvest.item'")]
    public void Malformed_defs_are_rejected_with_a_clear_message(string json, string expectedFragment)
    {
        var error = Should.Throw<DefValidationException>(() => TestContent.WithDefs(json));

        error.Message.ShouldContain(expectedFragment);
    }

    [Theory]
    [InlineData("""[{ "type": "Material", "id": "granite", "kind": "stone", "color": "#8A7F7A", "mineWorkTicks": 120, "minedItem": "boulder", "minedItemChance": 100 }]""", "minedItem points to 'boulder'")]
    [InlineData("""[{ "type": "Item", "id": "log", "kind": "raw", "visual": "item.log", "stackLimit": 10, "era": "space_age" }]""", "era points to 'space_age'")]
    [InlineData("""[{ "type": "Item", "id": "log", "kind": "raw", "visual": "item.log", "stackLimit": 10, "requires": ["sawmills"] }]""", "requires points to 'sawmills'")]
    [InlineData("""[{ "type": "Material", "id": "granite", "kind": "stone", "color": "grey", "mineWorkTicks": 120 }]""", "#RRGGBB")]
    [InlineData("""[{ "type": "Material", "id": "granite", "kind": "stone", "color": "#8A7F7A\n", "mineWorkTicks": 120 }]""", "#RRGGBB")]
    [InlineData("""[{ "type": "Need", "id": "rest", "max": 10000, "decayPerHour": 600, "lowThreshold": 1000, "criticalThreshold": 3000 }]""", "thresholds")]
    [InlineData("""[{ "type": "Item", "id": "Log", "kind": "raw", "visual": "item.log", "stackLimit": 10 }]""", "invalid id")]
    [InlineData("""[{ "type": "Era", "id": "second_age", "order": 0 }]""", "share order 0")]
    [InlineData("""[{ "type": "TimeConfig", "id": "other_time", "ticksPerSecond": 10, "minutesPerTick": 1, "daysPerSeason": 15, "seasons": ["spring"], "startYear": 1200, "startHour": 6 }]""", "exactly one TimeConfig")]
    public void Invalid_content_is_rejected_with_a_clear_message(string json, string expectedFragment)
    {
        var error = Should.Throw<DefValidationException>(() => TestContent.WithDefs(json));

        error.Message.ShouldContain(expectedFragment);
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
              { "type": "Item", "id": "oak", "kind": "raw", "visual": "item.oak", "stackLimit": 10 },
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
              { "type": "Item", "id": "log", "kind": "raw", "visual": "item.log", "stackLimit": 0 },
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
