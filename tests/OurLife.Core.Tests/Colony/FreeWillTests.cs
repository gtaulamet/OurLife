using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Colony;

/// <summary>The switch, the village and its stock targets (L2 of IP3.5, <c>docs/gdd/libre-albedrio.md</c>).</summary>
public class FreeWillTests
{
    [Fact]
    public void Free_will_starts_off_and_the_command_turns_it_on_and_off()
    {
        var bed = ColonyTestBed.Flat(8);
        bed.Simulation.FreeWill.ShouldBeFalse();

        bed.Simulation.Commands.Enqueue(new SetFreeWillCommand(true));
        bed.Simulation.Tick();
        bed.Simulation.FreeWill.ShouldBeTrue();

        bed.Simulation.Commands.Enqueue(new SetFreeWillCommand(false));
        bed.Simulation.Tick();
        bed.Simulation.FreeWill.ShouldBeFalse();
    }

    [Fact]
    public void The_targets_start_at_the_defaults_in_the_order_of_the_categories()
    {
        var bed = ColonyTestBed.Flat(8);

        FreeWillRules.Targets(bed.Simulation).ShouldBe([new StockTarget("food", 150), new StockTarget("wood", 40)]);
        bed.Simulation.StockTargets.ShouldBeEmpty();
    }

    [Fact]
    public void A_target_is_kept_within_zero_and_the_highest_and_nonsense_is_ignored()
    {
        var bed = ColonyTestBed.Flat(8);

        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand("wood", 60));
        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand("food", 5_000));
        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand("stone", 20));
        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand("gold", 20));
        bed.Simulation.Tick();

        FreeWillRules.Targets(bed.Simulation).ShouldBe([new StockTarget("food", 999), new StockTarget("wood", 60)]);
        bed.Simulation.StockTargets.Select(target => target.Category).ShouldBe(["food", "wood"]);

        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand("wood", -10));
        bed.Simulation.Tick();

        FreeWillRules.Targets(bed.Simulation).ShouldBe([new StockTarget("food", 999), new StockTarget("wood", 0)]);
    }

    [Fact]
    public void The_village_is_centred_on_the_arrival_and_reaches_its_radius_at_any_level()
    {
        var bed = new ColonyTestBed(ColonyTestBed.Level(20, 20, '#'), ColonyTestBed.Level(20, 20, '.'));
        var colonists = ColonyStart.Settle(bed.Simulation, ColonyStart.TheOnlyScenario(bed.Simulation.Content), "Valdeolmos");
        var center = bed.Simulation.VillageCenter!.Value;
        center.ShouldBe(bed.World.Get<Position>(colonists[0]).Cell);

        FreeWillRules.IsNearVillage(bed.Simulation, center).ShouldBeTrue();
        FreeWillRules.IsNearVillage(bed.Simulation, center with { Z = 0 }).ShouldBeTrue();
        FreeWillRules.IsNearVillage(bed.Simulation, Offset(center, 6, 0)).ShouldBeTrue();
        FreeWillRules.IsNearVillage(bed.Simulation, Offset(center, 0, -6)).ShouldBeTrue();
        FreeWillRules.IsNearVillage(bed.Simulation, Offset(center, 7, 0)).ShouldBeFalse();
        FreeWillRules.IsNearVillage(bed.Simulation, Offset(center, 5, 5)).ShouldBeFalse();
        FreeWillRules.IsNearVillage(bed.Simulation, Offset(center, 4, 4)).ShouldBeTrue();
    }

    [Fact]
    public void Without_a_village_nothing_is_near_it()
    {
        var bed = ColonyTestBed.Flat(8);

        bed.Simulation.VillageCenter.ShouldBeNull();
        FreeWillRules.IsNearVillage(bed.Simulation, new TileCoord(2, 2, 1)).ShouldBeFalse();
    }

    [Fact]
    public void An_old_game_gets_its_centre_on_the_colonist_nearest_the_middle_of_them_all()
    {
        var bed = ColonyTestBed.Flat(16);
        bed.AddColonist(14, 14, 1);
        bed.AddColonist(2, 2, 1);
        bed.AddColonist(3, 4, 1);
        bed.AddColonist(5, 2, 1);

        ColonyStart.MigrateVillageCenter(bed.Simulation);

        bed.Simulation.VillageCenter.ShouldBe(new TileCoord(2, 2, 1));
    }

    [Fact]
    public void Free_will_off_without_targets_leaves_the_fingerprint_of_a_game_before_it()
    {
        var bed = ColonyTestBed.Flat(8);
        var before = bed.Simulation.ComputeStateHash();

        bed.Simulation.VillageCenter = new TileCoord(3, 3, 1);
        bed.Simulation.ComputeStateHash().ShouldBe(before);

        bed.Simulation.Commands.Enqueue(new SetFreeWillCommand(true));
        bed.Simulation.ApplyCommandsNow();
        var on = bed.Simulation.ComputeStateHash();
        on.ShouldNotBe(before);

        bed.Simulation.Commands.Enqueue(new SetFreeWillCommand(false));
        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand("wood", 50));
        bed.Simulation.ApplyCommandsNow();
        bed.Simulation.ComputeStateHash().ShouldNotBe(before);
        bed.Simulation.ComputeStateHash().ShouldNotBe(on);
    }

    [Fact]
    public void A_target_order_without_a_category_is_ignored()
    {
        var bed = ColonyTestBed.Flat(8);

        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand(null!, 20));
        bed.Simulation.Tick();

        bed.Simulation.StockTargets.ShouldBeEmpty();
    }

    [Fact]
    public void Content_without_free_will_has_no_targets_and_ignores_the_commands()
    {
        var simulation = new Simulation(1, TestContent.Minimal);
        simulation.VillageCenter = new TileCoord(0, 0, 0);

        simulation.Commands.Enqueue(new SetStockTargetCommand("food", 20));
        simulation.Tick();

        FreeWillRules.ConfigOf(simulation.Content).ShouldBeNull();
        FreeWillRules.Targets(simulation).ShouldBeEmpty();
        simulation.StockTargets.ShouldBeEmpty();
        FreeWillRules.IsNearVillage(simulation, new TileCoord(0, 0, 0)).ShouldBeFalse();
    }

    [Fact]
    public void Two_free_will_configs_are_refused_when_loading()
    {
        Should.Throw<DefValidationException>(() => TestContent.WithDefs($"[{Trait}, {Config("a")}, {Config("b")}]"))
            .Message.ShouldContain("at most one FreeWillConfig");
    }

    [Theory]
    [InlineData("food", null)]
    [InlineData("wood", "no target for 'wood'")]
    [InlineData("gold", "gold")]
    public void A_task_needs_a_target_for_its_category(string category, string? problem)
    {
        var defs = $$"""[{{Trait}}, {{Config("default")}}, { "type": "InitiativeTask", "id": "task", "job": "harvest", "category": "{{category}}" }]""";

        if (problem is null)
        {
            TestContent.WithDefs(defs).All<InitiativeTaskDef>().Count.ShouldBe(1);
        }
        else
        {
            Should.Throw<DefValidationException>(() => TestContent.WithDefs(defs)).Message.ShouldContain(problem);
        }
    }

    [Theory]
    [InlineData("\"villageRadius\": 6", "\"villageRadius\": 0")]
    [InlineData("\"initiativeChancePercent\": 60", "\"initiativeChancePercent\": 101")]
    [InlineData("\"lowMoodBelow\": 30", "\"lowMoodBelow\": 80")]
    [InlineData("\"highMoodAbove\": 70", "\"highMoodAbove\": 101")]
    [InlineData("\"low\": 50", "\"low\": 160")]
    [InlineData("\"targetMax\": 999", "\"targetMax\": 0")]
    [InlineData("\"food\": 150", "\"food\": 1000")]
    [InlineData("\"food\": 150", "\"gold\": 150")]
    [InlineData("\"initiativeTrait\": \"diligence\"", "\"initiativeTrait\": \"greed\"")]
    public void A_wrong_config_is_refused_when_loading(string from, string to)
    {
        Should.Throw<DefValidationException>(() => TestContent.WithDefs($"[{Trait}, {Config("default").Replace(from, to, StringComparison.Ordinal)}]"));
    }

    private static TileCoord Offset(TileCoord cell, int dx, int dy) => cell with { X = cell.X + dx, Y = cell.Y + dy };

    private const string Trait = """{ "type": "Trait", "id": "diligence" }""";

    private static string Config(string id) => $$"""
        { "type": "FreeWillConfig", "id": "{{id}}", "villageRadius": 6, "initiativeChancePercent": 60, "initiativeTrait": "diligence",
          "seekChancePercent": 30, "seekTrait": "diligence", "seekRadius": 10, "avoidRadius": 4, "traitPercent": { "low": 50, "high": 150 },
          "lowMoodBelow": 30, "lowMoodPercent": 50, "highMoodAbove": 70, "highMoodPercent": 120,
          "defaultTargets": { "food": 150 }, "targetMax": 999 }
        """;
}
