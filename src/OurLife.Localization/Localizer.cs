using System.Globalization;
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

        if (!_catalogs.ContainsKey(fallbackLocale))
        {
            throw new ArgumentException($"There is no catalog for the fallback locale '{fallbackLocale}'.", nameof(fallbackLocale));
        }

        FallbackLocale = fallbackLocale;
        CurrentLocale = fallbackLocale;
        Locales = _catalogs.Keys.Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Raised after the current language changes, with the new locale.</summary>
    public event Action<string>? LocaleChanged;

    public string FallbackLocale { get; }

    public string CurrentLocale { get; private set; }

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
        if (!_catalogs.ContainsKey(locale))
        {
            throw new ArgumentException(
                $"Unknown locale '{locale}'. Available: {string.Join(", ", Locales)}.", nameof(locale));
        }

        if (locale == CurrentLocale)
        {
            return;
        }

        CurrentLocale = locale;
        LocaleChanged?.Invoke(locale);
    }

    public bool HasKey(string key) => TryFind(key, out _, out _);

    /// <summary>Formats a message in the current language.</summary>
    public string Format(string key, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        if (!TryFind(key, out var message, out var locale))
        {
            _missingKeys.Add(key);
            return $"⟦{key}⟧";
        }

        return message.Format(arguments, PluralRules.For(locale), CultureFor(locale));
    }

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

    private bool TryFind(string key, out IcuMessage message, out string locale)
    {
        foreach (var candidate in new[] { CurrentLocale, FallbackLocale })
        {
            if (_catalogs[candidate].Messages.TryGetValue(key, out var found))
            {
                message = found;
                locale = candidate;
                return true;
            }
        }

        message = null!;
        locale = string.Empty;
        return false;
    }
}
