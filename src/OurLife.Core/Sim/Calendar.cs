namespace OurLife.Core.Sim;

/// <summary>Converts ticks into calendar dates. Designed to run for centuries.</summary>
public sealed class Calendar
{
    public Calendar(TimeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        Settings = settings;
    }

    public TimeSettings Settings { get; }

    /// <summary>The number of the calendar day of a tick, from 0 on the first day, counted from midnight to midnight.</summary>
    public long DayNumberAt(long tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        return MinutesAt(tick) / TimeSettings.MinutesPerDay;
    }

    public GameDate DateAt(long tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);

        var totalMinutes = MinutesAt(tick);
        var dayNumber = totalMinutes / TimeSettings.MinutesPerDay;
        var minuteOfDay = (int)(totalMinutes % TimeSettings.MinutesPerDay);
        var year = checked(Settings.StartYear + (int)(dayNumber / Settings.DaysPerYear));
        var dayOfYear = (int)(dayNumber % Settings.DaysPerYear);
        var seasonIndex = dayOfYear / Settings.DaysPerSeason;

        return new GameDate(
            year,
            seasonIndex,
            Settings.Seasons[seasonIndex],
            (dayOfYear % Settings.DaysPerSeason) + 1,
            minuteOfDay / 60,
            minuteOfDay % 60);
    }

    /// <summary>Minutes from the midnight before the game started.</summary>
    private long MinutesAt(long tick) => (Settings.StartHour * 60L) + (tick * Settings.MinutesPerTick);
}
