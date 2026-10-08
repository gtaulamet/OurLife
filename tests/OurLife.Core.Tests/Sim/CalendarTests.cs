using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Sim;

public class CalendarTests
{
    // Plan defaults: 1 tick = 1 minute, days of 1440 ticks, seasons of 15 days, start 1200 at 06:00.
    private const int TicksPerDay = 1_440;
    private const int StartOffset = 6 * 60;

    private readonly Calendar _calendar = new(TestContent.Time);

    [Fact]
    public void Tick_zero_is_the_start_of_the_game()
    {
        _calendar.DateAt(0).ShouldBe(new GameDate(1200, 0, "spring", 1, 6, 0));
    }

    [Fact]
    public void Each_tick_is_one_minute()
    {
        _calendar.DateAt(1).ShouldBe(new GameDate(1200, 0, "spring", 1, 6, 1));
        _calendar.DateAt(60).ShouldBe(new GameDate(1200, 0, "spring", 1, 7, 0));
    }

    [Fact]
    public void The_day_changes_at_midnight()
    {
        _calendar.DateAt(TicksPerDay - StartOffset - 1).ShouldBe(new GameDate(1200, 0, "spring", 1, 23, 59));
        _calendar.DateAt(TicksPerDay - StartOffset).ShouldBe(new GameDate(1200, 0, "spring", 2, 0, 0));
    }

    [Fact]
    public void The_season_changes_after_its_days()
    {
        _calendar.DateAt((15 * TicksPerDay) - StartOffset).ShouldBe(new GameDate(1200, 1, "summer", 1, 0, 0));
    }

    [Fact]
    public void The_year_changes_after_four_seasons()
    {
        _calendar.DateAt((60 * TicksPerDay) - StartOffset).ShouldBe(new GameDate(1201, 0, "spring", 1, 0, 0));
    }

    [Fact]
    public void Centuries_later_the_date_is_still_exact()
    {
        var threeHundredYears = 300L * 60 * TicksPerDay;

        _calendar.DateAt(threeHundredYears).ShouldBe(new GameDate(1500, 0, "spring", 1, 6, 0));
    }

    [Fact]
    public void Settings_that_do_not_divide_a_day_are_rejected()
    {
        var settings = TestContent.Time with { MinutesPerTick = 7 };

        Should.Throw<ArgumentException>(() => new Calendar(settings));
    }

    [Fact]
    public void A_calendar_without_seasons_is_rejected()
    {
        var settings = TestContent.Time with { Seasons = [] };

        Should.Throw<ArgumentException>(() => new Calendar(settings));
    }
}
