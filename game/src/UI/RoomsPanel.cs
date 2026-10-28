using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Game.Bootstrap;
using OurLife.Game.Input;
using OurLife.Game.Rendering;
using OurLife.Game.Session;

namespace OurLife.Game.UI;

/// <summary>
/// The list of rooms and the assignment of beds (see "Dormitorios" and "Lo que se ve" in
/// <c>docs/gdd/habitaciones.md</c>): a row per room of the colony with its function and level, its score
/// and who sleeps there, and a click moves the camera to it and selects it. Under the list, the beds of the
/// chosen room, each with a menu to give it to a colonist or leave it free, and a button that gives a house
/// to a whole family at once: it sends an <see cref="AssignBedCommand"/> for each bed
/// <see cref="RoomRules.SuggestAssignments"/> suggests, so the choice of which bed is the Core's. While the panel is
/// open the layer of rooms shows. The rows are built again only when something they show changes.
/// </summary>
public partial class RoomsPanel : PanelContainer
{
    private const double RefreshSeconds = 0.25;

    private readonly List<EntityId> _people = [];
    private GameServices _services = null!;
    private GameSession _session = null!;
    private GameInput _input = null!;
    private WorldView _view = null!;
    private VBoxContainer _column = null!;
    private Button _layer = null!;
    private VBoxContainer _list = null!;
    private VBoxContainer _beds = null!;
    private string _signature = string.Empty;
    private bool _turnedLayerOn;
    private bool _settingLayer;
    private double _sinceRefresh;
    private int _chosenRoom = -1;
    private EntityId _familyOf;

    // The menus of the beds of the chosen room and the one that picks a family, by name, for the smoke.
    private readonly List<OptionButton> _bedMenus = [];
    private OptionButton? _familyMenu;
    private Button? _familyButton;

    public static RoomsPanel Create(GameServices services, GameSession session, GameInput input, WorldView view)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(view);
        var panel = new RoomsPanel { Name = nameof(RoomsPanel), _services = services, _session = session, _input = input, _view = view, Visible = false };
        panel._column = new VBoxContainer();
        panel._column.AddThemeConstantOverride("separation", 6);
        panel.AddChild(panel._column);
        panel._column.AddChild(new Label { Text = "ui.rooms.title" });
        panel._layer = new Button { Text = "ui.rooms.overlay", ToggleMode = true, FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        view.RoomLayer.ShownChanged += panel.OnLayerChanged;
        panel._layer.Pressed += () => view.RoomLayer.Shown = !view.RoomLayer.Shown;
        panel._column.AddChild(panel._layer);

        // A colony with many rooms scrolls instead of growing past the event log.
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(420, 150),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        panel._list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel._list.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(panel._list);
        panel._column.AddChild(scroll);
        panel._beds = new VBoxContainer();
        panel._beds.AddThemeConstantOverride("separation", 4);
        panel._column.AddChild(panel._beds);
        return panel;
    }

    /// <summary>The rows of the list as the player reads them, for the smoke.</summary>
    public IEnumerable<string> RowTexts => _list.GetChildren().OfType<HBoxContainer>().SelectMany(row => row.GetChildren().OfType<Button>()).Select(button => button.Text);

    /// <summary>How many beds the chosen room lists, for the smoke.</summary>
    public int BedRows => _bedMenus.Count;

    /// <summary>The room the beds shown belong to: <see cref="Room.Id"/>, or -1 for none.</summary>
    public int ChosenRoom => _chosenRoom;

    public override void _EnterTree()
    {
        _services.Localizer.LocaleChanged += OnLocaleChanged;
        VisibilityChanged += OnVisibilityChanged;
    }

    public override void _ExitTree()
    {
        _services.Localizer.LocaleChanged -= OnLocaleChanged;
        VisibilityChanged -= OnVisibilityChanged;
        _view.RoomLayer.ShownChanged -= OnLayerChanged;
    }

    public void Toggle()
    {
        Visible = !Visible;
        if (Visible)
        {
            _signature = string.Empty;
            Refresh();
        }
    }

    /// <summary>Whoever else turns the layer on or off (the key, the button) takes it over: closing the list no longer turns it off.</summary>
    private void OnLayerChanged()
    {
        if (!_settingLayer)
        {
            _turnedLayerOn = false;
        }
    }

    /// <summary>
    /// Opening the list turns the layer on if it was off, and closing it turns it off again only if the list was
    /// the one that turned it on: the key of the layer (L) is never overruled.
    /// </summary>
    private void OnVisibilityChanged()
    {
        if (IsVisibleInTree())
        {
            if (!_view.RoomLayer.Shown)
            {
                _settingLayer = true;
                _view.RoomLayer.Shown = true;
                _settingLayer = false;
                _turnedLayerOn = true;
            }
        }
        else if (_turnedLayerOn)
        {
            _turnedLayerOn = false;
            _settingLayer = true;
            _view.RoomLayer.Shown = false;
            _settingLayer = false;
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

    /// <summary>The player selected something: a cell inside a room makes it the chosen one, so its beds show.</summary>
    public void FollowSelection(Selection? selection)
    {
        if (selection is not { } chosen)
        {
            return;
        }

        var cell = chosen.IsColonist ? _session.Simulation.World.Get<Position>(chosen.Colonist).Cell : chosen.Cell;
        if (chosen.IsColonist)
        {
            _familyOf = chosen.Colonist;
        }

        if (!chosen.IsColonist && Rooms.At(_session.Simulation, cell) is { } room && room.Id != _chosenRoom)
        {
            _chosenRoom = room.Id;
            _signature = string.Empty;
        }
    }

    /// <summary>Chooses a room as a click on its row does, and moves the camera and the selection to it.</summary>
    public void Choose(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);
        _chosenRoom = room.Id;
        var cell = room.Middle;
        _view.Camera.GoTo(new Vector2(cell.X + 0.5f, cell.Y + 0.5f), cell.Z);
        _input.SelectCell(cell);
        _signature = string.Empty;
        Refresh();
    }

    /// <summary>Reads the rooms now, without waiting for the next refresh. For the smoke.</summary>
    public void Refresh()
    {
        _sinceRefresh = 0;
        _layer.SetPressedNoSignal(_view.RoomLayer.Shown);
        var simulation = _session.Simulation;
        var rooms = Rooms.All(simulation);
        var chosen = rooms.FirstOrDefault(room => room.Id == _chosenRoom);
        var world = simulation.World;
        world.Query<Colonist>(_people);

        var signature = new System.Text.StringBuilder();
        foreach (var room in rooms)
        {
            signature.Append(room.Id).Append(':').Append(room.Score).Append(':').Append(room.Level.Id).Append(':').Append(room.Roles.Count).Append(':').Append(string.Join(',', RoomRules.Owners(simulation, room).Select(owner => owner.ToString()))).Append(';');
        }

        signature.Append('|').Append(_chosenRoom).Append('|').Append(_familyOf).Append('|');
        foreach (var person in _people)
        {
            signature.Append(person).Append(',');
        }

        if (chosen is not null)
        {
            foreach (var bed in chosen.Beds)
            {
                signature.Append(world.Get<Bed>(bed).Owner).Append(Beds.IsTaken(simulation, bed) ? '+' : '-').Append(';');
            }
        }

        var text = signature.ToString();
        if (text == _signature)
        {
            return;
        }

        _signature = text;
        BuildList(simulation, rooms);
        BuildBeds(simulation, chosen);
    }

    private void BuildList(Simulation simulation, IReadOnlyList<Room> rooms)
    {
        Clear(_list);
        var text = _services.Text;
        if (rooms.Count == 0)
        {
            _list.AddChild(new Label { Text = "ui.rooms.empty", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(400, 0) });
            return;
        }

        var levels = simulation.Content.All<RoomLevelDef>().Count;
        foreach (var room in rooms)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);
            foreach (var role in room.Roles)
            {
                row.AddChild(KnowledgeIcons.Rect(RoomIcons.Role(role.Id)));
            }

            row.AddChild(KnowledgeIcons.Rect(RoomIcons.Level(room.Level.Id)));
            var line = RoomTexts.Line(text, room);
            var owners = RoomTexts.OwnerNames(text, simulation, room);
            if (owners.Length > 0)
            {
                line += "\n" + text.Format("ui.inspector.room_owners", ("names", owners));
            }

            var button = new Button
            {
                Text = line,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                FocusMode = FocusModeEnum.None,
                Alignment = HorizontalAlignment.Left,
                ToggleMode = true,
                ButtonPressed = room.Id == _chosenRoom,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = RoomTexts.Roles(text, room),
                TooltipAutoTranslateMode = AutoTranslateModeEnum.Disabled,
            };
            button.AddThemeColorOverride("font_color", RoomIcons.ColorOf(room.Level, levels).Lightened(0.35f));
            var id = room.Id;
            button.Pressed += () =>
            {
                if (Rooms.All(_session.Simulation).FirstOrDefault(each => each.Id == id) is { } found)
                {
                    Choose(found);
                }
            };
            row.AddChild(button);
            _list.AddChild(row);
        }
    }

    /// <summary>The beds of the chosen room, each with a menu of the colonists that can have it, and the button of the family.</summary>
    private void BuildBeds(Simulation simulation, Room? room)
    {
        Clear(_beds);
        _bedMenus.Clear();
        _familyMenu = null;
        _familyButton = null;
        if (room is null)
        {
            return;
        }

        var text = _services.Text;
        var world = simulation.World;
        _beds.AddChild(new HSeparator());
        _beds.AddChild(new Label { Text = RoomTexts.Kind(text, room), AutoTranslateMode = AutoTranslateModeEnum.Disabled });
        if (room.Beds.Count == 0)
        {
            _beds.AddChild(new Label { Text = "ui.rooms.no_beds" });
            return;
        }

        _beds.AddChild(new Label { Text = "ui.rooms.beds" });
        foreach (var bed in room.Beds)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            var buildable = simulation.Content.Get<BuildableDef>(world.Get<Furniture>(bed).Buildable);
            row.AddChild(new Label
            {
                Text = text.Capitalized(text.Format(buildable.NameKey!, ("count", 1))),
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                CustomMinimumSize = new Vector2(110, 0),
            });

            // Free first, then each colonist the bed is for: choosing one sends the command; free lets it go.
            var menu = new OptionButton
            {
                FocusMode = FocusModeEnum.None,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                CustomMinimumSize = new Vector2(200, 0),
                TooltipText = text.Format("ui.rooms.assign_bed"),
                TooltipAutoTranslateMode = AutoTranslateModeEnum.Disabled,
            };
            var candidates = new List<EntityId> { EntityId.None };
            menu.AddItem(text.Format("ui.rooms.free_bed"));
            var owner = Beds.IsTaken(simulation, bed) ? world.Get<Bed>(bed).Owner : EntityId.None;
            foreach (var person in _people)
            {
                if (Relationships.IsLivingColonist(world, person) && Beds.Fits(simulation, bed, person))
                {
                    candidates.Add(person);
                    menu.AddItem(ColonyText.FullName(world.Get<Colonist>(person)));
                }
            }

            menu.Selected = Math.Max(0, candidates.IndexOf(owner));
            var theBed = bed;
            menu.ItemSelected += index => _session.Enqueue(new AssignBedCommand(theBed, candidates[(int)index]));
            row.AddChild(menu);
            _beds.AddChild(row);
            _bedMenus.Add(menu);
        }

        // A house for a whole family: pick the colonist, and the Core says which bed for each.
        var family = new HBoxContainer();
        family.AddThemeConstantOverride("separation", 6);
        var familyMenu = new OptionButton { FocusMode = FocusModeEnum.None, AutoTranslateMode = AutoTranslateModeEnum.Disabled, CustomMinimumSize = new Vector2(200, 0) };
        var living = new List<EntityId>();
        foreach (var person in _people)
        {
            if (Relationships.IsLivingColonist(world, person))
            {
                living.Add(person);
                familyMenu.AddItem(ColonyText.FullName(world.Get<Colonist>(person)));
            }
        }

        if (living.Count == 0)
        {
            return;
        }

        familyMenu.Selected = Math.Max(0, living.IndexOf(_familyOf));
        var assign = new Button { Text = "ui.rooms.assign_family", FocusMode = FocusModeEnum.None };
        var roomId = room.Id;
        assign.Pressed += () => AssignFamily(roomId, living[familyMenu.Selected]);
        familyMenu.ItemSelected += index => _familyOf = living[(int)index];
        family.AddChild(familyMenu);
        family.AddChild(assign);
        _beds.AddChild(family);
        _familyMenu = familyMenu;
        _familyButton = assign;
    }

    /// <summary>Gives the colonist, their partner, children and siblings the beds of the room the Core suggests, one command each.</summary>
    public void AssignFamily(int roomId, EntityId colonist)
    {
        var simulation = _session.Simulation;
        if (Rooms.All(simulation).FirstOrDefault(each => each.Id == roomId) is not { } room)
        {
            return;
        }

        foreach (var assignment in RoomRules.SuggestAssignments(simulation, room, colonist))
        {
            _session.Enqueue(new AssignBedCommand(assignment.Bed, assignment.Colonist));
        }
    }

    /// <summary>Presses the button that assigns the family to the colonist at a position of its menu, as a click would. For the smoke.</summary>
    public bool PressAssignFamily(int menuIndex)
    {
        if (_familyMenu is null || _familyButton is null)
        {
            return false;
        }

        _familyMenu.Selected = menuIndex;
        _familyButton.EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    private static void Clear(Control box)
    {
        foreach (var child in box.GetChildren())
        {
            box.RemoveChild(child);
            child.QueueFree();
        }
    }
}
