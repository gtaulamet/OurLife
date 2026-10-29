using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
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
/// A buildable, the material to build it of and, for furniture, its quarter turns: what a drag
/// with the build tool plans.
/// </summary>
public readonly record struct BuildChoice(string Buildable, string Material, int Rotation = 0);

/// <summary>
/// The player's hands in a game (see "Controles" and "Selección de celdas" in
/// <c>docs/gdd/interfaz-ip1.md</c>): the cell under the mouse is framed; with an order active,
/// dragging marks a box of cells and sends a <see cref="DesignateCommand"/>; with something to
/// build chosen, it sends a <see cref="PlaceBlueprintCommand"/> (<c>docs/gdd/construccion.md</c>);
/// with neither, a click selects a colonist or a cell. Keys choose orders and set the time. Esc
/// and the right button back out: first the order or the building, then the selection, then they
/// ask for the pause menu.
/// </summary>
public partial class GameInput : Node3D
{
    private static readonly Color HoverColor = new(1f, 1f, 1f, 0.35f);
    private static readonly Color SelectionColor = new(1f, 0.95f, 0.4f, 0.45f);
    private static readonly Color BoxColor = new(1f, 0.7f, 0.2f, 0.3f);

    // Most pieces a drag previews: a large box shows only where pieces would go.
    private const int MaxPreviews = 64;

    // Keys 1 to 3 choose the speeds of GameSession.Speeds, in order.
    private static readonly string[] SpeedActions = [InputActions.Speed1, InputActions.Speed2, InputActions.Speed3];

    // Built once: the keys are checked every frame.
    private static readonly (DesignationKind Kind, string Action)[] OrderActions =
        Enum.GetValues<DesignationKind>().Select(kind => (kind, InputActions.Order(kind))).ToArray();

    private GameSession _session = null!;
    private WorldView _view = null!;
    private MeshInstance3D _hover = null!;
    private MeshInstance3D _selected = null!;
    private MeshInstance3D _box = null!;

    // Where the mouse is over the world, or null since it left the window.
    private Vector2? _mouse;
    private TileCoord? _hoverCell;

    // A left press over the world, until its release.
    private bool _pressed;
    private TileCoord? _dragStart;

    // The last cell the drag pointed at: the box keeps it if the mouse leaves the map or goes over the HUD.
    private TileCoord? _dragEnd;
    private int _dragLevel;
    private Selection? _selection;
    private DesignationKind? _order;
    private BuildChoice? _build;

    // The crop the farm tool sows, or null with the tool off; and the last one chosen, which the key brings back.
    private string? _farmCrop;
    private string? _lastFarmCrop;
    private ((TileCoord From, TileCoord To)? Box, long Tick, bool On)? _farmKey;

    // The last thing chosen to build, which the build key brings back.
    private BuildChoice? _lastBuild;

    // A cell, or a drag, pointed at in code for captures and the smoke, instead of the mouse.
    private TileCoord? _pointed;
    private (TileCoord From, TileCoord To)? _pointedDrag;

    // What the furniture previews shown were worked out for (see UpdatePreviews).
    private ((TileCoord From, TileCoord To)? Box, BuildChoice? Build, long Tick, string? Era)? _previewKey;

    /// <summary>Raised when the player selects something or clears the selection.</summary>
    public event Action<Selection?>? SelectionChanged;

    /// <summary>Raised when the active order changes; null means no order.</summary>
    public event Action<DesignationKind?>? OrderChanged;

    /// <summary>Raised when what to build changes; null means the build tool is off.</summary>
    public event Action<BuildChoice?>? BuildChanged;

    /// <summary>Raised when the crop of the farm tool changes; null means the tool is off.</summary>
    public event Action<string?>? FarmChanged;

    /// <summary>A drag with the build tool planned this many pieces of the choice (the HUD warns if the material falls short).</summary>
    public event Action<BuildChoice, int>? BuildPlaced;

    /// <summary>Raised when the player asks for the sheet of the selected colonist.</summary>
    public event Action? SheetRequested;

    /// <summary>
    /// Asked first when Esc is pressed: true if it closed something over the game, like the sheet
    /// of a colonist. While another screen is over the game this input is off, so that screen gets Esc.
    /// </summary>
    public Func<bool>? CloseFirst { get; set; }

    /// <summary>Raised when Esc has nothing left to back out of.</summary>
    public event Action? PauseMenuRequested;

    /// <summary>Raised by the colony panel's key. Like every game key, not while a menu covers the game.</summary>
    public event Action? ColonyPanelRequested;

    /// <summary>Raised by the key of the production panel, under the same rules.</summary>
    public event Action? ProductionPanelRequested;

    /// <summary>Raised by the key of the knowledge panel, under the same rules.</summary>
    public event Action? KnowledgePanelRequested;

    /// <summary>Raised by the key of the list of rooms, under the same rules.</summary>
    public event Action? RoomPanelRequested;

    /// <summary>Raised by the key of the inventory, under the same rules.</summary>
    public event Action? InventoryPanelRequested;

    /// <summary>Raised by the key of the layer of rooms, under the same rules.</summary>
    public event Action? RoomLayerRequested;

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

            // A change of order drops the drag under way, and the build tool.
            _order = value;
            DropDrag();
            OrderChanged?.Invoke(value);
            if (value is not null)
            {
                Build = null;
                FarmCrop = null;
            }
        }
    }

    /// <summary>What the next drag plans, or null; choosing something to build clears the order.</summary>
    public BuildChoice? Build
    {
        get => _build;
        set
        {
            if (_build == value)
            {
                return;
            }

            _build = value;
            _lastBuild = value ?? _lastBuild;
            DropDrag();
            BuildChanged?.Invoke(value);
            if (value is not null)
            {
                Order = null;
                FarmCrop = null;
            }
        }
    }

    /// <summary>The crop the next drag sows in a new farm zone, or null with the farm tool off; turning it on clears the order and the build tool.</summary>
    public string? FarmCrop
    {
        get => _farmCrop;
        set
        {
            if (_farmCrop == value)
            {
                return;
            }

            _farmCrop = value;
            _lastFarmCrop = value ?? _lastFarmCrop;
            DropDrag();
            _farmKey = null;
            FarmChanged?.Invoke(value);
            if (value is not null)
            {
                Order = null;
                Build = null;
            }
        }
    }

    /// <summary>The order, the build tool or the farm tool is on: a drag marks a box.</summary>
    private bool Marking => _order is not null || _build is not null || _farmCrop is not null;

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

    /// <summary>Turns the build tool on, with the last thing chosen or else the first of the bar, or off.</summary>
    public void ToggleBuild() => Build = Build is null ? _lastBuild ?? DefaultBuild() : null;

    /// <summary>The first buildable of the bar, of its first material: what the build tool starts with.</summary>
    private BuildChoice? DefaultBuild()
    {
        var first = Construction.BuildablesInOrder(_session.Simulation.Content).FirstOrDefault();
        return first is null ? null : new BuildChoice(first.Id, first.Materials[0]);
    }

    /// <summary>The crops the colony may sow now, in the order of the content.</summary>
    public IReadOnlyList<CropDef> UnlockedCrops()
    {
        var simulation = _session.Simulation;
        // What is harvested first, then the groves; each by id.
        return [.. simulation.Content.All<CropDef>()
            .Where(crop => simulation.Unlocks.IsUnlocked(crop, simulation.Progress))
            .OrderBy(crop => crop.IsTree)
            .ThenBy(crop => crop.Id, StringComparer.Ordinal)];
    }

    /// <summary>Turns the farm tool on, with the last crop chosen or else the first the colony knows, or off. Does nothing if no crop is known.</summary>
    public void ToggleFarm()
    {
        if (_farmCrop is not null)
        {
            FarmCrop = null;
            return;
        }

        var crops = UnlockedCrops();
        if (crops.Count > 0)
        {
            FarmCrop = _lastFarmCrop is { } last && crops.Any(crop => crop.Id == last) ? last : crops[0].Id;
        }
    }

    private void DropDrag()
    {
        _pressed = false;
        _dragStart = null;
        _dragEnd = null;
        _box.Visible = false;
    }

    public void ClearSelection() => Select(null);

    /// <summary>Selects a cell, as a click on it does; for screenshots.</summary>
    public void SelectCell(TileCoord cell) => Select(new Selection(EntityId.None, cell));

    /// <summary>Selects a colonist, as a click on it would.</summary>
    public void SelectColonist(EntityId colonist) =>
        Select(new Selection(colonist, _session.Simulation.World.Get<Position>(colonist).Cell));

    public override void _Process(double delta)
    {
        if (!TextFocus.IsTyping(this))
        {
            HandleKeys();
        }

        // Every frame, as the camera and the view level also move without the mouse. Over the
        // HUD the mouse points at no cell.
        var overHud = GetViewport().GuiGetHoveredControl() is not null;
        Hover(overHud ? null : _mouse);
        UpdatePreviews();
        UpdateFarmPreview();

        // A button released over the HUD never reaches _UnhandledInput: the drag ends here.
        if (_pressed && !Godot.Input.IsMouseButtonPressed(MouseButton.Left))
        {
            Release(null);
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

    // Before the HUD and the map see a click: it lets go of a text field written in.
    public override void _Input(InputEvent @event) => TextFocus.ReleaseOnClickOutside(this, @event);

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion:
                // _Process frames the cell under it, once per frame.
                _mouse = motion.Position;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
                _pressed = true;
                _dragStart = PickAt(press.Position);
                _dragEnd = _dragStart;
                _dragLevel = _view.ViewLevel;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release:
                Release(release.Position);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }:
                BackOut();
                break;
            case InputEventKey { Pressed: true, Echo: false } when @event.IsAction("ui_cancel"):
                if (CloseFirst?.Invoke() == true)
                {
                    break;
                }

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

    public override void _Notification(int what)
    {
        if (what == NotificationWMMouseExit)
        {
            _mouse = null;
        }
    }

    private void HandleKeys()
    {
        foreach (var (kind, action) in OrderActions)
        {
            if (Godot.Input.IsActionJustPressed(action))
            {
                Order = Order == kind ? null : kind;
            }
        }

        if (Godot.Input.IsActionJustPressed(InputActions.Build))
        {
            ToggleBuild();
        }

        if (Godot.Input.IsActionJustPressed(InputActions.Farm))
        {
            ToggleFarm();
        }

        if (Godot.Input.IsActionJustPressed(InputActions.ToggleColony))
        {
            ColonyPanelRequested?.Invoke();
        }

        if (Godot.Input.IsActionJustPressed(InputActions.ToggleProduction))
        {
            ProductionPanelRequested?.Invoke();
        }

        if (Godot.Input.IsActionJustPressed(InputActions.ToggleKnowledge))
        {
            KnowledgePanelRequested?.Invoke();
        }

        if (Godot.Input.IsActionJustPressed(InputActions.ToggleRooms))
        {
            RoomPanelRequested?.Invoke();
        }

        if (Godot.Input.IsActionJustPressed(InputActions.ToggleInventory))
        {
            InventoryPanelRequested?.Invoke();
        }

        if (Godot.Input.IsActionJustPressed(InputActions.ToggleRoomLayer))
        {
            RoomLayerRequested?.Invoke();
        }

        if (Godot.Input.IsActionJustPressed(InputActions.ToggleSheet))
        {
            SheetRequested?.Invoke();
        }

        if (Godot.Input.IsActionJustPressed(InputActions.RotateBuild) && Build is { } build
            && _session.Simulation.Content.Get<BuildableDef>(build.Buildable).IsFurniture)
        {
            Build = build with { Rotation = (build.Rotation + 1) % 4 };
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

    /// <summary>Points at a cell as the mouse would, for captures and the smoke; the mouse no longer moves the frame.</summary>
    public void PointAt(TileCoord cell) => _pointed = cell;

    /// <summary>The last thing chosen to build, which the build key brings back; the smoke puts it back after its check.</summary>
    internal BuildChoice? LastBuild
    {
        get => _lastBuild;
        set => _lastBuild = value;
    }

    /// <summary>Gives the frame back to the mouse after <see cref="PointAt"/> or <see cref="PointAtDrag"/>.</summary>
    public void StopPointing()
    {
        _pointed = null;
        _pointedDrag = null;
    }

    /// <summary>Shows a drag between two cells as the build tool would preview it, without placing anything; for captures.</summary>
    public void PointAtDrag(TileCoord from, TileCoord to)
    {
        _pointed = to;
        _pointedDrag = (from, to);
    }

    /// <summary>
    /// With furniture to build, its translucent model where it would go, turned, with an arrow to
    /// its front, and red where it cannot (<c>docs/gdd/vista-previa-de-muebles.md</c>): on the cell
    /// under the mouse, or on every cell of the drag. The cells come from the same query the
    /// command uses. Asked every frame, and by the smoke, but worked out again only when the box,
    /// the choice, the tick or the era change, or after an order of this input: the query walks
    /// every blueprint, plant and stockpile.
    /// </summary>
    internal void UpdatePreviews()
    {
        var simulation = _session.Simulation;
        var box = _build is null ? null : PreviewBox();
        var key = (box, _build, simulation.CurrentTick, simulation.Progress.CurrentEra);
        if (_previewKey == key)
        {
            return;
        }

        _previewKey = key;
        var previews = new List<Ghost>();
        if (_build is { } build && simulation.Content.Get<BuildableDef>(build.Buildable) is { Visual: { } visual } buildable
            && box is { } corners && Designations.TryClampBox(simulation.Map, corners.From, corners.To, out var min, out var max))
        {
            var valid = Construction.CellsToPlan(simulation, buildable, min, max);
            var size = (long)(max.X - min.X + 1) * (max.Y - min.Y + 1) * (max.Z - min.Z + 1);
            if (size <= MaxPreviews)
            {
                // Every cell of the box, in the order the command walks them: red where it puts nothing.
                var placed = valid.ToHashSet();
                for (var z = min.Z; z <= max.Z; z++)
                {
                    for (var y = min.Y; y <= max.Y; y++)
                    {
                        for (var x = min.X; x <= max.X; x++)
                        {
                            var cell = new TileCoord(x, y, z);
                            previews.Add(new Ghost(cell, visual, build.Rotation, placed.Contains(cell) ? GhostLook.Preview : GhostLook.Blocked));
                        }
                    }
                }
            }
            else
            {
                previews.AddRange(valid.Take(MaxPreviews).Select(cell => new Ghost(cell, visual, build.Rotation, GhostLook.Preview)));
            }
        }

        _view.Previews.Show(previews, simulation.Progress.CurrentEra);
    }

    /// <summary>
    /// With the farm tool on, the cells the drag (or the cell under the mouse) would turn into a
    /// zone, from the same query the command uses. Worked out again only when the box, the tick or
    /// the tool change: the query walks every plant, blueprint and zone. Also asked by the smoke.
    /// </summary>
    internal void UpdateFarmPreview()
    {
        var simulation = _session.Simulation;
        var on = _farmCrop is not null;
        var box = on ? PreviewBox() : null;
        var key = (box, simulation.CurrentTick, on);
        if (_farmKey == key)
        {
            return;
        }

        _farmKey = key;
        if (box is { } corners)
        {
            _view.FarmCells.Show(FarmRules.CellsToZone(simulation, corners.From, corners.To), _view.ViewLevel);
        }
        else
        {
            _view.FarmCells.Show([], _view.ViewLevel);
        }
    }

    /// <summary>The box a preview covers: the drag under way, or the cell pointed at, or nothing.</summary>
    private (TileCoord From, TileCoord To)? PreviewBox()
    {
        if (_pointedDrag is { } pointed)
        {
            return Box(null, pointed.From, pointed.To);
        }

        if (_pressed && _dragStart is { } start && _dragEnd is { } end)
        {
            return Box(null, start, end);
        }

        var hover = _pointed ?? _hoverCell;
        return hover is { } cell ? (cell, cell) : null;
    }

    /// <summary>Frames the cell under the point, or nothing without one, and stretches the drag box to it.</summary>
    private void Hover(Vector2? screenPoint)
    {
        _hoverCell = _pointed ?? (screenPoint is { } point ? PickAt(point) : null);
        _hover.Visible = _hoverCell is not null;
        if (_hoverCell is { } cell)
        {
            PlaceOn(_hover, cell, cell);
            if (_dragStart is not null)
            {
                _dragEnd = cell;
            }
        }

        _box.Visible = Marking && _dragStart is not null && _dragEnd is not null;
        if (_box.Visible)
        {
            var start = _dragStart!.Value;
            var (_, to) = Box(_order, start, _dragEnd!.Value);

            // A box over every visible level would hide the view: an order on plants is drawn
            // as the rectangle on the level of the first cell.
            PlaceOn(_box, start, IsCellOrder(_order) ? to : new TileCoord(to.X, to.Y, start.Z));
        }
    }

    /// <summary>
    /// Orders whose box stays on one level: Cancel too, so it never clears orders the view does not
    /// show, and building, which is no order (null).
    /// </summary>
    private static bool IsCellOrder(DesignationKind? order) => order is not (DesignationKind.Chop or DesignationKind.Harvest or DesignationKind.Reap);

    /// <summary>
    /// The box of cells a drag gives. Digging, stairs and cancelling stay on the level of the first
    /// cell, so a drag across a slope does not take every buried cell between the two heights;
    /// changing the view level while dragging reaches other levels. Chopping and gathering take
    /// every plant the view shows in the rectangle, from the deepest level drawn up to the view
    /// level, because plants grow at every height of a slope; the first cell's level is always in.
    /// </summary>
    private (TileCoord From, TileCoord To) Box(DesignationKind? order, TileCoord start, TileCoord end)
    {
        if (IsCellOrder(order))
        {
            return (start, _view.ViewLevel == _dragLevel ? new TileCoord(end.X, end.Y, start.Z) : end);
        }

        var bottom = Math.Min(start.Z, Math.Max(0, _view.ViewLevel - LevelShading.MaxDepth));
        var top = Math.Max(start.Z, _view.ViewLevel);
        return (new TileCoord(start.X, start.Y, bottom), new TileCoord(end.X, end.Y, top));
    }

    /// <summary>
    /// Ends a left press: at a point of the world, or at none when the button was released over
    /// the HUD. An order marks the box up to the cell released on, or else the last one the drag
    /// pointed at, as drawn; without an order, the press was a click.
    /// </summary>
    private void Release(Vector2? screenPoint)
    {
        if (!_pressed)
        {
            return;
        }

        var start = _dragStart;
        var end = (screenPoint is { } point ? PickAt(point) : null) ?? _dragEnd;
        _pressed = false;
        _dragStart = null;
        _dragEnd = null;
        _box.Visible = false;
        if (Marking)
        {
            if (start is { } first && end is { } last)
            {
                var (from, to) = Box(_order, first, last);
                // The blueprints change what the preview shows, even with the time stopped.
                _previewKey = null;
                _farmKey = null;
                _session.Enqueue(_build is { } build ? new PlaceBlueprintCommand(build.Buildable, build.Material, from, to, build.Rotation)
                    : _farmCrop is { } crop ? new PlaceFarmZoneCommand(from, to, crop)
                    : new DesignateCommand(_order!.Value, from, to));
                if (_build is { } placed && _session.Simulation.Content.TryGet<BuildableDef>(placed.Buildable, out var piece))
                {
                    BuildPlaced?.Invoke(placed, Construction.CellsToPlan(_session.Simulation, piece, from, to).Count);
                }
            }
        }
        else if (screenPoint is { } clicked)
        {
            ClickAt(clicked);
        }
    }

    /// <summary>
    /// Selects what a click at the point picks: the colonist drawn there, else the colonist or the
    /// cell under it. A click on nothing, like the sky, clears the selection.
    /// </summary>
    public void ClickAt(Vector2 screenPoint)
    {
        var ground = CellPicker.PickWithDistance(_view.Camera.Camera, screenPoint, _session.Simulation.Map, _view.ViewLevel);

        // Only a colonist in front of the ground the ray meets: not one behind a hill.
        if (ColonistAt(screenPoint, ground?.Distance ?? float.MaxValue) is { IsNone: false } colonist)
        {
            SelectColonist(colonist);
        }
        else
        {
            Select(ground is { } hit ? SelectionAt(hit.Cell) : null);
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

    /// <summary>Clears the order or the build tool, or else the selection. False if there was none.</summary>
    private bool BackOut()
    {
        if (_order is not null)
        {
            Order = null;
            return true;
        }

        if (_build is not null)
        {
            Build = null;
            return true;
        }

        if (_farmCrop is not null)
        {
            FarmCrop = null;
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
