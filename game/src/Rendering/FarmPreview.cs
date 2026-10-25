using Godot;
using OurLife.Core.Map;

namespace OurLife.Game.Rendering;

/// <summary>
/// Shows the cells where the farm tool would draw a zone before the drag ends (see "Zonas de
/// cultivo" in <c>docs/gdd/agricultura.md</c>): a thin translucent green slab over each valid
/// cell, so the player sees what the box takes and what it leaves out. The cells come from
/// <c>FarmRules.CellsToZone</c>, the same query the command uses; this node only draws them.
/// </summary>
public partial class FarmPreview : Node3D
{
    // A large box shows no more than this many slabs.
    private const int MaxCells = 4096;

    private static readonly Color ValidColor = new(0.45f, 0.95f, 0.35f, 0.55f);

    private MultiMeshInstance3D _instance = null!;
    private int _shown;

    public static FarmPreview Create()
    {
        var preview = new FarmPreview { Name = nameof(FarmPreview) };
        using var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        // The node keeps its own reference to the material (see MapRenderer).
        preview._instance = new MultiMeshInstance3D { Name = "Cells", MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        preview.AddChild(preview._instance);
        return preview;
    }

    /// <summary>How many cells are drawn now, for the smoke.</summary>
    public int ShownCells => _shown;

    /// <summary>Draws the cells the view shows, or nothing for an empty list.</summary>
    public void Show(IReadOnlyList<TileCoord> cells, int viewLevel)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var count = 0;
        foreach (var cell in cells)
        {
            count += LevelShading.IsVisible(cell.Z, viewLevel) ? 1 : 0;
        }

        count = Math.Min(count, MaxCells);
        _shown = count;
        if (count == 0)
        {
            _instance.Multimesh = null;
            return;
        }

        using var box = new BoxMesh();
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = box,
            InstanceCount = count,
        };

        var index = 0;
        foreach (var cell in cells)
        {
            if (index == count)
            {
                break;
            }

            if (!LevelShading.IsVisible(cell.Z, viewLevel))
            {
                continue;
            }

            var floor = new Vector3(cell.X + 0.5f, cell.Z + TerrainMeshes.FloorThickness + 0.03f, cell.Y + 0.5f);
            multimesh.SetInstanceTransform(index, new Transform3D(Basis.Identity.Scaled(new Vector3(0.92f, 0.03f, 0.92f)), floor));
            multimesh.SetInstanceColor(index, ValidColor);
            index++;
        }

        _instance.Multimesh = multimesh;

        // The node keeps its own reference (see MapRenderer).
        multimesh.Dispose();
    }
}
