using System.Text.Json;
using OurLife.Core.Defs;

namespace OurLife.Game.Rendering;

/// <summary>
/// A model of the catalog: the scene to instance, how to fit it into a cell, and whether its kit
/// stores sRGB colours in glTF's linear colour field, so they need correcting.
/// </summary>
/// <param name="Center">The model's origin is off its middle, like a corner: it is centred on the cell when instanced.</param>
/// <param name="Variants">
/// Other files of the same model by a tag, like a colonist's gender: the renderer picks one of
/// the tag's files per entity, and the plain <paramref name="ModelPath"/> without a tag.
/// </param>
/// <param name="Parts">More models drawn with this one, like berries on a bush.</param>
/// <param name="Turn">
/// Quarter turns, counterclockwise seen from above, that bring the model's front to +Z, where the
/// furniture preview's arrow points before it is turned (<c>docs/gdd/vista-previa-de-muebles.md</c>).
/// </param>
/// <param name="BoneScales">How much bigger some bones of the model's skeleton are drawn, by name, like the head of a child.</param>
/// <param name="Animations">
/// The model's own animation for an animation the renderer asks for, by name, like a baby that
/// crawls (<c>crouch</c>) when others walk; one left out plays as asked.
/// </param>
public sealed record VisualModel(
    string Id,
    string ModelPath,
    float Scale,
    bool SrgbAsLinear = false,
    bool Center = false,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Variants = null,
    IReadOnlyList<VisualPart>? Parts = null,
    int Turn = 0,
    IReadOnlyDictionary<string, float>? BoneScales = null,
    IReadOnlyDictionary<string, string>? Animations = null)
{
    /// <summary>The animation the model plays when <paramref name="name"/> is asked for.</summary>
    public string AnimationFor(string name) => Animations is not null && Animations.TryGetValue(name, out var own) ? own : name;

    /// <summary>Every model file this visual draws with: its own, its variants and its parts.</summary>
    public IEnumerable<string> Files =>
        new[] { ModelPath }
            .Concat(Variants?.Values.SelectMany(paths => paths) ?? [])
            .Concat(Parts?.Select(part => part.ModelPath) ?? []);
}

/// <summary>
/// A model drawn with another, placed by an offset from the other's origin, in cells, and with
/// its own scale: neither is affected by the other's scale.
/// </summary>
public sealed record VisualPart(string ModelPath, float X, float Y, float Z, float Scale);

/// <summary>
/// How the terrain shader paints a material (ADR-012): with only its colour and tone, or with
/// the grain of soil, the specks and cracks of granite, the strata of sedimentary rock, planks or bricks.
/// The values are those of the shader.
/// </summary>
public enum TerrainPattern
{
    Plain = 0,
    Soil = 1,
    Speckled = 2,
    Layered = 3,
    Planks = 5,
    Bricks = 6,
}

/// <summary>The three greens the shader mixes into grass, as #RRGGBB: the usual one, patches of dry grass and of deep grass.</summary>
public sealed record GrassColors(string Lush, string Dry, string Deep);

/// <summary>
/// A model scattered on grass to dress it, like a tuft or a flower: presentation only, never an
/// entity. <paramref name="Chance"/> is how many grass cells in a thousand get it.
/// </summary>
public sealed record DecorationModel(string Id, string ModelPath, float Scale, bool SrgbAsLinear, int Chance);

/// <summary>
/// The visual catalog (<c>assets/models/visuals.json</c>, ADR-009 and ADR-012): the colours of
/// the grass and of the cut, the pattern the terrain shader paints each material with, colours
/// that replace those of the kits, the kits whose colours need correcting, the models that dress
/// the grass, and the model of every visual id that defs name in their <c>visual</c> field.
/// An id may have era variants such as <c>house@modern</c>; without one, the plain id is used.
/// Plain C# without Godot, so the content tests check the real catalog. Unknown fields are errors.
/// </summary>
public sealed class VisualCatalog
{
    public const string ResourcePath = "res://assets/models/visuals.json";

    private readonly Dictionary<string, VisualModel> _models;
    private readonly List<string> _kits;

    private VisualCatalog(
        Dictionary<string, VisualModel> models,
        Dictionary<string, string> palette,
        GrassColors grass,
        string cutColor,
        Dictionary<string, TerrainPattern> patterns,
        List<DecorationModel> decorations,
        List<string> kits)
    {
        _models = models;
        _kits = kits;
        Palette = palette;
        Grass = grass;
        CutColor = cutColor;
        Patterns = patterns;
        Decorations = decorations;
    }

    /// <summary>
    /// Colours that replace those of the kits, by the name of the material inside the models
    /// (<c>leafsGreen</c>, <c>woodBark</c>), as #RRGGBB. They keep the palette of the era sober.
    /// </summary>
    public IReadOnlyDictionary<string, string> Palette { get; }

    /// <summary>The colours of the grass (ADR-012).</summary>
    public GrassColors Grass { get; }

    /// <summary>The pattern of each material, by the id of its def (ADR-012).</summary>
    public IReadOnlyDictionary<string, TerrainPattern> Patterns { get; }

    /// <summary>The models that dress the grass, in the order of the catalog.</summary>
    public IReadOnlyList<DecorationModel> Decorations { get; }

    /// <summary>Colour of rock and soil cut open by the view at the current level, as #RRGGBB.</summary>
    public string CutColor { get; }

    /// <summary>Every model, sorted by id.</summary>
    public IEnumerable<VisualModel> Models => _models.Values.OrderBy(model => model.Id, StringComparer.Ordinal);

    /// <exception cref="FormatException">The JSON is invalid or misses a field; the message says which.</exception>
    public static VisualCatalog Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            OnlyFields(root, "catalog", "terrain", "srgbAsLinearKits", "palette", "decoration", "models");
            var kits = new List<string>();
            if (root.TryGetProperty("srgbAsLinearKits", out var kitsElement))
            {
                foreach (var kit in kitsElement.EnumerateArray())
                {
                    var folder = StringValue(kit, "srgbAsLinearKits");
                    kits.Add(folder.EndsWith('/') ? folder : throw new FormatException($"srgbAsLinearKits: '{folder}' must end with '/'."));
                }
            }

            var terrain = root.GetProperty("terrain");
            OnlyFields(terrain, "terrain", "cutColor", "grass", "materials");
            var grassElement = terrain.GetProperty("grass");
            OnlyFields(grassElement, "terrain.grass", "lush", "dry", "deep");
            var grass = new GrassColors(ColorField(grassElement, "lush"), ColorField(grassElement, "dry"), ColorField(grassElement, "deep"));
            var patterns = new Dictionary<string, TerrainPattern>(StringComparer.Ordinal);
            foreach (var entry in terrain.GetProperty("materials").EnumerateObject())
            {
                var name = StringValue(entry.Value, $"terrain.materials.{entry.Name}");

                // By name only: a number would be read as any value of the enum.
                patterns.Add(entry.Name, Enum.GetNames<TerrainPattern>().FirstOrDefault(pattern => pattern.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } known
                    ? Enum.Parse<TerrainPattern>(known)
                    : throw new FormatException($"terrain.materials.{entry.Name}: unknown pattern '{name}'."));
            }

            var decorations = new List<DecorationModel>();
            if (root.TryGetProperty("decoration", out var decorationElement))
            {
                foreach (var entry in decorationElement.EnumerateObject())
                {
                    OnlyFields(entry.Value, $"decoration '{entry.Name}'", "model", "scale", "chance");
                    var path = StringValue(entry.Value.GetProperty("model"), $"{entry.Name}.model");
                    var scale = entry.Value.TryGetProperty("scale", out var scaleValue) ? scaleValue.GetSingle() : 1f;
                    var chance = entry.Value.GetProperty("chance").GetInt32();
                    if (scale <= 0 || chance <= 0)
                    {
                        throw new FormatException($"decoration '{entry.Name}': scale and chance must be positive.");
                    }

                    decorations.Add(new DecorationModel(entry.Name, path, scale, kits.Exists(kit => path.StartsWith(kit, StringComparison.Ordinal)), chance));
                }
            }

            if (decorations.Sum(decoration => (long)decoration.Chance) > 1000)
            {
                throw new FormatException("decoration: the chances add up to more than 1000 in a thousand.");
            }

            var models = new Dictionary<string, VisualModel>(StringComparer.Ordinal);
            foreach (var entry in root.GetProperty("models").EnumerateObject())
            {
                OnlyFields(entry.Value, $"'{entry.Name}'", "model", "scale", "center", "variants", "parts", "turn", "boneScales", "animations");
                var scale = entry.Value.TryGetProperty("scale", out var scaleValue) ? scaleValue.GetSingle() : 1f;
                if (scale <= 0)
                {
                    throw new FormatException($"'{entry.Name}': scale must be positive.");
                }

                var path = StringValue(entry.Value.GetProperty("model"), $"{entry.Name}.model");
                var srgbAsLinear = kits.Exists(kit => path.StartsWith(kit, StringComparison.Ordinal));
                var center = entry.Value.TryGetProperty("center", out var centerValue) && centerValue.GetBoolean();
                Dictionary<string, IReadOnlyList<string>>? variants = null;
                if (entry.Value.TryGetProperty("variants", out var variantsElement))
                {
                    variants = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
                    foreach (var tag in variantsElement.EnumerateObject())
                    {
                        var files = tag.Value.EnumerateArray().Select(file => StringValue(file, $"{entry.Name}.variants.{tag.Name}")).ToList();
                        variants.Add(tag.Name, files.Count > 0 ? files : throw new FormatException($"'{entry.Name}': the variants '{tag.Name}' are empty."));
                    }
                }

                List<VisualPart>? parts = null;
                if (entry.Value.TryGetProperty("parts", out var partsElement))
                {
                    parts = [];
                    foreach (var part in partsElement.EnumerateArray())
                    {
                        OnlyFields(part, $"'{entry.Name}' part", "model", "offset", "scale");
                        var offset = part.GetProperty("offset").EnumerateArray().Select(value => value.GetSingle()).ToArray();
                        var partScale = part.TryGetProperty("scale", out var partScaleValue) ? partScaleValue.GetSingle() : 1f;
                        if (offset.Length != 3 || partScale <= 0)
                        {
                            throw new FormatException($"'{entry.Name}': a part needs an offset of three numbers and a positive scale.");
                        }

                        parts.Add(new VisualPart(StringValue(part.GetProperty("model"), $"{entry.Name}.parts.model"), offset[0], offset[1], offset[2], partScale));
                    }
                }

                var turn = entry.Value.TryGetProperty("turn", out var turnValue) ? turnValue.GetInt32() : 0;
                if (turn is < 0 or > 3)
                {
                    throw new FormatException($"'{entry.Name}': turn must be from 0 to 3 quarter turns.");
                }

                Dictionary<string, float>? boneScales = null;
                if (entry.Value.TryGetProperty("boneScales", out var bonesElement))
                {
                    boneScales = new Dictionary<string, float>(StringComparer.Ordinal);
                    foreach (var bone in bonesElement.EnumerateObject())
                    {
                        var factor = bone.Value.GetSingle();
                        boneScales.Add(bone.Name, factor > 0 ? factor : throw new FormatException($"'{entry.Name}': the scale of bone '{bone.Name}' must be positive."));
                    }
                }

                Dictionary<string, string>? animations = null;
                if (entry.Value.TryGetProperty("animations", out var animationsElement))
                {
                    animations = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var animation in animationsElement.EnumerateObject())
                    {
                        animations.Add(animation.Name, StringValue(animation.Value, $"{entry.Name}.animations.{animation.Name}"));
                    }
                }

                models.Add(entry.Name, new VisualModel(entry.Name, path, scale, srgbAsLinear, center, variants, parts, turn, boneScales, animations));
            }

            var palette = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("palette", out var paletteElement))
            {
                foreach (var entry in paletteElement.EnumerateObject())
                {
                    palette.Add(entry.Name, ColorValue(entry.Value, entry.Name));
                }
            }

            return new VisualCatalog(models, palette, grass, ColorField(terrain, "cutColor"), patterns, decorations, kits);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            throw new FormatException($"Invalid visual catalog: {ex.Message}", ex);
        }
    }

    /// <summary>Whether a model file comes from a kit that stores its colours in glTF's linear field (<c>srgbAsLinearKits</c>).</summary>
    public bool NeedsColourCorrection(string path) => _kits.Exists(kit => path.StartsWith(kit, StringComparison.Ordinal));

    /// <summary>The model for an id in an era: the <c>id@era</c> variant if there is one, else the id.</summary>
    public bool TryResolve(string id, string? era, out VisualModel model)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (era is not null && _models.TryGetValue($"{id}@{era}", out model!))
        {
            return true;
        }

        return _models.TryGetValue(id, out model!);
    }

    private static string ColorField(JsonElement element, string name) => ColorValue(element.GetProperty(name), name);

    private static string ColorValue(JsonElement element, string name)
    {
        var value = StringValue(element, name);
        return MaterialDef.IsHexColor(value) ? value : throw new FormatException($"'{name}': '{value}' is not a #RRGGBB colour.");
    }

    /// <summary>Rejects the fields this catalog does not know, so a misspelt one is not silently ignored.</summary>
    private static void OnlyFields(JsonElement element, string where, params string[] known)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!known.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new FormatException($"{where}: unknown field '{property.Name}'.");
            }
        }
    }

    /// <summary>A JSON string; null, numbers and the rest are an error that names the field.</summary>
    private static string StringValue(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.String
            ? element.GetString()!
            : throw new FormatException($"'{name}' must be a string, found {element.ValueKind}.");
}
