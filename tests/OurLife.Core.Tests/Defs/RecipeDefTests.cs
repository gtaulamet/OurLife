using OurLife.Core.Defs;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Defs;

/// <summary>Recipes and workshops (<c>docs/gdd/oficios.md</c>, "Talleres" and "Recetas"): what the loader accepts.</summary>
public class RecipeDefTests
{
    private const string Prelude = """
        { "type": "Item", "id": "log", "category": "wood", "kind": "raw", "visual": "item.log", "stackLimit": 25 },
        { "type": "Item", "id": "plank", "category": "wood", "kind": "raw", "visual": "item.plank", "stackLimit": 25 },
        { "type": "Material", "id": "timber", "kind": "wood", "color": "#9A6B40", "mineWorkTicks": 1, "builtFrom": "log" },
        { "type": "WorkType", "id": "carpentry", "order": 6, "jobs": [], "workshop": true, "skill": true },
        { "type": "WorkType", "id": "mining", "order": 1, "jobs": ["mine"], "skill": true },
        { "type": "Quality", "id": "good", "order": 2, "below": 130, "valuePercent": 120, "comfortPercent": 110 },
        { "type": "Knowledge", "id": "carpentry" },
        { "type": "Buildable", "id": "bench", "shape": "furniture", "materials": ["timber"], "cost": 6, "workTicks": 10, "visual": "furniture.bench",
          "requires": ["carpentry"], "workstation": { "workType": "carpentry" }, "beauty": -1 },
        { "type": "Buildable", "id": "table", "shape": "furniture", "materials": ["timber"], "cost": 3, "workTicks": 10, "visual": "furniture.table" }
        """;

    private static DefDatabase Load(string extra) => TestContent.WithDefs($"[{Prelude}, {extra}]");

    [Fact]
    public void A_recipe_points_to_a_workshop_and_items_and_a_workshop_keeps_its_data()
    {
        var defs = Load("""
            { "type": "Recipe", "id": "saw", "workshop": "bench",
              "inputs": [{ "item": "log", "count": 1 }, { "category": "wood", "count": 2, "minQuality": "good" }],
              "outputs": [{ "item": "plank", "count": 2 }], "workTicks": 90, "quality": true, "order": 3 }
            """);

        var recipe = defs.Get<RecipeDef>("saw");
        recipe.Workshop.ShouldBe("bench");
        recipe.Inputs.Select(input => (input.Item, input.Category, input.Count, input.MinQuality))
            .ShouldBe([("log", null, 1, null), (null, "wood", 2, "good")]);
        recipe.MainOutput.Item.ShouldBe("plank");
        recipe.Quality.ShouldBeTrue();
        defs.Get<BuildableDef>("bench").Workstation!.WorkType.ShouldBe("carpentry");
        defs.Get<BuildableDef>("bench").Beauty.ShouldBe(-1);
        defs.Get<BuildableDef>("bench").HasQuality.ShouldBeFalse();
        defs.Get<BuildableDef>("table").HasQuality.ShouldBeTrue();
        recipe.TextCategory.ShouldBe("recipe");
    }

    [Theory]
    [InlineData("""{ "type": "Recipe", "id": "saw", "workshop": "forge", "inputs": [{ "item": "log", "count": 1 }], "outputs": [{ "item": "plank", "count": 2 }], "workTicks": 90 }""", "workshop points to")]
    [InlineData("""{ "type": "Recipe", "id": "saw", "workshop": "table", "inputs": [{ "item": "log", "count": 1 }], "outputs": [{ "item": "plank", "count": 2 }], "workTicks": 90 }""", "is not a workshop")]
    [InlineData("""{ "type": "Recipe", "id": "saw", "workshop": "bench", "inputs": [{ "item": "branch", "count": 1 }], "outputs": [{ "item": "plank", "count": 2 }], "workTicks": 90 }""", "inputs.item points to")]
    [InlineData("""{ "type": "Recipe", "id": "saw", "workshop": "bench", "inputs": [{ "item": "log", "count": 1 }], "outputs": [{ "item": "board", "count": 2 }], "workTicks": 90 }""", "outputs.item points to")]
    [InlineData("""{ "type": "Recipe", "id": "saw", "workshop": "bench", "inputs": [{ "category": "metal", "count": 1 }], "outputs": [{ "item": "plank", "count": 2 }], "workTicks": 90 }""", "inputs.category points to")]
    [InlineData("""{ "type": "Recipe", "id": "saw", "workshop": "bench", "inputs": [{ "item": "log", "count": 1, "minQuality": "divine" }], "outputs": [{ "item": "plank", "count": 2 }], "workTicks": 90 }""", "inputs.minQuality points to")]
    [InlineData("""{ "type": "Recipe", "id": "saw", "workshop": "bench", "inputs": [{ "item": "log", "category": "wood", "count": 1 }], "outputs": [{ "item": "plank", "count": 2 }], "workTicks": 90 }""", "an item or a category, and only one")]
    [InlineData("""{ "type": "Recipe", "id": "saw", "workshop": "bench", "inputs": [{ "count": 1 }], "outputs": [{ "item": "plank", "count": 2 }], "workTicks": 90 }""", "an item or a category, and only one")]
    [InlineData("""{ "type": "Recipe", "id": "saw", "workshop": "bench", "inputs": [], "outputs": [{ "item": "plank", "count": 2 }], "workTicks": 90 }""", "inputs cannot be empty")]
    [InlineData("""{ "type": "Recipe", "id": "saw", "workshop": "bench", "inputs": [{ "item": "log", "count": 1 }], "outputs": [], "workTicks": 90 }""", "outputs cannot be empty")]
    [InlineData("""{ "type": "Recipe", "id": "saw", "workshop": "bench", "inputs": [{ "item": "log", "count": 0 }], "outputs": [{ "item": "plank", "count": 2 }], "workTicks": 90 }""", "counts of inputs and outputs must be positive")]
    [InlineData("""{ "type": "Recipe", "id": "saw", "workshop": "bench", "inputs": [{ "item": "log", "count": 1 }], "outputs": [{ "item": "plank", "count": 2 }], "workTicks": 0 }""", "workTicks must be at least 1")]
    [InlineData("""{ "type": "Recipe", "id": "saw", "workshop": "bench", "requires": ["sawing"], "inputs": [{ "item": "log", "count": 1 }], "outputs": [{ "item": "plank", "count": 2 }], "workTicks": 90 }""", "requires points to")]
    [InlineData("""{ "type": "Buildable", "id": "forge", "shape": "furniture", "materials": ["timber"], "cost": 6, "workTicks": 10, "visual": "furniture.forge", "workstation": { "workType": "smithing" } }""", "workstation.workType points to")]
    [InlineData("""{ "type": "Buildable", "id": "forge", "shape": "furniture", "materials": ["timber"], "cost": 6, "workTicks": 10, "visual": "furniture.forge", "workstation": { "workType": "mining" } }""", "not a work type of workshops")]
    [InlineData("""{ "type": "Buildable", "id": "forge", "shape": "furniture", "materials": ["timber"], "cost": 6, "workTicks": 10, "visual": "furniture.forge", "storage": true, "workstation": { "workType": "carpentry" } }""", "can be a workshop")]
    [InlineData("""{ "type": "Buildable", "id": "wall", "shape": "wall", "materials": ["timber"], "cost": 2, "workTicks": 10, "workstation": { "workType": "carpentry" } }""", "can be a workshop")]
    [InlineData("""{ "type": "Buildable", "id": "wall", "shape": "wall", "materials": ["timber"], "cost": 2, "workTicks": 10, "beauty": 2 }""", "only furniture has beauty")]
    [InlineData("""{ "type": "Scenario", "id": "start", "creature": "human", "colonists": 5, "startNeedsMin": 0, "founderMinYears": 18, "founderMaxYears": 40, "founderSkillMin": 3, "founderSkillMax": 10, "founderSkillBonus": 4, "startingKnowledge": ["alchemy"] }""", "startingKnowledge points to")]
    public void An_invalid_recipe_or_workshop_is_rejected_with_a_clear_message(string extra, string expectedFragment)
    {
        var error = Should.Throw<DefValidationException>(() => Load(extra));

        error.Message.ShouldContain(expectedFragment);
    }

    [Fact]
    public void A_recipe_is_unlocked_by_the_knowledge_it_requires()
    {
        var defs = Load("""
            { "type": "Knowledge", "id": "sawing", "requires": ["carpentry"] },
            { "type": "Recipe", "id": "saw", "requires": ["sawing"], "workshop": "bench",
              "inputs": [{ "item": "log", "count": 1 }], "outputs": [{ "item": "plank", "count": 2 }], "workTicks": 90 }
            """);
        var progress = new ProgressState("test_age");
        var unlocks = new UnlockService(defs);
        var recipe = defs.Get<RecipeDef>("saw");

        unlocks.IsUnlocked(recipe, progress).ShouldBeFalse();
        unlocks.Missing(recipe, progress).ShouldBe(["sawing"]);
        progress.Learn("sawing");
        unlocks.IsUnlocked(recipe, progress).ShouldBeTrue();
        unlocks.Missing(recipe, progress).ShouldBeEmpty();
        unlocks.Missing(defs.Get<BuildableDef>("bench"), progress).ShouldBe(["carpentry"]);
    }
}
