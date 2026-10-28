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
/// The production orders of the village (see "Órdenes de producción" in <c>docs/gdd/oficios.md</c>):
/// one row per order, in the order of the list, with what it makes, its mode, what is done and its
/// state, and buttons to move it, change its mode and number, pause it and remove it. Below, the
/// form of a new order: a recipe, a mode and a number. The rows refresh a few times per second
/// while the panel is open, and are built again only when something they show changes.
/// </summary>
public partial class ProductionPanel : PanelContainer, IToggleablePanel
{
    private const double RefreshSeconds = 0.25;
    private const int DefaultQuantity = 5;

    private GameServices _services = null!;
    private GameSession _session = null!;
    private VBoxContainer _list = null!;
    private VBoxContainer _content = null!;
    private ScrollContainer _scroll = null!;
    private OptionButton _recipes = null!;
    private OptionButton _modes = null!;
    private Label _quantityLabel = null!;
    private IReadOnlyList<RecipeDef> _recipeDefs = [];
    private int _newQuantity = DefaultQuantity;
    private string _signature = string.Empty;
    private double _sinceRefresh;

    public static ProductionPanel Create(GameServices services, GameSession session)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(session);
        var panel = new ProductionPanel { Name = nameof(ProductionPanel), _services = services, _session = session, Visible = false };
        panel.CustomMinimumSize = new Vector2(520, 0);
        return panel;
    }

    /// <summary>The rows of the orders as the player reads them, for the smoke.</summary>
    public IEnumerable<string> OrderTexts => _list is null ? [] : _list.GetChildren().OfType<HBoxContainer>().Select(row => row.GetChildren().OfType<Label>().First().Text);

    public override void _EnterTree() => _services.Localizer.LocaleChanged += OnLocaleChanged;

    public override void _ExitTree() => _services.Localizer.LocaleChanged -= OnLocaleChanged;

    public void Toggle()
    {
        Visible = !Visible;
        if (Visible)
        {
            Build();
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        UiBuilder.FitToCanvas(_scroll, _content);
        _sinceRefresh += delta;
        if (_sinceRefresh >= RefreshSeconds)
        {
            RefreshList();
        }
    }

    private void OnLocaleChanged(string locale)
    {
        if (Visible)
        {
            Build();
        }
    }

    /// <summary>The whole panel, again: the texts of the form too, which depend on the language.</summary>
    private void Build()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        var text = _services.Text;
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);

        // Many orders scroll rather than reach the event log (UiBuilder.FitToCanvas).
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(_scroll);
        _scroll.AddChild(column);
        _content = column;
        column.AddChild(new Label { Text = "ui.production.title" });
        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 4);
        column.AddChild(_list);

        column.AddChild(new Label { Text = "ui.production.new" });
        var form = new HBoxContainer();
        form.AddThemeConstantOverride("separation", 6);
        column.AddChild(form);
        var content = _session.Simulation.Content;
        _recipeDefs = content.All<RecipeDef>()
            .OrderBy(recipe => content.Get<BuildableDef>(recipe.Workshop).Order)
            .ThenBy(recipe => recipe.Workshop, StringComparer.Ordinal)
            .ThenBy(recipe => recipe.Order)
            .ThenBy(recipe => recipe.Id, StringComparer.Ordinal)
            .ToList();
        _recipes = new OptionButton { AutoTranslateMode = AutoTranslateModeEnum.Disabled, FocusMode = FocusModeEnum.None };
        foreach (var recipe in _recipeDefs)
        {
            _recipes.AddItem(RecipeLabel(recipe));
        }

        form.AddChild(_recipes);
        _modes = new OptionButton { AutoTranslateMode = AutoTranslateModeEnum.Disabled, FocusMode = FocusModeEnum.None };
        foreach (var mode in Enum.GetValues<WorkOrderMode>())
        {
            _modes.AddItem(text.Format($"ui.production.kind.{mode.ToString().ToLowerInvariant()}"));
        }

        form.AddChild(_modes);
        var less = new Button { Text = "ui.production.less", FocusMode = FocusModeEnum.None };
        less.Pressed += () =>
        {
            _newQuantity = Less(_newQuantity);
            _quantityLabel.Text = _newQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        };
        form.AddChild(less);
        _quantityLabel = new Label
        {
            Text = _newQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            CustomMinimumSize = new Vector2(36, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        form.AddChild(_quantityLabel);
        var more = new Button { Text = "ui.production.more", FocusMode = FocusModeEnum.None };
        more.Pressed += () =>
        {
            _newQuantity = More(_newQuantity);
            _quantityLabel.Text = _newQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        };
        form.AddChild(more);
        var add = new Button { Text = "ui.production.add", FocusMode = FocusModeEnum.None };
        add.Pressed += AddOrder;
        form.AddChild(add);
        _signature = string.Empty;
        RefreshList();
    }

    /// <summary>The recipe's name in the list, with the knowledge it still lacks.</summary>
    private string RecipeLabel(RecipeDef recipe)
    {
        var text = _services.Text;
        var simulation = _session.Simulation;
        var name = text.Capitalized(text.Format(recipe.NameKey!));
        var missing = RecipeKnowledge.Missing(simulation, recipe);
        return missing.Count == 0
            ? name
            : text.Format("ui.production.recipe_locked", ("recipe", name), ("knowledge", RecipeKnowledge.Names(_services, simulation, missing)));
    }

    private void AddOrder()
    {
        if (_recipes.Selected < 0 || _recipes.Selected >= _recipeDefs.Count || _modes.Selected < 0)
        {
            return;
        }

        _session.Enqueue(new CreateWorkOrderCommand(_recipeDefs[_recipes.Selected].Id, Enum.GetValues<WorkOrderMode>()[_modes.Selected], _newQuantity));
    }

    /// <summary>Reads the orders now, without waiting for the next refresh. For the smoke.</summary>
    public void Refresh()
    {
        if (_list is not null)
        {
            RefreshList();
        }
    }

    private void RefreshList()
    {
        _sinceRefresh = 0;

        // What a recipe lacks changes as the colony learns, while the panel stays open.
        for (var index = 0; index < _recipeDefs.Count && index < _recipes.ItemCount; index++)
        {
            var label = RecipeLabel(_recipeDefs[index]);
            if (_recipes.GetItemText(index) != label)
            {
                _recipes.SetItemText(index, label);
            }
        }

        var simulation = _session.Simulation;
        var world = simulation.World;
        var orders = WorkOrderRules.InOrder(simulation)
            .Select(entity => (Entity: entity, Order: world.Get<WorkOrder>(entity), Status: WorkOrderRules.Status(simulation, entity)))
            .ToList();
        var signature = string.Join(
            ';',
            orders.Select(item => $"{item.Entity.Index}:{item.Order.Recipe}:{item.Order.Mode}:{item.Order.Quantity}:{item.Order.Done}:{item.Order.Paused}:{item.Status}"));
        if (signature == _signature)
        {
            return;
        }

        _signature = signature;
        foreach (var child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }

        var text = _services.Text;
        if (orders.Count == 0)
        {
            _list.AddChild(new Label { Text = "ui.production.empty" });
            return;
        }

        for (var index = 0; index < orders.Count; index++)
        {
            var (entity, order, status) = orders[index];
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);
            var recipe = text.Capitalized(text.Format(simulation.Content.Get<RecipeDef>(order.Recipe).NameKey!));
            row.AddChild(new Label
            {
                Text = text.Format(
                    "ui.production.order",
                    ("recipe", recipe),
                    ("mode", ModeName(order.Mode, order.Quantity)),
                    ("done", order.Done),
                    ("status", text.Format($"ui.workshop.status.{InspectorPanel.StatusId(status)}"))),
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(280, 0),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            });
            var position = index;
            AddButton(row, "ui.production.up", () => _session.Enqueue(new MoveWorkOrderCommand(entity, position - 1)), index > 0);
            AddButton(row, "ui.production.down", () => _session.Enqueue(new MoveWorkOrderCommand(entity, position + 1)), index < orders.Count - 1);
            AddButton(row, "ui.production.mode", () => _session.Enqueue(NextMode(entity, order)));
            if (order.Mode != WorkOrderMode.Forever)
            {
                AddButton(row, "ui.production.less", () => _session.Enqueue(new SetWorkOrderCommand(entity, order.Mode, Less(order.Quantity), order.Paused)));
                AddButton(row, "ui.production.more", () => _session.Enqueue(new SetWorkOrderCommand(entity, order.Mode, More(order.Quantity), order.Paused)));
            }

            AddButton(row, order.Paused ? "ui.production.resume" : "ui.production.pause", () => _session.Enqueue(new SetWorkOrderCommand(entity, order.Mode, order.Quantity, !order.Paused)));
            AddButton(row, "ui.workshop.remove", () => _session.Enqueue(new RemoveWorkOrderCommand(entity)));
            _list.AddChild(row);
        }
    }

    private static void AddButton(HBoxContainer row, string text, Action pressed, bool enabled = true)
    {
        var button = new Button { Text = text, FocusMode = FocusModeEnum.None, Disabled = !enabled };
        button.Pressed += pressed;
        row.AddChild(button);
    }

    /// <summary>The order with the next mode: count, keep, repeat. A number that repeat dropped comes back as the usual one.</summary>
    private static SetWorkOrderCommand NextMode(EntityId entity, WorkOrder order)
    {
        var next = (WorkOrderMode)(((int)order.Mode + 1) % Enum.GetValues<WorkOrderMode>().Length);
        return new SetWorkOrderCommand(entity, next, order.Quantity < 1 ? DefaultQuantity : order.Quantity, order.Paused);
    }

    private string ModeName(WorkOrderMode mode, int quantity) => _services.Text.Format(
        $"ui.production.mode.{mode.ToString().ToLowerInvariant()}",
        ("quantity", quantity));

    private static int More(int quantity) => quantity >= 10 ? quantity + 5 : quantity + 1;

    private static int Less(int quantity) => Math.Max(1, quantity > 10 ? quantity - 5 : quantity - 1);
}
