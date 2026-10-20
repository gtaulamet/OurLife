using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Save;
using OurLife.Core.Sim;

namespace OurLife.Content.Tests;

/// <summary>
/// Games saved by earlier versions load with the content of today, and their colonists get what
/// came after (ADR-013, "Guardado y migración" in <c>docs/gdd/vida-y-generaciones.md</c>). Each file
/// was written by the code and content of its version, with seed 7 and 20 000 ticks:
/// <c>v0.2.5.olsave</c> in format 1, <c>h3.2.olsave</c> in format 2, by H3.2, <c>h3.3.olsave</c>
/// in format 3, by H3.3, <c>h3.4.olsave</c> in format 4, by H3.4, <c>h3.5.olsave</c> in format 5,
/// by H3.5, <c>h3.6.olsave</c> in format 6, by H3.6, <c>v0.3.0.olsave</c> in format 7, by v0.3.0,
/// and <c>l2.olsave</c> in format 8, by L2 of IP3.5, with free will on and a wood target of 70.
/// </summary>
public class OldSaveTests
{
    [Theory]
    [InlineData("v0.2.5.olsave", 1, "0.2.5", 5)]
    [InlineData("h3.2.olsave", 2, "0.3.0-dev", 5)]
    [InlineData("h3.3.olsave", 3, "0.3.0-dev", 5)]
    [InlineData("h3.4.olsave", 4, "0.3.0-dev", 5)]
    [InlineData("h3.5.olsave", 5, "0.3.0-dev", 5)]
    [InlineData("h3.6.olsave", 6, "0.3.0-dev", 5)]
    [InlineData("v0.3.0.olsave", 7, "0.3.0", 7)]
    [InlineData("l2.olsave", 8, "0.3.5-dev", 7)]
    public void An_old_game_loads_with_ages_surnames_personalities_skills_and_a_pace(string file, int format, string gameVersion, int founders)
    {
        var simulation = Load(file, out var header);

        header.FormatVersion.ShouldBe(format);
        header.GameVersion.ShouldBe(gameVersion);
        simulation.CurrentTick.ShouldBe(20_000);
        simulation.LifePace.ShouldBe("fast");
        var colonists = Colonists(simulation);
        colonists.Count.ShouldBe(founders);
        colonists.Select(colonist => LifeRules.AgeYears(simulation, colonist)!.Value).ShouldAllBe(years => years >= 18 && years <= 41);
        colonists.Select(colonist => simulation.World.Get<Colonist>(colonist).Surname).Distinct().Count().ShouldBe(founders);
        colonists.Select(colonist => simulation.World.Get<Personality>(colonist).Traits.Count)
            .ShouldAllBe(count => count == GameContent.Defs.All<TraitDef>().Count);
        colonists.Select(colonist => simulation.World.Get<Skills>(colonist).Values.Count)
            .ShouldAllBe(count => count == GameContent.Defs.All<WorkTypeDef>().Count(type => type.Skill));
        colonists.Select(colonist => simulation.World.Get<Relations>(colonist).Others.Count).ShouldAllBe(count => count == founders - 1);

        // Format 8 (IP3.5): the village centre, migrated to the cell of one of the colonists, or saved.
        if (format <= 7)
        {
            colonists.Select(colonist => simulation.World.Get<Position>(colonist).Cell).ShouldContain(simulation.VillageCenter!.Value);
        }
        else
        {
            simulation.VillageCenter.ShouldNotBeNull();
        }
    }

    [Fact]
    public void A_game_of_format_8_keeps_free_will_and_its_targets()
    {
        var simulation = Load("l2.olsave", out _);

        simulation.FreeWill.ShouldBeTrue();
        simulation.VillageCenter.ShouldNotBeNull();
        // Only the wood target was chosen in that game: food has the content's default.
        var food = GameContent.Defs.All<FreeWillConfigDef>()[0].DefaultTargets["food"];
        FreeWillRules.Targets(simulation).ShouldBe([new StockTarget("food", food), new StockTarget("wood", 70)]);
        simulation.World.Store<Job>().Count.ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData("v0.2.5.olsave")]
    [InlineData("h3.2.olsave")]
    [InlineData("h3.3.olsave")]
    [InlineData("h3.4.olsave")]
    [InlineData("h3.5.olsave")]
    [InlineData("h3.6.olsave")]
    [InlineData("v0.3.0.olsave")]
    [InlineData("l2.olsave")]
    public void The_same_old_game_always_migrates_the_same_way_and_goes_on(string file)
    {
        var first = Load(file, out _);
        var second = Load(file, out _);

        second.ComputeStateHash().ShouldBe(first.ComputeStateHash());

        // Free will is off in an old game, so the fingerprint leaves out the migrated centre.
        second.VillageCenter.ShouldBe(first.VillageCenter);
        first.Run(5_000);
        second.Run(5_000);
        second.ComputeStateHash().ShouldBe(first.ComputeStateHash());
        Colonists(first).ShouldNotBeEmpty();
    }

    private static Simulation Load(string file, out SaveHeader header)
    {
        using var stream = File.OpenRead(RepoPaths.Combine("tests", "OurLife.Content.Tests", "Fixtures", file));
        var simulation = SaveGame.Load(stream, GameContent.Defs, out header);
        ColonySystems.AddTo(simulation);
        return simulation;
    }

    private static List<EntityId> Colonists(Simulation simulation)
    {
        var colonists = new List<EntityId>();
        simulation.World.Query<Colonist>(colonists);
        return colonists;
    }
}
