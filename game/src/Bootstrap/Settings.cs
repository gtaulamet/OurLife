using Godot;
using OurLife.Localization;

namespace OurLife.Game.Bootstrap;

/// <summary>
/// Player preferences, stored in <c>user://settings.cfg</c>. Without a saved preference the
/// language is Spanish, whatever the language of the operating system, and the game is windowed.
/// </summary>
public sealed class Settings
{
    private const string FilePath = "user://settings.cfg";
    private const string Section = "general";
    private const string LocaleKey = "locale";
    private const string FullscreenKey = "fullscreen";

    private readonly bool _persistent;

    private Settings(string locale, bool fullscreen, bool persistent)
    {
        Locale = locale;
        Fullscreen = fullscreen;
        _persistent = persistent;
    }

    public string Locale { get; private set; }

    public bool Fullscreen { get; private set; }

    public static Settings Load()
    {
        using var config = new ConfigFile();
        if (config.Load(FilePath) != Error.Ok)
        {
            return new Settings(Localizer.DefaultLocale, fullscreen: false, persistent: true);
        }

        return new Settings(
            config.GetValue(Section, LocaleKey, Localizer.DefaultLocale).AsString(),
            config.GetValue(Section, FullscreenKey, false).AsBool(),
            persistent: true);
    }

    /// <summary>Settings that are never written, for smoke tests and screenshots.</summary>
    public static Settings InMemory() => new(Localizer.DefaultLocale, fullscreen: false, persistent: false);

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
        var error = config.Save(FilePath);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Could not save {FilePath}: {error}");
        }
    }
}
