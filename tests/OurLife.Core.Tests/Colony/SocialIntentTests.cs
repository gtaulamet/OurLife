using System.Text.RegularExpressions;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Pathfinding;
using OurLife.Core.Save;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Colony;

/// <summary>
/// Social life with intent (L4 of IP3.5, "Vida social con intención" in
/// <c>docs/gdd/libre-albedrio.md</c>). The test content seeks within 10 cells, those of an opinion
/// of 20 or more, the partner, family and friends with double weight, avoids rivals within 4
/// cells, and its colonists interact within 4 cells.
/// </summary>
public class SocialIntentTests
{
    private static readonly TileCoord Center = new(16, 16, 1);

    [Fact]
    public void A_colonist_seeks_someone_it_likes_and_stays_by_it()
    {
        var bed = FreeBed();
        var seeker = bed.AddColonist(8, 16, 1);
        var liked = bed.AddColonist(17, 16, 1, name: "Sancha");
        Relationships.Ensure(bed.Simulation, seeker, liked).Opinion = 25;

        RunUntil(bed, () => bed.JobOf(seeker) is { Kind: JobKind.Visit }, 600).ShouldBeTrue();
        bed.JobOf(seeker)!.Target.ShouldBe(liked);
        RunUntil(bed, () => bed.JobOf(seeker) is { Kind: JobKind.Visit, Stage: JobStage.Working }, 600).ShouldBeTrue();

        // While it waits, the two come within the 4 cells of an interaction.
        var nearest = int.MaxValue;
        for (var tick = 0; tick < 300 && bed.JobOf(seeker) is { Kind: JobKind.Visit }; tick++)
        {
            nearest = Math.Min(nearest, Chebyshev(bed, seeker, liked));
            bed.Simulation.Tick();
        }

        nearest.ShouldBeLessThanOrEqualTo(4);
    }

    [Fact]
    public void A_colonist_seeks_nobody_it_does_not_like_a_rival_or_someone_asleep()
    {
        var bed = FreeBed();
        var seeker = bed.AddColonist(8, 16, 1);
        var stranger = bed.AddColonist(16, 12, 1, name: "Sancha");
        var rival = bed.AddColonist(16, 16, 1, name: "Urraca");
        var asleep = bed.AddColonist(16, 20, 1, rest: 0, name: "Jimena");
        Relationships.Ensure(bed.Simulation, seeker, rival).Opinion = 25;
        Relationships.Ensure(bed.Simulation, rival, seeker).Opinion = -50;
        Relationships.Ensure(bed.Simulation, seeker, asleep).Opinion = 25;
        bed.World.Add(asleep, new Job { Kind = JobKind.Sleep, Stage = JobStage.Working, Cell = new TileCoord(16, 20, 1) });

        // While the third one sleeps.
        var ticks = 0;
        for (; ticks < 2_000 && bed.JobOf(asleep) is { Kind: JobKind.Sleep }; ticks++)
        {
            bed.Simulation.Tick();
            (bed.JobOf(seeker)?.Kind).ShouldNotBe(JobKind.Visit);
        }

        ticks.ShouldBeGreaterThan(300);
        bed.World.IsAlive(stranger).ShouldBeTrue();
    }

    [Theory]
    [InlineData("befriend")]
    [InlineData("reconcile")]
    public void A_colonist_seeks_one_it_wishes_to_befriend_or_make_up_with_even_without_a_good_opinion(string desire)
    {
        var bed = FreeBed();
        var seeker = bed.AddColonist(8, 16, 1);
        var wished = bed.AddColonist(17, 16, 1, name: "Sancha");
        DesireRules.DesiresOf(bed.World, seeker).Active.Add(new ActiveDesire { Desire = desire, Target = wished, UntilTick = 100_000 });

        Visits(bed, seeker, 600).ShouldContain(wished);
    }

    [Fact]
    public void A_relative_it_quarrelled_with_is_not_sought()
    {
        var bed = FreeBed();
        var seeker = bed.AddColonist(8, 16, 1);
        var partner = bed.AddColonist(17, 16, 1, name: "Sancha");
        bed.World.Add(seeker, new Family { Partner = new Relative { Entity = partner, Name = "Sancha" } });
        Relationships.Ensure(bed.Simulation, seeker, partner).Opinion = -60;

        Visits(bed, seeker, 1_000).ShouldBeEmpty();
    }

    [Fact]
    public void A_colonist_in_a_crisis_seeks_nobody()
    {
        var bed = FreeBed();
        var seeker = bed.AddColonist(8, 16, 1);
        bed.World.Add(seeker, new Crisis { Kind = "refuse_work", UntilTick = long.MaxValue });
        var liked = bed.AddColonist(17, 16, 1, name: "Sancha");
        Relationships.Ensure(bed.Simulation, seeker, liked).Opinion = 60;

        Visits(bed, seeker, 1_000).ShouldBeEmpty();
    }

    [Fact]
    public void A_child_seeks_only_its_family()
    {
        var bed = FreeBed();
        var child = bed.AddColonist(8, 16, 1);
        bed.World.Add(child, new Life { AgeDays = 6 * 60, Stage = "child" });
        var liked = bed.AddColonist(17, 16, 1, name: "Sancha");
        Relationships.Ensure(bed.Simulation, child, liked).Opinion = 60;

        Visits(bed, child, 1_000).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("asleep")]
    [InlineData("to bed")]
    [InlineData("isolating")]
    [InlineData("rivals")]
    public void A_visit_ends_when_the_other_goes_to_sleep_the_visitor_isolates_or_they_become_rivals(string change)
    {
        var bed = FreeBed();
        var seeker = bed.AddColonist(8, 16, 1);
        var liked = bed.AddColonist(17, 16, 1, name: "Sancha");
        Relationships.Ensure(bed.Simulation, seeker, liked).Opinion = 25;
        RunUntil(bed, () => bed.JobOf(seeker) is { Kind: JobKind.Visit }, 600).ShouldBeTrue();

        switch (change)
        {
            case "asleep":
                Settle(bed, liked, JobKind.Sleep, bed.World.Get<Position>(liked).Cell);
                break;
            case "to bed":
                Settle(bed, liked, JobKind.Sleep, bed.World.Get<Position>(liked).Cell);
                bed.JobOf(liked)!.Stage = JobStage.Moving;
                break;
            case "isolating":
                bed.World.Add(seeker, new Crisis { Kind = "isolate", UntilTick = long.MaxValue });
                break;
            default:
                Relationships.Ensure(bed.Simulation, liked, seeker).Opinion = -50;
                break;
        }

        bed.Simulation.Tick();

        (bed.JobOf(seeker)?.Kind).ShouldNotBe(JobKind.Visit);
    }

    [Fact]
    public void Two_who_like_each_other_never_set_off_to_each_other_at_once()
    {
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var bed = FreeBed(seed);
            var a = bed.AddColonist(8, 16, 1);
            var b = bed.AddColonist(17, 16, 1, name: "Sancha");
            Relationships.Ensure(bed.Simulation, a, b).Opinion = 25;
            Relationships.Ensure(bed.Simulation, b, a).Opinion = 25;
            for (var tick = 0; tick < 1_000; tick++)
            {
                bed.Simulation.Tick();
                (bed.JobOf(a) is { Kind: JobKind.Visit } && bed.JobOf(b) is { Kind: JobKind.Visit }).ShouldBeFalse($"seed {seed}");
            }
        }
    }

    [Fact]
    public void A_visit_waiting_ends_when_the_other_goes_far()
    {
        var bed = FreeBed();
        var seeker = bed.AddColonist(8, 16, 1);
        var liked = bed.AddColonist(17, 16, 1, name: "Sancha");
        Relationships.Ensure(bed.Simulation, seeker, liked).Opinion = 25;
        RunUntil(bed, () => bed.JobOf(seeker) is { Kind: JobKind.Visit, Stage: JobStage.Working }, 600).ShouldBeTrue();

        var far = bed.World.Get<Position>(seeker).Cell with { Y = 4 };
        var position = bed.World.Get<Position>(liked);
        (position.X, position.Y) = (far.X, far.Y);
        Settle(bed, liked, JobKind.Wander, far);
        bed.Simulation.Tick();

        (bed.JobOf(seeker) is { Kind: JobKind.Visit, Stage: JobStage.Working }).ShouldBeFalse();
    }

    [Fact]
    public void A_visitor_on_its_way_wanders_off_when_the_other_goes_out_of_reach_for_seeking()
    {
        var bed = WalledBed();
        var seeker = bed.AddColonist(2, 16, 1);
        var liked = bed.AddColonist(10, 16, 1, name: "Sancha");
        Relationships.Ensure(bed.Simulation, seeker, liked).Opinion = 25;
        RunUntil(bed, () => bed.JobOf(seeker) is { Kind: JobKind.Visit, Stage: JobStage.Moving }, 600).ShouldBeTrue();

        var gone = new TileCoord(28, 2, 1);
        var position = bed.World.Get<Position>(liked);
        (position.X, position.Y) = (gone.X, gone.Y);
        Settle(bed, liked, JobKind.Wander, gone);
        bed.Simulation.Tick();

        bed.JobOf(seeker)!.Kind.ShouldBe(JobKind.Wander);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void With_free_will_a_child_does_not_play_with_a_rival(bool freeWill)
    {
        var bed = ColonyTestBed.Flat(32);
        bed.Simulation.FreeWill = freeWill;
        bed.Simulation.VillageCenter = Center;
        var child = bed.AddColonist(8, 16, 1);
        var rival = bed.AddColonist(14, 16, 1, name: "Urraca");
        foreach (var kid in new[] { child, rival })
        {
            bed.World.Add(kid, new Life { AgeDays = 6 * 60, Stage = "child" });
        }

        Relationships.Ensure(bed.Simulation, child, rival).Opinion = -60;
        var played = false;
        for (var tick = 0; tick < 600 && !played; tick++)
        {
            bed.Simulation.Tick();
            played = bed.JobOf(child) is { Kind: JobKind.Play } job && job.Target == rival;
        }

        played.ShouldBe(!freeWill);
    }

    [Fact]
    public void A_visitor_on_its_way_follows_the_other_where_it_went()
    {
        var bed = WalledBed();
        var seeker = bed.AddColonist(2, 16, 1);
        var liked = bed.AddColonist(10, 16, 1, name: "Sancha");
        Relationships.Ensure(bed.Simulation, seeker, liked).Opinion = 25;
        RunUntil(bed, () => bed.JobOf(seeker) is { Kind: JobKind.Visit, Stage: JobStage.Moving }, 600).ShouldBeTrue();

        // Out of reach of where it was going, but within reach for seeking.
        var moved = new TileCoord(4, 24, 1);
        var position = bed.World.Get<Position>(liked);
        (position.X, position.Y) = (moved.X, moved.Y);
        Settle(bed, liked, JobKind.Wander, moved);

        // The same visit, on its way all along: neither dropped nor waiting where the other was.
        var followed = false;
        for (var tick = 0; tick < 60 && !followed; tick++)
        {
            bed.Simulation.Tick();
            var job = bed.JobOf(seeker);
            job.ShouldNotBeNull();
            job.Kind.ShouldBe(JobKind.Visit);
            followed = job.Cell == moved;
            (followed || job.Stage == JobStage.Moving).ShouldBeTrue();
        }

        followed.ShouldBeTrue();
    }

    [Fact]
    public void The_partner_is_sought_more_often_than_one_it_only_thinks_well_of()
    {
        // The first one sought, in many games where both are near and neither was met yet.
        var partnerVisits = 0;
        var friendVisits = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var bed = FreeBed(seed);
            var seeker = bed.AddColonist(16, 16, 1);
            var partner = bed.AddColonist(16, 10, 1, name: "Sancha");
            var friend = bed.AddColonist(16, 22, 1, name: "Urraca");
            bed.World.Add(seeker, new Family { Partner = new Relative { Entity = partner, Name = "Sancha" } });
            Relationships.Ensure(bed.Simulation, seeker, friend).Opinion = 30;

            if (!RunUntil(bed, () => bed.JobOf(seeker) is { Kind: JobKind.Visit }, 300))
            {
                continue;
            }

            if (bed.JobOf(seeker)!.Target == partner)
            {
                partnerVisits++;
            }
            else
            {
                friendVisits++;
            }
        }

        (partnerVisits + friendVisits).ShouldBeGreaterThan(30);
        partnerVisits.ShouldBeGreaterThan(friendVisits);
    }

    [Fact]
    public void Two_who_like_each_other_far_apart_become_friends_sooner_with_free_will()
    {
        var with = 0L;
        var without = 0L;
        for (ulong seed = 1; seed <= 5; seed++)
        {
            with += DaysToFriends(seed, freeWill: true);
            without += DaysToFriends(seed, freeWill: false);
        }

        with.ShouldBeLessThan(without);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_colonist_wanders_away_from_a_rival_near_it_even_of_its_family(bool partner)
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var bed = FreeBed(seed);
            var colonist = bed.AddColonist(16, 16, 1);
            var rival = bed.AddColonist(19, 16, 1, name: "Urraca");
            Relationships.Ensure(bed.Simulation, colonist, rival).Opinion = -50;
            if (partner)
            {
                bed.World.Add(colonist, new Family { Partner = new Relative { Entity = rival, Name = "Urraca" } });
            }

            var start = Chebyshev(bed, colonist, rival);

            RunUntil(bed, () => bed.JobOf(colonist) is { Kind: JobKind.Wander, Stage: JobStage.Working }, 600).ShouldBeTrue();

            Chebyshev(bed, colonist, rival).ShouldBeGreaterThan(start, $"seed {seed}");
        }
    }

    [Fact]
    public void With_free_will_off_nobody_seeks_company_or_avoids_a_rival_and_the_game_is_as_without_it()
    {
        var with = OffBed(ColonyTestBed.Content);
        var without = OffBed(ContentWithoutFreeWill());

        Visits(with.Bed, with.Seeker, 2_000).ShouldBeEmpty();
        without.Bed.Simulation.Run(2_000);

        with.Bed.Simulation.ComputeStateHash().ShouldBe(without.Bed.Simulation.ComputeStateHash());
    }

    [Fact]
    public void Turning_free_will_off_ends_a_visit()
    {
        var bed = FreeBed();
        var seeker = bed.AddColonist(8, 16, 1);
        var liked = bed.AddColonist(17, 16, 1, name: "Sancha");
        Relationships.Ensure(bed.Simulation, seeker, liked).Opinion = 25;
        RunUntil(bed, () => bed.JobOf(seeker) is { Kind: JobKind.Visit }, 600).ShouldBeTrue();

        bed.Simulation.Commands.Enqueue(new SetFreeWillCommand(false));
        bed.Simulation.Tick();

        (bed.JobOf(seeker)?.Kind).ShouldNotBe(JobKind.Visit);
    }

    [Fact]
    public void A_visit_is_saved_and_goes_on_after_loading()
    {
        var bed = FreeBed();
        var seeker = bed.AddColonist(8, 16, 1);
        var liked = bed.AddColonist(17, 16, 1, name: "Sancha");
        Relationships.Ensure(bed.Simulation, seeker, liked).Opinion = 25;
        RunUntil(bed, () => bed.JobOf(seeker) is { Kind: JobKind.Visit }, 600).ShouldBeTrue();

        var file = new MemoryStream();
        SaveGame.Write(file, bed.Simulation, new SaveInfo("game-1", IsAutosave: false, "test", DateTimeOffset.UnixEpoch));
        var loaded = SaveGame.Load(new MemoryStream(file.ToArray()), ColonyTestBed.Content);
        ColonySystems.AddTo(loaded);

        loaded.World.Get<Job>(seeker).Kind.ShouldBe(JobKind.Visit);
        loaded.ComputeStateHash().ShouldBe(bed.Simulation.ComputeStateHash());
        loaded.Run(300);
        bed.Simulation.Run(300);
        loaded.ComputeStateHash().ShouldBe(bed.Simulation.ComputeStateHash());
    }

    [Theory]
    [InlineData(0, false, 1_500)]
    [InlineData(50, false, 3_000)]
    [InlineData(100, false, 4_500)]
    [InlineData(50, true, 1_500)]
    public void A_sociable_colonist_seeks_company_more_and_a_low_mood_halves_it(int sociability, bool lowMood, int expected)
    {
        var bed = FreeBed();
        var colonist = bed.AddColonist(8, 8, 1);
        bed.World.Add(colonist, new Personality { Traits = [new TraitValue { Trait = "sociability", Value = sociability }] });
        if (lowMood)
        {
            MoodRules.Feel(bed.Simulation, colonist, "death_child");
            MoodRules.Feel(bed.Simulation, colonist, "death_child");
        }

        FreeWillRules.SeekChance(bed.Simulation, colonist).ShouldBe(expected);
    }

    /// <summary>
    /// The days until two colonists who think well of each other, ten cells apart, each with food
    /// by it, become friends; at most <paramref name="days"/>.
    /// </summary>
    private static long DaysToFriends(ulong seed, bool freeWill, int days = 30)
    {
        var bed = freeWill ? FreeBed(seed, size: 40) : ColonyTestBed.Flat(40, seed);
        bed.Simulation.VillageCenter = new TileCoord(20, 20, 1);
        var a = bed.AddColonist(15, 20, 1);
        var b = bed.AddColonist(25, 20, 1, name: "Sancha");
        bed.AddItems("berries", 500, 14, 20, 1);
        bed.AddItems("berries", 500, 26, 20, 1);
        Relationships.Ensure(bed.Simulation, a, b).Opinion = 25;
        Relationships.Ensure(bed.Simulation, b, a).Opinion = 25;
        var ticksPerDay = bed.Simulation.Time.TicksPerDay;
        for (var day = 0; day < days; day++)
        {
            for (var tick = 0; tick < ticksPerDay; tick++)
            {
                bed.Simulation.Tick();
                if (Relationships.Kind(bed.Simulation, a, b) == RelationKind.Friend)
                {
                    return day;
                }
            }
        }

        return days;
    }

    /// <summary>A flat bed with free will on, the village in its middle and no stock targets, so nobody works on its own.</summary>
    private static ColonyTestBed FreeBed(ulong seed = 1, int size = 32)
    {
        var bed = ColonyTestBed.Flat(size, seed);
        bed.Simulation.FreeWill = true;
        bed.Simulation.VillageCenter = new TileCoord(size / 2, size / 2, 1);
        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand("food", 0));
        bed.Simulation.Commands.Enqueue(new SetStockTargetCommand("wood", 0));
        return bed;
    }

    /// <summary>A bed with free will off: a colonist with one it likes far away and a rival beside it.</summary>
    private static (ColonyTestBed Bed, EntityId Seeker) OffBed(DefDatabase content)
    {
        var bed = ColonyTestBed.Flat(32, content: content);
        bed.Simulation.VillageCenter = Center;
        var seeker = bed.AddColonist(8, 16, 1);
        var liked = bed.AddColonist(17, 16, 1, name: "Sancha");
        var rival = bed.AddColonist(10, 16, 1, name: "Urraca");
        Relationships.Ensure(bed.Simulation, seeker, liked).Opinion = 25;
        Relationships.Ensure(bed.Simulation, seeker, rival).Opinion = -50;
        return (bed, seeker);
    }

    /// <summary>The test content without free will: no FreeWillConfig and no InitiativeTask.</summary>
    private static DefDatabase ContentWithoutFreeWill()
    {
        var json = Regex.Replace(ColonyTestBed.ContentJson, @"\{ ""type"": ""FreeWillConfig"".*?""targetMax"": 999 \},", string.Empty, RegexOptions.Singleline);
        json = Regex.Replace(json, @"\{ ""type"": ""InitiativeTask""[^}]*\},", string.Empty);
        json.ShouldNotContain("FreeWillConfig");
        return TestContent.WithDefs(json);
    }

    /// <summary>Those a colonist went to keep company in so many ticks, in order.</summary>
    private static List<EntityId> Visits(ColonyTestBed bed, EntityId colonist, int ticks)
    {
        var seen = new List<EntityId>();
        for (var tick = 0; tick < ticks; tick++)
        {
            bed.Simulation.Tick();
            if (bed.JobOf(colonist) is { Kind: JobKind.Visit } job && !seen.Contains(job.Target))
            {
                seen.Add(job.Target);
            }
        }

        return seen;
    }

    /// <summary>
    /// A bed with free will on and a wall at x = 6 between the left and the rest, open only at the
    /// bottom: a long way round, so a visitor looks again on the way.
    /// </summary>
    private static ColonyTestBed WalledBed()
    {
        var rows = Enumerable.Range(0, 32).Select(y => y < 28 ? new string('.', 6) + "#" + new string('.', 25) : new string('.', 32));
        var bed = new ColonyTestBed(1, ColonyTestBed.Level(32, 32, '#'), string.Join("\n", rows));
        bed.Simulation.FreeWill = true;
        bed.Simulation.VillageCenter = Center;
        return bed;
    }

    /// <summary>Gives a colonist a job it is already doing where it stands, for a long while.</summary>
    private static void Settle(ColonyTestBed bed, EntityId colonist, JobKind kind, TileCoord cell)
    {
        bed.World.Get<Movement>(colonist).Stop();
        bed.World.Remove<Job>(colonist);
        bed.World.Add(colonist, new Job { Kind = kind, Stage = JobStage.Working, Cell = cell, UntilTick = 100_000 });
    }

    private static int Chebyshev(ColonyTestBed bed, EntityId a, EntityId b)
    {
        var from = bed.World.Get<Position>(a).Cell;
        var to = bed.World.Get<Position>(b).Cell;
        return Math.Max(Math.Abs(from.X - to.X), Math.Abs(from.Y - to.Y));
    }

    private static bool RunUntil(ColonyTestBed bed, Func<bool> done, int ticks)
    {
        for (var tick = 0; tick < ticks && !done(); tick++)
        {
            bed.Simulation.Tick();
        }

        return done();
    }
}
