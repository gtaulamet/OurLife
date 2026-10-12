using Godot;

namespace OurLife.Game.Rendering;

/// <summary>
/// Loads the models of the visual catalog and instances them. The Kenney kits export flat, fully
/// metallic materials with washed-out colours; every surface gets a matte copy instead, with the
/// colour of the catalog's palette or else the model's own (corrected for the kits the catalog
/// names), darkened for the depth below the current level (<see cref="LevelShading"/>). Copies
/// are cached and shared.
/// </summary>
public sealed class ModelLibrary : IDisposable
{
    private readonly Dictionary<string, PackedScene> _scenes = new(StringComparer.Ordinal);
    private readonly Dictionary<(Material Source, int Depth), Material> _shaded = [];

    public ModelLibrary(VisualCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Catalog = catalog;
    }

    public VisualCatalog Catalog { get; }

    /// <summary>The scene of a model, or null if it cannot be loaded (for example, not imported yet).</summary>
    public PackedScene? Load(VisualModel model)
    {
        if (!_scenes.TryGetValue(model.ModelPath, out var scene))
        {
            scene = ResourceLoader.Exists(model.ModelPath) ? GD.Load<PackedScene>(model.ModelPath) : null;
            if (scene is null)
            {
                return null;
            }

            _scenes.Add(model.ModelPath, scene);
        }

        return scene;
    }

    /// <summary>
    /// A new instance of a model, scaled as the catalog says. A model to centre goes inside a holder
    /// node, moved so the middle of its meshes lies over the holder's origin.
    /// </summary>
    /// <exception cref="InvalidOperationException">The model cannot be loaded.</exception>
    public Node3D Instantiate(VisualModel model)
    {
        var scene = Load(model) ?? throw new InvalidOperationException(
            $"Cannot load the model of '{model.Id}' ({model.ModelPath}). Import the project: godot --headless --path game --import");
        var node = scene.Instantiate<Node3D>();
        if (!model.Center)
        {
            node.Scale = Vector3.One * model.Scale;
            return node;
        }

        var middle = BoundsOf(node, Transform3D.Identity)?.GetCenter() ?? Vector3.Zero;
        node.Position -= new Vector3(middle.X, 0, middle.Z);
        var holder = new Node3D { Name = node.Name, Scale = Vector3.One * model.Scale };
        holder.AddChild(node);
        return holder;
    }

    /// <summary>The box around the meshes under a node, in the space <paramref name="toParent"/> leads to.</summary>
    private static Aabb? BoundsOf(Node node, Transform3D toParent)
    {
        var transform = node is Node3D spatial ? toParent * spatial.Transform : toParent;
        Aabb? bounds = null;
        if (node is MeshInstance3D { Mesh: not null } mesh)
        {
            bounds = transform * mesh.GetAabb();
        }

        foreach (var child in node.GetChildren())
        {
            if (BoundsOf(child, transform) is { } inner)
            {
                bounds = bounds is { } box ? box.Merge(inner) : inner;
            }
        }

        return bounds;
    }

    /// <summary>The mesh instances of a model instance, to shade them later without walking its nodes.</summary>
    public static IReadOnlyList<MeshInstance3D> MeshesOf(Node3D instance)
    {
        var meshes = new List<MeshInstance3D>();
        CollectMeshes(instance, meshes);
        return meshes;
    }

    /// <summary>Gives every surface of the meshes of an instance of a model its lit material for that depth.</summary>
    public void Shade(VisualModel model, IReadOnlyList<MeshInstance3D> meshes, int depth)
    {
        foreach (var meshInstance in meshes)
        {
            // The node keeps the mesh; the materials are released in Dispose.
            using var mesh = meshInstance.Mesh;
            if (mesh is null)
            {
                continue;
            }

            for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                var source = mesh.SurfaceGetMaterial(surface);
                if (source is not null)
                {
                    meshInstance.SetSurfaceOverrideMaterial(surface, ShadedCopy(source, depth, model.SrgbAsLinear));
                }
            }
        }
    }

    /// <summary>
    /// Releases the C# references to the scenes and materials. Godot .NET crashes on exit if C#
    /// still holds references to resources; the nodes keep their own.
    /// </summary>
    public void Dispose()
    {
        // A source material appears once per depth, and an unconverted one is its own copy.
        var resources = new HashSet<Resource>(_scenes.Values);
        foreach (var ((source, _), shaded) in _shaded)
        {
            resources.Add(source);
            resources.Add(shaded);
        }

        foreach (var resource in resources)
        {
            resource.Dispose();
        }

        _shaded.Clear();
        _scenes.Clear();
    }

    private static void CollectMeshes(Node node, List<MeshInstance3D> meshes)
    {
        // The mesh itself is read when shading, so no C# reference to it stays alive here.
        if (node is MeshInstance3D mesh)
        {
            meshes.Add(mesh);
        }

        foreach (var child in node.GetChildren())
        {
            CollectMeshes(child, meshes);
        }
    }

    /// <summary>
    /// A lit copy of a material for a depth. A source material belongs to one model file, and so
    /// to one kit, so the cache does not need the colour correction in its key.
    /// </summary>
    private Material ShadedCopy(Material source, int depth, bool srgbAsLinear)
    {
        if (_shaded.TryGetValue((source, depth), out var cached))
        {
            return cached;
        }

        var brightness = LevelShading.Brightness(depth);
        Material shaded = source;
        if (source is BaseMaterial3D original)
        {
            // Some kits store their sRGB palette in glTF's linear colour field, and the importer
            // converts it to sRGB once more, which washes it out: undo that conversion.
            using var texture = original.AlbedoTexture;
            var color = Catalog.Palette.TryGetValue(source.ResourceName, out var replacement)
                ? Color.FromHtml(replacement)
                : srgbAsLinear ? original.AlbedoColor.SrgbToLinear() : original.AlbedoColor;
            shaded = new StandardMaterial3D
            {
                AlbedoColor = color * new Color(brightness, brightness, brightness),
                AlbedoTexture = texture,
                TextureFilter = original.TextureFilter,
                Transparency = original.Transparency,
                CullMode = original.CullMode,
                Metallic = 0f,
                Roughness = 1f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
            };
        }

        _shaded.Add((source, depth), shaded);
        return shaded;
    }
}
