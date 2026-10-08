using Godot;
using OurLife.Game.Bootstrap;

namespace OurLife.Game.UI;

/// <summary>
/// Temporary screen of milestone H1.3. It shows texts translated by Godot from their keys, texts
/// formatted by the localizer and every Spanish character in the pixel font, plus a button that
/// switches the language live. The main menu replaces it in H1.7.
/// </summary>
public partial class LanguageProbeScreen : Control
{
    private GameServices _services = null!;
    private Label _languageLabel = null!;
    private Label _startLabel = null!;
    private Label _settlersLabel = null!;

    public static LanguageProbeScreen Create(GameServices services)
    {
        var screen = new LanguageProbeScreen { _services = services };
        screen.Build();
        return screen;
    }

    public override void _EnterTree() => _services.Localizer.LocaleChanged += OnLocaleChanged;

    public override void _ExitTree() => _services.Localizer.LocaleChanged -= OnLocaleChanged;

    private void Build()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        Theme = UiTheme.Create();

        var background = new ColorRect { Color = new Color(0.11f, 0.10f, 0.09f) };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 16);
        center.AddChild(column);

        column.AddChild(KeyLabel("ui.game.title", 64));
        column.AddChild(KeyLabel("ui.probe.title", 32));
        column.AddChild(KeyLabel("ui.mainmenu.new_game", 48));

        _languageLabel = FormattedLabel(32);
        _startLabel = FormattedLabel(32);
        _settlersLabel = FormattedLabel(32);
        column.AddChild(_languageLabel);
        column.AddChild(_startLabel);
        column.AddChild(_settlersLabel);
        column.AddChild(KeyLabel("ui.probe.charset", 32));

        var switchButton = new Button { Text = "ui.probe.switch_language" };
        switchButton.AddThemeFontSizeOverride("font_size", 32);
        switchButton.Pressed += SwitchLanguage;
        column.AddChild(switchButton);

        Refresh();
    }

    private void OnLocaleChanged(string locale) => Refresh();

    private void Refresh()
    {
        var text = _services.Text;
        var localizer = _services.Localizer;
        var start = new Core.Sim.Calendar(_services.Defs.Time).DateAt(0);

        _languageLabel.Text = text.Format(
            "ui.probe.current_language", ("language", localizer.Format($"ui.language.{localizer.CurrentLocale}")));
        _startLabel.Text = text.Format("ui.probe.start", ("date", text.Date(start)), ("clock", text.Clock(start)));
        _settlersLabel.Text = text.Format("ui.probe.settlers", ("count", 5));
    }

    private void SwitchLanguage()
    {
        var locales = _services.Localizer.Locales;
        var current = locales.ToList().IndexOf(_services.Localizer.CurrentLocale);
        _services.ChangeLanguage(locales[(current + 1) % locales.Count]);
    }

    /// <summary>A label whose text is a key: Godot translates it and updates it when the language changes.</summary>
    private static Label KeyLabel(string key, int fontSize)
    {
        var label = new Label { Text = key, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    /// <summary>A label filled from code with already formatted text, so Godot must not translate it.</summary>
    private static Label FormattedLabel(int fontSize)
    {
        var label = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }
}
