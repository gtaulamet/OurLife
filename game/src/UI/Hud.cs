using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Sim;
using OurLife.Game.Bootstrap;
using OurLife.Game.Input;
using OurLife.Game.Rendering;
using OurLife.Game.Session;

namespace OurLife.Game.UI;

/// <summary>
/// The HUD of a game, drawn over the 3D view (see "HUD de la partida" in
/// <c>docs/gdd/interfaz-ip1.md</c>): date and clock, time buttons and level on top; the inspector
/// on the right; the event log, the order hint, the build bar (<c>docs/gdd/construccion.md</c>)
/// and the orders bar at the bottom; under the top bar, the alerts under way and the colony panel
/// (<c>docs/gdd/gestion.md</c>). Buttons whose
/// text is a key are translated by Godot; texts with arguments are formatted here again when the
/// language changes. Empty space lets the mouse through to the world.
/// </summary>
public partial class Hud : CanvasLayer
{
    private const int Margin = 12;
    private const int Gap = 8;

    // Under the top bar, where the left column and the inspector start.
    private const int PanelsTop = 56;

    // Colours of the alerts: a warning and a danger.
    private static readonly Color WarningColor = new(1f, 0.85f, 0.3f);
    private static readonly Color DangerColor = new(1f, 0.4f, 0.35f);

    private readonly Dictionary<DesignationKind, Button> _orderButtons = [];
    private readonly List<(int Speed, Button Button)> _speedButtons = [];
    private GameServices _services = null!;
    private GameSession _session = null!;
    private GameInput _input = null!;
    private WorldView _view = null!;
    private Label _date = null!;
    private Label _level = null!;
    private Label _hint = null!;
    private HBoxContainer _buildRow = null!;
    private VBoxContainer _alerts = null!;
    private readonly List<(Button Line, bool Danger)> _alertLines = [];
    private Control _ordersBar = null!;
    private List<(EntityId Colonist, string Key)> _shownAlerts = [];
    private bool _alertsChanged;
    private Button _buildButton = null!;
    private Button _pause = null!;
    private long _shownTick = -1;

    public InspectorPanel Inspector { get; private set; } = null!;

    public EventLog Log { get; private set; } = null!;

    public ColonyPanel Colony { get; private set; } = null!;

    public static Hud Create(GameServices services, GameSession session, GameInput input, WorldView view, string colonyName)
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

        root.AddChild(hud.BuildTopBar(colonyName));
        root.AddChild(hud.BuildBottom());

        // Under the top bar on the left: the alerts, or the colony panel when it is open. The panel
        // says what every colonist does, and with the alerts above it, it reached the event log on
        // a small canvas (docs/gdd/tamano-de-la-interfaz.md).
        var left = new VBoxContainer { Name = "Left", MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(Margin, PanelsTop) };
        left.AddThemeConstantOverride("separation", Gap);
        root.AddChild(left);
        hud._alerts = new VBoxContainer { Name = "Alerts", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        left.AddChild(hud._alerts);
        hud.Colony = ColonyPanel.Create(services, session, input);
        left.AddChild(hud.Colony);
        hud.Colony.VisibilityChanged += hud.ShowAlertsUnlessColony;

        hud.Inspector = InspectorPanel.Create(services, session.Simulation, session.Enqueue);
        hud.Inspector.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        hud.Inspector.Position = new Vector2(-Margin, PanelsTop);
        hud.Inspector.GrowHorizontal = Control.GrowDirection.Begin;
        root.AddChild(hud.Inspector);

        input.SelectionChanged += hud.Inspector.Inspect;
        input.OrderChanged += hud.OnOrderChanged;
        input.BuildChanged += hud.OnBuildChanged;
        input.ColonyPanelRequested += hud.Colony.Toggle;
        session.ClockChanged += hud.RefreshTimeButtons;
        view.ViewLevelChanged += hud.OnViewLevelChanged;
        hud.RefreshAll();
        return hud;
    }

    public override void _EnterTree() => _services.Localizer.LocaleChanged += OnLocaleChanged;

    public override void _ExitTree() => _services.Localizer.LocaleChanged -= OnLocaleChanged;

    /// <summary>Follows the events of a frame: new messages for the log, and news for the inspector.</summary>
    public void Apply(IReadOnlyList<ISimEvent> events)
    {
        Log.Apply(events, _session.Simulation.Calendar, _session.Simulation.World);
        Inspector.NoteChange();
        _alertsChanged = true;
    }

    public override void _Process(double delta)
    {

        // The clock only changes with a tick: no need to format it every frame.
        var tick = _session.Simulation.CurrentTick;
        if (tick != _shownTick)
        {
            _shownTick = tick;
            RefreshDate();

            // An alert also ends without a message, like an idle colonist that took work.
            _alertsChanged = true;
        }

        // Once per frame at most, after the events and the tick of the frame.
        if (_alertsChanged)
        {
            _alertsChanged = false;
            RefreshAlerts(force: false);
        }

        KeepInspectorClear();
    }

    /// <summary>
    /// On a narrow canvas, as with a large interface (docs/gdd/tamano-de-la-interfaz.md), the open
    /// colony panel reaches the inspector: the inspector then goes under the panel, if it fits
    /// above the bars at the bottom; if not, it stays at the top, over the panel, and not off the screen.
    /// </summary>
    private void KeepInspectorClear()
    {
        var top = (float)PanelsTop;
        if (Colony.Visible && Inspector.Visible)
        {
            var panel = Colony.GetGlobalRect();
            var below = panel.End.Y + Gap;
            if (panel.End.X + Gap > Inspector.GetGlobalRect().Position.X && below + Inspector.Size.Y + Gap <= BottomBarsTop())
            {
                top = below;
            }
        }

        if (!Mathf.IsEqualApprox(Inspector.Position.Y, top))
        {
            Inspector.Position = new Vector2(Inspector.Position.X, top);
        }
    }

    /// <summary>The top of the full-width rows at the bottom: the hint, the build bar and the orders. The log sits on the left.</summary>
    private float BottomBarsTop() =>
        new Control[] { _hint, _buildRow, _ordersBar }.Where(row => row.Visible).Min(row => row.GetGlobalRect().Position.Y);

    private Control BuildTopBar(string colonyName)
    {
        var panel = new PanelContainer { Name = "TopBar" };
        panel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        panel.OffsetLeft = Margin;
        panel.OffsetRight = -Margin;
        panel.OffsetTop = Margin;

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", Gap * 2);
        panel.AddChild(row);

        // The village's name is a proper name: it is never translated.
        row.AddChild(new Label { Text = colonyName, AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled });
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

        var colony = new Button { Text = "ui.colony.button", FocusMode = Control.FocusModeEnum.None };
        colony.Pressed += () => Colony.Toggle();
        row.AddChild(colony);

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

        // What to build and of what, shown while the build tool is on.
        _buildRow = new HBoxContainer { Name = "BuildBar", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _buildRow.AddThemeConstantOverride("separation", Gap / 2);
        column.AddChild(_buildRow);

        var orders = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        orders.AddThemeConstantOverride("separation", Gap / 2);
        column.AddChild(orders);
        _ordersBar = orders;
        foreach (var kind in Enum.GetValues<DesignationKind>())
        {
            // Building sits among the orders, before Cancel, which closes the bar.
            if (kind == DesignationKind.Cancel)
            {
                _buildButton = new Button { Text = "ui.orders.build", ToggleMode = true, FocusMode = Control.FocusModeEnum.None };
                _buildButton.Pressed += _input.ToggleBuild;
                orders.AddChild(_buildButton);
            }

            // The key is translated by Godot, live; UiTextTests checks it exists for every order.
            var key = $"ui.orders.{DesignationKinds.Id(kind)}";
            var button = new Button { Text = key, ToggleMode = true, FocusMode = Control.FocusModeEnum.None };
            button.Pressed += () => _input.Order = _input.Order == kind ? null : kind;
            orders.AddChild(button);
            _orderButtons.Add(kind, button);
        }

        return column;
    }

    private void OnLocaleChanged(string locale)
    {
        RefreshAll();
        RefreshBuildBar();
        RefreshAlerts(force: true);
    }

    /// <summary>
    /// One line per alert under way, yellow for a warning and red for a danger; a click selects the
    /// colonist. The lines are built again only when the alerts change, or with <paramref name="force"/>.
    /// </summary>
    private void RefreshAlerts(bool force)
    {
        var simulation = _session.Simulation;
        var alerts = ColonyAlerts.Active(simulation);
        var shown = alerts.Select(alert => (alert.Colonist, alert.Text.Key)).ToList();
        if (!force && shown.SequenceEqual(_shownAlerts))
        {
            return;
        }

        _shownAlerts = shown;
        _alertLines.Clear();
        foreach (var child in _alerts.GetChildren())
        {
            _alerts.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var alert in alerts)
        {
            var line = new Button
            {
                Text = _services.Text.Resolve(alert.Text, simulation.World),
                AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
                FocusMode = Control.FocusModeEnum.None,
                Alignment = HorizontalAlignment.Left,
            };
            line.AddThemeColorOverride("font_color", alert.Severity == NotificationSeverity.Danger ? DangerColor : WarningColor);
            var colonist = alert.Colonist;
            line.Pressed += () => _input.SelectColonist(colonist);
            _alerts.AddChild(line);
            _alertLines.Add((line, alert.Severity == NotificationSeverity.Danger));
        }

        ShowAlertsUnlessColony();
    }

    /// <summary>
    /// With the colony panel open, only dangers show: the panel already says which colonists are
    /// idle, and the warnings above it pushed it onto the event log on a small canvas.
    /// </summary>
    private void ShowAlertsUnlessColony()
    {
        var any = false;
        foreach (var (line, danger) in _alertLines)
        {
            line.Visible = danger || !Colony.Visible;
            any |= line.Visible;
        }

        _alerts.Visible = any;
    }

    private void OnBuildChanged(BuildChoice? build)
    {
        RefreshBuildBar();
        RefreshHint();
    }

    /// <summary>
    /// Builds the bar again: a button per buildable, then one per material the chosen one allows,
    /// the chosen ones pressed. Choosing a buildable keeps the material if it allows it.
    /// </summary>
    private void RefreshBuildBar()
    {
        var build = _input.Build;
        _buildButton.SetPressedNoSignal(build is not null);
        _buildRow.Visible = build is not null;
        foreach (var child in _buildRow.GetChildren())
        {
            _buildRow.RemoveChild(child);
            child.QueueFree();
        }

        if (build is not { } choice)
        {
            return;
        }

        var content = _session.Simulation.Content;
        foreach (var buildable in Construction.BuildablesInOrder(content))
        {
            var material = buildable.Materials.Contains(choice.Material) ? choice.Material : buildable.Materials[0];
            AddBuildButton(buildable, buildable.Id == choice.Buildable, choice with { Buildable = buildable.Id, Material = material });
        }

        _buildRow.AddChild(new VSeparator());
        foreach (var material in content.Get<BuildableDef>(choice.Buildable).Materials)
        {
            AddBuildButton(content.Get<MaterialDef>(material), material == choice.Material, choice with { Material = material });
        }
    }

    private void AddBuildButton(Def def, bool chosen, BuildChoice choice)
    {
        var text = _services.Text;
        var button = new Button
        {
            Text = text.Capitalized(text.Format(def.NameKey!, ("count", 1))),
            AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
            ToggleMode = true,
            ButtonPressed = chosen,
            FocusMode = Control.FocusModeEnum.None,
        };

        // Pressing the chosen one again keeps it chosen: the bar is drawn again either way.
        button.Pressed += () =>
        {
            _input.Build = choice;
            RefreshBuildBar();
        };
        _buildRow.AddChild(button);
    }

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
        var text = _services.Text;
        _hint.Visible = _input.Order is not null || _input.Build is not null;
        if (_input.Order is { } order)
        {
            _hint.Text = text.Format("ui.orders.hint", ("order", text.Format($"ui.orders.name.{DesignationKinds.Id(order)}")));
        }
        else if (_input.Build is { } build)
        {
            var content = _session.Simulation.Content;
            var name = text.Format(
                "ui.orders.name.build",
                ("buildable", text.Format(content.Get<BuildableDef>(build.Buildable).NameKey!, ("count", 1))),
                ("material", text.Format(content.Get<MaterialDef>(build.Material).NameKey!, ("count", 1))));
            _hint.Text = text.Format("ui.orders.hint", ("order", name));

            // Furniture can be turned before placing it: the second line says how far.
            if (content.Get<BuildableDef>(build.Buildable).IsFurniture)
            {
                _hint.Text += "\n" + text.Format("ui.orders.rotate_hint", ("quarter", build.Rotation + 1));
            }
        }
    }
}
