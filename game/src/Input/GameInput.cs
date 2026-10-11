using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Game.Rendering;
using OurLife.Game.Session;

namespace OurLife.Game.Input;

/// <summary>What the player selected: a colonist, or a cell when there is none.</summary>
public readonly record struct Selection(EntityId Colonist, TileCoord Cell)
{
    public bool IsColonist => !Colonist.IsNone;
}

/// <summary>
/// The player's hands in a game (see "Controles" and "Selección de celdas" in
/// <c>docs/gdd/interfaz-ip1.md</c>): the cell under the mouse is framed; with an order active,
/// dragging marks a box of cells and sends a <see cref="DesignateCommand"/>; without one, a click
/// selects a colonist or a cell. Keys choose orders and set the time. Esc and the right button
/// back out: first the order, then the selection, then they ask for the pause menu.
/// </summary>
public partial class GameInput : Node3D
{
    private static readonly Color HoverColor = new(1f, 1f, 1f, 0.35f);
    private static readonly Color SelectionColor = new(1f, 0.95f, 0.4f, 0.45f);
    private static readonly Color BoxColor = new(1f, 0.7f, 0.2f, 0.3f);

    // Keys 1 to 3 choose the speeds of GameSession.Speeds, in order.
    private static readonly string[] SpeedActions = [InputActions.Speed1, InputActions.Speed2, InputActions.Speed3];

    private GameSession _session = null!;
    private WorldView _view = null!;
    private MeshInstance3D _hover = null!;
    private MeshInstance3D _selected = null!;
    private MeshInstance3D _box = null!;
    private Vector2? _mouse;
    private TileCoord? _hoverCell;
    private TileCoord? _dragStart;
    private int _dragLevel;
    private Selection? _selection;
    private DesignationKind? _order;

    /// <summary>Raised when the player selects something or clears the selection.</summary>
    public event Action<Selection?>? SelectionChanged;

    /// <summary>Raised when the active order changes; null means no order.</summary>
    public event Action<DesignationKind?>? OrderChanged;

    /// <summary>Raised when Esc has nothing left to back out of.</summary>
    public event Action? PauseMenuRequested;

    public Selection? Selected => _selection;

    /// <summary>The order the next drag gives, or null to select.</summary>
    public DesignationKind? Order
    {
        get => _order;
        set
        {
            if (_order == value)
            {
                return;
            }

            _order = value;
            _dragStart = null;
            _box.Visible = false;
            OrderChanged?.Invoke(value);
        }
    }

    public static GameInput Create(GameSession session, WorldView view)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(view);
        var input = new GameInput { Name = nameof(GameInput), _session = session, _view = view };
        input._hover = input.Marker("Hover", HoverColor);
        input._selected = input.Marker("Selected", SelectionColor);
        input._box = input.Marker("DragBox", BoxColor);
        return input;
    }

    public override void _Ready() => InputActions.Register();

    public void ClearSelection() => Select(null);

    /// <summary>Selects a colonist, as a click on it would.</summary>
    public void SelectColonist(EntityId colonist) =>
        Select(new Selection(colonist, _session.Simulation.World.Get<Position>(colonist).Cell));

    public override void _Process(double delta)
    {
        HandleKeys();

        // Every frame, as the camera and the view level also move without the mouse.
        if (_mouse is { } mouse)
        {
            Hover(mouse);
        }

        // A selected colonist is followed as it walks; one that died is no longer selected.
        if (_selection is not { } selection)
        {
            return;
        }

        var cell = selection.Cell;
        if (selection.IsColonist)
        {
            var world = _session.Simulation.World;
            if (!world.IsAlive(selection.Colonist))
            {
                Select(null);
                return;
            }

            cell = world.Get<Position>(selection.Colonist).Cell;
            PlaceOn(_selected, cell, cell);
        }

        // The frame is drawn through the terrain: it hides with what it marks, when the view cuts it off.
        _selected.Visible = LevelShading.IsVisible(cell.Z, _view.ViewLevel);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion:
                // _Process frames the cell under it, once per frame.
                _mouse = motion.Position;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
                _dragStart = PickAt(press.Position);
                _dragLevel = _view.ViewLevel;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release:
                Release(release.Position);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }:
                BackOut();
                break;
            case InputEventKey { Pressed: true, Echo: false } when @event.IsAction("ui_cancel"):
                if (!BackOut())
                {
                    PauseMenuRequested?.Invoke();
                }

                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    private void HandleKeys()
    {
        foreach (var kind in Enum.GetValues<DesignationKind>())
        {
            if (Godot.Input.IsActionJustPressed(InputActions.Order(kind)))
            {
                Order = Order == kind ? null : kind;
            }
        }

        if (Godot.Input.IsActionJustPressed(InputActions.TogglePause))
        {
            _session.TogglePause();
        }

        for (var i = 0; i < Math.Min(SpeedActions.Length, GameSession.Speeds.Count); i++)
        {
            if (Godot.Input.IsActionJustPressed(SpeedActions[i]))
            {
                _session.SetSpeed(GameSession.Speeds[i]);
            }
        }
    }

    private TileCoord? PickAt(Vector2 screenPoint) =>
        CellPicker.Pick(_view.Camera.Camera, screenPoint, _session.Simulation.Map, _view.ViewLevel);

    private void Hover(Vector2 screenPoint)
    {
        _hoverCell = PickAt(screenPoint);
        _hover.Visible = _hoverCell is not null;
        if (_hoverCell is { } cell)
        {
            PlaceOn(_hover, cell, cell);
        }

        _box.Visible = _order is not null && _dragStart is not null && _hoverCell is not null;
        if (_box.Visible)
        {
            PlaceOn(_box, _dragStart!.Value, BoxCorner(_dragStart.Value, _hoverCell!.Value));
        }
    }

    /// <summary>
    /// The far corner of a dragged box. It stays on the level of the first cell, so a drag across
    /// a slope does not take every buried cell between the two heights; changing the view level
    /// while dragging reaches other levels.
    /// </summary>
    private TileCoord BoxCorner(TileCoord start, TileCoord end) =>
        _view.ViewLevel == _dragLevel ? new TileCoord(end.X, end.Y, start.Z) : end;

    private void Release(Vector2 screenPoint)
    {
        var start = _dragStart;
        _dragStart = null;
        _box.Visible = false;
        if (start is null)
        {
            return;
        }

        if (_order is { } order)
        {
            if (PickAt(screenPoint) is { } corner)
            {
                _session.Enqueue(new DesignateCommand(order, start.Value, BoxCorner(start.Value, corner)));
            }
        }
        else
        {
            ClickAt(screenPoint);
        }
    }

    /// <summary>Selects what a click at the point picks: the colonist drawn there, else the colonist or the cell under it.</summary>
    public void ClickAt(Vector2 screenPoint)
    {
        var ground = CellPicker.PickWithDistance(_view.Camera.Camera, screenPoint, _session.Simulation.Map, _view.ViewLevel);

        // Only a colonist in front of the ground the ray meets: not one behind a hill.
        if (ColonistAt(screenPoint, ground?.Distance ?? float.MaxValue) is { IsNone: false } colonist)
        {
            SelectColonist(colonist);
        }
        else if (ground is { } hit)
        {
            Select(SelectionAt(hit.Cell));
        }
    }

    /// <summary>
    /// The colonist whose drawn model is under the point, nearest the camera and nearer than
    /// <paramref name="limit"/>, or none. The model stands tall over its cell, so the ray through
    /// its body meets the ground behind it.
    /// </summary>
    private EntityId ColonistAt(Vector2 screenPoint, float limit)
    {
        var camera = _view.Camera.Camera;
        var from = camera.ProjectRayOrigin(screenPoint);
        var direction = camera.ProjectRayNormal(screenPoint);
        var colonists = new List<EntityId>();
        _session.Simulation.World.Query<Colonist>(colonists);
        var nearest = EntityId.None;
        var nearestDistance = limit;
        foreach (var colonist in colonists)
        {
            if (_view.DrawnBounds(colonist) is { } bounds && RayEntry(bounds, from, direction) is { } distance
                && distance < nearestDistance)
            {
                nearest = colonist;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    /// <summary>How far along the ray it enters the box (slab test), or null if it misses.</summary>
    private static float? RayEntry(Aabb box, Vector3 from, Vector3 direction)
    {
        float enter = 0, leave = float.MaxValue;
        for (var axis = 0; axis < 3; axis++)
        {
            float origin = from[axis], step = direction[axis], min = box.Position[axis], max = box.End[axis];
            if (step == 0)
            {
                if (origin < min || origin > max)
                {
                    return null;
                }

                continue;
            }

            var t0 = (min - origin) / step;
            var t1 = (max - origin) / step;
            enter = Math.Max(enter, Math.Min(t0, t1));
            leave = Math.Min(leave, Math.Max(t0, t1));
            if (enter > leave)
            {
                return null;
            }
        }

        return enter;
    }

    /// <summary>Clears the order, or else the selection. False if there was neither.</summary>
    private bool BackOut()
    {
        if (_order is not null)
        {
            Order = null;
            return true;
        }

        if (_selection is not null)
        {
            Select(null);
            return true;
        }

        return false;
    }

    /// <summary>
    /// The colonist on the cell or on the one above it (a wall's top), lowest id first; else the
    /// cell. Only colonists the view draws: not one the level cut hides.
    /// </summary>
    private Selection SelectionAt(TileCoord cell)
    {
        var world = _session.Simulation.World;
        var colonists = new List<EntityId>();
        world.Query<Colonist, Position>(colonists);
        var above = new TileCoord(cell.X, cell.Y, cell.Z + 1);
        foreach (var colonist in colonists)
        {
            var position = world.Get<Position>(colonist).Cell;
            if ((position == cell || position == above) && LevelShading.IsVisible(position.Z, _view.ViewLevel))
            {
                return new Selection(colonist, position);
            }
        }

        return new Selection(EntityId.None, cell);
    }

    private void Select(Selection? selection)
    {
        _selection = selection;
        _selected.Visible = selection is not null;
        if (selection is { } chosen)
        {
            PlaceOn(_selected, chosen.Cell, chosen.Cell);
        }

        SelectionChanged?.Invoke(selection);
    }

    /// <summary>Stretches a marker over the box of cells between two corners, a little larger than the cells.</summary>
    private static void PlaceOn(MeshInstance3D marker, TileCoord a, TileCoord b)
    {
        var min = new Vector3(Math.Min(a.X, b.X), Math.Min(a.Z, b.Z), Math.Min(a.Y, b.Y));
        var max = new Vector3(Math.Max(a.X, b.X) + 1, Math.Max(a.Z, b.Z) + 1, Math.Max(a.Y, b.Y) + 1);
        marker.Position = (min + max) / 2;
        marker.Scale = (max - min) + new Vector3(0.06f, 0.06f, 0.06f);
    }

    private MeshInstance3D Marker(string name, Color color)
    {
        using var mesh = new BoxMesh();
        using var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = color,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = true,
        };

        // The node keeps its own references (see MapRenderer).
        var marker = new MeshInstance3D { Name = name, Mesh = mesh, MaterialOverride = material, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(marker);
        return marker;
    }
}
