using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
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
/// and the orders bar at the bottom. Buttons whose
/// text is a key are translated by Godot; texts with arguments are formatted here again when the
/// language changes. Empty space lets the mouse through to the world.
/// </summary>
public partial class Hud : CanvasLayer
{
    private const int Margin = 12;
    private const int Gap = 8;

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
    private Button _buildButton = null!;
    private Button _pause = null!;
    private long _shownTick = -1;

    public InspectorPanel Inspector { get; private set; } = null!;

    public EventLog Log { get; private set; } = null!;

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

        hud.Inspector = InspectorPanel.Create(services, session.Simulation, session.Enqueue);
        hud.Inspector.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        hud.Inspector.Position = new Vector2(-Margin, 56);
        hud.Inspector.GrowHorizontal = Control.GrowDirection.Begin;
        root.AddChild(hud.Inspector);

        input.SelectionChanged += hud.Inspector.Inspect;
        input.OrderChanged += hud.OnOrderChanged;
        input.BuildChanged += hud.OnBuildChanged;
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
        Log.Apply(events, _session.Simulation.Calendar);
        Inspector.NoteChange();
    }

    public override void _Process(double delta)
    {
        // The clock only changes with a tick: no need to format it every frame.
        var tick = _session.Simulation.CurrentTick;
        if (tick != _shownTick)
        {
            _shownTick = tick;
            RefreshDate();
        }
    }

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
            AddBuildButton(buildable, buildable.Id == choice.Buildable, new BuildChoice(buildable.Id, material));
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
        }
    }
}
