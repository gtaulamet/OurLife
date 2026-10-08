using OurLife.Core.Defs;
using OurLife.Localization;

namespace OurLife.Content.Tests;

/// <summary>The real content of <c>game/content</c>, loaded once for every test.</summary>
internal static class GameContent
{
    private static readonly Lazy<IContentSource> SourceLazy = new(() => new FileSystemContentSource(RepoPaths.Content));
    private static readonly Lazy<DefDatabase> DefsLazy = new(() => DefLoader.Load(Source));
    private static readonly Lazy<IReadOnlyDictionary<string, LocaleCatalog>> CatalogsLazy = new(LoadCatalogs);

    public static IContentSource Source => SourceLazy.Value;

    public static DefDatabase Defs => DefsLazy.Value;

    /// <summary>Catalogs by locale code.</summary>
    public static IReadOnlyDictionary<string, LocaleCatalog> Catalogs => CatalogsLazy.Value;

    public static LocaleCatalog Spanish => Catalogs[Localizer.DefaultLocale];

    private static IReadOnlyDictionary<string, LocaleCatalog> LoadCatalogs() =>
        Source.ListDirectories("locales").ToDictionary(
            locale => locale,
            locale => LocaleCatalog.Parse(
                locale,
                Source.ListFiles($"locales/{locale}", ".json")
                    .Select(path => new CatalogFile(path, Source.ReadText(path)))));
}
