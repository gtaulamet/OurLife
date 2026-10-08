using System.Globalization;

namespace OurLife.Core.Sim;

/// <summary>
/// A moment in the game calendar. <see cref="Year"/> is absolute (for example 1203) and
/// <see cref="Day"/> is 1-based within the season. Players see it through localized text;
/// <see cref="ToString"/> is only for logs.
/// </summary>
public readonly record struct GameDate(int Year, int SeasonIndex, string SeasonId, int Day, int Hour, int Minute)
{
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Year}-{SeasonId}-{Day:00} {Hour:00}:{Minute:00}");
}
