using Godot;
using OurLife.Game.UI;
using OurLife.Localization;

namespace OurLife.Game.Bootstrap;

/// <summary>
/// Player preferences, stored in <c>user://settings.cfg</c>. Without a saved preference the
/// language is Spanish, whatever the language of the operating system, the game is windowed, the
/// day and night cycle is on and the interface size is automatic.
/// </summary>
public sealed class Settings
{
    private const string FilePath = "user://settings.cfg";
    private const string Section = "general";
    private const string LocaleKey = "locale";
    private const string FullscreenKey = "fullscreen";
    private const string DayNightKey = "day_night";
    private const string UiScaleKey = "ui_scale";

    private readonly bool _persistent;

    // The size written to the file; UiScale may differ for this session only (--ui-scale).
    private int _savedUiScale;

    private Settings(string locale, bool fullscreen, bool dayNight, int uiScale, bool persistent)
    {
        Locale = locale;
        Fullscreen = fullscreen;
        DayNightCycle = dayNight;
        UiScale = UiScaleRules.IsValid(uiScale) ? uiScale : UiScaleRules.Auto;
        _savedUiScale = UiScale;
        _persistent = persistent;
    }

    public string Locale { get; private set; }

    public bool Fullscreen { get; private set; }

    /// <summary>Whether the light follows the hour of the game (see <c>docs/gdd/dia-y-noche.md</c>).</summary>
    public bool DayNightCycle { get; private set; }

    /// <summary>
    /// Size of the interface in percent, or <see cref="UiScaleRules.Auto"/> to pick it from the
    /// window (see <c>docs/gdd/tamano-de-la-interfaz.md</c>).
    /// </summary>
    public int UiScale { get; private set; }

    /// <summary>Raised when the interface size changes, so the window applies it.</summary>
    public event Action? UiScaleChanged;

    public static Settings Load()
    {
        using var config = new ConfigFile();
        if (config.Load(FilePath) != Error.Ok)
        {
            return new Settings(Localizer.DefaultLocale, fullscreen: false, dayNight: true, UiScaleRules.Auto, persistent: true);
        }

        return new Settings(
            config.GetValue(Section, LocaleKey, Localizer.DefaultLocale).AsString(),
            config.GetValue(Section, FullscreenKey, false).AsBool(),
            config.GetValue(Section, DayNightKey, true).AsBool(),
            config.GetValue(Section, UiScaleKey, UiScaleRules.Auto).AsInt32(),
            persistent: true);
    }

    /// <summary>Settings that are never written, for smoke tests and screenshots.</summary>
    public static Settings InMemory() => new(Localizer.DefaultLocale, fullscreen: false, dayNight: true, UiScaleRules.Auto, persistent: false);

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

    /// <summary>
    /// Sets the interface size: <see cref="UiScaleRules.Auto"/> or one of the steps; anything else
    /// is ignored. Without <paramref name="save"/> it lasts for this session only, as with <c>--ui-scale</c>.
    /// </summary>
    public void SetUiScale(int uiScale, bool save = true)
    {
        if (!UiScaleRules.IsValid(uiScale) || (uiScale == UiScale && (!save || uiScale == _savedUiScale)))
        {
            return;
        }

        UiScale = uiScale;
        if (save)
        {
            _savedUiScale = uiScale;
            Save();
        }

        UiScaleChanged?.Invoke();
    }

    /// <summary>Draws the interface of a window at the size it gets from this setting and its current size.</summary>
    public void ApplyUiScale(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        // Changing the factor changes the size of the 2D canvas, which raises SizeChanged again.
        var size = window.Size;
        var factor = UiScaleRules.Effective(UiScale, size.X, size.Y) / 100f;
        if (!Mathf.IsEqualApprox(window.ContentScaleFactor, factor))
        {
            window.ContentScaleFactor = factor;
        }
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
        config.SetValue(Section, UiScaleKey, _savedUiScale);
        var error = config.Save(FilePath);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Could not save {FilePath}: {error}");
        }
    }
}
