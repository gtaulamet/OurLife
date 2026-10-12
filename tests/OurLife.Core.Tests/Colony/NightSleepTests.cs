using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Entities;
using OurLife.Core.Map;

namespace OurLife.Core.Tests.Colony;

/// <summary>
/// Night sleep (<c>docs/gdd/dia-y-noche.md</c>). The test content's night goes from 21:00 to
/// 06:00 and tick 0 is 06:00, one minute per tick.
/// </summary>
public class NightSleepTests
{
    private const int NightStarts = 15 * 60;
    private const int NextMorning = 24 * 60;
    private const int Recheck = 30;

    [Fact]
    public void Colonists_leave_their_work_to_sleep_at_nine_and_get_up_at_six()
    {
        var bed = RockToDig();
        var colonist = bed.AddColonist(1, 1, 1);

        bed.Simulation.Run(NightStarts - 10);
        bed.JobOf(colonist)!.Kind.ShouldBe(JobKind.Mine);

        bed.Simulation.Run(10 + Recheck);
        bed.JobOf(colonist)!.Kind.ShouldBe(JobKind.Sleep);

        bed.Simulation.Run(NextMorning - NightStarts - Recheck - 10);
        bed.JobOf(colonist)!.Kind.ShouldBe(JobKind.Sleep);

        bed.Simulation.Run(10 + Recheck);
        bed.JobOf(colonist)!.Kind.ShouldBe(JobKind.Mine);
    }

    [Fact]
    public void By_day_a_tired_colonist_keeps_awake()
    {
        var bed = ColonyTestBed.Flat(8);
        var colonist = bed.AddColonist(2, 2, 1, rest: 2000);

        bed.Simulation.Run(Recheck);

        bed.JobOf(colonist)?.Kind.ShouldNotBe(JobKind.Sleep);
    }

    [Fact]
    public void By_day_an_exhausted_colonist_sleeps()
    {
        var bed = ColonyTestBed.Flat(8);
        var colonist = bed.AddColonist(2, 2, 1, rest: 900);

        bed.Simulation.Run(3);

        bed.JobOf(colonist)!.Kind.ShouldBe(JobKind.Sleep);
    }

    [Fact]
    public void A_colonist_rested_before_dawn_sleeps_on_until_six()
    {
        var bed = ColonyTestBed.Flat(8);
        bed.Simulation.Run(NightStarts);
        var colonist = bed.AddColonist(2, 2, 1, rest: 8000);

        // 2000 points at 1200 an hour: rested before 23:00.
        bed.Simulation.Run(3 * 60);
        bed.Need(colonist, "rest").Value.ShouldBe(10_000);
        bed.JobOf(colonist)!.Kind.ShouldBe(JobKind.Sleep);

        bed.Simulation.Run(NextMorning - NightStarts - (3 * 60) - 1);
        bed.JobOf(colonist)!.Kind.ShouldBe(JobKind.Sleep);

        bed.Simulation.Run(Recheck);
        bed.JobOf(colonist)?.Kind.ShouldNotBe(JobKind.Sleep);
    }

    [Fact]
    public void At_night_a_nearly_rested_colonist_goes_to_bed_too()
    {
        // Like one who slept by day, exhausted, and woke up at dusk.
        var bed = RockToDig();
        bed.Simulation.Run(NightStarts);
        var colonist = bed.AddColonist(1, 1, 1, rest: 9500);

        bed.Simulation.Run(Recheck);

        bed.JobOf(colonist)!.Kind.ShouldBe(JobKind.Sleep);
    }

    [Fact]
    public void A_colonist_woken_by_hunger_at_night_goes_back_to_bed()
    {
        var bed = ColonyTestBed.Flat(8);
        bed.Simulation.Run(NightStarts + 60);
        var colonist = bed.AddColonist(2, 2, 1, hunger: 1400, rest: 10_000);
        bed.AddItems("provisions", 5, 4, 4, 1);

        bed.Simulation.Run(Recheck);
        bed.JobOf(colonist)!.Kind.ShouldBe(JobKind.Eat);

        bed.Simulation.Run(3 * 60);
        bed.JobOf(colonist)!.Kind.ShouldBe(JobKind.Sleep);
    }

    [Theory]
    [InlineData(21, 6, 20, false)]
    [InlineData(21, 6, 21, true)]
    [InlineData(21, 6, 0, true)]
    [InlineData(21, 6, 5, true)]
    [InlineData(21, 6, 6, false)]
    [InlineData(1, 5, 0, false)]
    [InlineData(1, 5, 1, true)]
    [InlineData(1, 5, 5, false)]
    public void The_night_may_cross_midnight_or_not(int start, int end, int hour, bool night)
    {
        var ai = ColonyRules.From(ColonyTestBed.Content).Ai;
        var config = new AiConfigDef
        {
            Id = "test",
            EatTicksPerUnit = ai.EatTicksPerUnit,
            WanderRadius = ai.WanderRadius,
            WanderWaitMin = ai.WanderWaitMin,
            WanderWaitMax = ai.WanderWaitMax,
            UnreachableCooldownTicks = ai.UnreachableCooldownTicks,
            RecheckTicks = ai.RecheckTicks,
            NightStartHour = start,
            NightEndHour = end,
            DeconstructRefundPercent = ai.DeconstructRefundPercent,
        };

        config.IsNight(hour).ShouldBe(night);
    }

    /// <summary>A floor with a big block of rock, all ordered to be dug: work for more than a day.</summary>
    private static ColonyTestBed RockToDig()
    {
        var floor = ColonyTestBed.Level(16, 16, '.').ToCharArray();
        for (var y = 4; y < 14; y++)
        {
            for (var x = 4; x < 14; x++)
            {
                floor[(y * 17) + x] = '#';
            }
        }

        var bed = new ColonyTestBed(ColonyTestBed.Level(16, 16, '#'), new string(floor));
        bed.Designate(DesignationKind.Mine, new TileCoord(4, 4, 1), new TileCoord(13, 13, 1));
        return bed;
    }
}
