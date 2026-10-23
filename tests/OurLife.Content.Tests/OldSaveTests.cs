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
/// <c>l2.olsave</c> in format 8, by L2 of IP3.5, with free will on and a wood target of 70, and
/// <c>l3.olsave</c> in format 9, by L3, the same with 20 500 ticks, while two gather on their own,
/// and <c>o2.olsave</c> in format 11, by the code of <c>main</c> before O2 of IP4 (the seven skills of
/// the trades), the same game as <c>l3.olsave</c> but with the saplings of L4b, and <c>o3.olsave</c>
/// in format 12, by the code of <c>main</c> before O3 of IP4 (workshops, recipes and furniture of
/// planks): seed 7, 20 500 ticks, free will on, a stockpile, every tree near the arrival ordered
/// down and a bed of timber built from the logs.
/// </summary>
public class OldSaveTests
{
    [Theory]
    [InlineData("v0.2.5.olsave", 1, "0.2.5", 5, 20_000)]
    [InlineData("h3.2.olsave", 2, "0.3.0-dev", 5, 20_000)]
    [InlineData("h3.3.olsave", 3, "0.3.0-dev", 5, 20_000)]
    [InlineData("h3.4.olsave", 4, "0.3.0-dev", 5, 20_000)]
    [InlineData("h3.5.olsave", 5, "0.3.0-dev", 5, 20_000)]
    [InlineData("h3.6.olsave", 6, "0.3.0-dev", 5, 20_000)]
    [InlineData("v0.3.0.olsave", 7, "0.3.0", 7, 20_000)]
    [InlineData("l2.olsave", 8, "0.3.5-dev", 7, 20_000)]
    [InlineData("l3.olsave", 9, "0.3.5-dev", 7, 20_500)]
    [InlineData("o2.olsave", 11, "0.3.5-dev", 7, 20_500)]
    [InlineData("o3.olsave", 12, "0.3.5-dev", 7, 20_500)]
    public void An_old_game_loads_with_ages_surnames_personalities_skills_and_a_pace(string file, int format, string gameVersion, int founders, long tick)
    {
        var simulation = Load(file, out var header);

        header.FormatVersion.ShouldBe(format);
        header.GameVersion.ShouldBe(gameVersion);
        simulation.CurrentTick.ShouldBe(tick);
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

    [Fact]
    public void A_game_of_format_9_keeps_the_work_on_its_own_under_way()
    {
        var simulation = Load("l3.olsave", out _);
        var jobs = new List<EntityId>();
        simulation.World.Query<Job>(jobs);

        jobs.Count(colonist => simulation.World.Get<Job>(colonist) is { Kind: JobKind.Harvest, Initiative: true }).ShouldBe(2);
        simulation.Run(2_000);
        Colonists(simulation).Count.ShouldBe(7);
    }

    [Fact]
    public void A_game_of_format_11_gets_the_seven_skills_of_the_trades_like_a_founder_and_the_default_priorities()
    {
        var simulation = Load("o2.olsave", out _);
        var trades = new[] { "carpentry", "stonecutting", "cooking", "farming", "research", "smelting", "smithing" };
        var scenario = ColonyStart.TheOnlyScenario(GameContent.Defs);

        var colonists = Colonists(simulation);
        colonists.Count.ShouldBe(7);
        foreach (var colonist in colonists)
        {
            var skills = simulation.World.Get<Skills>(colonist).Values;
            skills.Count.ShouldBe(13);
            skills.Select(skill => skill.WorkType).ShouldBe(skills.Select(skill => skill.WorkType).Order(StringComparer.Ordinal));
            foreach (var trade in trades)
            {
                var skill = skills.Single(skill => skill.WorkType == trade);
                skill.Level.ShouldBeInRange(scenario.FounderSkillMin, scenario.FounderSkillMax);
                skill.Experience.ShouldBe(0);
            }

            // The priorities are kept only when they differ from the default one: the game had none.
            simulation.World.Has<WorkPriorities>(colonist).ShouldBeFalse();
        }
    }

    [Fact]
    public void A_game_of_format_11_has_no_planks_from_the_arrival_and_keeps_its_piles_as_they_were()
    {
        var simulation = Load("o2.olsave", out _);

        Stocks.Count(simulation, "plank").ShouldBe(0);
        var piles = new List<EntityId>();
        simulation.World.Query<ItemStack>(piles);
        piles.ShouldNotBeEmpty();
        piles.Select(simulation.World.Get<ItemStack>).ShouldAllBe(stack => stack.Quality == null && stack.Freshness == null);
    }

    [Fact]
    public void A_game_of_format_12_keeps_its_bed_of_timber_and_needs_nothing_for_workshops()
    {
        var simulation = Load("o3.olsave", out _);
        var world = simulation.World;

        // The furniture of timber of the old content still works, with the default quality, which is not written.
        var beds = new List<EntityId>();
        world.Query<Bed>(beds);
        beds.Count.ShouldBe(1);
        var furniture = world.Get<Furniture>(beds[0]);
        (furniture.Buildable, furniture.Material, furniture.Quality).ShouldBe(("bed", "timber", null));

        // There was nothing of workshops: no workshops, no production orders, and the knowledge of a new game from the migration.
        world.Store<Workshop>().Count.ShouldBe(0);
        world.Store<WorkOrder>().Count.ShouldBe(0);
        simulation.Progress.Knowledge.OrderBy(id => id, StringComparer.Ordinal).ShouldBe(["carpentry", "cooking"]);
        simulation.Run(3_000);
        Colonists(simulation).Count.ShouldBe(7);
    }

    [Fact]
    public void A_migrated_game_is_saved_in_the_new_format_and_loads_again_with_the_same_state()
    {
        var simulation = Load("o3.olsave", out _);
        var output = new MemoryStream();
        SaveGame.Write(output, simulation, new SaveInfo("old-game", IsAutosave: false, "test", DateTimeOffset.UnixEpoch));

        var again = SaveGame.Load(new MemoryStream(output.ToArray()), GameContent.Defs, out var header);

        header.FormatVersion.ShouldBe(SaveGame.FormatVersion);
        header.FormatVersion.ShouldBe(13);
        again.ComputeStateHash().ShouldBe(simulation.ComputeStateHash());
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
    [InlineData("l3.olsave")]
    [InlineData("o2.olsave")]
    [InlineData("o3.olsave")]
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
