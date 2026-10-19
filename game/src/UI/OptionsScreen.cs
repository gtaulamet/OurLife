using Godot;
using OurLife.Game.Bootstrap;
using OurLife.Game.Rendering;

namespace OurLife.Game.UI;

/// <summary>
/// Options, from the main menu or the pause menu: the language, applied live, the fullscreen
/// mode, the size of the interface, the decoration of the grass, the day and night cycle, the
/// frames per second (<c>docs/gdd/indicadores.md</c>) and free will (<c>docs/gdd/libre-albedrio.md</c>).
/// All are saved at once.
/// </summary>
public partial class OptionsScreen : Control
{
    private GameServices _services = null!;
    private readonly List<(string Locale, Button Button)> _languages = [];
    private readonly List<(bool Fullscreen, Button Button)> _modes = [];
    private readonly List<(bool On, Button Button)> _cycle = [];
    private readonly List<(bool On, Button Button)> _fps = [];
    private readonly List<(bool On, Button Button)> _freeWill = [];
    private readonly List<(int Setting, Button Button)> _sizes = [];
    private readonly List<(DecorationLevel Level, Button Button)> _decorations = [];

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

        column.AddChild(UiBuilder.Caption("ui.options.decoration"));
        var decorations = new HBoxContainer();
        decorations.AddThemeConstantOverride("separation", UiBuilder.Gap);
        column.AddChild(decorations);
        foreach (var (level, key) in DecorationLevels.InOrder)
        {
            var button = new Button { Text = key, ToggleMode = true };
            button.AddThemeFontSizeOverride("font_size", UiBuilder.MenuSize);
            button.Pressed += () => screen.ChooseDecoration(level);
            decorations.AddChild(button);
            screen._decorations.Add((level, button));
        }

        // The switches side by side: at 720p a row more would not fit (docs/ip/IP2.5.md, risks).
        var switches = new HBoxContainer();
        switches.AddThemeConstantOverride("separation", UiBuilder.Gap * 3);
        column.AddChild(switches);
        switches.AddChild(Switch("ui.options.day_night", screen._cycle, screen.ChooseDayNight));
        switches.AddChild(Switch("ui.options.show_fps", screen._fps, screen.ChooseShowFps));
        switches.AddChild(Switch("ui.options.free_will", screen._freeWill, screen.ChooseFreeWill));

        column.AddChild(UiBuilder.MenuButton("ui.common.back", () => screen.Closed?.Invoke()));
        screen.RefreshLanguages();
        screen.RefreshModes();
        screen.RefreshCycle();
        screen.RefreshFps();
        screen.RefreshFreeWill();
        screen.RefreshSizes();
        screen.RefreshDecorations();
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
        var chosen = _services.Settings.UiScale;

        // A size the window cannot hold gives way to the largest that fits: that one shows pressed.
        var shown = chosen == UiScaleRules.Auto ? chosen : UiScaleRules.Effective(chosen, window.X, window.Y);
        foreach (var (setting, button) in _sizes)
        {
            button.Text = setting == UiScaleRules.Auto
                ? _services.Text.Format("ui.options.ui_scale_auto", ("percent", UiScaleRules.AutoFor(window.X, window.Y)))
                : _services.Text.Format("ui.options.ui_scale_percent", ("percent", setting));
            button.Disabled = setting != UiScaleRules.Auto && !UiScaleRules.IsAvailable(setting, window.X, window.Y);
            button.SetPressedNoSignal(setting == shown);
        }
    }

    private void ChooseDecoration(DecorationLevel level)
    {
        _services.Settings.SetDecoration(level);
        RefreshDecorations();
    }

    private void RefreshDecorations()
    {
        foreach (var (level, button) in _decorations)
        {
            button.SetPressedNoSignal(level == _services.Settings.Decoration);
        }
    }

    private void ChooseDayNight(bool on)
    {
        _services.Settings.SetDayNightCycle(on);
        RefreshCycle();
    }

    /// <summary>A caption with On and Off below it; the buttons are added to <paramref name="buttons"/>.</summary>
    private static VBoxContainer Switch(string captionKey, List<(bool On, Button Button)> buttons, Action<bool> choose)
    {
        var group = new VBoxContainer();
        group.AddThemeConstantOverride("separation", UiBuilder.Gap);
        group.AddChild(UiBuilder.Caption(captionKey));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UiBuilder.Gap);
        group.AddChild(row);
        foreach (var (key, on) in new[] { ("ui.common.on", true), ("ui.common.off", false) })
        {
            var button = new Button { Text = key, ToggleMode = true };
            button.AddThemeFontSizeOverride("font_size", UiBuilder.MenuSize);
            button.Pressed += () => choose(on);
            row.AddChild(button);
            buttons.Add((on, button));
        }

        return group;
    }

    private void ChooseShowFps(bool on)
    {
        _services.Settings.SetShowFps(on);
        RefreshFps();
    }

    private void RefreshFps()
    {
        foreach (var (on, button) in _fps)
        {
            button.SetPressedNoSignal(on == _services.Settings.ShowFps);
        }
    }

    private void ChooseFreeWill(bool on)
    {
        _services.Settings.SetFreeWill(on);
        RefreshFreeWill();
    }

    private void RefreshFreeWill()
    {
        foreach (var (on, button) in _freeWill)
        {
            button.SetPressedNoSignal(on == _services.Settings.FreeWill);
        }
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
