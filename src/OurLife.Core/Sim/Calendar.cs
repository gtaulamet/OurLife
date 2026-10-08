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

    public GameDate DateAt(long tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);

        var totalMinutes = (Settings.StartHour * 60L) + (tick * Settings.MinutesPerTick);
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
}
