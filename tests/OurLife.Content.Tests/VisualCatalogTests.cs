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
        foreach (var model in Catalog.Models)
        {
            if (!model.ModelPath.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                problems.Add($"'{model.Id}': {model.ModelPath} is not a res:// path");
                continue;
            }

            var file = GamePath(model.ModelPath);
            if (!File.Exists(file))
            {
                problems.Add($"'{model.Id}': {model.ModelPath} does not exist");
            }
            else if (!File.Exists(file + ".import"))
            {
                problems.Add($"'{model.Id}': {model.ModelPath}.import is missing; import the project and version it");
            }
        }

        problems.ShouldBeEmpty(string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Only_the_models_of_the_catalog_are_in_the_repository()
    {
        var referenced = Catalog.Models.Select(model => Path.GetFullPath(GamePath(model.ModelPath))).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var extra = Directory.EnumerateFiles(RepoPaths.Combine("game", "assets", "models"), "*.glb", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .Where(file => !referenced.Contains(file))
            .ToList();

        extra.ShouldBeEmpty(string.Join(Environment.NewLine, extra));
    }

    [Fact]
    public void Every_palette_colour_replaces_a_material_of_some_model()
    {
        var materials = Catalog.Models
            .SelectMany(model => GlbMaterialNames(GamePath(model.ModelPath)))
            .ToHashSet(StringComparer.Ordinal);

        var unused = Catalog.Palette.Keys.Where(name => !materials.Contains(name)).ToList();

        unused.ShouldBeEmpty(string.Join(Environment.NewLine, unused));
    }

    [Fact]
    public void An_era_variant_wins_over_the_plain_id_and_falls_back_to_it()
    {
        var catalog = VisualCatalog.Parse("""
            {
              "terrain": { "grassColor": "#00FF00", "cutColor": "#000000" },
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
              "terrain": { "grassColor": "#00FF00", "cutColor": "#000000" },
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
        // Every model in the repository comes from a Kenney kit, and all of them need it.
        var uncorrected = Catalog.Models.Where(model => !model.SrgbAsLinear).Select(model => model.Id).ToList();

        uncorrected.ShouldBeEmpty(string.Join(Environment.NewLine, uncorrected));
    }

    [Fact]
    public void The_palette_is_read_by_material_name()
    {
        var catalog = VisualCatalog.Parse("""
            {
              "terrain": { "grassColor": "#00FF00", "cutColor": "#000000" },
              "palette": { "leafsGreen": "#4C7A2B" },
              "models": {}
            }
            """);

        catalog.Palette.ShouldBe(new Dictionary<string, string> { ["leafsGreen"] = "#4C7A2B" });
    }

    [Theory]
    [InlineData("""{ "terrain": { "grassColor": "#00FF00", "cutColor": "#000000" } }""")]
    [InlineData("""{ "terrain": { "grassColor": "green", "cutColor": "#000000" }, "models": {} }""")]
    [InlineData("""{ "terrain": { "grassColor": "#00FF00", "cutColor": "#000000" }, "models": { "a": { "model": "res://a.glb", "scale": 0 } } }""")]
    [InlineData("""{ "terrain": { "grassColor": "#00FF00", "cutColor": "#000000" }, "models": { "a": { "scale": 1 } } }""")]
    [InlineData("""{ "terrain": { "grassColor": "#00FF00", "cutColor": "#000000" }, "models": {}, }""")]
    [InlineData("""{ "terrain": { "grassColor": "#00FF00", "cutColor": "#000000" }, "palette": { "grass": "#12345" }, "models": {} }""")]
    [InlineData("""{ "terrain": { "grassColor": "#00FF00", "cutColor": "#000000" }, "palette": { "grass": "#12345 " }, "models": {} }""")]
    [InlineData("""{ "terrain": { "grassColor": "# 12345", "cutColor": "#000000" }, "models": {} }""")]
    [InlineData("""{ "terrain": { "grassColor": "#00FF00", "cutColor": "#000000" }, "palette": { "grass": null }, "models": {} }""")]
    [InlineData("""{ "terrain": { "grassColor": "#00FF00", "cutColor": "#000000" }, "models": { "a": { "model": null } } }""")]
    [InlineData("""{ "terrain": { "grassColor": "#00FF00", "cutColor": null }, "models": {} }""")]
    [InlineData("""{ "terrain": { "grassColor": "#00FF00", "cutColor": "#000000" }, "pallete": {}, "models": {} }""")]
    [InlineData("""{ "terrain": { "grassColor": "#00FF00", "cutColor": "#000000" }, "srgbAsLinearKits": ["res://kit"], "models": {} }""")]
    [InlineData("""{ "terrain": { "grassColor": "#00FF00", "cutColor": "#000000", "skyColor": "#0000FF" }, "models": {} }""")]
    [InlineData("""{ "terrain": { "grassColor": "#00FF00", "cutColor": "#000000" }, "models": { "a": { "model": "res://a.glb", "scal": 2 } } }""")]
    public void An_invalid_catalog_is_rejected_with_a_format_error(string json)
    {
        Should.Throw<FormatException>(() => VisualCatalog.Parse(json));
    }

    private static IEnumerable<(Def Def, string Visual)> VisualDefs() =>
        GameContent.Defs.AllDefs().Select(def => (def, DefVisuals.Of(def))).Where(entry => entry.Item2 is not null).Select(entry => (entry.def, entry.Item2!));

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

    private static string GamePath(string resourcePath) =>
        RepoPaths.Combine(["game", .. resourcePath[ResourcePrefix.Length..].Split('/')]);
}
