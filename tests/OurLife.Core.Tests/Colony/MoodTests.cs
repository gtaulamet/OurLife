using OurLife.Core.Colony;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Colony;

/// <summary>Thoughts and mood (H3.6, "Pensamientos y ánimo" in <c>docs/gdd/vida-y-generaciones.md</c>).</summary>
public class MoodTests
{
    private const int TicksPerDay = 24 * 60;

    [Fact]
    public void Being_hungry_lowers_the_mood_starving_more_and_eating_brings_it_back()
    {
        var bed = ColonyTestBed.Flat(8);
        var colonist = bed.AddColonist(2, 2, 1, hunger: 3_000);

        MoodRules.Value(bed.Simulation, colonist).ShouldBe(44);
        bed.Need(colonist, "hunger").Value = 1_000;
        MoodRules.Value(bed.Simulation, colonist).ShouldBe(35);
        MoodRules.Thoughts(bed.Simulation, colonist).Select(thought => thought.Id).ShouldBe(new[] { "starving" });

        bed.Need(colonist, "hunger").Value = 3_000;
        bed.AddItems("provisions", 3, 3, 2, 1);
        for (var tick = 0; tick < 200 && bed.Need(colonist, "hunger").Value < 4_000; tick++)
        {
            bed.Simulation.Tick();
        }

        MoodRules.Value(bed.Simulation, colonist).ShouldBe(50);
    }

    [Fact]
    public void A_chat_raises_the_mood_of_both()
    {
        var (bed, a, b) = SocialTests.TwoInAHut(1, 90, 90, 90, Gender.Female);
        for (var tick = 0; tick < TicksPerDay && !bed.DrainEvents().OfType<InteractionEvent>().Any(e => e.Interaction == "chat"); tick++)
        {
            bed.Simulation.Tick();
        }

        bed.World.Get<Mood>(a).Thoughts.ShouldContain(thought => thought.Thought == "chatted");
        bed.World.Get<Mood>(b).Thoughts.ShouldContain(thought => thought.Thought == "chatted");
        MoodRules.Value(bed.Simulation, a).ShouldBeGreaterThan(50);
    }

    [Fact]
    public void A_thought_lasts_its_hours_and_adds_up_to_its_limit()
    {
        var bed = ColonyTestBed.Flat(8);
        var colonist = bed.AddColonist(2, 2, 1);

        for (var i = 0; i < 4; i++)
        {
            MoodRules.Feel(bed.Simulation, colonist, "chatted");
        }

        MoodRules.Value(bed.Simulation, colonist).ShouldBe(56);
        bed.Simulation.Run(6 * 60);
        MoodRules.Value(bed.Simulation, colonist).ShouldBe(50);

        // The hourly pass clears what faded.
        bed.Simulation.Tick();
        bed.World.Get<Mood>(colonist).Thoughts.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("death_child death_child", 0, 80)]
    [InlineData("death_partner argued", 25, 90)]
    [InlineData("", 50, 100)]
    [InlineData("new_partner desire_fulfilled desire_fulfilled laughed laughed", 82, 105)]
    [InlineData("new_partner desire_fulfilled desire_fulfilled laughed laughed comforted learned learned", 94, 110)]
    public void The_mood_sets_the_speed_of_work(string thoughts, int mood, int percent)
    {
        var bed = ColonyTestBed.Flat(8);
        var colonist = bed.AddColonist(2, 2, 1);
        foreach (var thought in thoughts.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            MoodRules.Feel(bed.Simulation, colonist, thought);
        }

        MoodRules.Value(bed.Simulation, colonist).ShouldBe(mood);
        MoodRules.WorkSpeedPercent(bed.Simulation, colonist).ShouldBe(percent);
    }

    [Fact]
    public void Waking_up_in_a_bed_or_on_the_ground_leaves_its_thought()
    {
        var bed = ColonyTestBed.Flat(8);
        var colonist = bed.AddColonist(2, 2, 1, rest: 2_000);

        // From 6:00 on the ground until rested.
        for (var tick = 0; tick < TicksPerDay && !(bed.World.TryGet<Mood>(colonist, out var mood) && mood.Thoughts.Count > 0); tick++)
        {
            bed.Simulation.Tick();
        }

        bed.World.Get<Mood>(colonist).Thoughts.Select(thought => thought.Thought).ShouldBe(new[] { "slept_on_floor" });
        bed.World.Get<Mood>(colonist).FloorNights.ShouldBe(1);
    }

    [Fact]
    public void Going_up_a_level_leaves_a_thought()
    {
        var bed = ColonyTestBed.Flat(8);
        var colonist = bed.AddColonist(2, 2, 1);
        bed.World.Add(colonist, new Skills { Values = [new SkillValue { WorkType = "mining", Level = 0 }] });

        ColonyRules.From(bed.Simulation.Content).Skills.Gain(bed.Simulation, colonist, bed.Simulation.Content.Get<Core.Defs.WorkTypeDef>("mining"), 700);

        bed.World.Get<Mood>(colonist).Thoughts.ShouldContain(thought => thought.Thought == "learned");
    }

    [Fact]
    public void A_death_grieves_the_partner_most_and_moves_the_family()
    {
        var bed = ColonyTestBed.Flat(15, seed: 4);
        var colonists = ColonyStart.Settle(bed.Simulation, ColonyStart.TheOnlyScenario(bed.Simulation.Content), "Valdeolmos");
        var (wife, husband, other) = (colonists[0], colonists[1], colonists[2]);
        var sociability = PersonalityRules.Value(bed.Simulation, wife, "sociability");

        ColonistDeath.Die(bed.Simulation, husband, Core.Text.LocalizedText.Of("event.need.hunger.death").With("colonist", husband));

        bed.World.Get<Mood>(wife).Thoughts.Select(thought => thought.Thought).ShouldBe(new[] { "death_partner" });
        bed.World.Get<Mood>(other).Thoughts.Single().Thought.ShouldBeOneOf("death_colonist", "death_friend");
        PersonalityRules.Value(bed.Simulation, wife, "sociability").ShouldBe(Math.Max(0, sociability - 2));
    }
}
