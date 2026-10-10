using Godot;
using OurLife.Game.Bootstrap;

namespace OurLife.Game.UI;

/// <summary>
/// A new game: the village's name and the seed. An empty or invalid seed means a random one, and
/// the random seed comes from the presentation, never from the simulation.
/// </summary>
public partial class NewGameScreen : Control
{
    private GameServices _services = null!;
    private LineEdit _name = null!;
    private LineEdit _seed = null!;

    /// <summary>Raised with the village's name and the seed to play.</summary>
    public event Action<string, ulong>? StartRequested;

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

        column.AddChild(UiBuilder.MenuButton("ui.newgame.start", screen.Start));
        column.AddChild(UiBuilder.MenuButton("ui.common.back", () => screen.BackRequested?.Invoke()));
        return screen;
    }

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
        StartRequested?.Invoke(name, seed);
    }
}
