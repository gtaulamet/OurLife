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
/// each with what the colony has and a field where the player writes the target. The rows are built again when
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

    private readonly List<(string Category, Label Label, LineEdit Field)> _targetRows = [];

    // The digits of a count the target lines make room for; more widen them.
    private int _countDigits = 4;
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
        var targets = new HBoxContainer();
        targets.AddThemeConstantOverride("separation", 18);
        column.AddChild(targets);
        panel.BuildTargets(targets);
        return panel;
    }

    /// <summary>The lines of the stock targets as the player reads them, for the smoke.</summary>
    public IEnumerable<string> TargetTexts => _targetRows.Select(row => row.Label.Text);

    /// <summary>
    /// Writes a target in the field of its category and leaves it, as the player does, which
    /// gives the order; a category without a field is ignored. For the smoke.
    /// </summary>
    public void WriteTarget(string category, int target)
    {
        if (FieldOf(category) is not { } field)
        {
            return;
        }

        field.GrabFocus();
        field.Text = Number(target);
        field.ReleaseFocus();
    }

    /// <summary>The target a field shows, or null for a category without one. For the smoke.</summary>
    public int? TargetShown(string category) =>
        FieldOf(category) is { } field && int.TryParse(field.Text, System.Globalization.CultureInfo.InvariantCulture, out var shown) ? shown : null;

    /// <summary>The text field of a target, or null for a category without one. For the smoke.</summary>
    public LineEdit? TargetLine(string category) => FieldOf(category);

    private LineEdit? FieldOf(string category) => _targetRows.Find(entry => entry.Category == category).Field;

    /// <summary>
    /// Keys the field takes from the rest of the interface: Esc puts back the target it had, and
    /// Tab, which would move to another control, leaves the field too; both let go of it, without
    /// the game seeing the key.
    /// </summary>
    private void OnFieldKey(string category, LineEdit field, InputEvent input)
    {
        var cancel = input.IsActionPressed("ui_cancel");
        if (!cancel && !input.IsActionPressed("ui_focus_next") && !input.IsActionPressed("ui_focus_prev"))
        {
            return;
        }

        if (cancel)
        {
            field.Text = Number(TargetOf(category, FreeWillRules.Targets(_session.Simulation)));
        }

        field.AcceptEvent();
        field.ReleaseFocus();
    }

    /// <summary>
    /// Gives the order of the target written in a field when the player leaves it: kept within 0
    /// and the highest, and none if it is empty or did not change. Until the order applies, the
    /// panel shows what was asked.
    /// </summary>
    private void ApplyWritten(string category, LineEdit field)
    {
        TrackPending();
        var current = TargetOf(category, FreeWillRules.Targets(_session.Simulation));
        var target = current;

        // The digits again: the field cleans them when its text changes, deferred, and a key and
        // Enter in the same frame get here first. Without any, as when empty, nothing changes.
        var digits = OnlyDigits(field.Text);
        if (digits.Length > 0 && FreeWillRules.ConfigOf(_session.Simulation.Content) is { } config)
        {
            // Only too big is possible: past long, it is the highest anyway. Leading zeros do not make it bigger.
            var number = digits.TrimStart('0');
            target = number.Length == 0 ? 0
                : long.TryParse(number, System.Globalization.CultureInfo.InvariantCulture, out var written)
                    ? (int)Math.Min(written, config.TargetMax)
                    : config.TargetMax;
        }

        field.Text = Number(target);
        if (target != current)
        {
            _session.Enqueue(new SetStockTargetCommand(category, target));
            _pendingTargets[category] = target;
        }
    }

    private static string Number(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

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
            RefreshTargets();
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

        FixTargetWidths();
        RefreshRows();
        RefreshTargets();
    }

    /// <summary>
    /// One row for the stock targets, so the panel grows little on a small canvas: for each, what
    /// the colony has of it and a field where the player writes the target; what they do, in a
    /// tooltip. Built once: the categories with targets do not change over a game, and the texts
    /// refresh with the rows.
    /// </summary>
    private void BuildTargets(HBoxContainer row)
    {
        var targets = FreeWillRules.Targets(_session.Simulation);
        if (targets.Count == 0)
        {
            return;
        }

        const string Help = "ui.colony.targets_help";
        row.AddChild(new Label { Text = "ui.colony.targets", TooltipText = Help, MouseFilter = MouseFilterEnum.Stop });
        foreach (var target in targets)
        {
            var group = new HBoxContainer { TooltipText = Help };
            group.AddThemeConstantOverride("separation", 6);
            row.AddChild(group);
            var category = target.Category;
            // Against the field, with the room for longer counts on its left.
            var label = new Label
            {
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            group.AddChild(label);

            // A number the player writes, applied on leaving the field: with Enter, Tab or a click
            // elsewhere. No context menu: its items are the engine's, in English.
            var field = new LineEdit
            {
                Text = Number(target.Count),
                SelectAllOnFocus = true,
                ContextMenuEnabled = false,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                TooltipText = Help,

                // The number is not translated, its help is.
                TooltipAutoTranslateMode = AutoTranslateModeEnum.Always,
            };
            field.AddThemeStyleboxOverride("normal", UiTheme.FieldBox);
            field.TextSubmitted += _ => field.ReleaseFocus();
            field.FocusExited += () => ApplyWritten(category, field);
            field.GuiInput += input => OnFieldKey(category, field, input);

            // Only digits: "10.000", "10,000" or "10 000" become 10000.
            field.TextChanged += text => KeepDigits(field, text);
            group.AddChild(field);
            _targetRows.Add((category, label, field));
        }
    }

    /// <summary>The activity and the priorities of every row, as they are now.</summary>
    private void RefreshRows()
    {
        TrackPending();
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
    }

    /// <summary>
    /// What the colony has of each target, and the target as the player last set it, unless the
    /// player is writing in its field.
    /// </summary>
    private void RefreshTargets()
    {
        if (_targetRows.Count == 0)
        {
            return;
        }

        // Counted only while the panel is open, a few times per second.
        TrackPending();
        var stock = ColonyStock.ByCategory(_session.Simulation);
        var targets = FreeWillRules.Targets(_session.Simulation);
        var widest = 0;
        foreach (var (category, label, field) in _targetRows)
        {
            var line = stock.First(entry => entry.Category.Id == category);
            label.Text = TargetText(line.Category, line.Count);
            widest = Math.Max(widest, Digits(line.Count));
            var shown = Number(TargetOf(category, targets));
            if (!field.HasFocus() && field.Text != shown)
            {
                field.Text = shown;
            }
        }

        if (widest > _countDigits)
        {
            _countDigits = widest;
            FixTargetWidths();
        }
    }

    /// <summary>
    /// Each target line as wide as with the widest count it has made room for (four digits at
    /// least), in the current language, and each field as wide as the highest target, so the row
    /// stays still while the numbers change. Measured when the panel is built again or a count
    /// needs more room.
    /// </summary>
    private void FixTargetWidths()
    {
        if (FreeWillRules.ConfigOf(_session.Simulation.Content) is not { } config)
        {
            return;
        }

        // Eights, the widest digit of most fonts.
        var count = Eights(_countDigits);
        var highest = Eights(Digits(config.TargetMax)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var (category, label, field) in _targetRows)
        {
            var text = TargetText(_session.Simulation.Content.Get<ItemCategoryDef>(category), count);
            label.CustomMinimumSize = new Vector2(Mathf.Ceil(UiTheme.TextWidth(label, text)), 0);

            // Room for the box's margins and the caret beside the number.
            field.CustomMinimumSize = new Vector2(Mathf.Ceil(UiTheme.TextWidth(field, highest)) + 20, 0);
        }
    }

    /// <summary>Leaves only the digits of what the player typed or pasted, with the caret where it was among them.</summary>
    private static void KeepDigits(LineEdit line, string text)
    {
        var digits = OnlyDigits(text);
        if (digits != text)
        {
            var caret = line.CaretColumn - (text.Length - digits.Length);
            line.Text = digits;
            line.CaretColumn = Math.Max(0, caret);
        }
    }

    private static string OnlyDigits(string text) => new(text.Where(char.IsAsciiDigit).ToArray());

    private static int Digits(int number) => number.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;

    private static int Eights(int digits) => int.Parse(new string('8', Math.Min(digits, 9)), System.Globalization.CultureInfo.InvariantCulture);

    private string TargetText(ItemCategoryDef category, int count) =>
        _services.Text.Format("ui.colony.target", ("category", _services.Text.CategoryName(category)), ("count", count));

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

        TrackPending();
        var current = PriorityOf(colonist, workType);

        // The ring in order: 1, 2, 3, 4, then 0 (No).
        const int Steps = WorkPriorityLevels.Lowest + 1;
        var position = current == WorkPriorityLevels.Never ? WorkPriorityLevels.Lowest : current - 1;
        position = (position + (forward ? 1 : Steps - 1)) % Steps;
        var next = position == WorkPriorityLevels.Lowest ? WorkPriorityLevels.Never : position + 1;
        _session.Enqueue(new SetWorkPriorityCommand(colonist, workType, next));
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
        return _pending.TryGetValue((colonist, workType), out var pending)
            ? pending
            : WorkPriorityRules.Of(_session.Simulation.World, colonist, workType, _rules.Ai.DefaultWorkPriority);
    }

    /// <summary>A stock target as the player last set it: an order not yet applied, or else the game's.</summary>
    private int TargetOf(string category, IReadOnlyList<StockTarget> targets)
    {
        return _pendingTargets.TryGetValue(category, out var pending)
            ? pending
            : targets.First(target => target.Category == category).Count;
    }
}
