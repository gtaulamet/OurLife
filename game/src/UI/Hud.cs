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
/// <c>docs/gdd/interfaz-ip1.md</c>): date and clock, the colony's stock (<c>docs/gdd/indicadores.md</c>),
/// time buttons and level on top; the inspector on the right; the event log with, if the player
/// wants them, the frames per second beside it, the order hint, the build bar (<c>docs/gdd/construccion.md</c>)
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

    // The frames per second are counted over this long, and shown again as often.
    private const double FpsSeconds = 0.5;

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
    private readonly List<(ItemCategoryDef Category, Label Label)> _stock = [];

    // The categories the bar always shows, even with none: the colony's first needs.
    private static readonly string[] AlwaysShownStock = ["food", "wood", "stone"];
    private Label _fps = null!;
    private Control _fpsPanel = null!;
    private double _sinceFps;
    private int _framesSinceFps;
    private bool _stockChanged;
    private bool _stockWidthsChanged = true;
    private Label _hint = null!;
    private HBoxContainer _buildRow = null!;
    private HBoxContainer _farmRow = null!;
    private Button _farmButton = null!;
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

    /// <summary>The production orders, opened with the key or its button.</summary>
    public ProductionPanel Production { get; private set; } = null!;

    /// <summary>The tree of knowledge and the research queue, opened with the key or its button.</summary>
    public KnowledgePanel Knowledge { get; private set; } = null!;

    private Label _research = null!;
    private HBoxContainer _researchRow = null!;
    private double _sinceResearch;
    private bool _buildBarStale;

    /// <summary>The sheet of a colonist, over the game (see <c>docs/gdd/vida-y-generaciones.md</c>).</summary>
    public ColonistSheet Sheet { get; private set; } = null!;

    // The colonist selected now, if a colonist is: the sheet opens on it and follows it.
    private EntityId _selectedColonist;

    /// <summary>Whether the frames per second show, for the smoke.</summary>
    public bool ShowsFps => _fpsPanel.Visible && _fps.Text.Length > 0;

    /// <summary>The texts of the buttons of the farm tool's bar of crops (empty with the tool off), for the smoke.</summary>
    public IEnumerable<string> FarmBarTexts => _farmRow.GetChildren().OfType<Button>().Select(button => button.Text);

    /// <summary>Whether the button of the farm tool can be pressed, for the smoke.</summary>
    public bool FarmButtonEnabled => !_farmButton.Disabled;

    /// <summary>Presses the button of the crop at a position of the bar, as a click would, for the smoke.</summary>
    public void PressFarmBarButton(int index) =>
        _farmRow.GetChildren().OfType<Button>().ElementAt(index).EmitSignal(BaseButton.SignalName.Pressed);

    /// <summary>The lines of the stock that are on the bar now, for the smoke.</summary>
    public IEnumerable<string> ShownStockTexts => _stock.Where(entry => entry.Label.Visible).Select(entry => entry.Label.Text);

    /// <summary>The lines of the stock as the player reads them, for the smoke.</summary>
    public IEnumerable<string> StockTexts => _stock.Select(entry => entry.Label.Text);

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
        hud._research = new Label { Name = "Research", AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled };
        hud._researchRow = new HBoxContainer { Name = "ResearchRow", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        hud._researchRow.AddThemeConstantOverride("separation", 4);
        hud._researchRow.AddChild(KnowledgeIcons.Rect(KnowledgeIcons.Research()));
        hud._researchRow.AddChild(hud._research);
        left.AddChild(hud._researchRow);
        left.AddChild(hud._alerts);
        hud.Colony = ColonyPanel.Create(services, session, input);
        left.AddChild(hud.Colony);
        hud.Colony.VisibilityChanged += hud.ShowAlertsUnlessColony;
        hud.Production = ProductionPanel.Create(services, session);
        left.AddChild(hud.Production);
        hud.Knowledge = KnowledgePanel.Create(services, session);
        left.AddChild(hud.Knowledge);

        hud.Inspector = InspectorPanel.Create(services, session.Simulation, session.Enqueue);
        hud.Inspector.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        hud.Inspector.Position = new Vector2(-Margin, PanelsTop);
        hud.Inspector.GrowHorizontal = Control.GrowDirection.Begin;
        root.AddChild(hud.Inspector);

        hud.Sheet = ColonistSheet.Create(services, session);
        root.AddChild(hud.Sheet);

        input.SelectionChanged += hud.Inspector.Inspect;
        input.SelectionChanged += hud.FollowSelection;
        input.SheetRequested += hud.OnSheetKey;
        input.CloseFirst = () =>
        {
            if (!hud.Sheet.Visible)
            {
                return false;
            }

            hud.Sheet.Close();
            return true;
        };
        hud.Inspector.SheetRequested += hud.ToggleSheet;
        input.OrderChanged += hud.OnOrderChanged;
        input.BuildChanged += hud.OnBuildChanged;
        input.FarmChanged += hud.OnFarmChanged;
        input.ColonyPanelRequested += hud.Colony.Toggle;
        input.ProductionPanelRequested += hud.Production.Toggle;
        input.KnowledgePanelRequested += hud.Knowledge.Toggle;
        session.ClockChanged += hud.RefreshTimeButtons;
        view.ViewLevelChanged += hud.OnViewLevelChanged;
        hud.RefreshAll();
        return hud;
    }

    /// <summary>Opens the sheet of a colonist, or closes it if it shows that one; nothing without a colonist.</summary>
    public void ToggleSheet(EntityId colonist)
    {
        if (!colonist.IsNone)
        {
            Sheet.Toggle(colonist);
        }
    }

    /// <summary>F closes the sheet if it is open, whatever is selected, and else opens the selected colonist's.</summary>
    private void OnSheetKey()
    {
        if (Sheet.Visible)
        {
            Sheet.Close();
        }
        else
        {
            ToggleSheet(_selectedColonist);
        }
    }

    private void FollowSelection(Selection? selection)
    {
        _selectedColonist = selection is { IsColonist: true } chosen ? chosen.Colonist : EntityId.None;
        if (Sheet.Visible && !_selectedColonist.IsNone)
        {
            Sheet.ShowColonist(_selectedColonist);
        }
    }

    public override void _EnterTree()
    {
        _services.Localizer.LocaleChanged += OnLocaleChanged;
        _services.Settings.ShowFpsChanged += RefreshFps;
    }

    public override void _ExitTree()
    {
        _services.Localizer.LocaleChanged -= OnLocaleChanged;
        _services.Settings.ShowFpsChanged -= RefreshFps;
    }

    /// <summary>
    /// Follows the events of a frame: new messages for the log, and news for the inspector. Items
    /// only appear, change or go away with an event about an entity: then the stock is counted again.
    /// </summary>
    public void Apply(IReadOnlyList<ISimEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        Log.Apply(events, _session.Simulation.Calendar, _session.Simulation.World);
        Inspector.NoteChange();
        _alertsChanged = true;
        foreach (var simEvent in events)
        {
            if (simEvent is KnowledgeLearnedEvent)
            {
                _buildBarStale = true;
            }

            if (simEvent is EntitySpawnedEvent or EntityChangedEvent or EntityRemovedEvent)
            {
                _stockChanged = true;
            }
        }
    }

    public override void _Process(double delta)
    {
        _sinceFps += delta;
        _framesSinceFps++;
        if (_sinceFps >= FpsSeconds)
        {
            RefreshFps();
        }

        if (_stockChanged)
        {
            RefreshStock();
        }

        _sinceResearch += delta;
        if (_sinceResearch >= 0.5 || _buildBarStale)
        {
            _sinceResearch = 0;
            RefreshResearch();
            if (_buildBarStale)
            {
                _buildBarStale = false;
                RefreshBuildBar();
                RefreshFarmBar();
            }
        }

        if (_stockWidthsChanged)
        {
            FixStockWidths();
        }

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

    /// <summary>
    /// The top of what is drawn at the bottom under the inspector: the full-width rows (the hint,
    /// the build bar and the orders) and the frames per second on the right of the log's row. The
    /// log itself sits on the left.
    /// </summary>
    private float BottomBarsTop() =>
        new Control[] { _hint, _buildRow, _farmRow, _ordersBar, _fpsPanel }.Where(row => row.Visible).Min(row => row.GetGlobalRect().Position.Y);

    private Control BuildTopBar(string colonyName)
    {
        var panel = new PanelContainer { Name = "TopBar" };
        panel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        panel.OffsetLeft = Margin;
        panel.OffsetRight = -Margin;
        panel.OffsetTop = Margin;

        // Everything fits in one line down to a canvas of 960 × 540 (docs/gdd/tamano-de-la-interfaz.md).
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", Gap + (Gap / 4));
        panel.AddChild(row);

        // The village's name is a proper name: it is never translated.
        row.AddChild(new Label { Text = colonyName, AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled });
        // The date gives way first: on a narrow canvas it is cut rather than pushing the buttons off the screen.
        _date = new Label { AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true };
        row.AddChild(_date);

        // What the colony has, by category of items, in the order of the content.
        foreach (var line in ColonyStock.ByCategory(_session.Simulation))
        {
            var label = new Label { AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled };
            row.AddChild(label);
            _stock.Add((line.Category, label));
        }

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
        var production = new Button { Text = "ui.production.button", FocusMode = Control.FocusModeEnum.None };
        production.Pressed += () => Production.Toggle();
        row.AddChild(production);
        var knowledge = new Button { Text = "ui.knowledge.button", FocusMode = Control.FocusModeEnum.None };
        knowledge.Pressed += () => Knowledge.Toggle();
        row.AddChild(knowledge);

        _level = new Label { AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled, CustomMinimumSize = new Vector2(90, 0), HorizontalAlignment = HorizontalAlignment.Right };
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

        // The log on the left and, on the right of its row, the frames per second.
        var logRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddChild(logRow);
        Log = EventLog.Create(_services);
        Log.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin | Control.SizeFlags.Expand;
        logRow.AddChild(Log);
        // On a panel, like the log, so it reads over any ground.
        _fpsPanel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkEnd };
        logRow.AddChild(_fpsPanel);
        _fps = new Label { AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled, MouseFilter = Control.MouseFilterEnum.Ignore };
        _fpsPanel.AddChild(_fps);

        _hint = new Label { AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddChild(_hint);

        // What to build and of what, shown while the build tool is on.
        _buildRow = new HBoxContainer { Name = "BuildBar", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _buildRow.AddThemeConstantOverride("separation", Gap / 2);
        column.AddChild(_buildRow);

        // Which crop the farm tool sows, shown while the tool is on.
        _farmRow = new HBoxContainer { Name = "FarmBar", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _farmRow.AddThemeConstantOverride("separation", Gap / 2);
        column.AddChild(_farmRow);

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

            // The farm tool sits beside building, before Cancel.
            if (kind == DesignationKind.Cancel)
            {
                _farmButton = new Button { Text = "ui.orders.farm", ToggleMode = true, FocusMode = Control.FocusModeEnum.None };
                _farmButton.Pressed += _input.ToggleFarm;
                orders.AddChild(_farmButton);
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
        _stockWidthsChanged = true;
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

    /// <summary>Draws the farm tool's button and bar again, after the knowledge changed without an event; for the smoke.</summary>
    public void RefreshFarmTool() => RefreshFarmBar();

    private void OnFarmChanged(string? crop)
    {
        RefreshFarmBar();
        RefreshHint();
    }

    /// <summary>
    /// The farm tool's button and its bar of crops: a button per crop the colony knows, the chosen
    /// one pressed. Without a known crop the tool cannot be turned on, and the button says which
    /// knowledge would give the nearest one.
    /// </summary>
    private void RefreshFarmBar()
    {
        var text = _services.Text;
        var simulation = _session.Simulation;
        var crops = _input.UnlockedCrops();
        var chosen = _input.FarmCrop;
        _farmButton.SetPressedNoSignal(chosen is not null);
        _farmButton.Disabled = crops.Count == 0 && chosen is null;
        _farmButton.TooltipAutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
        _farmButton.TooltipText = string.Empty;
        if (_farmButton.Disabled)
        {
            var nearest = simulation.Content.All<CropDef>()
                .Select(crop => simulation.Unlocks.Missing(crop, simulation.Progress))
                .Where(missing => missing.Count > 0)
                .OrderBy(missing => missing.Count)
                .FirstOrDefault();
            if (nearest is not null)
            {
                _farmButton.TooltipText = text.Format("ui.orders.locked_hint", ("knowledge", string.Join(", ", nearest.Select(id => text.Format(simulation.Content.Get<KnowledgeDef>(id).NameKey!)))));
            }
        }

        _farmRow.Visible = chosen is not null;
        foreach (var child in _farmRow.GetChildren())
        {
            _farmRow.RemoveChild(child);
            child.QueueFree();
        }

        if (chosen is null)
        {
            return;
        }

        _farmRow.AddChild(new Label { Text = "ui.farm.choose_crop", VerticalAlignment = VerticalAlignment.Center });
        foreach (var crop in crops)
        {
            var id = crop.Id;
            var button = new Button
            {
                Text = text.Capitalized(text.Format(crop.NameKey!, ("count", 1))),
                AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
                ToggleMode = true,
                ButtonPressed = id == chosen,
                FocusMode = Control.FocusModeEnum.None,
            };

            // Pressing the chosen one again keeps it chosen.
            button.Pressed += () =>
            {
                _input.FarmCrop = id;
                RefreshFarmBar();
            };
            _farmRow.AddChild(button);
        }
    }

    /// <summary>The line of what is being researched, only with something in the queue.</summary>
    public void RefreshResearch()
    {
        var simulation = _session.Simulation;
        var current = ResearchRules.Current(simulation);
        _researchRow.Visible = current is not null;
        if (current is null)
        {
            return;
        }

        var text = _services.Text;
        _research.Text = text.Format(
            "ui.hud.research",
            ("knowledge", text.Format(simulation.Content.Get<KnowledgeDef>(current).NameKey!)),
            ("percent", ResearchRules.Progress(simulation, current) / 100));
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

        // What the village lacks the knowledge for shows dimmed, and says which in its hint.
        var simulation = _session.Simulation;
        if (!simulation.Unlocks.IsUnlocked(def, simulation.Progress))
        {
            var missing = simulation.Unlocks.Missing(def, simulation.Progress);
            button.Disabled = true;
            button.TooltipAutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
            button.TooltipText = missing.Count == 0
                ? string.Empty
                : text.Format("ui.orders.locked_hint", ("knowledge", string.Join(", ", missing.Select(id => text.Format(simulation.Content.Get<KnowledgeDef>(id).NameKey!)))));
        }

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
        RefreshStock();
        RefreshFps();
        RefreshDate();
        RefreshLevel();
        RefreshTimeButtons();
        RefreshHint();
        RefreshFarmBar();
    }

    private void RefreshDate()
    {
        var date = _session.Simulation.CurrentDate;
        _date.Text = _services.Text.Format("ui.hud.date", ("date", _services.Text.Date(date)), ("clock", _services.Text.Clock(date)));
    }

    /// <summary>What the colony has, by category of items, in the order the labels were built in.</summary>
    private void RefreshStock()
    {
        _stockChanged = false;
        var stock = ColonyStock.ByCategory(_session.Simulation);
        for (var i = 0; i < _stock.Count; i++)
        {
            _stock[i].Label.Text = StockText(_stock[i].Category, stock[i].Count);

            // The categories that came with the trades only show once the colony has some, so the bar is not full of zeros.
            _stock[i].Label.Visible = stock[i].Count > 0 || AlwaysShownStock.Contains(_stock[i].Category.Id);
        }
    }

    private string StockText(ItemCategoryDef category, int count)
    {
        var text = _services.Text;
        return text.Format("ui.hud.stock", ("category", text.CategoryName(category)), ("count", count));
    }

    /// <summary>
    /// Each line of the stock at least as wide as with three digits, so the top bar does not move
    /// while a count goes up to 999, and still fits on a canvas of 960 × 540. Measured with the
    /// label's font, once it has one in the tree.
    /// </summary>
    private void FixStockWidths()
    {
        if (_stock.Count == 0 || !_stock[0].Label.IsInsideTree())
        {
            return;
        }

        _stockWidthsChanged = false;
        foreach (var (category, label) in _stock)
        {
            label.CustomMinimumSize = new Vector2(Mathf.Ceil(UiTheme.TextWidth(label, StockText(category, 888))), 0);
        }
    }

    /// <summary>
    /// The frames per second, if the player wants them: the frames drawn since the last time over
    /// the time they took, which follows a change sooner than the engine's count of each second.
    /// </summary>
    private void RefreshFps()
    {
        _fpsPanel.Visible = _services.Settings.ShowFps;
        if (_fpsPanel.Visible && _sinceFps > 0 && _framesSinceFps > 0)
        {
            _fps.Text = _services.Text.Format("ui.hud.fps", ("fps", (int)Math.Round(_framesSinceFps / _sinceFps)));
        }
        else if (_fpsPanel.Visible && _fps.Text.Length == 0)
        {
            // Before the first count, the engine's.
            _fps.Text = _services.Text.Format("ui.hud.fps", ("fps", (int)Engine.GetFramesPerSecond()));
        }

        _sinceFps = 0;
        _framesSinceFps = 0;
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
        _hint.Visible = _input.Order is not null || _input.Build is not null || _input.FarmCrop is not null;
        if (_input.FarmCrop is not null)
        {
            _hint.Text = text.Format("ui.orders.hint", ("order", text.Format("ui.orders.name.farm")));
        }
        else if (_input.Order is { } order)
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
