using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Game.Bootstrap;
using OurLife.Game.Input;

namespace OurLife.Game.UI;

/// <summary>
/// Shows what the player selected (see "Inspector" in <c>docs/gdd/interfaz-ip1.md</c>): a
/// colonist's name, job, load and needs, or a cell's shape, material, orders, plant and items.
/// On a stockpile cell it also lists the zone's categories, each with a Yes/No button that changes
/// its filter (<c>docs/gdd/almacenes.md</c>). It refreshes a few times per second while time runs
/// or the simulation changes, and when the language changes.
/// </summary>
public partial class InspectorPanel : PanelContainer
{
    private const double RefreshSeconds = 0.2;

    private readonly List<string> _lines = [];
    private readonly List<EntityId> _entities = [];
    private GameServices _services = null!;
    private Simulation _simulation = null!;
    private Label _text = null!;
    private VBoxContainer _filters = null!;
    private Action<ISimCommand> _enqueue = null!;

    // The zone whose filters are shown, and its buttons by category, to update them in place.
    private EntityId _filterZone;
    private readonly List<(string Category, Button Button)> _filterButtons = [];
    private Selection? _selection;
    private double _sinceRefresh;
    private long _refreshedTick = -1;
    private bool _changed;

    /// <param name="enqueue">Gives the player's orders, like a filter change, to the game.</param>
    public static InspectorPanel Create(GameServices services, Simulation simulation, Action<ISimCommand> enqueue)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(enqueue);
        var panel = new InspectorPanel { Name = nameof(InspectorPanel), _services = services, _simulation = simulation, _enqueue = enqueue, Visible = false };
        panel.CustomMinimumSize = new Vector2(320, 0);
        var column = new VBoxContainer();
        panel.AddChild(column);
        panel._text = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        column.AddChild(panel._text);
        panel._filters = new VBoxContainer();
        column.AddChild(panel._filters);
        return panel;
    }

    public override void _EnterTree() => _services.Localizer.LocaleChanged += OnLocaleChanged;

    public override void _ExitTree() => _services.Localizer.LocaleChanged -= OnLocaleChanged;

    public void Inspect(Selection? selection)
    {
        _selection = selection;
        Visible = selection is not null;
        Refresh();
    }

    /// <summary>Something happened in the simulation, like an order given while paused: refresh even without a tick.</summary>
    public void NoteChange() => _changed = true;

    public override void _Process(double delta)
    {
        _sinceRefresh += delta;
        if (Visible && _sinceRefresh >= RefreshSeconds && (_changed || _simulation.CurrentTick != _refreshedTick))
        {
            Refresh();
        }
    }

    private void OnLocaleChanged(string locale)
    {
        // The category names are in the buttons' rows: build them again in the new language.
        _filterZone = EntityId.None;
        Refresh();
    }

    private void Refresh()
    {
        _sinceRefresh = 0;
        _changed = false;
        _refreshedTick = _simulation.CurrentTick;
        if (_selection is not { } selection)
        {
            return;
        }

        _lines.Clear();
        var zone = EntityId.None;
        if (selection.IsColonist && _simulation.World.IsAlive(selection.Colonist))
        {
            DescribeColonist(selection.Colonist);
        }
        else
        {
            DescribeCell(selection.Cell);
            zone = Stockpiles.ZoneAt(_simulation, selection.Cell, _entities);
        }

        _text.Text = string.Join("\n", _lines);
        ShowFilters(zone);
    }

    private void DescribeColonist(EntityId colonist)
    {
        var world = _simulation.World;
        var text = _services.Text;
        _lines.Add(world.Get<Colonist>(colonist).Name);
        _lines.Add(world.TryGet<Job>(colonist, out var job) ? text.Format(JobKinds.ReportKey(job.Kind)) : text.Format("ui.inspector.idle"));
        if (world.TryGet<Carried>(colonist, out var carried))
        {
            var item = NameOf(_simulation.Content.Get<ItemDef>(carried.Def), carried.Count);
            _lines.Add(text.Format("ui.inspector.carrying", ("count", carried.Count), ("item", item)));
        }
        foreach (var state in world.Get<Needs>(colonist).Values)
        {
            var need = _simulation.Content.Get<NeedDef>(state.Need);
            var percent = need.Max == 0 ? 0 : state.Value * 100 / need.Max;
            _lines.Add(text.Format("ui.inspector.need", ("need", text.Capitalized(NameOf(need))), ("percent", percent)));
        }
    }

    private void DescribeCell(TileCoord cell)
    {
        var map = _simulation.Map;
        var text = _services.Text;
        _lines.Add(text.Format("ui.inspector.cell", ("x", cell.X), ("y", cell.Y), ("z", cell.Z)));

        var shape = text.Format($"ui.inspector.shape.{TileShapes.Id(map.ShapeAt(cell))}");
        var material = map.MaterialAt(cell);
        var body = material is null
            ? shape
            : text.Format("ui.inspector.shape_material", ("shape", shape), ("material", NameOf(material)));
        _lines.Add(text.Capitalized(body));

        var flags = map.FlagsAt(cell);
        if ((flags & TileFlags.MineDesignation) != 0)
        {
            _lines.Add(text.Format("ui.inspector.order.mine"));
        }

        if ((flags & TileFlags.StairsDesignation) != 0)
        {
            _lines.Add(text.Format("ui.inspector.order.stairs"));
        }

        // What stands on the cell, or on the floor above a wall's top.
        var above = map.ShapeAt(cell) == TileShape.Wall ? new TileCoord(cell.X, cell.Y, cell.Z + 1) : cell;
        DescribeEntitiesAt(cell, above);
    }

    /// <summary>The plants and piles on either cell, in one pass over the entities.</summary>
    private void DescribeEntitiesAt(TileCoord cell, TileCoord other)
    {
        var world = _simulation.World;
        var text = _services.Text;
        world.Query<Position>(_entities);
        foreach (var entity in _entities)
        {
            var at = world.Get<Position>(entity).Cell;
            if (at != cell && at != other)
            {
                continue;
            }

            if (world.TryGet<Plant>(entity, out var plant))
            {
                var name = text.Capitalized(NameOf(_simulation.Content.Get<PlantDef>(plant.Def), 1));
                _lines.Add(world.Has<Harvested>(entity) ? text.Format("ui.inspector.plant_harvested", ("plant", name)) : name);
                if (world.Has<ChopOrder>(entity))
                {
                    _lines.Add(text.Format("ui.inspector.order.chop"));
                }

                if (world.Has<HarvestOrder>(entity))
                {
                    _lines.Add(text.Format("ui.inspector.order.harvest"));
                }
            }
            else if (world.TryGet<ItemStack>(entity, out var stack))
            {
                var item = NameOf(_simulation.Content.Get<ItemDef>(stack.Def), stack.Count);
                _lines.Add(text.Format("ui.inspector.items", ("count", stack.Count), ("item", item)));
            }
        }
    }

    /// <summary>
    /// The filters of the zone under the selected cell, one row per category with Yes and No; none
    /// off the stockpiles. The rows are built when the zone changes and updated in place otherwise,
    /// so a button keeps the mouse while the inspector refreshes.
    /// </summary>
    private void ShowFilters(EntityId zone)
    {
        var world = _simulation.World;
        if (!world.TryGet<Stockpile>(zone, out var stockpile))
        {
            ClearFilters();
            return;
        }

        if (zone != _filterZone)
        {
            ClearFilters();
            _filterZone = zone;
            var text = _services.Text;
            var title = new Label { Text = text.Format("ui.inspector.stockpile"), AutoTranslateMode = AutoTranslateModeEnum.Disabled };
            _filters.AddChild(title);
            foreach (var category in _simulation.Content.All<ItemCategoryDef>())
            {
                var row = new HBoxContainer();
                row.AddChild(new Label
                {
                    Text = text.Capitalized(NameOf(category)),
                    AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                });

                // One toggle: pressed means allowed, and its text says so.
                var button = new Button { ToggleMode = true, CustomMinimumSize = new Vector2(64, 0) };
                var id = category.Id;
                button.Toggled += allowed => _enqueue(new SetStockpileFilterCommand(_filterZone, id, allowed));
                row.AddChild(button);
                _filters.AddChild(row);
                _filterButtons.Add((id, button));
            }
        }

        foreach (var (category, button) in _filterButtons)
        {
            var allowed = stockpile.Categories.BinarySearch(category, StringComparer.Ordinal) >= 0;
            button.SetPressedNoSignal(allowed);
            button.Text = allowed ? "ui.common.on" : "ui.common.off";
        }
    }

    private void ClearFilters()
    {
        _filterZone = EntityId.None;
        _filterButtons.Clear();
        foreach (var child in _filters.GetChildren())
        {
            _filters.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>The name of a def in the current language; <paramref name="count"/> picks the plural where the text has one.</summary>
    private string NameOf(Def def, int count = 1) =>
        _services.Text.Format(def.NameKey ?? throw new InvalidOperationException($"Defs of type {def.GetType().Name} have no texts."), ("count", count));
}
