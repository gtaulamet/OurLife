namespace OurLife.Localization;

/// <summary>Returns the CLDR plural category ("one", "other"…) of a number in a language.</summary>
public delegate string PluralRule(decimal value);

/// <summary>Plural rules per language. A new language adds its CLDR rule here.</summary>
public static class PluralRules
{
    /// <summary>Spanish and English: "one" only for exactly 1, "other" for everything else.</summary>
    public static readonly PluralRule OneOther = value => value == 1m ? "one" : "other";

    private static readonly Dictionary<string, PluralRule> RulesByLanguage = new(StringComparer.Ordinal)
    {
        ["es"] = OneOther,
        ["en"] = OneOther,
    };

    /// <summary>Rule for a locale such as "es" or "en-GB"; unknown languages use <see cref="OneOther"/>.</summary>
    public static PluralRule For(string locale)
    {
        ArgumentNullException.ThrowIfNull(locale);
        var language = locale.Split('-', '_')[0];
        return RulesByLanguage.TryGetValue(language, out var rule) ? rule : OneOther;
    }
}
