using System.Globalization;
using OurLife.Core.Sim;
using OurLife.Core.Text;
using OurLife.Localization;

namespace OurLife.Game.Localization;

/// <summary>
/// Turns what the simulation says into text in the current language. Nested
/// <see cref="LocalizedText"/> arguments are resolved first. Entity arguments arrive in H1.6.
/// </summary>
public sealed class TextResolver
{
    private readonly Localizer _localizer;

    public TextResolver(Localizer localizer)
    {
        _localizer = localizer;
    }

    public string Resolve(LocalizedText text)
    {
        var arguments = new Dictionary<string, object?>(text.Args.Count, StringComparer.Ordinal);
        foreach (var (name, value) in text.Args)
        {
            arguments[name] = value is LocalizedText nested ? Resolve(nested) : value;
        }

        return _localizer.Format(text.Key, arguments);
    }

    public string Format(string key, params (string Name, object? Value)[] arguments) =>
        _localizer.Format(key, arguments.ToDictionary(argument => argument.Name, argument => argument.Value, StringComparer.Ordinal));

    /// <summary>Calendar date, like "Día 3 de primavera de 1200".</summary>
    public string Date(GameDate date) =>
        Format("time.date", ("day", date.Day), ("season", _localizer.Format($"time.season.{date.SeasonId}")), ("year", date.Year));

    /// <summary>Time of day, like "06:30".</summary>
    public string Clock(GameDate date) =>
        Format(
            "time.clock",
            ("hour", date.Hour.ToString("00", CultureInfo.InvariantCulture)),
            ("minute", date.Minute.ToString("00", CultureInfo.InvariantCulture)));
}
