using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Sim;
using OurLife.Game.Bootstrap;
using OurLife.Game.Input;
using OurLife.Game.Rendering;
using OurLife.Game.Session;

namespace OurLife.Game.UI;

/// <summary>
/// The HUD of a game, drawn over the 3D view (see "HUD de la partida" in
/// <c>docs/gdd/interfaz-ip1.md</c>): date and clock, time buttons and level on top; the inspector
/// on the right; the event log, the order hint and the orders bar at the bottom. Buttons whose
/// text is a key are translated by Godot; texts with arguments are formatted here again when the
/// language changes. Empty space lets the mouse through to the world.
/// </summary>
public partial class Hud : CanvasLayer
{
    private const int Margin = 12;
    private const int Gap = 8;

    private static readonly (DesignationKind Kind, string Id)[] Orders =
    [
        (DesignationKind.Mine, "mine"),
        (DesignationKind.Stairs, "stairs"),
        (DesignationKind.Chop, "chop"),
        (DesignationKind.Harvest, "harvest"),
        (DesignationKind.Cancel, "cancel"),
    ];

    private readonly Dictionary<DesignationKind, Button> _orderButtons = [];
    private readonly List<(int Speed, Button Button)> _speedButtons = [];
    private GameServices _services = null!;
    private GameSession _session = null!;
    private GameInput _input = null!;
    private WorldView _view = null!;
    private Label _date = null!;
    private Label _level = null!;
    private Label _hint = null!;
    private Button _pause = null!;
    private int _shownMinute = -1;

    public InspectorPanel Inspector { get; private set; } = null!;

    public EventLog Log { get; private set; } = null!;

    public static Hud Create(GameServices services, GameSession session, GameInput input, WorldView view)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(view);
        var hud = new Hud { Name = nameof(Hud), _services = services, _session = session, _input = input, _view = view };

        // A full-screen control that lets the mouse through to the world, for the theme and anchors.
        var root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiTheme.Create() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hud.AddChild(root);

        root.AddChild(hud.BuildTopBar());
        root.AddChild(hud.BuildBottom());

        hud.Inspector = InspectorPanel.Create(services, session.Simulation);
        hud.Inspector.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        hud.Inspector.Position = new Vector2(-Margin, 56);
        hud.Inspector.GrowHorizontal = Control.GrowDirection.Begin;
        root.AddChild(hud.Inspector);

        input.SelectionChanged += hud.Inspector.Inspect;
        input.OrderChanged += hud.OnOrderChanged;
        session.ClockChanged += hud.RefreshTimeButtons;
        view.ViewLevelChanged += hud.OnViewLevelChanged;
        hud.RefreshAll();
        return hud;
    }

    public override void _EnterTree() => _services.Localizer.LocaleChanged += OnLocaleChanged;

    public override void _ExitTree() => _services.Localizer.LocaleChanged -= OnLocaleChanged;

    /// <summary>Follows the events of a frame: new messages for the log.</summary>
    public void Apply(IReadOnlyList<ISimEvent> events) => Log.Apply(events, _session.Simulation.Calendar);

    public override void _Process(double delta)
    {
        // The clock only changes once per game minute: no need to format it every frame.
        var date = _session.Simulation.CurrentDate;
        if (date.Minute != _shownMinute)
        {
            _shownMinute = date.Minute;
            RefreshDate();
        }
    }

    private Control BuildTopBar()
    {
        var panel = new PanelContainer { Name = "TopBar" };
        panel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        panel.OffsetLeft = Margin;
        panel.OffsetRight = -Margin;
        panel.OffsetTop = Margin;

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", Gap * 2);
        panel.AddChild(row);

        _date = new Label { AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(_date);

        var time = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        time.AddThemeConstantOverride("separation", Gap / 2);
        row.AddChild(time);

        _pause = new Button { Text = "ui.hud.pause", ToggleMode = true, FocusMode = Control.FocusModeEnum.None };
        _pause.Pressed += () => _session.TogglePause();
        time.AddChild(_pause);
        foreach (var speed in GameSession.Speeds)
        {
            var button = new Button { ToggleMode = true, FocusMode = Control.FocusModeEnum.None, AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled };
            button.Pressed += () => _session.SetSpeed(speed);
            time.AddChild(button);
            _speedButtons.Add((speed, button));
        }

        _level = new Label { AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled, CustomMinimumSize = new Vector2(120, 0), HorizontalAlignment = HorizontalAlignment.Right };
        row.AddChild(_level);
        return panel;
    }

    private Control BuildBottom()
    {
        var column = new VBoxContainer { Name = "Bottom", MouseFilter = Control.MouseFilterEnum.Ignore };
        column.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        column.OffsetLeft = Margin;
        column.OffsetRight = -Margin;
        column.OffsetBottom = -Margin;
        column.GrowVertical = Control.GrowDirection.Begin;
        column.AddThemeConstantOverride("separation", Gap);

        Log = EventLog.Create(_services);
        Log.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        column.AddChild(Log);

        _hint = new Label { AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddChild(_hint);

        var orders = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        orders.AddThemeConstantOverride("separation", Gap / 2);
        column.AddChild(orders);
        foreach (var (kind, id) in Orders)
        {
            // The key is translated by Godot, live; UiTextTests checks it exists for every order.
            var key = $"ui.orders.{id}";
            var button = new Button { Text = key, ToggleMode = true, FocusMode = Control.FocusModeEnum.None };
            button.Pressed += () => _input.Order = _input.Order == kind ? null : kind;
            orders.AddChild(button);
            _orderButtons.Add(kind, button);
        }

        return column;
    }

    private void OnLocaleChanged(string locale) => RefreshAll();

    private void OnViewLevelChanged(int level) => RefreshLevel();

    private void OnOrderChanged(DesignationKind? order)
    {
        foreach (var (kind, button) in _orderButtons)
        {
            button.SetPressedNoSignal(kind == order);
        }

        RefreshHint();
    }

    private void RefreshAll()
    {
        RefreshDate();
        RefreshLevel();
        RefreshTimeButtons();
        RefreshHint();
    }

    private void RefreshDate()
    {
        var date = _session.Simulation.CurrentDate;
        _date.Text = _services.Text.Format("ui.hud.date", ("date", _services.Text.Date(date)), ("clock", _services.Text.Clock(date)));
    }

    private void RefreshLevel() => _level.Text = _services.Text.Format("ui.hud.level", ("level", _view.ViewLevel));

    private void RefreshTimeButtons()
    {
        _pause.SetPressedNoSignal(_session.Paused);
        foreach (var (speed, button) in _speedButtons)
        {
            button.Text = _services.Text.Format("ui.hud.speed", ("speed", speed));
            button.SetPressedNoSignal(!_session.Paused && _session.Speed == speed);
        }
    }

    private void RefreshHint()
    {
        _hint.Visible = _input.Order is not null;
        if (_input.Order is { } order)
        {
            var id = Orders.First(entry => entry.Kind == order).Id;
            _hint.Text = _services.Text.Format("ui.orders.hint", ("order", _services.Text.Format($"ui.orders.name.{id}")));
        }
    }
}
