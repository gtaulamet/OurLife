using System.Diagnostics.CodeAnalysis;
using OurLife.Localization.Icu;

namespace OurLife.Localization;

/// <summary>
/// Resolves message keys in the current language. Spanish is the default and the fallback:
/// a key missing in the current language uses Spanish, and a key missing everywhere is shown as
/// <c>⟦key⟧</c> and recorded in <see cref="MissingKeys"/>. The operating system language is
/// never used.
/// </summary>
public sealed class Localizer
{
    public const string DefaultLocale = "es";

    private readonly Dictionary<string, LocaleCatalog> _catalogs;
    private readonly SortedSet<string> _missingKeys = new(StringComparer.Ordinal);
    private readonly LocaleCatalog _fallback;
    private LocaleCatalog _current;

    public Localizer(IEnumerable<LocaleCatalog> catalogs, string fallbackLocale = DefaultLocale)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        _catalogs = new Dictionary<string, LocaleCatalog>(StringComparer.Ordinal);
        foreach (var catalog in catalogs)
        {
            if (!_catalogs.TryAdd(catalog.Locale, catalog))
            {
                throw new ArgumentException($"Two catalogs for the locale '{catalog.Locale}'.", nameof(catalogs));
            }
        }

        if (!_catalogs.TryGetValue(fallbackLocale, out var fallback))
        {
            throw new ArgumentException($"There is no catalog for the fallback locale '{fallbackLocale}'.", nameof(fallbackLocale));
        }

        _fallback = fallback;
        _current = fallback;
        Locales = _catalogs.Keys.Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Raised after the current language changes, with the new locale.</summary>
    public event Action<string>? LocaleChanged;

    public string FallbackLocale => _fallback.Locale;

    public string CurrentLocale => _current.Locale;

    /// <summary>Available locales, sorted.</summary>
    public IReadOnlyList<string> Locales { get; }

    /// <summary>Keys that were requested but exist in no catalog.</summary>
    public IReadOnlyCollection<string> MissingKeys => _missingKeys;

    public LocaleCatalog Catalog(string locale) =>
        _catalogs.TryGetValue(locale, out var catalog)
            ? catalog
            : throw new ArgumentException($"Unknown locale '{locale}'.", nameof(locale));

    public bool IsAvailable(string locale) => _catalogs.ContainsKey(locale);

    public void SetLocale(string locale)
    {
        if (!_catalogs.TryGetValue(locale, out var catalog))
        {
            throw new ArgumentException(
                $"Unknown locale '{locale}'. Available: {string.Join(", ", Locales)}.", nameof(locale));
        }

        if (catalog == _current)
        {
            return;
        }

        _current = catalog;
        LocaleChanged?.Invoke(locale);
    }

    public bool HasKey(string key) => TryFind(key, out _, out _);

    /// <summary>Formats a message in the current language.</summary>
    public string Format(string key, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        if (!TryFind(key, out var message, out var catalog))
        {
            _missingKeys.Add(key);
            return $"⟦{key}⟧";
        }

        return catalog.Format(message, arguments);
    }

    /// <summary>Finds a key in the current language, then in the fallback one.</summary>
    private bool TryFind(string key, [NotNullWhen(true)] out IcuMessage? message, [NotNullWhen(true)] out LocaleCatalog? catalog)
    {
        catalog = _current;
        if (catalog.Messages.TryGetValue(key, out message))
        {
            return true;
        }

        catalog = _fallback;
        if (catalog.Messages.TryGetValue(key, out message))
        {
            return true;
        }

        catalog = null;
        return false;
    }
}
