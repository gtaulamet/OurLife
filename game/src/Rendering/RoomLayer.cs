using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Game.UI;

namespace OurLife.Game.Rendering;

/// <summary>
/// The layer of rooms (see "Lo que se ve" in <c>docs/gdd/habitaciones.md</c>): the floor of each room in
/// the tone of its level, from dull to gold, and over its middle the icons of its functions and its level.
/// It only draws what <see cref="Rooms"/> says: nothing here changes the simulation. Hidden, it costs
/// nothing; shown, it is built again after a <see cref="RoomsChangedEvent"/> or a change of the view level,
/// not every frame.
/// </summary>
public partial class RoomLayer : Node3D
{
    private const float Alpha = 0.4f;

    // The pixels of an icon in the world's units, and how high over the floor the row floats.
    private const float IconPixel = 0.045f;
    private const float IconHeight = 1.55f;
    private const float IconSpacing = 0.78f;

    private Simulation _simulation = null!;
    private MultiMeshInstance3D _slabs = null!;
    private Node3D _icons = null!;
    private int _viewLevel;
    private bool _dirty = true;
    private int _roomsShown;

    public static RoomLayer Create(Simulation simulation, int viewLevel)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var layer = new RoomLayer { Name = nameof(RoomLayer), _simulation = simulation, _viewLevel = viewLevel, Visible = false };
        using var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        // The node keeps its own reference to the material; the C# one is released (see MapRenderer).
        layer._slabs = new MultiMeshInstance3D { Name = "Floors", MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        layer.AddChild(layer._slabs);
        layer._icons = new Node3D { Name = "Icons" };
        layer.AddChild(layer._icons);
        return layer;
    }

    /// <summary>The layer was turned on or off, by whoever did.</summary>
    public event Action? ShownChanged;

    /// <summary>Whether the layer shows. Turning it on builds it if the rooms changed while it was off.</summary>
    public bool Shown
    {
        get => Visible;
        set
        {
            var changed = Visible != value;
            Visible = value;
            if (changed)
            {
                ShownChanged?.Invoke();
            }

            if (value && _dirty)
            {
                Rebuild();
            }
        }
    }

    /// <summary>How many rooms the layer draws now (of the levels the view shows), for the smoke.</summary>
    public int RoomsShown
    {
        get
        {
            if (Visible && _dirty)
            {
                Rebuild();
            }

            return _roomsShown;
        }
    }

    /// <summary>The rooms changed: the next frame with the layer on draws them again.</summary>
    public void Apply(ISimEvent simEvent) => _dirty |= simEvent is RoomsChangedEvent;

    public void SetViewLevel(int viewLevel)
    {
        _viewLevel = viewLevel;
        _dirty = true;
    }

    public override void _Process(double delta)
    {
        if (Visible && _dirty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        _dirty = false;
        foreach (var child in _icons.GetChildren())
        {
            _icons.RemoveChild(child);
            child.QueueFree();
        }

        var rooms = Rooms.All(_simulation);
        var levels = _simulation.Content.All<RoomLevelDef>().Count;
        var cells = 0;
        _roomsShown = 0;
        foreach (var room in rooms)
        {
            if (LevelShading.IsVisible(room.Z, _viewLevel))
            {
                cells += room.Cells.Count;
            }
        }

        if (cells == 0)
        {
            _slabs.Multimesh = null;
            return;
        }

        using var box = new BoxMesh();
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = box,
            InstanceCount = cells,
        };

        var index = 0;
        foreach (var room in rooms)
        {
            if (!LevelShading.IsVisible(room.Z, _viewLevel))
            {
                continue;
            }

            _roomsShown++;
            var color = RoomIcons.ColorOf(room.Level, levels);
            color.A = Alpha;
            foreach (var cell in room.Cells)
            {
                var floor = new Vector3(cell.X + 0.5f, cell.Z + TerrainMeshes.FloorThickness + 0.03f, cell.Y + 0.5f);
                multimesh.SetInstanceTransform(index, new Transform3D(Basis.Identity.Scaled(new Vector3(0.96f, 0.03f, 0.96f)), floor));
                multimesh.SetInstanceColor(index, color);
                index++;
            }

            AddIcons(room);
        }

        _slabs.Multimesh = multimesh;

        // The node keeps its own reference (see MapRenderer).
        multimesh.Dispose();
    }

    /// <summary>A row of icons over the cell of the room nearest to its middle: its functions, then its level.</summary>
    private void AddIcons(Room room)
    {
        var best = room.Middle;

        var textures = new List<Texture2D>(room.Roles.Count + 1);
        foreach (var role in room.Roles)
        {
            if (RoomIcons.Role(role.Id) is { } roleIcon)
            {
                textures.Add(roleIcon);
            }
        }

        if (RoomIcons.Level(room.Level.Id) is { } levelIcon)
        {
            textures.Add(levelIcon);
        }

        var origin = new Vector3(best.X + 0.5f, best.Z + IconHeight, best.Y + 0.5f);
        for (var i = 0; i < textures.Count; i++)
        {
            var offset = (i - ((textures.Count - 1) / 2f)) * IconSpacing;
            var sprite = new Sprite3D
            {
                Texture = textures[i],
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
                Shaded = false,
                PixelSize = IconPixel,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };

            // Offset is in pixels of the texture, in the plane of the billboard: the row stays level on the screen
            // whichever way the camera is turned.
            sprite.Position = origin;
            sprite.Offset = new Vector2(offset / IconPixel, 0f);
            _icons.AddChild(sprite);
        }
    }
}
