using Godot;
using OurLife.Game.Bootstrap;

namespace OurLife.Game.UI;

/// <summary>
/// Options, from the main menu or the pause menu: the language, applied live, the fullscreen
/// mode and the day and night cycle. All are saved at once.
/// </summary>
public partial class OptionsScreen : Control
{
    private GameServices _services = null!;
    private readonly List<(string Locale, Button Button)> _languages = [];
    private readonly List<(bool Fullscreen, Button Button)> _modes = [];
    private readonly List<(bool On, Button Button)> _cycle = [];

    public event Action? Closed;

    public static OptionsScreen Create(GameServices services, bool dimmed)
    {
        ArgumentNullException.ThrowIfNull(services);
        var screen = new OptionsScreen { Name = nameof(OptionsScreen), _services = services };
        screen.SetAnchorsPreset(LayoutPreset.FullRect);
        var (root, column) = UiBuilder.Screen("Root", dimmed);
        screen.AddChild(root);

        column.AddChild(UiBuilder.Title("ui.options.title"));
        column.AddChild(UiBuilder.Caption("ui.options.language"));
        var languages = new HBoxContainer();
        languages.AddThemeConstantOverride("separation", UiBuilder.Gap);
        column.AddChild(languages);
        foreach (var locale in services.Localizer.Locales)
        {
            // The name of each language is the same in every language, like "Español".
            var key = $"ui.language.{locale}";
            var button = new Button { Text = key, ToggleMode = true };
            button.AddThemeFontSizeOverride("font_size", UiBuilder.MenuSize);
            button.Pressed += () => screen.ChooseLanguage(locale);
            languages.AddChild(button);
            screen._languages.Add((locale, button));
        }

        // Two options, like the languages: a check box's mark was hard to see on the dimmed backdrop.
        column.AddChild(UiBuilder.Caption("ui.options.display"));
        var modes = new HBoxContainer();
        modes.AddThemeConstantOverride("separation", UiBuilder.Gap);
        column.AddChild(modes);
        foreach (var (key, fullscreen) in new[] { ("ui.options.windowed", false), ("ui.options.fullscreen", true) })
        {
            var button = new Button { Text = key, ToggleMode = true };
            button.AddThemeFontSizeOverride("font_size", UiBuilder.MenuSize);
            button.Pressed += () => screen.ChooseFullscreen(fullscreen);
            modes.AddChild(button);
            screen._modes.Add((fullscreen, button));
        }

        column.AddChild(UiBuilder.Caption("ui.options.day_night"));
        var cycle = new HBoxContainer();
        cycle.AddThemeConstantOverride("separation", UiBuilder.Gap);
        column.AddChild(cycle);
        foreach (var (key, on) in new[] { ("ui.common.on", true), ("ui.common.off", false) })
        {
            var button = new Button { Text = key, ToggleMode = true };
            button.AddThemeFontSizeOverride("font_size", UiBuilder.MenuSize);
            button.Pressed += () => screen.ChooseDayNight(on);
            cycle.AddChild(button);
            screen._cycle.Add((on, button));
        }

        column.AddChild(UiBuilder.MenuButton("ui.common.back", () => screen.Closed?.Invoke()));
        screen.RefreshLanguages();
        screen.RefreshModes();
        screen.RefreshCycle();
        return screen;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            Closed?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }

    private void ChooseLanguage(string locale)
    {
        _services.ChangeLanguage(locale);
        RefreshLanguages();
    }

    private void RefreshLanguages()
    {
        foreach (var (locale, button) in _languages)
        {
            button.SetPressedNoSignal(locale == _services.Localizer.CurrentLocale);
        }
    }

    private void ChooseFullscreen(bool fullscreen)
    {
        _services.Settings.SetFullscreen(fullscreen);
        _services.Settings.ApplyWindowMode();
        RefreshModes();
    }

    private void ChooseDayNight(bool on)
    {
        _services.Settings.SetDayNightCycle(on);
        RefreshCycle();
    }

    private void RefreshCycle()
    {
        foreach (var (on, button) in _cycle)
        {
            button.SetPressedNoSignal(on == _services.Settings.DayNightCycle);
        }
    }

    private void RefreshModes()
    {
        foreach (var (fullscreen, button) in _modes)
        {
            button.SetPressedNoSignal(fullscreen == _services.Settings.Fullscreen);
        }
    }
}
