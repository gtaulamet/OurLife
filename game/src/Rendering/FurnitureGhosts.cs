using Godot;
using OurLife.Core.Map;

namespace OurLife.Game.Rendering;

/// <summary>How a translucent piece of furniture is drawn (<c>docs/gdd/vista-previa-de-muebles.md</c>).</summary>
public enum GhostLook
{
    /// <summary>Where the piece would go, while placing it.</summary>
    Preview,

    /// <summary>A cell where it cannot go: red.</summary>
    Blocked,

    /// <summary>A blueprint still waiting for material.</summary>
    Planned,

    /// <summary>A blueprint with all its material.</summary>
    Supplied,
}

/// <summary>A translucent piece of furniture on a cell, turned a number of quarters.</summary>
public readonly record struct Ghost(TileCoord Cell, string Visual, int Rotation, GhostLook Look);

/// <summary>
/// Draws translucent models of furniture, turned as they would be built: the preview while
/// placing and the blueprints already placed (<c>docs/gdd/vista-previa-de-muebles.md</c>). A
/// preview also gets a flat arrow on the floor towards the front of the piece, the way a model
/// faces before it is turned (+Z, as glTF models face). Nodes are pooled per model and reused.
/// </summary>
public partial class FurnitureGhosts : Node3D
{
    // Presentation colours, not balance.
    private static readonly Color BlockedColor = new(0.9f, 0.15f, 0.12f);
    private static readonly Color ArrowColor = new(1f, 0.8f, 0.1f, 1f);
    private static readonly Color BlockedArrowColor = new(0.95f, 0.15f, 0.1f, 1f);

    private readonly Dictionary<string, List<(Node3D Node, IReadOnlyList<MeshInstance3D> Meshes)>> _pool = new(StringComparer.Ordinal);
    private readonly List<MeshInstance3D> _arrows = [];
    private ModelLibrary _models = null!;
    private Mesh _arrowMesh = null!;
    private StandardMaterial3D _arrowMaterial = null!;
    private StandardMaterial3D _blockedArrowMaterial = null!;

    /// <summary>How many pieces the last <see cref="Show"/> drew.</summary>
    public int Shown { get; private set; }

    public static FurnitureGhosts Create(VisualCatalog catalog, string name)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new FurnitureGhosts
        {
            Name = name,
            _models = new ModelLibrary(catalog),
            _arrowMesh = ArrowMesh(),
            _arrowMaterial = ArrowMaterial(ArrowColor),
            _blockedArrowMaterial = ArrowMaterial(BlockedArrowColor),
        };
    }

    /// <summary>Draws these pieces and nothing else. A visual the catalog lacks for the era is skipped.</summary>
    public void Show(IReadOnlyList<Ghost> ghosts, string? era)
    {
        ArgumentNullException.ThrowIfNull(ghosts);
        foreach (var node in _pool.Values.SelectMany(nodes => nodes))
        {
            node.Node.Visible = false;
        }

        foreach (var arrow in _arrows)
        {
            arrow.Visible = false;
        }

        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        var arrows = 0;
        Shown = 0;
        foreach (var ghost in ghosts)
        {
            if (!_models.Catalog.TryResolve(ghost.Visual, era, out var model))
            {
                continue;
            }

            var index = used.GetValueOrDefault(ghost.Visual);
            used[ghost.Visual] = index + 1;
            var (node, meshes) = Take(ghost.Visual, model, index);
            var color = ghost.Look == GhostLook.Blocked ? BlockedColor : (Color?)null;
            var alpha = ghost.Look == GhostLook.Planned ? 0.45f : 0.75f;

            // A preview is a cursor: it shows through what is in front, over the cursor's frame.
            var preview = ghost.Look is GhostLook.Preview or GhostLook.Blocked;
            _models.ShadeGhost(model, meshes, color, alpha, onTop: preview);
            var yaw = ghost.Rotation * 90f;
            node.Position = CellBase(ghost.Cell);
            node.RotationDegrees = new Vector3(0, yaw, 0);
            node.Visible = true;
            Shown++;

            if (preview)
            {
                var arrow = TakeArrow(arrows++);
                arrow.MaterialOverride = ghost.Look == GhostLook.Blocked ? _blockedArrowMaterial : _arrowMaterial;
                arrow.Position = CellBase(ghost.Cell);
                arrow.RotationDegrees = new Vector3(0, yaw, 0);
                arrow.Visible = true;
            }
        }
    }

    public override void _Notification(int what)
    {
        // On deletion, not on leaving the tree (see MapRenderer).
        if (what == NotificationPredelete)
        {
            _models.Dispose();
            _arrowMesh.Dispose();
            _arrowMaterial.Dispose();
            _blockedArrowMaterial.Dispose();
        }
    }

    /// <summary>The bottom of a cell, on its floor, where the models of entities stand.</summary>
    private static Vector3 CellBase(TileCoord cell) => new(cell.X + 0.5f, cell.Z + TerrainMeshes.FloorThickness, cell.Y + 0.5f);

    private (Node3D Node, IReadOnlyList<MeshInstance3D> Meshes) Take(string visual, VisualModel model, int index)
    {
        if (!_pool.TryGetValue(visual, out var nodes))
        {
            nodes = [];
            _pool.Add(visual, nodes);
        }

        if (index < nodes.Count)
        {
            return nodes[index];
        }

        var node = _models.Instantiate(model);
        AddChild(node);
        SetNoShadows(node);
        var entry = (node, ModelLibrary.MeshesOf(node));
        nodes.Add(entry);
        return entry;
    }

    private MeshInstance3D TakeArrow(int index)
    {
        if (index < _arrows.Count)
        {
            return _arrows[index];
        }

        var arrow = new MeshInstance3D { Mesh = _arrowMesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(arrow);
        _arrows.Add(arrow);
        return arrow;
    }

    /// <summary>A ghost casts no shadow: it is not there yet.</summary>
    private static void SetNoShadows(Node node)
    {
        if (node is GeometryInstance3D geometry)
        {
            geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }

        foreach (var child in node.GetChildren())
        {
            SetNoShadows(child);
        }
    }

    /// <summary>A flat arrow just above the floor, from the middle of the cell to its +Z edge.</summary>
    private static ArrayMesh ArrowMesh()
    {
        using var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        tool.SetNormal(Vector3.Up);
        const float y = 0.02f;
        Vector3[] points =
        [
            // The shaft, then the head.
            new(-0.08f, y, 0.05f), new(0.08f, y, 0.05f), new(0.08f, y, 0.28f),
            new(-0.08f, y, 0.05f), new(0.08f, y, 0.28f), new(-0.08f, y, 0.28f),
            new(-0.22f, y, 0.28f), new(0.22f, y, 0.28f), new(0f, y, 0.5f),
        ];
        foreach (var point in points)
        {
            tool.AddVertex(point);
        }

        return tool.Commit();
    }

    /// <summary>The arrow is drawn over everything, the model included, so it is never hidden under the piece.</summary>
    private static StandardMaterial3D ArrowMaterial(Color color) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = color,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        NoDepthTest = true,
        RenderPriority = 2,
    };
}
