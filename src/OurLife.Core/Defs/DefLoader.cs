using System.Text.Json;
using System.Text.Json.Serialization;

namespace OurLife.Core.Defs;

/// <summary>
/// Loads every <c>.json</c> file under <c>defs/</c>. Each file holds an array of defs; each def
/// names its class in <c>"type"</c>. JSON may have comments and trailing commas. Unknown or
/// misspelled fields are errors, so typos never pass silently.
/// </summary>
public static class DefLoader
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
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
            document = JsonDocument.Parse(json, DocumentOptions);
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
}
