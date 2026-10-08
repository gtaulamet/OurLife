namespace OurLife.Core.Sim;

/// <summary>
/// Time constants of the simulation, built from the <c>TimeConfig</c> def of the content
/// (<c>game/content/defs/config.json</c>). A day always has 24 hours of 60 minutes.
/// </summary>
public sealed record TimeSettings
{
    public const int MinutesPerDay = 24 * 60;

    /// <summary>Ticks per real second at speed 1×.</summary>
    public required int TicksPerSecond { get; init; }

    /// <summary>Game minutes that pass in one tick. Must divide a day evenly.</summary>
    public required int MinutesPerTick { get; init; }

    public required int DaysPerSeason { get; init; }

    /// <summary>Season ids in calendar order; their names come from the keys <c>time.season.&lt;id&gt;</c>.</summary>
    public required IReadOnlyList<string> Seasons { get; init; }

    /// <summary>Absolute year in which a new game starts, for example 1200.</summary>
    public required int StartYear { get; init; }

    /// <summary>Hour of the first day at which a new game starts.</summary>
    public required int StartHour { get; init; }

    public int TicksPerDay => MinutesPerDay / MinutesPerTick;

    public int DaysPerYear => DaysPerSeason * Seasons.Count;

    /// <exception cref="ArgumentException">Some value is out of range.</exception>
    public void Validate()
    {
        Require(TicksPerSecond > 0, "TicksPerSecond must be positive.");
        Require(MinutesPerTick > 0 && MinutesPerDay % MinutesPerTick == 0, "MinutesPerTick must divide 1440.");
        Require(DaysPerSeason > 0, "DaysPerSeason must be positive.");
        Require(Seasons is { Count: > 0 }, "There must be at least one season.");
        Require(Seasons.All(id => !string.IsNullOrWhiteSpace(id)), "Season ids cannot be empty.");
        Require(StartHour is >= 0 and < 24, "StartHour must be between 0 and 23.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ArgumentException(message);
        }
    }
}
