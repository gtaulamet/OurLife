using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Game.Rendering;

/// <summary>
/// Marks the player's orders on the map with a translucent colour per kind: walls to dig and
/// cells for stairs as boxes around the cell, plants to chop or gather as a floating marker
/// (see "Dibujo del mundo" in <c>docs/gdd/interfaz-ip1.md</c>), and stockpile cells as a thin
/// tinted floor (<c>docs/gdd/almacenes.md</c>). Only the levels the view shows are marked. Rebuilt
/// when orders or stockpiles change, when a marked plant is chopped or gathered, or when the view
/// level changes.
/// </summary>
public partial class OrderMarkers : Node3D
{
    // Presentation colours, not balance: they only tell orders apart.
    private static readonly Color MineColor = new(1f, 0.55f, 0.1f, 0.45f);
    private static readonly Color StairsColor = new(0.25f, 0.55f, 1f, 0.5f);
    private static readonly Color ChopColor = new(0.95f, 0.2f, 0.15f, 0.8f);
    private static readonly Color HarvestColor = new(1f, 0.85f, 0.15f, 0.8f);
    private static readonly Color StockpileColor = new(0.15f, 0.65f, 0.95f, 0.5f);

    private readonly List<(Transform3D Transform, Color Color)> _marks = [];
    private readonly List<EntityId> _plants = [];
    private readonly List<EntityId> _zones = [];

    // Plants with an order: chopping or gathering one ends its order without a designation event.
    private readonly HashSet<EntityId> _markedPlants = [];
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
        // Marks cast no shadow: so close to the terrain, their shadows striped it.
        markers._instance = new MultiMeshInstance3D { Name = "Marks", MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        markers.AddChild(markers._instance);
        markers.Rebuild();
        return markers;
    }

    /// <summary>
    /// Follows an event of the simulation. A change asks for a rebuild at the end of the frame, so
    /// several changes in a frame cost one; changes of other entities, like piles, cost nothing.
    /// </summary>
    public void Apply(ISimEvent simEvent) =>
        _dirty |= simEvent switch
        {
            DesignationsChangedEvent or StockpilesChangedEvent => true,
            EntityChangedEvent changed => _markedPlants.Contains(changed.Entity),
            EntityRemovedEvent removed => _markedPlants.Contains(removed.Entity),
            _ => false,
        };

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
                // A slab on the wall's top, just above the cap of the cut: boxes around the whole
                // wall overlapped the terrain and striped.
                _marks.Add((new Transform3D(Basis.Identity.Scaled(new Vector3(0.94f, 0.05f, 0.94f)), centre + new Vector3(0, 1.06f, 0)), MineColor));
            }

            if ((flags & TileFlags.StairsDesignation) != 0)
            {
                _marks.Add((new Transform3D(Basis.Identity.Scaled(new Vector3(0.9f, 0.12f, 0.9f)), centre + new Vector3(0, 0.08f, 0)), StairsColor));
            }
        }

        _simulation.World.Query<Stockpile>(_zones);
        foreach (var zone in _zones)
        {
            foreach (var cell in _simulation.World.Get<Stockpile>(zone).Cells)
            {
                if (LevelShading.IsVisible(cell.Z, _viewLevel))
                {
                    // Thin and pale, just over the floor, so the piles on it show through.
                    var floor = new Vector3(cell.X + 0.5f, cell.Z + TerrainMeshes.FloorThickness + 0.02f, cell.Y + 0.5f);
                    _marks.Add((new Transform3D(Basis.Identity.Scaled(new Vector3(0.96f, 0.02f, 0.96f)), floor), StockpileColor));
                }
            }
        }

        _markedPlants.Clear();
        AddPlantMarks<ChopOrder>(ChopColor);
        AddPlantMarks<HarvestOrder>(HarvestColor);

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

    /// <summary>A floating marker over each plant with the order <typeparamref name="TOrder"/>.</summary>
    private void AddPlantMarks<TOrder>(Color color)
        where TOrder : class
    {
        var world = _simulation.World;
        world.Query<TOrder, Position>(_plants);
        foreach (var plant in _plants)
        {
            // A plant with both orders shows the first one: chopping it ends both.
            if (!_markedPlants.Add(plant))
            {
                continue;
            }

            var position = world.Get<Position>(plant);
            if (LevelShading.IsVisible(position.Z, _viewLevel))
            {
                var above = new Vector3(position.X + 0.5f, position.Z + 2.2f, position.Y + 0.5f);
                _marks.Add((new Transform3D(Basis.Identity.Scaled(new Vector3(0.3f, 0.3f, 0.3f)), above), color));
            }
        }
    }
}
