using OurLife.Core.Sim;

namespace OurLife.Core.Defs;

/// <summary>Time constants of the simulation. The content must define exactly one.</summary>
public sealed class TimeConfigDef : Def
{
    public required int TicksPerSecond { get; init; }

    public required int MinutesPerTick { get; init; }

    public required int DaysPerSeason { get; init; }

    public required IReadOnlyList<string> Seasons { get; init; }

    public required int StartYear { get; init; }

    public required int StartHour { get; init; }

    public TimeSettings ToTimeSettings() => new()
    {
        TicksPerSecond = TicksPerSecond,
        MinutesPerTick = MinutesPerTick,
        DaysPerSeason = DaysPerSeason,
        Seasons = Seasons,
        StartYear = StartYear,
        StartHour = StartHour,
    };

    internal override IEnumerable<string> Validate()
    {
        string? problem = null;
        try
        {
            ToTimeSettings().Validate();
        }
        catch (ArgumentException ex)
        {
            problem = ex.Message;
        }

        if (problem is not null)
        {
            yield return problem;
        }
    }
}
