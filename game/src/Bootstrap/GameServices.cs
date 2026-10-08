using OurLife.Core.Defs;
using OurLife.Game.Content;
using OurLife.Game.Localization;
using OurLife.Localization;

namespace OurLife.Game.Bootstrap;

/// <summary>Content, texts and preferences shared by every screen.</summary>
public sealed class GameServices
{
    private GameServices(DefDatabase defs, IReadOnlyList<LocaleCatalog> catalogs, Localizer localizer, Settings settings)
    {
        Defs = defs;
        Catalogs = catalogs;
        Localizer = localizer;
        Settings = settings;
        Text = new TextResolver(localizer);
    }

    public DefDatabase Defs { get; }

    public IReadOnlyList<LocaleCatalog> Catalogs { get; }

    public Localizer Localizer { get; }

    public Settings Settings { get; }

    public TextResolver Text { get; }

    /// <exception cref="DefValidationException">The defs have problems.</exception>
    /// <exception cref="LocalizationException">The catalogs have problems.</exception>
    public static GameServices Load(IContentSource content, Settings settings)
    {
        var defs = DefLoader.Load(content);
        var catalogs = LocaleCatalogs.Load(content);
        var localizer = new Localizer(catalogs);
        if (localizer.IsAvailable(settings.Locale))
        {
            localizer.SetLocale(settings.Locale);
        }

        return new GameServices(defs, catalogs, localizer, settings);
    }

    /// <summary>Changes the language everywhere and remembers the choice.</summary>
    public void ChangeLanguage(string locale)
    {
        Localizer.SetLocale(locale);
        Settings.SetLocale(locale);
    }
}
