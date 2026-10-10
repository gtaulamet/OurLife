using Godot;
using OurLife.Game.Bootstrap;

namespace OurLife.Game.UI;

/// <summary>
/// Options, from the main menu or the pause menu: the language, applied live, and the fullscreen
/// mode. Both are saved at once.
/// </summary>
public partial class OptionsScreen : Control
{
    private GameServices _services = null!;
    private readonly List<(string Locale, Button Button)> _languages = [];

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

        var fullscreen = new CheckBox { Text = "ui.options.fullscreen", ButtonPressed = services.Settings.Fullscreen };
        fullscreen.AddThemeFontSizeOverride("font_size", UiBuilder.MenuSize);
        fullscreen.Toggled += on =>
        {
            services.Settings.SetFullscreen(on);
            services.Settings.ApplyWindowMode();
        };
        column.AddChild(fullscreen);

        column.AddChild(UiBuilder.MenuButton("ui.common.back", () => screen.Closed?.Invoke()));
        screen.RefreshLanguages();
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
}
