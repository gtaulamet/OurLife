using OurLife.Core.Defs;
using OurLife.Localization;

namespace OurLife.Game.Content;

/// <summary>Loads one catalog per folder of <c>content/locales</c>.</summary>
public static class LocaleCatalogs
{
    /// <exception cref="LocalizationException">Some catalog has problems.</exception>
    public static IReadOnlyList<LocaleCatalog> Load(IContentSource source) =>
        source.ListDirectories("locales")
            .Select(locale => LocaleCatalog.Parse(
                locale,
                source.ListFiles($"locales/{locale}", ".json")
                    .Select(path => new CatalogFile(path, source.ReadText(path)))))
            .ToArray();
}
