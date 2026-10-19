using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
using OurLife.Game.Bootstrap;
using OurLife.Game.Input;
using OurLife.Game.Session;

namespace OurLife.Game.UI;

/// <summary>
/// The colony at a glance (see "Lista de la colonia" in <c>docs/gdd/gestion.md</c>): a row per
/// colonist with its name, age and mood (<c>docs/gdd/vida-y-generaciones.md</c>), what it is doing
/// and a button per work type with its priority. A click
/// moves it on (1 → 2 → 3 → 4 → No → 1), a right click back; a click on a name
/// selects the colonist. Below, the stock targets of the village (<c>docs/gdd/libre-albedrio.md</c>),
/// each with what the colony has and buttons to lower and raise it. The rows are built again when
/// colonists come or go, or the language changes, and their texts refresh a few times per second
/// while the panel is open.
/// </summary>
public partial class ColonyPanel : PanelContainer
{
    private const double RefreshSeconds = 0.25;

    private readonly List<EntityId> _colonists = [];
    private readonly List<(EntityId Colonist, Label Age, Label Mood, Label Activity, List<(string WorkType, Button Button)> Priorities)> _rows = [];
    private GameServices _services = null!;
    private GameSession _session = null!;
    private GameInput _input = null!;
    private GridContainer _grid = null!;
    private VBoxContainer _targets = null!;
    private readonly List<(string Category, Label Label)> _targetRows = [];
    private ColonyRules _rules = null!;

    // Priorities asked in the current tick, before their orders apply.
    private readonly Dictionary<(EntityId Colonist, string WorkType), int> _pending = [];
    private readonly Dictionary<string, int> _pendingTargets = new(StringComparer.Ordinal);
    private long _pendingTick = -1;
    private double _sinceRefresh;

    public static ColonyPanel Create(GameServices services, GameSession session, GameInput input)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(input);
        var panel = new ColonyPanel
        {
            Name = nameof(ColonyPanel),
            _services = services,
            _session = session,
            _input = input,
            _rules = ColonyRules.From(session.Simulation.Content),
            Visible = false,
        };
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        panel.AddChild(column);
        column.AddChild(new Label { Text = "ui.colony.title" });
        panel._grid = new GridContainer();
        panel._grid.AddThemeConstantOverride("h_separation", 6);
        panel._grid.AddThemeConstantOverride("v_separation", 4);
        column.AddChild(panel._grid);
        column.AddChild(new Label { Text = "ui.colony.help", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(480, 0) });
        panel._targets = new VBoxContainer();
        panel._targets.AddThemeConstantOverride("separation", 4);
        column.AddChild(panel._targets);
        panel.BuildTargets();
        return panel;
    }

    /// <summary>The lines of the stock targets as the player reads them, for the smoke.</summary>
    public IEnumerable<string> TargetTexts => _targetRows.Select(row => row.Label.Text);

    /// <summary>Lowers or raises the target of a category by one step, as its buttons do.</summary>
    public void StepTarget(string category, bool up)
    {
        if (FreeWillRules.ConfigOf(_session.Simulation.Content) is not { } config)
        {
            return;
        }

        var target = Math.Clamp(TargetOf(category, FreeWillRules.Targets(_session.Simulation)) + (up ? config.TargetStep : -config.TargetStep), 0, config.TargetMax);
        _session.Enqueue(new SetStockTargetCommand(category, target));
        TrackPending();
        _pendingTargets[category] = target;
        RefreshRows();
    }

    public override void _EnterTree() => _services.Localizer.LocaleChanged += OnLocaleChanged;

    public override void _ExitTree() => _services.Localizer.LocaleChanged -= OnLocaleChanged;

    public void Toggle()
    {
        Visible = !Visible;
        if (Visible)
        {
            Rebuild();
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _sinceRefresh += delta;
        if (_sinceRefresh < RefreshSeconds)
        {
            return;
        }

        _sinceRefresh = 0;
        _session.Simulation.World.Query<Colonist>(_colonists);
        if (!_colonists.SequenceEqual(_rows.Select(row => row.Colonist)))
        {
            Rebuild();
        }
        else
        {
            RefreshRows();
        }
    }

    private void OnLocaleChanged(string locale)
    {
        if (Visible)
        {
            Rebuild();
        }
    }

    private IReadOnlyList<WorkTypeDef> WorkTypes => _rules.WorkTypes;

    private void Rebuild()
    {
        _sinceRefresh = 0;
        foreach (var child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);
            child.QueueFree();
        }

        _rows.Clear();
        var text = _services.Text;
        var workTypes = WorkTypes;
        _grid.Columns = 4 + workTypes.Count;
        _grid.AddChild(new Label { Text = "ui.colony.name" });
        _grid.AddChild(new Label { Text = "ui.colony.age", HorizontalAlignment = HorizontalAlignment.Center });
        _grid.AddChild(new Label { Text = "ui.colony.mood", HorizontalAlignment = HorizontalAlignment.Center });
        _grid.AddChild(new Label { Text = "ui.colony.activity", CustomMinimumSize = new Vector2(200, 0) });
        foreach (var type in workTypes)
        {
            _grid.AddChild(new Label
            {
                Text = text.Capitalized(text.Format(type.NameKey!, ("count", 1))),
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        var world = _session.Simulation.World;
        world.Query<Colonist>(_colonists);
        foreach (var colonist in _colonists)
        {
            var name = new Button
            {
                Text = world.Get<Colonist>(colonist).Name,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                Flat = true,
                FocusMode = FocusModeEnum.None,
                Alignment = HorizontalAlignment.Left,
            };
            var selected = colonist;
            name.Pressed += () => _input.SelectColonist(selected);
            _grid.AddChild(name);

            var age = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, HorizontalAlignment = HorizontalAlignment.Center };
            _grid.AddChild(age);
            var mood = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, HorizontalAlignment = HorizontalAlignment.Center };
            _grid.AddChild(mood);

            var activity = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled };
            _grid.AddChild(activity);

            var priorities = new List<(string WorkType, Button Button)>();
            foreach (var type in workTypes)
            {
                var button = new Button { AutoTranslateMode = AutoTranslateModeEnum.Disabled, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(56, 0) };
                var id = type.Id;
                button.Pressed += () => Step(selected, id, forward: true);
                button.GuiInput += input =>
                {
                    if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
                    {
                        Step(selected, id, forward: false);
                    }
                };
                _grid.AddChild(button);
                priorities.Add((id, button));
            }

            _rows.Add((colonist, age, mood, activity, priorities));
        }

        RefreshRows();
    }

    /// <summary>
    /// A line per stock target: what the colony has of the target, and buttons to lower and raise
    /// it. Built once: the categories with targets do not change over a game, and the texts refresh
    /// with the rows.
    /// </summary>
    private void BuildTargets()
    {
        var targets = FreeWillRules.Targets(_session.Simulation);
        if (targets.Count == 0)
        {
            return;
        }

        _targets.AddChild(new Label { Text = "ui.colony.targets" });
        foreach (var target in targets)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            _targets.AddChild(row);
            var category = target.Category;
            foreach (var (key, up) in new[] { ("ui.colony.target_less", false), ("ui.colony.target_more", true) })
            {
                var button = new Button { Text = key, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(32, 0) };
                button.Pressed += () => StepTarget(category, up);
                row.AddChild(button);
            }

            var label = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, VerticalAlignment = VerticalAlignment.Center };
            row.AddChild(label);
            _targetRows.Add((category, label));
        }

        _targets.AddChild(new Label { Text = "ui.colony.targets_help", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(480, 0) });
    }

    /// <summary>The activity and the priorities of every row, as they are now.</summary>
    private void RefreshRows()
    {
        var world = _session.Simulation.World;
        var text = _services.Text;
        foreach (var (colonist, age, mood, activity, priorities) in _rows)
        {
            if (!world.IsAlive(colonist))
            {
                continue;
            }

            // Numbers, the same in every language.
            age.Text = LifeRules.AgeYears(_session.Simulation, colonist)?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            mood.Text = MoodRules.Value(_session.Simulation, colonist).ToString(System.Globalization.CultureInfo.InvariantCulture);
            activity.Text = text.Activity(world, colonist);
            foreach (var (workType, button) in priorities)
            {
                var priority = PriorityOf(colonist, workType);
                button.Text = priority == WorkPriorityLevels.Never
                    ? text.Format("ui.colony.priority_off")
                    : text.Format("ui.colony.priority", ("priority", priority));
            }
        }

        if (_targetRows.Count == 0)
        {
            return;
        }

        // Counted only while the panel is open, a few times per second.
        var stock = ColonyStock.ByCategory(_session.Simulation);
        var targets = FreeWillRules.Targets(_session.Simulation);
        foreach (var (category, label) in _targetRows)
        {
            var line = stock.First(entry => entry.Category.Id == category);
            label.Text = text.Format(
                "ui.colony.target",
                ("category", text.CategoryName(line.Category)),
                ("count", line.Count),
                ("target", TargetOf(category, targets)));
        }
    }

    /// <summary>
    /// Moves a priority one step along the ring 1, 2, 3, 4, No, forwards or back, and gives the
    /// order; paused, it applies at once.
    /// </summary>
    private void Step(EntityId colonist, string workType, bool forward)
    {
        var world = _session.Simulation.World;
        if (!world.IsAlive(colonist))
        {
            return;
        }

        var current = PriorityOf(colonist, workType);

        // The ring in order: 1, 2, 3, 4, then 0 (No).
        const int Steps = WorkPriorityLevels.Lowest + 1;
        var position = current == WorkPriorityLevels.Never ? WorkPriorityLevels.Lowest : current - 1;
        position = (position + (forward ? 1 : Steps - 1)) % Steps;
        var next = position == WorkPriorityLevels.Lowest ? WorkPriorityLevels.Never : position + 1;
        _session.Enqueue(new SetWorkPriorityCommand(colonist, workType, next));
        TrackPending();
        _pending[(colonist, workType)] = next;
        RefreshRows();
    }

    /// <summary>
    /// With time running an order applies on the next tick: until then the panel shows, and steps
    /// from, what was asked, so quick clicks add up. Forgets what was asked in an earlier tick.
    /// </summary>
    private void TrackPending()
    {
        if (_pendingTick != _session.Simulation.CurrentTick)
        {
            _pending.Clear();
            _pendingTargets.Clear();
            _pendingTick = _session.Simulation.CurrentTick;
        }
    }

    /// <summary>A colonist's priority as the player last set it: an order not yet applied, or else the world's.</summary>
    private int PriorityOf(EntityId colonist, string workType)
    {
        TrackPending();
        return _pending.TryGetValue((colonist, workType), out var pending)
            ? pending
            : WorkPriorityRules.Of(_session.Simulation.World, colonist, workType, _rules.Ai.DefaultWorkPriority);
    }

    /// <summary>A stock target as the player last set it: an order not yet applied, or else the game's.</summary>
    private int TargetOf(string category, IReadOnlyList<StockTarget> targets)
    {
        TrackPending();
        return _pendingTargets.TryGetValue(category, out var pending)
            ? pending
            : targets.First(target => target.Category == category).Count;
    }
}
