using Godot;

namespace OurLife.Game.Rendering;

/// <summary>The pieces the terrain is drawn with, one cell wide (ADR-009).</summary>
public enum TerrainPiece
{
    /// <summary>A full block of soil or rock.</summary>
    Block,

    /// <summary>A thin slab on the bottom of an open cell.</summary>
    Floor,

    /// <summary>A wedge that rises towards +X; rotated towards the higher neighbour.</summary>
    Ramp,

    /// <summary>Steps that rise towards +X.</summary>
    Steps,

    /// <summary>A dark slab: the opening of stairs that go down.</summary>
    Hole,

    /// <summary>A dark slab on top of a block that the view cuts at the current level.</summary>
    Cut,
}

/// <summary>
/// Builds the meshes of the terrain pieces in code. Their origin is the centre of the bottom of
/// the cell, so a piece goes at <c>(x + 0.5, z, y + 0.5)</c>. Colour comes from each instance.
/// </summary>
public static class TerrainMeshes
{
    public const float FloorThickness = 0.06f;

    public static Mesh Create(TerrainPiece piece) => piece switch
    {
        TerrainPiece.Block => Boxes((new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 1, 0.5f))),
        TerrainPiece.Floor or TerrainPiece.Hole => Boxes((new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, FloorThickness, 0.5f))),
        TerrainPiece.Cut => Boxes((new Vector3(-0.5f, 1, -0.5f), new Vector3(0.5f, 1.02f, 0.5f))),
        TerrainPiece.Steps => Boxes(
            (new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0.34f, 0.5f)),
            (new Vector3(-0.17f, 0.34f, -0.5f), new Vector3(0.5f, 0.67f, 0.5f)),
            (new Vector3(0.17f, 0.67f, -0.5f), new Vector3(0.5f, 1f, 0.5f))),
        TerrainPiece.Ramp => Ramp(),
        _ => throw new ArgumentOutOfRangeException(nameof(piece), piece, null),
    };

    private static ArrayMesh Boxes(params (Vector3 Min, Vector3 Max)[] boxes)
    {
        using var tool = Begin();
        foreach (var (min, max) in boxes)
        {
            AddQuad(tool, Vector3.Up, new(min.X, max.Y, min.Z), new(max.X, max.Y, min.Z), new(max.X, max.Y, max.Z), new(min.X, max.Y, max.Z));
            AddQuad(tool, Vector3.Down, new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z), new(max.X, min.Y, max.Z), new(min.X, min.Y, max.Z));
            AddQuad(tool, Vector3.Right, new(max.X, min.Y, min.Z), new(max.X, max.Y, min.Z), new(max.X, max.Y, max.Z), new(max.X, min.Y, max.Z));
            AddQuad(tool, Vector3.Left, new(min.X, min.Y, min.Z), new(min.X, max.Y, min.Z), new(min.X, max.Y, max.Z), new(min.X, min.Y, max.Z));
            AddQuad(tool, Vector3.Back, new(min.X, min.Y, max.Z), new(max.X, min.Y, max.Z), new(max.X, max.Y, max.Z), new(min.X, max.Y, max.Z));
            AddQuad(tool, Vector3.Forward, new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z), new(max.X, max.Y, min.Z), new(min.X, max.Y, min.Z));
        }

        return tool.Commit();
    }

    private static ArrayMesh Ramp()
    {
        Vector3 lowNear = new(-0.5f, 0, -0.5f), lowFar = new(-0.5f, 0, 0.5f);
        Vector3 baseNear = new(0.5f, 0, -0.5f), baseFar = new(0.5f, 0, 0.5f);
        Vector3 topNear = new(0.5f, 1, -0.5f), topFar = new(0.5f, 1, 0.5f);

        using var tool = Begin();
        AddQuad(tool, new Vector3(-1, 1, 0).Normalized(), lowNear, topNear, topFar, lowFar);
        AddQuad(tool, Vector3.Right, baseNear, topNear, topFar, baseFar);
        AddQuad(tool, Vector3.Down, lowNear, baseNear, baseFar, lowFar);
        AddTriangle(tool, Vector3.Forward, lowNear, baseNear, topNear);
        AddTriangle(tool, Vector3.Back, lowFar, baseFar, topFar);
        return tool.Commit();
    }

    private static SurfaceTool Begin()
    {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        return tool;
    }

    /// <summary>A quad given by its corners in order around the edge.</summary>
    private static void AddQuad(SurfaceTool tool, Vector3 normal, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        AddTriangle(tool, normal, a, b, c);
        AddTriangle(tool, normal, a, c, d);
    }

    /// <summary>
    /// Adds a flat-shaded triangle facing <paramref name="normal"/>. Godot treats clockwise
    /// triangles as front faces, so the corners are swapped when needed.
    /// </summary>
    private static void AddTriangle(SurfaceTool tool, Vector3 normal, Vector3 a, Vector3 b, Vector3 c)
    {
        if ((b - a).Cross(c - a).Dot(normal) > 0)
        {
            (b, c) = (c, b);
        }

        foreach (var vertex in new[] { a, b, c })
        {
            tool.SetNormal(normal);
            tool.AddVertex(vertex);
        }
    }
}
