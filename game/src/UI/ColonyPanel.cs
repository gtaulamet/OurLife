using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Game.Bootstrap;
using OurLife.Game.Input;
using OurLife.Game.Session;

namespace OurLife.Game.UI;

/// <summary>
/// The colony at a glance (see "Lista de la colonia" in <c>docs/gdd/gestion.md</c>): a row per
/// colonist with its name, what it is doing and a button per work type with its priority. A click
/// moves it on (1 → 2 → 3 → 4 → No → 1), a right click back; a click on a name
/// selects the colonist. The rows are built again when colonists come or go, or the language
/// changes, and their texts refresh a few times per second while the panel is open.
/// </summary>
public partial class ColonyPanel : PanelContainer
{
    private const double RefreshSeconds = 0.25;

    private readonly List<EntityId> _colonists = [];
    private readonly List<(EntityId Colonist, Label Activity, List<(string WorkType, Button Button)> Priorities)> _rows = [];
    private GameServices _services = null!;
    private GameSession _session = null!;
    private GameInput _input = null!;
    private GridContainer _grid = null!;
    private ColonyRules _rules = null!;
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
        return panel;
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
        _grid.Columns = 2 + workTypes.Count;
        _grid.AddChild(new Label { Text = "ui.colony.name" });
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

            _rows.Add((colonist, activity, priorities));
        }

        RefreshRows();
    }

    /// <summary>The activity and the priorities of every row, as they are now.</summary>
    private void RefreshRows()
    {
        var world = _session.Simulation.World;
        var text = _services.Text;
        var defaultPriority = _rules.Ai.DefaultWorkPriority;
        foreach (var (colonist, activity, priorities) in _rows)
        {
            if (!world.IsAlive(colonist))
            {
                continue;
            }

            activity.Text = world.TryGet<Job>(colonist, out var job) ? text.Format(JobKinds.ReportKey(job.Kind)) : text.Format("ui.inspector.idle");
            foreach (var (workType, button) in priorities)
            {
                var priority = WorkPriorityRules.Of(world, colonist, workType, defaultPriority);
                button.Text = priority == WorkPriorityLevels.Never
                    ? text.Format("ui.colony.priority_off")
                    : text.Format("ui.colony.priority", ("priority", priority));
            }
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

        var defaultPriority = _rules.Ai.DefaultWorkPriority;
        var current = WorkPriorityRules.Of(world, colonist, workType, defaultPriority);

        // The ring in order: 1, 2, 3, 4, then 0 (No).
        const int Steps = WorkPriorityLevels.Lowest + 1;
        var position = current == WorkPriorityLevels.Never ? WorkPriorityLevels.Lowest : current - 1;
        position = (position + (forward ? 1 : Steps - 1)) % Steps;
        var next = position == WorkPriorityLevels.Lowest ? WorkPriorityLevels.Never : position + 1;
        _session.Enqueue(new SetWorkPriorityCommand(colonist, workType, next));
        RefreshRows();
    }
}
