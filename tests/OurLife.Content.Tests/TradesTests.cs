using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
using OurLife.Core.WorldGen;

namespace OurLife.Content.Tests;

/// <summary>
/// The real content of O2 of IP4 (<c>docs/gdd/oficios.md</c>): the qualities, the skills of the
/// trades, the new items, categories and materials, and the planks of the arrival.
/// </summary>
public class TradesTests
{
    private static readonly string[] Trades = ["carpentry", "stonecutting", "cooking", "farming", "research", "smelting", "smithing"];

    [Fact]
    public void The_content_has_the_five_qualities_of_the_spec_and_the_roll_that_picks_them()
    {
        var rules = QualityRules.From(GameContent.Defs);

        rules.Ladder.Select(quality => quality.Id).ShouldBe(["poor", "normal", "good", "excellent", "masterwork"]);
        rules.Ladder.Select(quality => quality.Below).ShouldBe([30, 100, 130, 150, 9_999]);
        rules.Ladder.Select(quality => quality.ValuePercent).ShouldBe([75, 100, 120, 150, 200]);
        rules.Ladder.Select(quality => quality.ComfortPercent).ShouldBe([90, 100, 110, 120, 130]);
        rules.Default.Id.ShouldBe("normal");
        (rules.Config.Die, rules.Config.PerLevel).ShouldBe((100, 3));
    }

    [Theory]
    [InlineData(0, 30, 70, 0, 0, 0)]
    [InlineData(5, 15, 70, 15, 0, 0)]
    [InlineData(10, 0, 70, 30, 0, 0)]
    [InlineData(15, 0, 55, 30, 15, 0)]
    [InlineData(20, 0, 40, 30, 20, 10)]
    public void The_real_content_gives_the_distributions_of_the_spec(int level, int poor, int normal, int good, int excellent, int masterwork)
    {
        var rules = QualityRules.From(GameContent.Defs);

        var counts = Enumerable.Range(0, rules.Config.Die).GroupBy(die => rules.ForScore(die + (rules.Config.PerLevel * level)).Id)
            .ToDictionary(group => group.Key, group => group.Count());

        counts.GetValueOrDefault("poor").ShouldBe(poor);
        counts.GetValueOrDefault("normal").ShouldBe(normal);
        counts.GetValueOrDefault("good").ShouldBe(good);
        counts.GetValueOrDefault("excellent").ShouldBe(excellent);
        counts.GetValueOrDefault("masterwork").ShouldBe(masterwork);
    }

    [Fact]
    public void Only_a_level_17_or_more_can_make_a_masterwork()
    {
        var rules = QualityRules.From(GameContent.Defs);
        var best = rules.Config.Die - 1;

        rules.ForScore(best + (rules.Config.PerLevel * 16)).Id.ShouldBe("excellent");
        rules.ForScore(best + (rules.Config.PerLevel * 17)).Id.ShouldBe("masterwork");
    }

    [Fact]
    public void The_seven_trades_are_work_types_with_a_skill_and_no_jobs_of_their_own()
    {
        var types = GameContent.Defs.All<WorkTypeDef>().ToDictionary(type => type.Id);

        foreach (var trade in Trades)
        {
            types[trade].Skill.ShouldBeTrue(trade);
            types[trade].Jobs.ShouldBeEmpty(trade);
        }

        Trades.Where(trade => types[trade].Light).ShouldBe(["cooking", "farming", "research"]);
        SkillRules.From(GameContent.Defs).WorkTypes.Count.ShouldBe(13);
        ColonyRules.From(GameContent.Defs).WorkTypes.Select(type => type.Id).Take(6)
            .ShouldBe(["childcare", "mining", "woodcutting", "gathering", "construction", "hauling"]);
    }

    [Fact]
    public void An_adolescent_does_the_light_trades_but_not_the_heavy_ones()
    {
        var adolescent = LifeRules.From(GameContent.Defs).Stages.Single(stage => stage.Id == "adolescent");

        Trades.Where(trade => LifeRules.DoesWork(adolescent, GameContent.Defs.Get<WorkTypeDef>(trade))).ShouldBe(["cooking", "farming", "research"]);
    }

    [Fact]
    public void The_new_categories_items_and_materials_are_in_the_content_as_the_spec_says()
    {
        ItemCategoryDef.InOrder(GameContent.Defs).Select(category => category.Id).ShouldBe(["food", "wood", "stone", "grain", "refined", "ore", "tool"]);

        var items = GameContent.Defs.All<ItemDef>().ToDictionary(item => item.Id);
        items["plank"].Category.ShouldBe("refined");
        items["plank"].StackLimit.ShouldBe(25);
        items["wheat"].ReserveValue.ShouldBe(800);
        items["wheat"].SpoilDays.ShouldBe(200);
        items["bread"].Meal.ShouldBeTrue();
        items["bread"].Nutrition.ShouldBe(800);
        items["berries"].SpoilDays.ShouldBe(60);
        items["provisions"].SpoilDays.ShouldBe(0);
        new[] { "bread", "pottage", "pickled_cabbage" }.ShouldAllBe(id => items[id].Meal && items[id].Kind == ItemKind.Food);

        GameContent.Defs.Get<MaterialDef>("planks").BuiltFrom.ShouldBe("plank");
        GameContent.Defs.Get<MaterialDef>("ashlar").BuiltFrom.ShouldBe("stone_block");
        GameContent.Defs.Get<MaterialDef>("timber").BuiltFrom.ShouldBe("log");
    }

    [Fact]
    public void The_four_iron_tools_are_one_per_pile_and_speed_up_the_work_of_the_spec()
    {
        var tools = GameContent.Defs.All<ItemDef>().Where(item => item.Tool is not null).ToList();

        tools.Select(tool => (tool.Id, tool.Tool!.WorkType, tool.Tool.SpeedPercent)).ShouldBe(
            [("iron_axe", "woodcutting", 30), ("iron_hammer", "construction", 20), ("iron_hoe", "farming", 25), ("iron_pickaxe", "mining", 30)],
            ignoreOrder: true);
        tools.ShouldAllBe(tool => tool.StackLimit == 1 && tool.Category == "tool");
        tools.ShouldAllBe(tool => GameContent.Defs.Get<WorkTypeDef>(tool.Tool!.WorkType).Skill);
    }

    [Fact]
    public void Walls_floors_ramps_and_stairs_accept_the_four_materials_and_the_door_planks_or_timber()
    {
        foreach (var id in new[] { "wall", "floor", "ramp", "stairs" })
        {
            GameContent.Defs.Get<BuildableDef>(id).Materials.ShouldBe(["timber", "masonry", "planks", "ashlar"]);
        }

        GameContent.Defs.Get<BuildableDef>("door").Materials.ShouldBe(["timber", "planks"]);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    public void The_colonists_arrive_with_the_16_planks_and_the_13_skills(ulong seed)
    {
        var simulation = new Simulation(seed, GameContent.Defs);
        WorldGenerator.Generate(simulation, GameContent.Defs.All<WorldGenDef>()[0]);

        var colonists = ColonyStart.Settle(simulation, ColonyStart.TheOnlyScenario(GameContent.Defs), "Valdeolmos");

        Stocks.Count(simulation, "plank").ShouldBe(16);
        Stocks.Count(simulation, "provisions").ShouldBe(42);
        var piles = new List<Core.Ecs.EntityId>();
        simulation.World.Query<ItemStack>(piles);
        piles.Select(simulation.World.Get<ItemStack>).ShouldAllBe(stack => stack.Quality == null && stack.Freshness == null);
        foreach (var colonist in colonists)
        {
            var skills = simulation.World.Get<Skills>(colonist).Values;
            skills.Count.ShouldBe(13);
            Trades.ShouldAllBe(trade => skills.Any(skill => skill.WorkType == trade));
            skills.Select(skill => skill.WorkType).ShouldBe(skills.Select(skill => skill.WorkType).Order(StringComparer.Ordinal));
            skills.ShouldAllBe(skill => skill.Level >= 3 && skill.Level <= 14);
            skills.Count(skill => skill.Level > 10).ShouldBeLessThanOrEqualTo(1);
        }

        // Nobody has a priority of its own: the new types start at the default one.
        colonists.ShouldAllBe(colonist => !simulation.World.Has<WorkPriorities>(colonist));
    }

    [Fact]
    public void A_newborn_starts_with_the_13_skills_at_level_zero()
    {
        var skills = SkillRules.From(GameContent.Defs).Newborn();

        skills.Values.Count.ShouldBe(13);
        skills.Values.ShouldAllBe(skill => skill.Level == 0 && skill.Experience == 0);
    }

    [Fact]
    public void Nobody_takes_a_trade_work_that_does_not_exist_yet_so_a_new_colony_works_as_before()
    {
        // A trade has no jobs until O3: its priority changes nothing and the colonists keep to the old work.
        var simulation = new Simulation(7, GameContent.Defs);
        WorldGenerator.Generate(simulation, GameContent.Defs.All<WorldGenDef>()[0]);
        var colonists = ColonyStart.Settle(simulation, ColonyStart.TheOnlyScenario(GameContent.Defs), "Valdeolmos");
        ColonySystems.AddTo(simulation);
        simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonists[0], "carpentry", 1));
        simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonists[1], "smithing", WorkPriorityLevels.Never));

        simulation.Run(2_000);

        simulation.World.Get<WorkPriorities>(colonists[0]).Entries.Single().WorkType.ShouldBe("carpentry");
        simulation.World.Get<WorkPriorities>(colonists[1]).Entries.Single().Priority.ShouldBe(WorkPriorityLevels.Never);
        simulation.World.Store<Colonist>().Count.ShouldBe(7);
    }
}
