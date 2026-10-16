using OurLife.Core.Colony;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Colony;

/// <summary>What colonists do together, and who does it (H3.5, "Interacciones" in <c>docs/gdd/vida-y-generaciones.md</c>).</summary>
public class InteractionTests
{
    private const int TicksPerDay = 24 * 60;

    [Fact]
    public void A_master_teaches_an_apprentice_who_lives_beside_it()
    {
        var (bed, master, apprentice) = SocialTests.TwoInAHut(5, 50, 50, 50, Gender.Female);
        bed.World.Add(master, new Skills { Values = [new SkillValue { WorkType = "mining", Level = 15 }] });
        bed.World.Add(apprentice, new Skills { Values = [new SkillValue { WorkType = "mining", Level = 2 }] });

        bed.Simulation.Run(2 * TicksPerDay);

        var skill = bed.World.Get<Skills>(apprentice).Find("mining")!;
        (skill.Level > 2 || skill.Experience > 0).ShouldBeTrue();
        var recent = bed.World.Get<RecentInteractions>(apprentice).Entries;
        recent.Count.ShouldBeLessThanOrEqualTo(5);
        bed.World.Get<Skills>(master).Find("mining")!.Experience.ShouldBe(0);
    }

    [Fact]
    public void A_baby_does_not_interact()
    {
        var (bed, _, baby) = SocialTests.TwoInAHut(1, 100, 50, 50);
        bed.World.Add(baby, new Life { AgeDays = 60, Stage = "baby" });

        bed.Simulation.Run(2 * TicksPerDay);

        bed.DrainEvents().OfType<InteractionEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void Only_children_play_and_only_adults_court()
    {
        var (bed, child, other) = SocialTests.TwoInAHut(2, 90, 50, 50);
        bed.World.Add(child, new Life { AgeDays = 6 * 60, Stage = "child" });
        bed.World.Add(other, new Life { AgeDays = 8 * 60, Stage = "child" });

        bed.Simulation.Run(2 * TicksPerDay);

        var kinds = bed.DrainEvents().OfType<InteractionEvent>().Select(e => e.Interaction).Distinct().ToList();
        kinds.ShouldContain("play");
        kinds.ShouldNotContain("flirt");
        kinds.ShouldNotContain("teach");
        kinds.ShouldNotContain("argue");
    }

    [Fact]
    public void An_interaction_is_remembered_by_both_and_the_last_five_are_kept()
    {
        var (bed, a, b) = SocialTests.TwoInAHut(3, 90, 90, 90, Gender.Female);

        bed.Simulation.Run(3 * TicksPerDay);

        var ofA = bed.World.Get<RecentInteractions>(a).Entries;
        var ofB = bed.World.Get<RecentInteractions>(b).Entries;
        ofA.Count.ShouldBe(5);
        ofB.Count.ShouldBe(5);
        ofA[^1].Tick.ShouldBe(ofB[^1].Tick);
        ofA[^1].Started.ShouldNotBe(ofB[^1].Started);
        ofA.Select(entry => entry.Tick).ShouldBeInOrder();
    }

    [Fact]
    public void The_same_seed_gives_the_same_interactions()
    {
        static List<string> Interactions()
        {
            var (bed, _, _) = SocialTests.TwoInAHut(9, 60, 40, 40);
            bed.Simulation.Run(2 * TicksPerDay);
            return [.. bed.DrainEvents().OfType<InteractionEvent>().Select(e => $"{e.Tick}:{e.Initiator}:{e.Interaction}")];
        }

        var first = Interactions();

        first.Count.ShouldBeGreaterThan(5);
        Interactions().ShouldBe(first);
    }
}
