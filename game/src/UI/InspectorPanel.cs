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
/// refreshes a few times per second while time runs, and when the language changes.
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

    public override void _Process(double delta)
    {
        _sinceRefresh += delta;
        if (Visible && _sinceRefresh >= RefreshSeconds)
        {
            Refresh();
        }
    }

    private void OnLocaleChanged(string locale) => Refresh();

    private void Refresh()
    {
        _sinceRefresh = 0;
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
            _lines.Add(text.Format("ui.inspector.need", ("need", text.Capitalized(text.Format($"def.need.{need.Id}.name"))), ("percent", percent)));
        }
    }

    private void DescribeCell(TileCoord cell)
    {
        var map = _simulation.Map;
        var text = _services.Text;
        _lines.Add(text.Format("ui.inspector.cell", ("x", cell.X), ("y", cell.Y), ("z", cell.Z)));

        var shape = text.Format($"ui.inspector.shape.{ShapeId(map.ShapeAt(cell))}");
        var material = map.MaterialAt(cell);
        var body = material is null
            ? shape
            : text.Format("ui.inspector.shape_material", ("shape", shape), ("material", text.Format($"def.material.{material.Id}.name")));
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
        DescribeEntitiesAt(cell);
        if (map.ShapeAt(cell) == TileShape.Wall && cell.Z + 1 < map.Size.Depth)
        {
            DescribeEntitiesAt(new TileCoord(cell.X, cell.Y, cell.Z + 1));
        }
    }

    private void DescribeEntitiesAt(TileCoord cell)
    {
        var world = _simulation.World;
        var text = _services.Text;
        world.Query<Position>(_entities);
        foreach (var entity in _entities)
        {
            if (world.Get<Position>(entity).Cell != cell)
            {
                continue;
            }

            if (world.TryGet<Plant>(entity, out var plant))
            {
                var name = text.Capitalized(text.Format($"def.plant.{plant.Def}.name", ("count", 1)));
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
                var item = text.Format($"def.item.{stack.Def}.name", ("count", stack.Count));
                _lines.Add(text.Format("ui.inspector.items", ("count", stack.Count), ("item", item)));
            }
        }
    }

    private static string ShapeId(TileShape shape) => shape switch
    {
        TileShape.Empty => "empty",
        TileShape.Floor => "floor",
        TileShape.Wall => "wall",
        TileShape.Ramp => "ramp",
        TileShape.StairUp => "stair_up",
        TileShape.StairDown => "stair_down",
        TileShape.StairUpDown => "stair_updown",
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
    };
}
