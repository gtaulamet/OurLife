using Godot;
using OurLife.Game.Bootstrap;

namespace OurLife.Game.UI;

/// <summary>
/// Options, from the main menu or the pause menu: the language, applied live, the fullscreen
/// mode, the size of the interface and the day and night cycle. All are saved at once.
/// </summary>
public partial class OptionsScreen : Control
{
    private GameServices _services = null!;
    private readonly List<(string Locale, Button Button)> _languages = [];
    private readonly List<(bool Fullscreen, Button Button)> _modes = [];
    private readonly List<(bool On, Button Button)> _cycle = [];
    private readonly List<(int Setting, Button Button)> _sizes = [];

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

        // The size buttons say a percentage, formatted for the language, so Godot does not translate them.
        column.AddChild(UiBuilder.Caption("ui.options.ui_scale"));
        var sizes = new HBoxContainer();
        sizes.AddThemeConstantOverride("separation", UiBuilder.Gap);
        column.AddChild(sizes);
        foreach (var setting in UiScaleRules.Steps.Prepend(UiScaleRules.Auto))
        {
            var button = new Button { ToggleMode = true, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
            button.AddThemeFontSizeOverride("font_size", UiBuilder.MenuSize);
            button.Pressed += () => screen.ChooseUiScale(setting);
            sizes.AddChild(button);
            screen._sizes.Add((setting, button));
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
        screen.RefreshSizes();
        return screen;
    }

    public override void _EnterTree()
    {
        _services.Localizer.LocaleChanged += OnLocaleChanged;
        GetTree().Root.SizeChanged += RefreshSizes;
    }

    public override void _ExitTree()
    {
        _services.Localizer.LocaleChanged -= OnLocaleChanged;
        GetTree().Root.SizeChanged -= RefreshSizes;
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

    private void OnLocaleChanged(string locale) => RefreshSizes();

    private void ChooseUiScale(int setting)
    {
        _services.Settings.SetUiScale(setting);
        RefreshSizes();
    }

    /// <summary>
    /// The texts of the size buttons, which change with the language and, for Auto, with the
    /// window; the sizes the window cannot hold are disabled.
    /// </summary>
    private void RefreshSizes()
    {
        // Also called before the screen enters the tree, where GetTree() is not available.
        var window = ((SceneTree)Engine.GetMainLoop()).Root.Size;
        foreach (var (setting, button) in _sizes)
        {
            button.Text = setting == UiScaleRules.Auto
                ? _services.Text.Format("ui.options.ui_scale_auto", ("percent", UiScaleRules.AutoFor(window.X, window.Y)))
                : _services.Text.Format("ui.options.ui_scale_percent", ("percent", setting));
            button.Disabled = setting != UiScaleRules.Auto && !UiScaleRules.IsAvailable(setting, window.X, window.Y);
            button.SetPressedNoSignal(setting == _services.Settings.UiScale);
        }
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
