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
    private readonly Dictionary<string, Texture2D?> _textures = new(StringComparer.Ordinal);
    private readonly Dictionary<(Material Source, int Depth, bool SrgbAsLinear), Material> _shaded = [];
    private readonly Dictionary<(Material Source, Color? Color, float Alpha, bool OnTop), Material> _ghosts = [];

    public ModelLibrary(VisualCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Catalog = catalog;
    }

    public VisualCatalog Catalog { get; }

    /// <summary>The scene of a model file, or null if it cannot be loaded, cached by its path.</summary>
    public PackedScene? Load(string path)
    {
        if (!_scenes.TryGetValue(path, out var scene))
        {
            scene = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
            if (scene is null)
            {
                return null;
            }

            _scenes.Add(path, scene);
        }

        return scene;
    }

    /// <summary>A texture, like the icon of a balloon, or null if there is none at the path; cached and released with the library.</summary>
    public Texture2D? LoadTexture(string path)
    {
        if (!_textures.TryGetValue(path, out var texture))
        {
            texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            _textures.Add(path, texture);
        }

        return texture;
    }

    /// <summary>
    /// A new instance of a model, scaled as the catalog says, from its own file or from
    /// <paramref name="file"/>, one of its variants. A model to centre goes inside a holder node,
    /// moved so the middle of its meshes lies over the holder's origin. A model with parts goes
    /// inside an unscaled node, with each part at its offset and scale. Bones the catalog scales,
    /// like a child's head, are scaled in the skeleton.
    /// </summary>
    /// <exception cref="InvalidOperationException">The model or a part cannot be loaded.</exception>
    public Node3D Instantiate(VisualModel model, string? file = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        var body = InstantiateBody(model, file ?? model.ModelPath);
        if (model.BoneScales is { Count: > 0 } bones)
        {
            ScaleBones(body, bones);
        }

        // Turned inside a holder: whoever places the instance sets the holder's own rotation.
        if (model.Turn != 0)
        {
            var turned = new Node3D { Name = body.Name };
            body.RotationDegrees = new Vector3(0, model.Turn * 90f, 0);
            turned.AddChild(body);
            body = turned;
        }

        if (model.Parts is not { Count: > 0 } parts)
        {
            return body;
        }

        var whole = new Node3D { Name = body.Name };
        whole.AddChild(body);
        foreach (var part in parts)
        {
            var node = LoadOrThrow(model, part.ModelPath).Instantiate<Node3D>();
            node.Scale = Vector3.One * part.Scale;
            node.Position = new Vector3(part.X, part.Y, part.Z);
            whole.AddChild(node);
        }

        return whole;
    }

    /// <summary>
    /// Scales bones of the instance's skeleton, around their own joint. The rest is scaled too:
    /// an animation that does not move a bone's scale leaves it at its rest, so it holds while the
    /// colonist walks or works. Skinned meshes follow the bones; a bone the model lacks is skipped.
    /// </summary>
    private static void ScaleBones(Node node, IReadOnlyDictionary<string, float> bones)
    {
        if (node is Skeleton3D skeleton)
        {
            foreach (var (name, factor) in bones)
            {
                var bone = skeleton.FindBone(name);
                if (bone < 0)
                {
                    continue;
                }

                var rest = skeleton.GetBoneRest(bone);
                skeleton.SetBoneRest(bone, new Transform3D(rest.Basis.Scaled(Vector3.One * factor), rest.Origin));
                skeleton.SetBonePoseScale(bone, skeleton.GetBonePoseScale(bone) * factor);
            }
        }

        foreach (var child in node.GetChildren())
        {
            ScaleBones(child, bones);
        }
    }

    private PackedScene LoadOrThrow(VisualModel model, string path) =>
        Load(path) ?? throw new InvalidOperationException(
            $"Cannot load the model of '{model.Id}' ({path}). Import the project: godot --headless --path game --import");

    private Node3D InstantiateBody(VisualModel model, string path)
    {
        var node = LoadOrThrow(model, path).Instantiate<Node3D>();
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

    /// <summary>The box around the meshes of an instance, in its parent's space: with its scale, before it is placed.</summary>
    public static Aabb? BoundsOf(Node3D instance) => BoundsOf(instance, Transform3D.Identity);

    /// <summary>
    /// The height of an instance as drawn, in its parent's space. The meshes are measured as
    /// modelled, so a bone the catalog enlarges (<see cref="VisualModel.BoneScales"/>), like a
    /// child's head, raises the top by what lies over its joint times its growth.
    /// </summary>
    public static float HeightOf(Node3D instance, VisualModel model)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(model);
        if (BoundsOf(instance) is not { } bounds)
        {
            return 0f;
        }

        var top = bounds.End.Y;
        if (model.BoneScales is { Count: > 0 } bones && FindSkeleton(instance, Transform3D.Identity) is { } found)
        {
            foreach (var (name, factor) in bones)
            {
                var bone = found.Skeleton.FindBone(name);
                if (bone >= 0)
                {
                    var joint = (found.ToParent * found.Skeleton.GetBoneGlobalRest(bone)).Origin.Y;
                    top = Math.Max(top, joint + ((bounds.End.Y - joint) * factor));
                }
            }
        }

        return top - bounds.Position.Y;
    }

    /// <summary>The first skeleton under a node, with the transform from its space to the node's parent's.</summary>
    private static (Skeleton3D Skeleton, Transform3D ToParent)? FindSkeleton(Node node, Transform3D toParent)
    {
        var transform = node is Node3D spatial ? toParent * spatial.Transform : toParent;
        if (node is Skeleton3D skeleton)
        {
            return (skeleton, transform);
        }

        foreach (var child in node.GetChildren())
        {
            if (FindSkeleton(child, transform) is { } found)
            {
                return found;
            }
        }

        return null;
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
    public void Shade(VisualModel model, IReadOnlyList<MeshInstance3D> meshes, int depth) =>
        Override(meshes, source => ShadedCopy(source, depth, model.SrgbAsLinear));

    /// <summary>
    /// Gives every surface of an instance a translucent copy of its material, for the previews and
    /// blueprints of furniture (<c>docs/gdd/vista-previa-de-muebles.md</c>): its own colour and
    /// texture, or a flat <paramref name="color"/> instead, with <paramref name="alpha"/>. One
    /// <paramref name="onTop"/> is drawn over everything, after the frame of the cursor, which
    /// would otherwise wash it out.
    /// </summary>
    public void ShadeGhost(VisualModel model, IReadOnlyList<MeshInstance3D> meshes, Color? color, float alpha, bool onTop) =>
        Override(meshes, source => GhostCopy(source, model.SrgbAsLinear, color, alpha, onTop));

    /// <summary>Gives every surface of the meshes the material <paramref name="copyOf"/> makes from its own.</summary>
    private static void Override(IReadOnlyList<MeshInstance3D> meshes, Func<Material, Material> copyOf)
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
                    meshInstance.SetSurfaceOverrideMaterial(surface, copyOf(source));
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
        resources.UnionWith(_textures.Values.OfType<Texture2D>());
        foreach (var ((source, _, _), shaded) in _shaded)
        {
            resources.Add(source);
            resources.Add(shaded);
        }

        foreach (var ((source, _, _, _), ghost) in _ghosts)
        {
            resources.Add(source);
            resources.Add(ghost);
        }

        foreach (var resource in resources)
        {
            resource.Dispose();
        }

        _shaded.Clear();
        _ghosts.Clear();
        _scenes.Clear();
        _textures.Clear();
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
    /// A lit copy of a material for a depth and a colour correction. The same file may be drawn
    /// alone and as a part of another model, so the correction is part of the cache's key.
    /// </summary>
    private Material ShadedCopy(Material source, int depth, bool srgbAsLinear)
    {
        if (_shaded.TryGetValue((source, depth, srgbAsLinear), out var cached))
        {
            return cached;
        }

        var brightness = LevelShading.Brightness(depth);
        Material shaded = source;
        if (source is BaseMaterial3D original)
        {
            using var texture = original.AlbedoTexture;
            shaded = new StandardMaterial3D
            {
                AlbedoColor = ColorOf(original, srgbAsLinear) * new Color(brightness, brightness, brightness),
                AlbedoTexture = texture,
                TextureFilter = original.TextureFilter,
                Transparency = original.Transparency,
                CullMode = original.CullMode,
                Metallic = 0f,
                Roughness = 1f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
            };
        }

        _shaded.Add((source, depth, srgbAsLinear), shaded);
        return shaded;
    }

    /// <summary>A translucent copy of a material, cached; one that is not a <see cref="BaseMaterial3D"/> stays as it is.</summary>
    private Material GhostCopy(Material source, bool srgbAsLinear, Color? color, float alpha, bool onTop)
    {
        if (_ghosts.TryGetValue((source, color, alpha, onTop), out var cached))
        {
            return cached;
        }

        Material ghost = source;
        if (source is BaseMaterial3D original)
        {
            // A flat colour, like the red of a cell where the piece cannot go, hides the texture.
            using var texture = color is null ? original.AlbedoTexture : null;
            var albedo = color ?? ColorOf(original, srgbAsLinear);
            ghost = new StandardMaterial3D
            {
                AlbedoColor = new Color(albedo, alpha),
                AlbedoTexture = texture,
                TextureFilter = original.TextureFilter,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = original.CullMode,
                Metallic = 0f,
                Roughness = 1f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
                NoDepthTest = onTop,
                RenderPriority = onTop ? 1 : 0,
            };
        }

        _ghosts.Add((source, color, alpha, onTop), ghost);
        return ghost;
    }

    /// <summary>The colour a material of a model is drawn with (see <see cref="ColorOf"/>); white for one that is not a <see cref="BaseMaterial3D"/>.</summary>
    public Color SurfaceColor(Material material, bool srgbAsLinear)
    {
        ArgumentNullException.ThrowIfNull(material);
        return material is BaseMaterial3D original ? ColorOf(original, srgbAsLinear) : Colors.White;
    }

    /// <summary>
    /// The colour of a model's material: the catalog's palette for its name, or its own. Some kits
    /// store their sRGB palette in glTF's linear colour field, and the importer converts it to sRGB
    /// once more, which washes it out: that conversion is undone for them.
    /// </summary>
    private Color ColorOf(BaseMaterial3D original, bool srgbAsLinear) =>
        Catalog.Palette.TryGetValue(original.ResourceName, out var replacement)
            ? Color.FromHtml(replacement)
            : srgbAsLinear ? original.AlbedoColor.SrgbToLinear() : original.AlbedoColor;
}
