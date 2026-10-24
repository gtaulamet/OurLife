using System.Globalization;
using OurLife.Core.Colony;
using OurLife.Core.Ecs;
using OurLife.Core.Sim;
using OurLife.Core.Text;
using OurLife.Localization;

namespace OurLife.Game.Localization;

/// <summary>
/// Turns what the simulation says into text in the current language. Nested
/// <see cref="LocalizedText"/> arguments are resolved first. A colonist argument becomes its name,
/// with its gender in <c>&lt;name&gt;_gender</c> (see "Argumentos con referencias" in
/// <c>docs/i18n.md</c>); def arguments arrive with the first text that needs them.
/// </summary>
public sealed class TextResolver
{
    private readonly Localizer _localizer;

    public TextResolver(Localizer localizer)
    {
        _localizer = localizer;
    }

    /// <summary>The text in the current language, with its colonists looked up in <paramref name="world"/> (<see cref="ColonyText.Freeze"/>).</summary>
    public string Resolve(LocalizedText text, World world) => Resolve(ColonyText.Freeze(text, world));

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

    /// <summary>
    /// The text with its first letter in upper case, by the rules of the current language. Names of
    /// materials and items are written in lower case; the interface decides when one starts a line.
    /// </summary>
    public string Capitalized(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return text;
        }

        // The catalog's culture, which falls back to the invariant one for a language .NET does not know.
        var culture = _localizer.Catalog(_localizer.CurrentLocale).Culture;
        return culture.TextInfo.ToUpper(text[0]) + text[1..];
    }

    /// <summary>The name of an item category starting a line, like "Comida", as the stock of the HUD and the targets show it.</summary>
    public string CategoryName(OurLife.Core.Defs.ItemCategoryDef category)
    {
        ArgumentNullException.ThrowIfNull(category);
        return Capitalized(Format(category.NameKey!));
    }

    /// <summary>What a colonist is doing, like "Talando un árbol", or that it has nothing to do.</summary>
    public string Activity(World world, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.TryGet<OurLife.Core.Entities.Job>(colonist, out var job))
        {
            return Format("ui.inspector.idle");
        }

        // Sowing and reaping say which crop: the name is def.crop.<id>.name.
        return job.Crop is { } crop
            ? Format(OurLife.Core.Entities.JobKinds.ReportKey(job), ("crop", Format($"def.crop.{crop}.name")))
            : Format(OurLife.Core.Entities.JobKinds.ReportKey(job));
    }

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
