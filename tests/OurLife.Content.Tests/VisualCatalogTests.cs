using System.Text.Json;
using OurLife.Core.Defs;
using OurLife.Game.Rendering;

namespace OurLife.Content.Tests;

/// <summary>
/// The real visual catalog (<c>game/assets/models/visuals.json</c>, ADR-009) covers every def
/// drawn on the map, and only the models it uses are in the repository.
/// </summary>
public class VisualCatalogTests
{
    private const string ResourcePrefix = "res://";

    private static readonly Lazy<VisualCatalog> CatalogLazy = new(() =>
        VisualCatalog.Parse(File.ReadAllText(RepoPaths.Combine("game", "assets", "models", "visuals.json"))));

    private static VisualCatalog Catalog => CatalogLazy.Value;

    [Fact]
    public void Every_def_with_a_visual_has_a_model_without_era_variant()
    {
        // The plain id is the fallback of every era variant, so it must always exist.
        var missing = VisualDefs()
            .Where(entry => !Catalog.TryResolve(entry.Visual, era: null, out _))
            .Select(entry => $"{entry.Def.GetType().Name} '{entry.Def.Id}': '{entry.Visual}'")
            .ToList();

        missing.ShouldBeEmpty(string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void Every_model_of_the_catalog_is_used_by_a_def()
    {
        var used = VisualDefs().Select(entry => entry.Visual).ToHashSet(StringComparer.Ordinal);

        // A plant that can be gathered may have a "<visual>.harvested" model for while it has no fruit.
        foreach (var plant in GameContent.Defs.All<PlantDef>().Where(plant => plant.Harvest is not null))
        {
            used.Add($"{plant.Visual}.harvested");
        }

        var unused = Catalog.Models.Select(model => BaseId(model.Id)).Where(id => !used.Contains(id)).ToList();

        unused.ShouldBeEmpty(string.Join(Environment.NewLine, unused));
    }

    [Fact]
    public void Era_variants_name_an_existing_era()
    {
        var eras = GameContent.Defs.All<EraDef>().Select(era => era.Id).ToHashSet(StringComparer.Ordinal);

        var invalid = Catalog.Models
            .Where(model => model.Id.Contains('@', StringComparison.Ordinal))
            .Where(model => !eras.Contains(model.Id[(model.Id.IndexOf('@', StringComparison.Ordinal) + 1)..]))
            .Select(model => model.Id)
            .ToList();

        invalid.ShouldBeEmpty(string.Join(Environment.NewLine, invalid));
    }

    [Fact]
    public void Every_model_file_exists_and_has_its_import_file()
    {
        var problems = new List<string>();
        foreach (var (id, path) in ModelFiles())
        {
            if (!path.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                problems.Add($"'{id}': {path} is not a res:// path");
                continue;
            }

            var file = GamePath(path);
            if (!File.Exists(file))
            {
                problems.Add($"'{id}': {path} does not exist");
            }
            else if (!File.Exists(file + ".import"))
            {
                problems.Add($"'{id}': {path}.import is missing; import the project and version it");
            }
        }

        problems.ShouldBeEmpty(string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Only_the_models_of_the_catalog_are_in_the_repository()
    {
        var referenced = ModelFiles().Select(model => Path.GetFullPath(GamePath(model.Path))).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var extra = Directory.EnumerateFiles(RepoPaths.Combine("game", "assets", "models"), "*.glb", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .Where(file => !referenced.Contains(file))
            .ToList();

        extra.ShouldBeEmpty(string.Join(Environment.NewLine, extra));
    }

    [Fact]
    public void Every_palette_colour_replaces_a_material_of_some_model()
    {
        var materials = ModelFiles()
            .SelectMany(model => GlbMaterialNames(GamePath(model.Path)))
            .ToHashSet(StringComparer.Ordinal);

        var unused = Catalog.Palette.Keys.Where(name => !materials.Contains(name)).ToList();

        unused.ShouldBeEmpty(string.Join(Environment.NewLine, unused));
    }

    [Fact]
    public void An_era_variant_wins_over_the_plain_id_and_falls_back_to_it()
    {
        var catalog = VisualCatalog.Parse("""
            {
              "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} },
              "models": {
                "house": { "model": "res://house.glb" },
                "house@modern": { "model": "res://house_modern.glb", "scale": 2 }
              }
            }
            """);

        catalog.TryResolve("house", "modern", out var modern).ShouldBeTrue();
        modern.ModelPath.ShouldBe("res://house_modern.glb");
        modern.Scale.ShouldBe(2f);
        catalog.TryResolve("house", "medieval", out var medieval).ShouldBeTrue();
        medieval.ModelPath.ShouldBe("res://house.glb");
        medieval.Scale.ShouldBe(1f);
        catalog.TryResolve("tower", "modern", out _).ShouldBeFalse();
        catalog.Palette.ShouldBeEmpty();
    }

    [Fact]
    public void Only_models_of_the_kits_named_get_their_colours_corrected()
    {
        var catalog = VisualCatalog.Parse("""
            {
              "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} },
              "srgbAsLinearKits": ["res://kit/"],
              "models": {
                "a": { "model": "res://kit/a.glb" },
                "b": { "model": "res://kit_other/b.glb" }
              }
            }
            """);

        catalog.TryResolve("a", null, out var kitModel).ShouldBeTrue();
        kitModel.SrgbAsLinear.ShouldBeTrue();
        catalog.TryResolve("b", null, out var otherModel).ShouldBeTrue();
        otherModel.SrgbAsLinear.ShouldBeFalse();
    }

    [Fact]
    public void Every_kit_model_of_the_catalog_gets_its_colours_corrected()
    {
        // Every Kenney kit needs it; KayKit stores its colours right, in a texture (ADR-012).
        static bool IsKenney(string path) => path.Contains("/kenney_", StringComparison.Ordinal);
        var uncorrected = Catalog.Models.Where(model => IsKenney(model.ModelPath) && !model.SrgbAsLinear).Select(model => model.Id)
            .Concat(Catalog.Decorations.Where(decoration => IsKenney(decoration.ModelPath) && !decoration.SrgbAsLinear).Select(decoration => decoration.Id))
            .ToList();

        uncorrected.ShouldBeEmpty(string.Join(Environment.NewLine, uncorrected));
    }

    [Fact]
    public void The_palette_is_read_by_material_name()
    {
        var catalog = VisualCatalog.Parse("""
            {
              "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} },
              "palette": { "leafsGreen": "#4C7A2B" },
              "models": {}
            }
            """);

        catalog.Palette.ShouldBe(new Dictionary<string, string> { ["leafsGreen"] = "#4C7A2B" });
    }

    [Fact]
    public void Every_material_has_a_terrain_pattern_and_every_pattern_names_a_material()
    {
        var materials = GameContent.Defs.All<MaterialDef>().Select(material => material.Id).ToList();

        materials.Where(id => !Catalog.Patterns.ContainsKey(id)).ShouldBeEmpty("materials without a pattern in terrain.materials");
        Catalog.Patterns.Keys.Where(id => !materials.Contains(id)).ShouldBeEmpty("patterns of materials that do not exist");
    }

    [Fact]
    public void Every_file_of_a_model_needs_the_same_colour_correction()
    {
        // A model is shaded with one correction for all its files: its variants and its parts.
        var mixed = Catalog.Models
            .Where(model => model.Files.Any(file => Catalog.NeedsColourCorrection(file) != model.SrgbAsLinear))
            .Select(model => model.Id)
            .ToList();

        mixed.ShouldBeEmpty(string.Join(Environment.NewLine, mixed));
    }

    [Fact]
    public void Variant_tags_are_genders()
    {
        // A colonist takes the variants of its gender, by its name in lower case.
        var genders = Enum.GetNames<OurLife.Core.Entities.Gender>().Select(name => name.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var unknown = Catalog.Models
            .SelectMany(model => (model.Variants?.Keys ?? []).Where(tag => !genders.Contains(tag)).Select(tag => $"'{model.Id}': '{tag}'"))
            .ToList();

        unknown.ShouldBeEmpty(string.Join(Environment.NewLine, unknown));
    }

    [Fact]
    public void Decoration_models_have_no_textures()
    {
        // The decoration is baked with one colour per material: a texture would be lost (ADR-012).
        var textured = Catalog.Decorations
            .Where(decoration => GlbHasImages(GamePath(decoration.ModelPath)))
            .Select(decoration => decoration.Id)
            .ToList();

        textured.ShouldBeEmpty(string.Join(Environment.NewLine, textured));
    }

    [Fact]
    public void Variants_and_parts_are_read()
    {
        var catalog = VisualCatalog.Parse("""
            {
              "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} },
              "models": {
                "human": { "model": "res://a.glb", "variants": { "female": ["res://f1.glb", "res://f2.glb"], "male": ["res://m.glb"] } },
                "bush": { "model": "res://bush.glb", "parts": [{ "model": "res://berries.glb", "offset": [0.1, 0.4, -0.2], "scale": 2 }] }
              }
            }
            """);

        catalog.TryResolve("human", null, out var human).ShouldBeTrue();
        human.Variants!["female"].ShouldBe(["res://f1.glb", "res://f2.glb"]);
        human.Files.ShouldBe(["res://a.glb", "res://f1.glb", "res://f2.glb", "res://m.glb"]);
        catalog.TryResolve("bush", null, out var bush).ShouldBeTrue();
        bush.Parts.ShouldBe([new VisualPart("res://berries.glb", 0.1f, 0.4f, -0.2f, 2f)]);
        bush.Turn.ShouldBe(0);
    }

    [Fact]
    public void Patterns_grass_and_decoration_are_read()
    {
        var catalog = VisualCatalog.Parse("""
            {
              "terrain": {
                "cutColor": "#000000",
                "grass": { "lush": "#4D752E", "dry": "#728038", "deep": "#355B26" },
                "materials": { "soil": "soil", "granite": "Speckled" }
              },
              "srgbAsLinearKits": ["res://kit/"],
              "decoration": {
                "tuft": { "model": "res://kit/tuft.glb", "scale": 0.5, "chance": 120 },
                "flower": { "model": "res://flower.glb", "chance": 20 }
              },
              "models": {}
            }
            """);

        catalog.Grass.ShouldBe(new GrassColors("#4D752E", "#728038", "#355B26"));
        catalog.Patterns["soil"].ShouldBe(TerrainPattern.Soil);
        catalog.Patterns["granite"].ShouldBe(TerrainPattern.Speckled);
        catalog.Decorations.ShouldBe([
            new DecorationModel("tuft", "res://kit/tuft.glb", 0.5f, SrgbAsLinear: true, 120),
            new DecorationModel("flower", "res://flower.glb", 1f, SrgbAsLinear: false, 20),
        ]);
    }

    [Theory]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} } }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "green", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "models": { "a": { "model": "res://a.glb", "scale": 0 } } }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "models": { "a": { "scale": 1 } } }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "models": {}, }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "palette": { "grass": "#12345" }, "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "palette": { "grass": "#12345 " }, "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "# 12345", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "palette": { "grass": null }, "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "models": { "a": { "model": null } } }""")]
    [InlineData("""{ "terrain": { "cutColor": null, "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "pallete": {}, "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "srgbAsLinearKits": ["res://kit"], "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {}, "skyColor": "#0000FF" }, "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "models": { "a": { "model": "res://a.glb", "scal": 2 } } }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "materials": {} }, "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00" }, "materials": {} }, "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": { "soil": "grainy" } }, "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": { "soil": 1 } }, "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grassColor": "#00FF00", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "decoration": { "tuft": { "model": "res://tuft.glb" } }, "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "decoration": { "tuft": { "model": "res://tuft.glb", "chance": 0 } }, "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "decoration": { "tuft": { "model": "res://tuft.glb", "chance": 600 }, "flower": { "model": "res://flower.glb", "chance": 500 } }, "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "decoration": { "tuft": { "model": "res://tuft.glb", "chance": 10, "colour": "#FFFFFF" } }, "models": {} }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "models": { "a": { "model": "res://a.glb", "variants": { "female": [] } } } }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "models": { "a": { "model": "res://a.glb", "parts": [{ "model": "res://b.glb", "offset": [0, 1] }] } } }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "models": { "a": { "model": "res://a.glb", "parts": [{ "model": "res://b.glb", "offset": [0, 1, 0], "scale": 0 }] } } }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "models": { "a": { "model": "res://a.glb", "parts": [{ "model": "res://b.glb", "offset": [0, 1, 0], "tint": "#FF0000" }] } } }""")]
    [InlineData("""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "models": { "a": { "model": "res://a.glb", "turn": 4 } } }""")]
    public void An_invalid_catalog_is_rejected_with_a_format_error(string json)
    {
        Should.Throw<FormatException>(() => VisualCatalog.Parse(json));
    }

    private static IEnumerable<(Def Def, string Visual)> VisualDefs() =>
        GameContent.Defs.AllDefs().Select(def => (def, DefVisuals.Of(def))).Where(entry => entry.Item2 is not null).Select(entry => (entry.def, entry.Item2!));

    /// <summary>Every model file the catalog names: the models of the defs and those of the decoration.</summary>
    private static IEnumerable<(string Id, string Path)> ModelFiles() =>
        Catalog.Models.SelectMany(model => model.Files.Select(file => (model.Id, file)))
            .Concat(Catalog.Decorations.Select(decoration => (decoration.Id, decoration.ModelPath)));

    private static string BaseId(string id)
    {
        var at = id.IndexOf('@', StringComparison.Ordinal);
        return at < 0 ? id : id[..at];
    }

    /// <summary>Names of the materials of a binary glTF, read from its JSON chunk.</summary>
    private static IEnumerable<string> GlbMaterialNames(string file)
    {
        var bytes = File.ReadAllBytes(file);
        var jsonLength = BitConverter.ToInt32(bytes, 12);
        using var document = JsonDocument.Parse(bytes.AsMemory(20, jsonLength));
        if (!document.RootElement.TryGetProperty("materials", out var materials))
        {
            return [];
        }

        return materials.EnumerateArray()
            .Where(material => material.TryGetProperty("name", out _))
            .Select(material => material.GetProperty("name").GetString()!)
            .ToList();
    }

    /// <summary>Whether a binary glTF carries images, read from its JSON chunk.</summary>
    private static bool GlbHasImages(string file)
    {
        var bytes = File.ReadAllBytes(file);
        var jsonLength = BitConverter.ToInt32(bytes, 12);
        using var document = JsonDocument.Parse(bytes.AsMemory(20, jsonLength));
        return document.RootElement.TryGetProperty("images", out var images) && images.GetArrayLength() > 0;
    }

    private static string GamePath(string resourcePath) =>
        RepoPaths.Combine(["game", .. resourcePath[ResourcePrefix.Length..].Split('/')]);
}
