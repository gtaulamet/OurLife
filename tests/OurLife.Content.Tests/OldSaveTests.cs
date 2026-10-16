using OurLife.Core.Colony;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Save;
using OurLife.Core.Sim;

namespace OurLife.Content.Tests;

/// <summary>
/// A game saved by v0.2.5 (<c>Fixtures/v0.2.5.olsave</c>: seed 7, 20 000 ticks, written by the code
/// and content of that release) loads with the content of today, and its colonists get what IP3
/// adds (see "Guardado y migración" in <c>docs/gdd/vida-y-generaciones.md</c>).
/// </summary>
public class OldSaveTests
{
    private static readonly string Fixture = RepoPaths.Combine("tests", "OurLife.Content.Tests", "Fixtures", "v0.2.5.olsave");

    [Fact]
    public void A_game_of_v0_2_5_loads_with_ages_surnames_and_the_default_pace()
    {
        var simulation = Load(out var header);

        header.FormatVersion.ShouldBe(1);
        header.GameVersion.ShouldBe("0.2.5");
        simulation.CurrentTick.ShouldBe(20_000);
        simulation.LifePace.ShouldBe("fast");
        var colonists = Colonists(simulation);
        colonists.Count.ShouldBe(5);
        colonists.Select(colonist => LifeRules.AgeYears(simulation, colonist)!.Value).ShouldAllBe(years => years >= 18 && years <= 40);
        colonists.Select(colonist => simulation.World.Get<Colonist>(colonist).Surname).Distinct().Count().ShouldBe(5);
        colonists.Select(colonist => simulation.World.Get<Personality>(colonist).Traits.Count)
            .ShouldAllBe(count => count == GameContent.Defs.All<Core.Defs.TraitDef>().Count);
    }

    [Fact]
    public void The_same_old_game_always_migrates_the_same_way_and_goes_on()
    {
        var first = Load(out _);
        var second = Load(out _);

        second.ComputeStateHash().ShouldBe(first.ComputeStateHash());
        first.Run(5_000);
        second.Run(5_000);
        second.ComputeStateHash().ShouldBe(first.ComputeStateHash());
        Colonists(first).ShouldNotBeEmpty();
    }

    private static Simulation Load(out SaveHeader header)
    {
        using var file = File.OpenRead(Fixture);
        var simulation = SaveGame.Load(file, GameContent.Defs, out header);
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
