using System.Text.Json;
using System.Text.Json.Serialization;

namespace OurLife.Core.Defs;

/// <summary>
/// Loads every <c>.json</c> file under <c>defs/</c>. Each file holds an array of defs; each def
/// names its class in <c>"type"</c>. Files are strict JSON, without comments or trailing commas,
/// because the Godot editor reads them too. Unknown or misspelled fields are errors, so typos
/// never pass silently, and so is <c>null</c>: a field without a value is left out.
/// </summary>
public static class DefLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    /// <exception cref="DefValidationException">Lists every problem in the content.</exception>
    public static DefDatabase Load(IContentSource source, string directory = "defs")
    {
        ArgumentNullException.ThrowIfNull(source);
        var defs = new List<Def>();
        var errors = new List<string>();

        foreach (var path in source.ListFiles(directory, ".json"))
        {
            ParseFile(path, source.ReadText(path), defs, errors);
        }

        if (errors.Count > 0)
        {
            throw new DefValidationException(errors);
        }

        return DefDatabase.Build(defs);
    }

    private static void ParseFile(string path, string json, List<Def> defs, List<string> errors)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            errors.Add($"{path}: invalid JSON: {ex.Message}");
            return;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                errors.Add($"{path}: the file must contain a JSON array of defs.");
                return;
            }

            var index = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var where = $"{path}[{index++}]";
                if (element.ValueKind != JsonValueKind.Object
                    || !element.TryGetProperty("type", out var typeProperty)
                    || typeProperty.ValueKind != JsonValueKind.String)
                {
                    errors.Add($"{where}: every def needs a \"type\" string.");
                    continue;
                }

                var typeName = typeProperty.GetString()!;
                if (!DefTypes.TryGetClrType(typeName, out var clrType))
                {
                    errors.Add($"{where}: unknown def type '{typeName}'. Known types: {string.Join(", ", DefTypes.Names)}.");
                    continue;
                }

                // Deserialization lets null through even for required fields, and validation expects values.
                var nulls = new List<string>();
                CollectNulls(element, string.Empty, nulls);
                if (nulls.Count > 0)
                {
                    errors.Add($"{where} ({typeName}): null in {string.Join(", ", nulls)}. Leave out a field that has no value.");
                    continue;
                }

                try
                {
                    var def = (Def)element.Deserialize(clrType, SerializerOptions)!;
                    def.SourceFile = path;
                    defs.Add(def);
                }
                catch (JsonException ex)
                {
                    errors.Add($"{where} ({typeName}): {ex.Message}");
                }
            }
        }
    }

    /// <summary>Paths of every null value, like <c>harvest.item</c> or <c>requires[0]</c>.</summary>
    private static void CollectNulls(JsonElement element, string path, List<string> nulls)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                nulls.Add($"'{path}'");
                break;

            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectNulls(property.Value, path.Length == 0 ? property.Name : $"{path}.{property.Name}", nulls);
                }

                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    CollectNulls(item, $"{path}[{index++}]", nulls);
                }

                break;
        }
    }
}
