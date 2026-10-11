using Godot;
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
/// colonist's name, job and needs, or a cell's shape, material, orders, plant and items. It
/// refreshes a few times per second while time runs or the simulation changes, and when the
/// language changes.
/// </summary>
public partial class InspectorPanel : PanelContainer
{
    private const double RefreshSeconds = 0.2;

    private readonly List<string> _lines = [];
    private readonly List<EntityId> _entities = [];
    private GameServices _services = null!;
    private Simulation _simulation = null!;
    private Label _text = null!;
    private Selection? _selection;
    private double _sinceRefresh;
    private long _refreshedTick = -1;
    private bool _changed;

    public static InspectorPanel Create(GameServices services, Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(simulation);
        var panel = new InspectorPanel { Name = nameof(InspectorPanel), _services = services, _simulation = simulation, Visible = false };
        panel.CustomMinimumSize = new Vector2(320, 0);
        panel._text = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        panel.AddChild(panel._text);
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

    private void OnLocaleChanged(string locale) => Refresh();

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
        if (selection.IsColonist && _simulation.World.IsAlive(selection.Colonist))
        {
            DescribeColonist(selection.Colonist);
        }
        else
        {
            DescribeCell(selection.Cell);
        }

        _text.Text = string.Join("\n", _lines);
    }

    private void DescribeColonist(EntityId colonist)
    {
        var world = _simulation.World;
        var text = _services.Text;
        _lines.Add(world.Get<Colonist>(colonist).Name);
        _lines.Add(world.TryGet<Job>(colonist, out var job) ? text.Format(JobKinds.ReportKey(job.Kind)) : text.Format("ui.inspector.idle"));
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

    /// <summary>The name of a def in the current language; <paramref name="count"/> picks the plural where the text has one.</summary>
    private string NameOf(Def def, int count = 1) =>
        _services.Text.Format(def.NameKey ?? throw new InvalidOperationException($"Defs of type {def.GetType().Name} have no texts."), ("count", count));
}
