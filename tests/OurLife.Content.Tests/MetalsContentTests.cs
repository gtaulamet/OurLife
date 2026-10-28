using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Sim;

namespace OurLife.Content.Tests;

/// <summary>
/// The real content of O8 (<c>docs/gdd/oficios.md</c>, "Talleres", "Recetas" and "Herramientas"): the
/// kiln, the bloomery and the forge, the recipes that go from ore to iron tools, the tools, and the
/// knowledge that opens each step.
/// </summary>
public class MetalsContentTests
{
    private static string Describe(RecipeDef recipe) =>
        $"{string.Join("+", recipe.Inputs.Select(input => $"{input.Count} {input.Item}"))} -> " +
        $"{string.Join("+", recipe.Outputs.Select(output => $"{output.Count} {output.Item}"))} @ {recipe.Workshop} in {recipe.WorkTicks}" +
        $"{(recipe.Quality ? " with quality" : string.Empty)}, needs {string.Join(",", recipe.Requires)}";

    [Theory]
    [InlineData("charcoal", "4 log -> 2 charcoal @ charcoal_kiln in 240, needs charcoal_burning")]
    [InlineData("iron_ingot", "2 iron_ore+2 charcoal -> 1 iron_ingot @ bloomery in 300, needs metallurgy")]
    [InlineData("iron_axe", "1 iron_ingot+1 handle -> 1 iron_axe @ forge in 360 with quality, needs ironwork")]
    [InlineData("iron_pickaxe", "1 iron_ingot+1 handle -> 1 iron_pickaxe @ forge in 360 with quality, needs ironwork")]
    [InlineData("iron_hoe", "1 iron_ingot+1 handle -> 1 iron_hoe @ forge in 300 with quality, needs ironwork")]
    [InlineData("iron_hammer", "1 iron_ingot+1 handle -> 1 iron_hammer @ forge in 300 with quality, needs ironwork")]
    public void The_recipes_of_fire_and_iron_are_the_ones_of_the_spec(string id, string expected)
    {
        var recipe = GameContent.Defs.Get<RecipeDef>(id);

        Describe(recipe).ShouldBe(expected);
        recipe.Era.ShouldBe("medieval");
    }

    [Fact]
    public void The_forge_recipes_are_listed_axe_pickaxe_hoe_and_hammer()
    {
        Recipes.Of(GameContent.Defs, "forge").Select(recipe => recipe.Id).ShouldBe(["iron_axe", "iron_pickaxe", "iron_hoe", "iron_hammer"]);
        Recipes.Of(GameContent.Defs, "bloomery").Select(recipe => recipe.Id).ShouldBe(["iron_ingot"]);
        Recipes.Of(GameContent.Defs, "charcoal_kiln").Select(recipe => recipe.Id).ShouldBe(["charcoal"]);
    }

    [Fact]
    public void The_iron_tools_speed_up_the_work_of_the_spec_one_per_unit_and_the_work_types_exist()
    {
        var tools = GameContent.Defs.All<ItemDef>().Where(item => item.Tool is not null)
            .Select(item => (item.Id, item.Tool!.WorkType, item.Tool.SpeedPercent, item.StackLimit, item.Category));

        tools.ShouldBe(
        [
            ("iron_axe", "woodcutting", 30, 1, "tool"),
            ("iron_hammer", "construction", 20, 1, "tool"),
            ("iron_hoe", "farming", 25, 1, "tool"),
            ("iron_pickaxe", "mining", 30, 1, "tool"),
        ]);
    }

    [Fact]
    public void A_masterwork_axe_adds_sixty_percent_and_a_poor_one_twenty_two()
    {
        var simulation = new Simulation(1, GameContent.Defs);
        var axe = GameContent.Defs.Get<ItemDef>("iron_axe");

        Tools.SpeedPercent(simulation, axe, null).ShouldBe(30);
        Tools.SpeedPercent(simulation, axe, "poor").ShouldBe(22);
        Tools.SpeedPercent(simulation, axe, "good").ShouldBe(36);
        Tools.SpeedPercent(simulation, axe, "excellent").ShouldBe(45);
        Tools.SpeedPercent(simulation, axe, "masterwork").ShouldBe(60);
    }

    [Fact]
    public void The_kiln_the_bloomery_and_the_forge_are_unlocked_by_the_knowledge_of_fire_and_metal_which_comes_in_a_chain()
    {
        var knowledge = GameContent.Defs.All<KnowledgeDef>().ToDictionary(known => known.Id);
        knowledge["charcoal_burning"].Requires.ShouldBe(["stonecutting"]);
        knowledge["metallurgy"].Requires.ShouldBe(["charcoal_burning"]);
        knowledge["ironwork"].Requires.ShouldBe(["metallurgy"]);

        var simulation = new Simulation(1, GameContent.Defs);
        var unlocks = simulation.Unlocks;
        foreach (var (buildable, needs) in new[] { ("charcoal_kiln", "charcoal_burning"), ("bloomery", "metallurgy"), ("forge", "ironwork") })
        {
            unlocks.Missing(GameContent.Defs.Get<BuildableDef>(buildable), simulation.Progress).ShouldContain(needs);
        }

        unlocks.Missing(GameContent.Defs.Get<RecipeDef>("iron_axe"), simulation.Progress).ShouldContain("ironwork");
    }
}
