using Godot;
using OurLife.Core.Sim;

namespace OurLife.Game.Rendering;

/// <summary>
/// The seasons in the view (<c>docs/gdd/estaciones-visibles.md</c>): once per hour of the game it
/// works out the look of the year, the grass, the leaves and the snow, and hands it to whoever
/// paints them. Presentation only: it reads the calendar and changes nothing, like the
/// <see cref="DayNightCycle"/>. A forced season, for screenshots, holds that look all year.
/// </summary>
public partial class SeasonCycle : Node
{
    /// <summary>The season to hold all year (<c>--season</c>), or null to follow the calendar. Set before the game is shown.</summary>
    public static string? Forced { get; set; }

    private Simulation _simulation = null!;
    private SeasonCatalog _catalog = null!;
    private Action<SeasonLook> _show = null!;

    // What is shown, to skip frames where the hour did not change.
    private (int Year, int Season, int Day, int Hour)? _shown;

    /// <summary>Whether the catalog can paint every season of the calendar: with a season it lacks, nothing changes.</summary>
    public static bool CanPaint(Simulation simulation, VisualCatalog catalog) =>
        catalog.Seasons is { } seasons && simulation.Time.Seasons.All(seasons.Seasons.ContainsKey);

    /// <summary>The look of the calendar's moment, or of the forced season.</summary>
    public static SeasonLook LookAt(Simulation simulation, SeasonCatalog catalog, string? forced)
    {
        if (forced is not null)
        {
            return SeasonLook.Of(catalog, forced);
        }

        var date = simulation.CurrentDate;
        var daysIn = (date.Day - 1) + ((date.Hour * 60 + date.Minute) / (double)TimeSettings.MinutesPerDay);
        return SeasonLook.At(catalog, simulation.Time.Seasons, simulation.Time.DaysPerSeason, date.SeasonIndex, daysIn);
    }

    /// <param name="show">Receives the look at the start and each hour it changes.</param>
    public static SeasonCycle Create(Simulation simulation, SeasonCatalog catalog, Action<SeasonLook> show)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(show);
        var cycle = new SeasonCycle { Name = nameof(SeasonCycle), _simulation = simulation, _catalog = catalog, _show = show };
        cycle.Apply();
        return cycle;
    }

    public override void _Process(double delta) => Apply();

    private void Apply()
    {
        var date = _simulation.CurrentDate;
        var now = (date.Year, date.SeasonIndex, date.Day, date.Hour);
        if (_shown == now)
        {
            return;
        }

        _shown = now;
        _show(LookAt(_simulation, _catalog, Forced));
    }
}
