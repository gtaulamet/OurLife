using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using OurLife.Localization.Icu;

namespace OurLife.Localization;

/// <summary>A JSON catalog file: its name, for error messages, and its content.</summary>
public readonly record struct CatalogFile(string Name, string Json);

/// <summary>
/// Every message of one language, parsed and validated. Built from flat JSON objects
/// (key → ICU pattern); a key may appear in only one file of the language. Files are strict
/// JSON, without comments or trailing commas, because the Godot editor reads them too.
/// </summary>
public sealed partial class LocaleCatalog
{
    private readonly SortedDictionary<string, IcuMessage> _messages;
    private readonly Dictionary<string, string> _sourceFiles;

    private LocaleCatalog(string locale, SortedDictionary<string, IcuMessage> messages, Dictionary<string, string> sourceFiles)
    {
        Locale = locale;
        _messages = messages;
        _sourceFiles = sourceFiles;
        PluralRule = PluralRules.For(locale);
        Culture = CultureFor(locale);
    }

    public string Locale { get; }

    /// <summary>Plural rule of the language, for formatting its messages.</summary>
    public PluralRule PluralRule { get; }

    /// <summary>Culture for numbers in this language; the invariant culture if .NET does not know it.</summary>
    public CultureInfo Culture { get; }

    /// <summary>Messages sorted by key.</summary>
    public IReadOnlyDictionary<string, IcuMessage> Messages => _messages;

    public static bool IsValidKey(string? key) => key is not null && KeyPattern().IsMatch(key);

    /// <exception cref="LocalizationException">Lists every invalid file, key or pattern.</exception>
    public static LocaleCatalog Parse(string locale, IEnumerable<CatalogFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (locale is null || !LocalePattern().IsMatch(locale))
        {
            throw new ArgumentException($"Invalid locale '{locale}'. Use codes like 'es' or 'en-GB'.", nameof(locale));
        }

        var messages = new SortedDictionary<string, IcuMessage>(StringComparer.Ordinal);
        var sourceFiles = new Dictionary<string, string>(StringComparer.Ordinal);
        var errors = new List<string>();

        foreach (var file in files.OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            ParseFile(file, messages, sourceFiles, errors);
        }

        if (errors.Count > 0)
        {
            throw new LocalizationException($"The '{locale}' catalog has {errors.Count} problem(s):", errors);
        }

        return new LocaleCatalog(locale, messages, sourceFiles);
    }

    /// <summary>File that defines a key, for error messages.</summary>
    public string SourceOf(string key) => _sourceFiles.TryGetValue(key, out var file) ? file : "?";

    /// <summary>Formats one of this language's messages.</summary>
    public string Format(IcuMessage message, IReadOnlyDictionary<string, object?>? arguments = null) =>
        message.Format(arguments, PluralRule, Culture);

    private static CultureInfo CultureFor(string locale)
    {
        try
        {
            return CultureInfo.GetCultureInfo(locale);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private static void ParseFile(
        CatalogFile file,
        SortedDictionary<string, IcuMessage> messages,
        Dictionary<string, string> sourceFiles,
        List<string> errors)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(file.Json);
        }
        catch (JsonException ex)
        {
            errors.Add($"{file.Name}: invalid JSON: {ex.Message}");
            return;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{file.Name}: the file must contain one JSON object of key → message.");
                return;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                var key = property.Name;
                if (!IsValidKey(key))
                {
                    errors.Add($"{file.Name}: invalid key '{key}'. Use lowercase segments separated by dots.");
                    continue;
                }

                if (sourceFiles.TryGetValue(key, out var firstFile))
                {
                    errors.Add($"{file.Name}: key '{key}' is already defined in {firstFile}.");
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    errors.Add($"{file.Name}: the value of '{key}' must be a string.");
                    continue;
                }

                try
                {
                    messages.Add(key, IcuMessage.Parse(property.Value.GetString()!));
                    sourceFiles.Add(key, file.Name);
                }
                catch (IcuSyntaxException ex)
                {
                    errors.Add($"{file.Name}: '{key}': {ex.Message}");
                }
            }
        }
    }

    [GeneratedRegex(@"^[a-z0-9_]+(\.[a-z0-9_]+)+$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^[a-z]{2,3}(-[A-Z]{2})?$")]
    private static partial Regex LocalePattern();
}
