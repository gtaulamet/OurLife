using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
using OurLife.Game.Bootstrap;
using OurLife.Game.Session;

namespace OurLife.Game.UI;

/// <summary>
/// The tree of knowledge and the research queue (see "Panel de conocimientos" in
/// <c>docs/gdd/conocimientos.md</c>): a row per branch and a column per tier, each knowledge a card
/// with its state and progress; a click on an available one puts it in the queue, a click on a
/// queued one takes it out. Under the tree, the queue, with buttons to reorder it, and a warning
/// when there is no scriptorium. It is built again only when something it shows changes.
/// </summary>
public partial class KnowledgePanel : PanelContainer
{
    private const double RefreshSeconds = 0.25;

    private GameServices _services = null!;
    private GameSession _session = null!;
    private VBoxContainer _column = null!;
    private string _signature = string.Empty;
    private double _sinceRefresh;

    public static KnowledgePanel Create(GameServices services, GameSession session)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(session);
        var panel = new KnowledgePanel { Name = nameof(KnowledgePanel), _services = services, _session = session, Visible = false };
        panel._column = new VBoxContainer();
        panel._column.AddThemeConstantOverride("separation", 6);
        panel.AddChild(panel._column);
        return panel;
    }

    /// <summary>The cards as the player reads them, one line each, for the smoke.</summary>
    public IEnumerable<string> CardTexts => _column.GetChildren().OfType<GridContainer>().SelectMany(grid => grid.GetChildren().OfType<VBoxContainer>().SelectMany(cell => cell.GetChildren().OfType<Button>())).Select(button => button.Text);

    public override void _EnterTree() => _services.Localizer.LocaleChanged += OnLocaleChanged;

    public override void _ExitTree() => _services.Localizer.LocaleChanged -= OnLocaleChanged;

    public void Toggle()
    {
        Visible = !Visible;
        if (Visible)
        {
            _signature = string.Empty;
            Refresh();
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _sinceRefresh += delta;
        if (_sinceRefresh >= RefreshSeconds)
        {
            Refresh();
        }
    }

    private void OnLocaleChanged(string locale)
    {
        _signature = string.Empty;
        if (Visible)
        {
            Refresh();
        }
    }

    /// <summary>Reads the knowledge now, without waiting for the next refresh. For the smoke.</summary>
    public void Refresh()
    {
        _sinceRefresh = 0;
        var simulation = _session.Simulation;
        var defs = simulation.Content.All<KnowledgeDef>().OrderBy(def => def.Order).ThenBy(def => def.Id, StringComparer.Ordinal).ToList();
        var desks = Desks(simulation);
        var signature = string.Join(
            ';',
            defs.Select(def => $"{def.Id}:{ResearchRules.State(simulation, def.Id)}:{ResearchRules.Progress(simulation, def.Id) / 100}")
                .Concat(simulation.Progress.ResearchQueue)
                .Append(desks.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (signature == _signature)
        {
            return;
        }

        _signature = signature;
        foreach (var child in _column.GetChildren())
        {
            _column.RemoveChild(child);
            child.QueueFree();
        }

        var text = _services.Text;
        _column.AddChild(new Label { Text = "ui.knowledge.title" });
        var branches = defs.Select(def => def.Branch).Distinct(StringComparer.Ordinal).ToList();
        var tiers = defs.Count == 0 ? 1 : defs.Max(def => def.Tier);
        var grid = new GridContainer { Columns = tiers + 1 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        _column.AddChild(grid);
        foreach (var branch in branches)
        {
            var head = new HBoxContainer { CustomMinimumSize = new Vector2(120, 0) };
            head.AddThemeConstantOverride("separation", 4);
            head.AddChild(KnowledgeIcons.Rect(KnowledgeIcons.Branch(branch), 2));
            head.AddChild(new Label { Text = BranchKey(branch), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            grid.AddChild(head);
            for (var tier = 1; tier <= tiers; tier++)
            {
                var here = defs.Where(def => def.Branch == branch && def.Tier == tier).ToList();
                var cell = new VBoxContainer { CustomMinimumSize = new Vector2(150, 0) };
                foreach (var def in here)
                {
                    cell.AddChild(Card(simulation, def));
                }

                grid.AddChild(cell);
            }
        }

        _column.AddChild(new Label { Text = "ui.knowledge.queue" });
        var queue = simulation.Progress.ResearchQueue;
        if (queue.Count == 0)
        {
            _column.AddChild(new Label { Text = "ui.knowledge.queue_empty" });
        }

        for (var index = 0; index < queue.Count; index++)
        {
            _column.AddChild(QueueRow(simulation, queue, index));
        }

        if (desks == 0)
        {
            _column.AddChild(new Label { Text = "ui.knowledge.no_scriptorium", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 0) });
        }
    }

    /// <summary>The key of the name of a branch of the tree.</summary>
    private static string BranchKey(string branch) => branch switch
    {
        "crafts" => "ui.knowledge.branch.crafts",
        "field" => "ui.knowledge.branch.field",
        "fire_metal" => "ui.knowledge.branch.fire_metal",
        _ => branch,
    };

    private Button Card(Simulation simulation, KnowledgeDef def)
    {
        var text = _services.Text;
        var state = ResearchRules.State(simulation, def.Id);
        var name = text.Capitalized(text.Format(def.NameKey!));
        var line = state switch
        {
            KnowledgeState.Learned => text.Format("ui.knowledge.state.learned"),
            KnowledgeState.Blocked => text.Format("ui.knowledge.state.blocked"),
            KnowledgeState.Queued => text.Format("ui.knowledge.state.queued", ("position", simulation.Progress.ResearchQueue.ToList().IndexOf(def.Id) + 1)),
            _ => text.Format("ui.knowledge.state.available"),
        };
        var progress = ResearchRules.Progress(simulation, def.Id) / 100;
        var showsProgress = state is KnowledgeState.Available or KnowledgeState.Queued;
        var button = new Button
        {
            Text = text.Format(showsProgress ? "ui.knowledge.card_progress" : "ui.knowledge.card", ("name", name), ("state", line), ("percent", progress)),
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            FocusMode = FocusModeEnum.None,
            Disabled = state is KnowledgeState.Learned or KnowledgeState.Blocked,
            Alignment = HorizontalAlignment.Left,
            Icon = KnowledgeIcons.State(state),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            TooltipText = Tooltip(simulation, def),
            TooltipAutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        var id = def.Id;
        button.Pressed += () => ToggleQueued(id);
        return button;
    }

    private string Tooltip(Simulation simulation, KnowledgeDef def)
    {
        var text = _services.Text;
        var lines = new List<string> { text.Format(def.DescKey!) };
        if (def.Cost > 0)
        {
            lines.Add(text.Format("ui.knowledge.cost", ("points", def.Cost)));
        }

        var missing = simulation.Unlocks.Missing(def, simulation.Progress);
        if (missing.Count > 0 && !simulation.Progress.Knowledge.Contains(def.Id))
        {
            lines.Add(text.Format("ui.knowledge.requires", ("knowledge", string.Join(", ", missing.Select(id => text.Format(simulation.Content.Get<KnowledgeDef>(id).NameKey!))))));
        }

        var unlocks = ResearchRules.Unlocks(simulation, def.Id);
        if (unlocks.Count > 0)
        {
            lines.Add(text.Format("ui.knowledge.unlocks", ("items", string.Join(", ", unlocks.Select(unlock => text.Format(unlock.NameKey!, ("count", 1)))))));
        }

        if (def.Practice is { } practice)
        {
            lines.Add(text.Format("ui.knowledge.practice", ("work_type", text.Format(simulation.Content.Get<WorkTypeDef>(practice.WorkType).NameKey!))));
        }

        return string.Join("\n", lines);
    }

    private Control QueueRow(Simulation simulation, IReadOnlyList<string> queue, int index)
    {
        var text = _services.Text;
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        var id = queue[index];
        var name = text.Capitalized(text.Format(simulation.Content.Get<KnowledgeDef>(id).NameKey!));
        row.AddChild(new Label
        {
            Text = text.Format("ui.knowledge.queue_row", ("position", index + 1), ("name", name), ("percent", ResearchRules.Progress(simulation, id) / 100)),
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            CustomMinimumSize = new Vector2(240, 0),
        });
        AddButton(row, "ui.production.up", () => Move(queue, index, -1), index > 0);
        AddButton(row, "ui.production.down", () => Move(queue, index, 1), index < queue.Count - 1);
        AddButton(row, "ui.workshop.remove", () => ToggleQueued(id));
        return row;
    }

    private static void AddButton(HBoxContainer row, string text, Action pressed, bool enabled = true)
    {
        var button = new Button { Text = text, FocusMode = FocusModeEnum.None, Disabled = !enabled };
        button.Pressed += pressed;
        row.AddChild(button);
    }

    private void Move(IReadOnlyList<string> queue, int index, int by)
    {
        var list = queue.ToList();
        (list[index], list[index + by]) = (list[index + by], list[index]);
        _session.Enqueue(new SetResearchQueueCommand(list));
    }

    private void ToggleQueued(string knowledge)
    {
        var list = _session.Simulation.Progress.ResearchQueue.ToList();
        if (!list.Remove(knowledge))
        {
            list.Add(knowledge);
        }

        _session.Enqueue(new SetResearchQueueCommand(list));
    }

    /// <summary>How many scriptoria stand: the workshops of the work type of research.</summary>
    public static int Desks(Simulation simulation)
    {
        var world = simulation.World;
        if (ResearchRules.ConfigOf(simulation) is not { } config)
        {
            return 0;
        }

        var shops = new List<EntityId>();
        world.Query<Workshop, Furniture>(shops);
        return shops.Count(shop => simulation.Content.Get<BuildableDef>(world.Get<Furniture>(shop).Buildable).Workstation?.WorkType == config.WorkType);
    }
}
