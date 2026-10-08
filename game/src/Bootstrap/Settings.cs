using Godot;
using OurLife.Localization;

namespace OurLife.Game.Bootstrap;

/// <summary>
/// Player preferences, stored in <c>user://settings.cfg</c>. Without a saved preference the
/// language is Spanish, whatever the language of the operating system.
/// </summary>
public sealed class Settings
{
    private const string FilePath = "user://settings.cfg";
    private const string Section = "general";
    private const string LocaleKey = "locale";

    private readonly bool _persistent;

    private Settings(string locale, bool persistent)
    {
        Locale = locale;
        _persistent = persistent;
    }

    public string Locale { get; private set; }

    public static Settings Load()
    {
        using var config = new ConfigFile();
        var locale = config.Load(FilePath) == Error.Ok
            ? config.GetValue(Section, LocaleKey, Localizer.DefaultLocale).AsString()
            : Localizer.DefaultLocale;
        return new Settings(locale, persistent: true);
    }

    /// <summary>Settings that are never written, for smoke tests and screenshots.</summary>
    public static Settings InMemory() => new(Localizer.DefaultLocale, persistent: false);

    public void SetLocale(string locale)
    {
        Locale = locale;
        if (_persistent)
        {
            Save();
        }
    }

    private void Save()
    {
        using var config = new ConfigFile();
        config.SetValue(Section, LocaleKey, Locale);
        var error = config.Save(FilePath);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Could not save {FilePath}: {error}");
        }
    }
}
