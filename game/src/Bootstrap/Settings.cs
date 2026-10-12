using Godot;
using OurLife.Localization;

namespace OurLife.Game.Bootstrap;

/// <summary>
/// Player preferences, stored in <c>user://settings.cfg</c>. Without a saved preference the
/// language is Spanish, whatever the language of the operating system, the game is windowed and
/// the day and night cycle is on.
/// </summary>
public sealed class Settings
{
    private const string FilePath = "user://settings.cfg";
    private const string Section = "general";
    private const string LocaleKey = "locale";
    private const string FullscreenKey = "fullscreen";
    private const string DayNightKey = "day_night";

    private readonly bool _persistent;

    private Settings(string locale, bool fullscreen, bool dayNight, bool persistent)
    {
        Locale = locale;
        Fullscreen = fullscreen;
        DayNightCycle = dayNight;
        _persistent = persistent;
    }

    public string Locale { get; private set; }

    public bool Fullscreen { get; private set; }

    /// <summary>Whether the light follows the hour of the game (see <c>docs/gdd/dia-y-noche.md</c>).</summary>
    public bool DayNightCycle { get; private set; }

    public static Settings Load()
    {
        using var config = new ConfigFile();
        if (config.Load(FilePath) != Error.Ok)
        {
            return new Settings(Localizer.DefaultLocale, fullscreen: false, dayNight: true, persistent: true);
        }

        return new Settings(
            config.GetValue(Section, LocaleKey, Localizer.DefaultLocale).AsString(),
            config.GetValue(Section, FullscreenKey, false).AsBool(),
            config.GetValue(Section, DayNightKey, true).AsBool(),
            persistent: true);
    }

    /// <summary>Settings that are never written, for smoke tests and screenshots.</summary>
    public static Settings InMemory() => new(Localizer.DefaultLocale, fullscreen: false, dayNight: true, persistent: false);

    public void SetLocale(string locale)
    {
        Locale = locale;
        Save();
    }

    public void SetFullscreen(bool fullscreen)
    {
        Fullscreen = fullscreen;
        Save();
    }

    public void SetDayNightCycle(bool on)
    {
        DayNightCycle = on;
        Save();
    }

    /// <summary>Puts the window in the saved mode: fullscreen or windowed.</summary>
    public void ApplyWindowMode() =>
        DisplayServer.WindowSetMode(Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);

    private void Save()
    {
        if (!_persistent)
        {
            return;
        }

        using var config = new ConfigFile();
        config.SetValue(Section, LocaleKey, Locale);
        config.SetValue(Section, FullscreenKey, Fullscreen);
        config.SetValue(Section, DayNightKey, DayNightCycle);
        var error = config.Save(FilePath);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Could not save {FilePath}: {error}");
        }
    }
}
