using Godot;

namespace OurLife.Game.Rendering;

/// <summary>
/// The triangles of a model of the decoration (<see cref="DecorationModel"/>), with the colour of
/// each of its materials baked into the vertices: the catalog's palette, or the model's own
/// corrected (see <see cref="ModelLibrary"/>). The decoration of a chunk is then one mesh of a
/// single surface, drawn with one material and one draw call, darkened per level like the terrain.
/// </summary>
public sealed class DecorationMesh
{
    private DecorationMesh(Vector3[] vertices, Vector3[] normals, Color[] colors)
    {
        Vertices = vertices;
        Normals = normals;
        Colors = colors;
    }

    /// <summary>The corners of the triangles, three by three, scaled as the catalog says.</summary>
    public Vector3[] Vertices { get; }

    public Vector3[] Normals { get; }

    public Color[] Colors { get; }

    /// <summary>The triangles of a model, or null if it cannot be loaded.</summary>
    public static DecorationMesh? Bake(ModelLibrary library, DecorationModel decoration)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(decoration);
        var model = new VisualModel(decoration.Id, decoration.ModelPath, decoration.Scale, decoration.SrgbAsLinear);
        if (library.Load(model) is not { } scene)
        {
            return null;
        }

        var (vertices, normals, colors) = (new List<Vector3>(), new List<Vector3>(), new List<Color>());
        var root = scene.Instantiate<Node3D>();
        try
        {
            Append(library, root, Transform3D.Identity.Scaled(Vector3.One * decoration.Scale), decoration.SrgbAsLinear, vertices, normals, colors);
        }
        finally
        {
            root.Free();
        }

        return new DecorationMesh([.. vertices], [.. normals], [.. colors]);
    }

    /// <summary>
    /// One mesh with every piece placed: each model at each transform. Null when there are none.
    /// </summary>
    public static ArrayMesh? Combine(IReadOnlyList<DecorationMesh?> models, IReadOnlyList<(int Model, Transform3D Transform)> pieces)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(pieces);
        var count = pieces.Sum(piece => models[piece.Model]?.Vertices.Length ?? 0);
        if (count == 0)
        {
            return null;
        }

        var (vertices, normals, colors) = (new Vector3[count], new Vector3[count], new Color[count]);
        var at = 0;
        foreach (var (index, transform) in pieces)
        {
            if (models[index] is not { } model)
            {
                continue;
            }

            for (var i = 0; i < model.Vertices.Length; i++, at++)
            {
                vertices[at] = transform * model.Vertices[i];
                normals[at] = (transform.Basis * model.Normals[i]).Normalized();
                colors[at] = model.Colors[i];
            }
        }

        using var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>Adds the triangles of a node and its children, in the space of the model's root.</summary>
    private static void Append(
        ModelLibrary library, Node node, Transform3D parent, bool srgbAsLinear, List<Vector3> vertices, List<Vector3> normals, List<Color> colors)
    {
        var transform = node is Node3D spatial ? parent * spatial.Transform : parent;
        if (node is MeshInstance3D meshInstance)
        {
            using var mesh = meshInstance.Mesh;
            for (var surface = 0; mesh is not null && surface < mesh.GetSurfaceCount(); surface++)
            {
                using var material = mesh.SurfaceGetMaterial(surface);
                var color = material is null ? Godot.Colors.White : library.SurfaceColor(material, srgbAsLinear);
                using var arrays = mesh.SurfaceGetArrays(surface);
                var points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var pointNormals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
                var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                var count = indices.Length > 0 ? indices.Length : points.Length;
                for (var i = 0; i < count; i++)
                {
                    var vertex = indices.Length > 0 ? indices[i] : i;
                    vertices.Add(transform * points[vertex]);
                    normals.Add((transform.Basis * pointNormals[vertex]).Normalized());
                    colors.Add(color);
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            Append(library, child, transform, srgbAsLinear, vertices, normals, colors);
        }
    }
}
