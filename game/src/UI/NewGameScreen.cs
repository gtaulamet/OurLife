using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Game.Bootstrap;

namespace OurLife.Game.UI;

/// <summary>
/// A new game: the village's name, the seed and the pace of life (see
/// <c>docs/gdd/vida-y-generaciones.md</c>). An empty or invalid seed means a random one, and the
/// random seed comes from the presentation, never from the simulation.
/// </summary>
public partial class NewGameScreen : Control
{
    private readonly List<(LifePaceDef Pace, Button Button)> _paces = [];
    private GameServices _services = null!;
    private LineEdit _name = null!;
    private LineEdit _seed = null!;
    private Label _paceDescription = null!;
    private string _pace = string.Empty;

    /// <summary>Raised with the village's name, the seed and the id of the pace of life to play.</summary>
    public event Action<string, ulong, string>? StartRequested;

    public event Action? BackRequested;

    public static NewGameScreen Create(GameServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var screen = new NewGameScreen { Name = nameof(NewGameScreen), _services = services };
        screen.SetAnchorsPreset(LayoutPreset.FullRect);
        var (root, column) = UiBuilder.Screen("Root", dimmed: false);
        screen.AddChild(root);

        column.AddChild(UiBuilder.Title("ui.newgame.title"));
        column.AddChild(UiBuilder.Caption("ui.newgame.colony_name"));
        screen._name = Field(services.Text.Format("ui.newgame.default_name"));
        column.AddChild(screen._name);

        column.AddChild(UiBuilder.Caption("ui.newgame.seed"));
        var seedRow = new HBoxContainer();
        seedRow.AddThemeConstantOverride("separation", UiBuilder.Gap);
        screen._seed = Field(RandomSeed().ToString(System.Globalization.CultureInfo.InvariantCulture));
        screen._seed.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        seedRow.AddChild(screen._seed);
        var random = new Button { Text = "ui.newgame.random_seed" };
        random.AddThemeFontSizeOverride("font_size", UiBuilder.MenuSize);
        random.Pressed += () => screen._seed.Text = RandomSeed().ToString(System.Globalization.CultureInfo.InvariantCulture);
        seedRow.AddChild(random);
        column.AddChild(seedRow);

        column.AddChild(UiBuilder.Caption("ui.newgame.life_pace"));
        var paces = new HBoxContainer();
        paces.AddThemeConstantOverride("separation", UiBuilder.Gap);
        column.AddChild(paces);
        foreach (var pace in services.Defs.All<LifePaceDef>().OrderBy(pace => pace.LifeDaysPerDay).ThenBy(pace => pace.Id, StringComparer.Ordinal))
        {
            // Formatted here: the names are written in lower case, and a button starts with a capital.
            var button = new Button { ToggleMode = true, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
            button.AddThemeFontSizeOverride("font_size", UiBuilder.MenuSize);
            var id = pace.Id;
            button.Pressed += () => screen.ChoosePace(id);
            paces.AddChild(button);
            screen._paces.Add((pace, button));
        }

        screen._paceDescription = new Label
        {
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(360, 0),
        };
        column.AddChild(screen._paceDescription);
        screen._pace = LifeRules.From(services.Defs).DefaultPace.Id;
        screen.RefreshPaces();

        column.AddChild(UiBuilder.MenuButton("ui.newgame.start", screen.Start));
        column.AddChild(UiBuilder.MenuButton("ui.common.back", () => screen.BackRequested?.Invoke()));
        return screen;
    }

    public override void _EnterTree() => _services.Localizer.LocaleChanged += OnLocaleChanged;

    public override void _ExitTree() => _services.Localizer.LocaleChanged -= OnLocaleChanged;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            BackRequested?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }

    private static LineEdit Field(string text)
    {
        var field = new LineEdit { Text = text, AutoTranslateMode = AutoTranslateModeEnum.Disabled, CustomMinimumSize = new Vector2(360, 0) };
        field.AddThemeFontSizeOverride("font_size", UiBuilder.MenuSize);
        return field;
    }

    private static ulong RandomSeed() => (ulong)Random.Shared.NextInt64(1, int.MaxValue);

    private void OnLocaleChanged(string locale) => RefreshPaces();

    private void ChoosePace(string pace)
    {
        _pace = pace;
        RefreshPaces();
    }

    private void RefreshPaces()
    {
        foreach (var (pace, button) in _paces)
        {
            button.Text = _services.Text.Capitalized(_services.Text.Format(pace.NameKey!));
            button.SetPressedNoSignal(pace.Id == _pace);
            if (pace.Id == _pace)
            {
                _paceDescription.Text = _services.Text.Format(pace.DescKey!);
            }
        }
    }

    private void Start()
    {
        var name = _name.Text.Trim();
        if (name.Length == 0)
        {
            name = _services.Text.Format("ui.newgame.default_name");
        }

        var seed = ulong.TryParse(_seed.Text.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var typed) && typed > 0
            ? typed
            : RandomSeed();
        StartRequested?.Invoke(name, seed, _pace);
    }
}
