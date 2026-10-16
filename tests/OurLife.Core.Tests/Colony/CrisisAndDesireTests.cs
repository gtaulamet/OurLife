using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Colony;

/// <summary>Crises and desires (H3.6, "Crisis" and "Deseos" in <c>docs/gdd/vida-y-generaciones.md</c>).</summary>
public class CrisisAndDesireTests
{
    private const int TicksPerDay = 24 * 60;

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    public void In_a_very_low_mood_for_a_day_a_crisis_comes_stops_the_work_and_ends(ulong seed)
    {
        var bed = new ColonyTestBed(seed, ColonyTestBed.Level(8, 8, '#'), "########\n" + ColonyTestBed.Level(8, 7, '.'));
        var colonist = bed.AddColonist(2, 2, 1);
        bed.World.Add(colonist, new Personality { Traits = [new TraitValue { Trait = "patience", Value = 50 }] });
        MoodRules.Feel(bed.Simulation, colonist, "death_child");
        MoodRules.Feel(bed.Simulation, colonist, "death_child");
        bed.Designate(DesignationKind.Mine, new TileCoord(0, 0, 1), new TileCoord(7, 0, 1));

        var started = -1L;
        for (var tick = 0; tick < TicksPerDay && started < 0; tick++)
        {
            bed.Simulation.Tick();
            if (bed.DrainEvents().OfType<NotificationEvent>().Any(notice => notice.Text.Key.StartsWith("event.mood.crisis.", StringComparison.Ordinal)))
            {
                started = bed.Simulation.CurrentTick;
            }
        }

        started.ShouldBeGreaterThan(0, "no crisis in a day under 15");
        var crisis = MoodRules.CrisisOf(bed.Simulation, colonist)!;
        PersonalityRules.Value(bed.Simulation, colonist, "patience").ShouldBe(48);
        var ended = false;
        for (var tick = 0; tick < (crisis.DurationHours * 60) + 61; tick++)
        {
            bed.Simulation.Tick();
            if (MoodRules.CrisisOf(bed.Simulation, colonist) is not null)
            {
                (bed.JobOf(colonist)?.Kind ?? JobKind.Wander).ShouldNotBe(JobKind.Mine);
            }

            ended |= bed.DrainEvents().OfType<NotificationEvent>().Any(notice => notice.Text.Key == MoodSystem.EndKey(crisis));
        }

        ended.ShouldBeTrue();
        bed.World.Has<Crisis>(colonist).ShouldBeFalse();
    }

    [Fact]
    public void A_colonist_who_isolates_itself_does_not_interact()
    {
        var (bed, a, _) = SocialTests.TwoInAHut(1, 100, 90, 90, Gender.Female);
        bed.World.Add(a, new Crisis { Kind = "isolate", UntilTick = 6 * 60 });

        bed.Simulation.Run(6 * 60);

        bed.DrainEvents().OfType<InteractionEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void A_desire_that_comes_true_leaves_its_thought_and_one_that_expires_the_opposite()
    {
        var bed = ColonyTestBed.Flat(8);
        var lucky = bed.AddColonist(2, 2, 1);
        var unlucky = bed.AddColonist(5, 5, 1, name: "Urraca");
        foreach (var colonist in new[] { lucky, unlucky })
        {
            bed.World.Add(colonist, new Desires { Active = [new ActiveDesire { Desire = "own_bed", UntilTick = 3 * 60 }] });
        }

        var furniture = bed.World.CreateEntity();
        bed.World.Add(furniture, new Position { X = 7, Y = 7, Z = 1 });
        bed.World.Add(furniture, new Bed { Owner = lucky });

        bed.Simulation.Run((3 * 60) + 1);

        bed.World.Get<Desires>(lucky).Active.ShouldBeEmpty();
        bed.World.Get<Mood>(lucky).Thoughts.Select(thought => thought.Thought).ShouldContain("desire_fulfilled");
        bed.World.Get<Desires>(unlucky).Active.ShouldBeEmpty();
        bed.World.Get<Mood>(unlucky).Thoughts.Select(thought => thought.Thought).ShouldContain("desire_frustrated");
    }

    [Fact]
    public void Two_nights_on_the_ground_wake_the_wish_for_a_bed()
    {
        var content = Certain("own_bed");
        var bed = ColonyTestBed.Flat(8);
        var simulation = new Simulation(1, content);
        simulation.AttachMap(bed.Map);
        ColonySystems.AddTo(simulation);
        var colonist = simulation.World.CreateEntity();
        simulation.World.Add(colonist, new Position { X = 2, Y = 2, Z = 1 });
        simulation.World.Add(colonist, new Colonist { Name = "Elvira", Gender = Gender.Female, Creature = "human" });
        simulation.World.Add(colonist, new Needs { Values = [new NeedState { Need = "hunger", Value = 10_000 }, new NeedState { Need = "rest", Value = 10_000 }] });
        simulation.World.Add(colonist, new Movement());
        simulation.AddSystem(new FeedEveryone());

        // Two nights, and the morning after the second.
        simulation.Run((2 * TicksPerDay) + 120);

        simulation.World.Get<Mood>(colonist).FloorNights.ShouldBeGreaterThanOrEqualTo(2);
        simulation.World.Get<Desires>(colonist).Active.Select(desire => desire.Desire).ShouldBe(new[] { "own_bed" });
    }

    [Fact]
    public void Desires_are_born_one_a_day_and_two_at_most()
    {
        var content = Certain("own_bed", "reconcile");
        var simulation = new Simulation(1, content);
        var colonist = simulation.World.CreateEntity();
        var other = simulation.World.CreateEntity();

        DesireRules.Consider(simulation, colonist, DesireKind.OwnBed);
        DesireRules.Consider(simulation, colonist, DesireKind.Reconcile, other);
        simulation.World.Get<Desires>(colonist).Active.Count.ShouldBe(1);

        simulation.Run(TicksPerDay);
        DesireRules.Consider(simulation, colonist, DesireKind.Reconcile, other);
        simulation.Run(TicksPerDay);
        DesireRules.Consider(simulation, colonist, DesireKind.Reconcile, simulation.World.CreateEntity());
        simulation.World.Get<Desires>(colonist).Active.Count.ShouldBe(2);
    }

    /// <summary>The test content with these desires always born at their moment.</summary>
    private static DefDatabase Certain(params string[] desires)
    {
        var json = ColonyTestBed.ContentJson;
        foreach (var desire in desires)
        {
            var at = json.IndexOf($"\"id\": \"{desire}\"", StringComparison.Ordinal);
            var chance = json.IndexOf("\"chancePercent\": ", at, StringComparison.Ordinal) + "\"chancePercent\": ".Length;
            var end = json.IndexOf(',', chance);
            json = json[..chance] + "100" + json[end..];
        }

        return TestContent.WithDefs(json);
    }

    /// <summary>Keeps everyone fed, so a test about sleep needs no food.</summary>
    private sealed class FeedEveryone : ISimSystem
    {
        public void Tick(Simulation simulation)
        {
            var colonists = new List<EntityId>();
            simulation.World.Query<Needs>(colonists);
            foreach (var colonist in colonists)
            {
                simulation.World.Get<Needs>(colonist).Find("hunger")!.Value = 10_000;
            }
        }
    }
}
