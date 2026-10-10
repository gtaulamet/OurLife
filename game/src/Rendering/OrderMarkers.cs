using Godot;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Game.Rendering;

/// <summary>
/// Marks the player's orders on the map with a translucent colour per kind: walls to dig and
/// cells for stairs as boxes around the cell, plants to chop or gather as a floating marker
/// (see "Dibujo del mundo" in <c>docs/gdd/interfaz-ip1.md</c>). Only the levels the view shows
/// are marked. Rebuilt whenever orders or plants change, or the view level does.
/// </summary>
public partial class OrderMarkers : Node3D
{
    // Presentation colours, not balance: they only tell orders apart.
    private static readonly Color MineColor = new(1f, 0.55f, 0.1f, 0.45f);
    private static readonly Color StairsColor = new(0.25f, 0.55f, 1f, 0.5f);
    private static readonly Color ChopColor = new(0.95f, 0.2f, 0.15f, 0.8f);
    private static readonly Color HarvestColor = new(1f, 0.85f, 0.15f, 0.8f);

    private readonly List<(Transform3D Transform, Color Color)> _marks = [];
    private readonly List<EntityId> _plants = [];
    private Simulation _simulation = null!;
    private MultiMeshInstance3D _instance = null!;
    private int _viewLevel;
    private bool _dirty = true;

    public static OrderMarkers Create(Simulation simulation, int viewLevel)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var markers = new OrderMarkers { Name = nameof(OrderMarkers), _simulation = simulation, _viewLevel = viewLevel };
        using var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        // The node keeps its own reference to the material; the C# one is released (see MapRenderer).
        markers._instance = new MultiMeshInstance3D { Name = "Marks", MaterialOverride = material };
        markers.AddChild(markers._instance);
        markers.Rebuild();
        return markers;
    }

    /// <summary>Asks for a rebuild at the end of the frame; several changes in a frame cost one.</summary>
    public void MarkDirty() => _dirty = true;

    public void SetViewLevel(int viewLevel)
    {
        _viewLevel = viewLevel;
        _dirty = true;
    }

    public override void _Process(double delta)
    {
        if (_dirty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        _dirty = false;
        _marks.Clear();
        var map = _simulation.Map;
        foreach (var index in map.OrderedCells)
        {
            var cell = map.Size.CoordOf(index);
            if (!LevelShading.IsVisible(cell.Z, _viewLevel))
            {
                continue;
            }

            var flags = map.FlagsAt(cell);
            var centre = new Vector3(cell.X + 0.5f, cell.Z, cell.Y + 0.5f);
            if ((flags & TileFlags.MineDesignation) != 0)
            {
                // A box a little larger than the wall, so it shows over every face.
                _marks.Add((new Transform3D(Basis.Identity.Scaled(new Vector3(1.04f, 1.04f, 1.04f)), centre + new Vector3(0, 0.5f, 0)), MineColor));
            }

            if ((flags & TileFlags.StairsDesignation) != 0)
            {
                _marks.Add((new Transform3D(Basis.Identity.Scaled(new Vector3(0.9f, 0.12f, 0.9f)), centre + new Vector3(0, 0.08f, 0)), StairsColor));
            }
        }

        var world = _simulation.World;
        world.Query<Plant, Position>(_plants);
        foreach (var plant in _plants)
        {
            var chop = world.Has<ChopOrder>(plant);
            if (!chop && !world.Has<HarvestOrder>(plant))
            {
                continue;
            }

            var position = world.Get<Position>(plant);
            if (LevelShading.IsVisible(position.Z, _viewLevel))
            {
                var above = new Vector3(position.X + 0.5f, position.Z + 2.2f, position.Y + 0.5f);
                _marks.Add((new Transform3D(Basis.Identity.Scaled(new Vector3(0.3f, 0.3f, 0.3f)), above), chop ? ChopColor : HarvestColor));
            }
        }

        using var box = new BoxMesh();
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = box,
            InstanceCount = _marks.Count,
        };

        for (var i = 0; i < _marks.Count; i++)
        {
            multimesh.SetInstanceTransform(i, _marks[i].Transform);
            multimesh.SetInstanceColor(i, _marks[i].Color);
        }

        _instance.Multimesh = multimesh;

        // The node keeps its own reference (see MapRenderer).
        multimesh.Dispose();
    }
}
