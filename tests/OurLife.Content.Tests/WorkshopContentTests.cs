using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Core.WorldGen;

namespace OurLife.Content.Tests;

/// <summary>
/// The real content of workshops, recipes and knowledge of O3 (<c>docs/gdd/oficios.md</c>, "Talleres" and
/// "Recetas", and criterion 14): what exists, what is unlocked at the start and what the Core rejects.
/// </summary>
public class WorkshopContentTests
{
    private static readonly string[] Recipes =
    [
        "bread", "charcoal", "flour", "handle", "iron_axe", "iron_hammer", "iron_hoe", "iron_ingot", "iron_pickaxe", "pickled_cabbage", "plank", "pottage", "stone_block",
    ];

    [Fact]
    public void The_recipes_of_O3_exist_and_each_one_is_made_at_a_workshop_with_a_work_type_of_workshops()
    {
        GameContent.Defs.All<RecipeDef>().Select(recipe => recipe.Id).ShouldBe(Recipes);
        foreach (var recipe in GameContent.Defs.All<RecipeDef>())
        {
            var workshop = GameContent.Defs.Get<BuildableDef>(recipe.Workshop);
            workshop.Workstation.ShouldNotBeNull();
            GameContent.Defs.Get<WorkTypeDef>(workshop.Workstation.WorkType).Workshop.ShouldBeTrue();
            recipe.Requires.ShouldNotBeEmpty();
            recipe.Inputs.ShouldNotBeEmpty();
            recipe.WorkTicks.ShouldBeGreaterThan(0);
        }
    }

    [Fact]
    public void Nothing_of_workshops_recipes_or_knowledge_is_of_an_era_after_the_medieval_one()
    {
        var medieval = GameContent.Defs.Get<EraDef>("medieval").Order;
        var defs = GameContent.Defs.All<RecipeDef>().Cast<Def>()
            .Concat(GameContent.Defs.All<KnowledgeDef>())
            .Concat(GameContent.Defs.All<BuildableDef>().Where(buildable => buildable.Workstation is not null));

        foreach (var def in defs)
        {
            def.Era.ShouldNotBeNull($"{def.GetType().Name} '{def.Id}' needs an era");
            GameContent.Defs.Get<EraDef>(def.Era).Order.ShouldBeLessThanOrEqualTo(medieval, $"{def.GetType().Name} '{def.Id}'");
        }
    }

    [Fact]
    public void The_workshops_are_the_benches_the_kitchen_the_scriptorium_and_the_kiln_the_bloomery_and_the_forge()
    {
        var workshops = GameContent.Defs.All<BuildableDef>().Where(buildable => buildable.Workstation is not null)
            .Select(buildable => (buildable.Id, buildable.Workstation!.WorkType, buildable.Cost, buildable.WorkTicks, buildable.Beauty, string.Join(",", buildable.Requires)));

        workshops.ShouldBe(
        [
            ("bloomery", "smelting", 12, 450, -4, "metallurgy"),
            ("carpenter_bench", "carpentry", 6, 240, -1, "carpentry"),
            ("charcoal_kiln", "smelting", 8, 300, -2, "charcoal_burning"),
            ("forge", "smithing", 12, 450, -4, "ironwork"),
            ("kitchen", "cooking", 8, 300, 0, "cooking"),
            ("mason_bench", "stonecutting", 6, 240, -1, "stonecutting"),
            ("scriptorium", "research", 6, 240, 1, ""),
        ]);
        foreach (var workshop in GameContent.Defs.All<BuildableDef>().Where(buildable => buildable.Workstation is not null))
        {
            workshop.IsFurniture.ShouldBeTrue();
        }

        GameContent.Defs.Get<WorkTypeDef>("cooking").Light.ShouldBeTrue();
        GameContent.Defs.Get<WorkTypeDef>("carpentry").Light.ShouldBeFalse();
    }

    [Fact]
    public void The_furniture_is_built_of_planks_at_the_same_cost_and_timber_still_exists()
    {
        var expected = new Dictionary<string, (int Cost, int Beauty)>
        {
            ["bed"] = (4, 4), ["cradle"] = (2, 3), ["table"] = (3, 3), ["chair"] = (2, 2), ["chest"] = (4, 2),
        };

        foreach (var (id, (cost, beauty)) in expected)
        {
            var buildable = GameContent.Defs.Get<BuildableDef>(id);
            buildable.Materials.ShouldBe(["planks"]);
            (buildable.Cost, buildable.Beauty).ShouldBe((cost, beauty));
        }

        GameContent.Defs.Get<BuildableDef>("door").Materials.ShouldContain("planks");
        GameContent.Defs.Get<MaterialDef>("timber").BuiltFrom.ShouldBe("log");
        GameContent.Defs.Get<MaterialDef>("planks").BuiltFrom.ShouldBe("plank");
    }

    [Fact]
    public void A_colony_starts_knowing_carpentry_and_cooking_and_only_what_they_open_is_available()
    {
        var simulation = NewColony();
        var unlocks = simulation.Unlocks;

        simulation.Progress.Knowledge.ShouldBe(["carpentry", "cooking"]);
        foreach (var unlocked in new[] { "plank", "handle", "pottage" })
        {
            unlocks.IsUnlocked(GameContent.Defs.Get<RecipeDef>(unlocked), simulation.Progress).ShouldBeTrue(unlocked);
        }

        foreach (var locked in new[] { "stone_block", "flour", "bread", "pickled_cabbage" })
        {
            unlocks.IsUnlocked(GameContent.Defs.Get<RecipeDef>(locked), simulation.Progress).ShouldBeFalse(locked);
        }

        unlocks.Missing(GameContent.Defs.Get<RecipeDef>("bread"), simulation.Progress).ShouldBe(["milling"]);
        unlocks.Missing(GameContent.Defs.Get<BuildableDef>("mason_bench"), simulation.Progress).ShouldBe(["stonecutting"]);
    }

    [Fact]
    public void The_core_plans_the_carpenter_bench_but_rejects_the_mason_bench_the_village_cannot_build_yet()
    {
        var simulation = NewColony();
        var arrival = simulation.World.Get<Position>(Colonists(simulation)[0]).Cell;
        var (from, to) = (new TileCoord(arrival.X + 1, arrival.Y - 6, arrival.Z), new TileCoord(arrival.X + 6, arrival.Y + 6, arrival.Z));
        var cells = Construction.CellsToPlan(simulation, GameContent.Defs.Get<BuildableDef>("carpenter_bench"), from, to);
        cells.Count.ShouldBeGreaterThan(1);

        simulation.Commands.Enqueue(new PlaceBlueprintCommand("mason_bench", "masonry", cells[0], cells[0]));
        simulation.Commands.Enqueue(new PlaceBlueprintCommand("carpenter_bench", "planks", cells[1], cells[1]));
        simulation.ApplyCommandsNow();

        var blueprints = new List<Core.Ecs.EntityId>();
        simulation.World.Query<Blueprint, Position>(blueprints);
        blueprints.Select(blueprint => (simulation.World.Get<Blueprint>(blueprint).Buildable, simulation.World.Get<Position>(blueprint).Cell))
            .ShouldBe([("carpenter_bench", cells[1])]);
    }

    private static Simulation NewColony()
    {
        var simulation = new Simulation(1, GameContent.Defs);
        WorldGenerator.Generate(simulation, GameContent.Defs.All<WorldGenDef>()[0]);
        ColonyStart.Settle(simulation, ColonyStart.TheOnlyScenario(GameContent.Defs), "Valdeolmos");
        ColonySystems.AddTo(simulation);
        return simulation;
    }

    private static List<Core.Ecs.EntityId> Colonists(Simulation simulation)
    {
        var colonists = new List<Core.Ecs.EntityId>();
        simulation.World.Query<Colonist>(colonists);
        return colonists;
    }
}
