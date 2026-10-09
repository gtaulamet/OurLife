using OurLife.Core.Defs;
using OurLife.Game.Content;
using OurLife.Localization;

namespace OurLife.Content.Tests;

/// <summary>
/// The real content of <c>game/content</c>, loaded once for every test with the same loaders
/// the game uses (<see cref="LocaleCatalogs"/> is linked from the game project).
/// </summary>
internal static class GameContent
{
    private static readonly Lazy<IContentSource> SourceLazy = new(() => new FileSystemContentSource(RepoPaths.Content));
    private static readonly Lazy<DefDatabase> DefsLazy = new(() => DefLoader.Load(Source));
    private static readonly Lazy<IReadOnlyDictionary<string, LocaleCatalog>> CatalogsLazy =
        new(() => LocaleCatalogs.Load(Source).ToDictionary(catalog => catalog.Locale));

    public static IContentSource Source => SourceLazy.Value;

    public static DefDatabase Defs => DefsLazy.Value;

    /// <summary>Catalogs by locale code.</summary>
    public static IReadOnlyDictionary<string, LocaleCatalog> Catalogs => CatalogsLazy.Value;

    public static LocaleCatalog Spanish => Catalogs[Localizer.DefaultLocale];
}
