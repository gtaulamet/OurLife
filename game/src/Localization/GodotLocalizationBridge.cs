using Godot;
using OurLife.Localization;

namespace OurLife.Game.Localization;

/// <summary>
/// Registers the catalogs in Godot's <see cref="TranslationServer"/> and keeps its language in
/// step with the <see cref="Localizer"/>. Controls whose text is a key (for example a Label with
/// <c>ui.mainmenu.new_game</c>) are then translated by Godot and change language live.
/// Messages with arguments are formatted through <see cref="TextResolver"/> instead: Godot gets
/// every message already formatted without arguments, so quotes are resolved and a key used
/// where arguments were needed shows <c>⟦name⟧</c> rather than raw ICU syntax.
/// </summary>
public sealed class GodotLocalizationBridge : IDisposable
{
    private readonly Localizer _localizer;
    private readonly List<Translation> _translations = [];

    public GodotLocalizationBridge(Localizer localizer, IEnumerable<LocaleCatalog> catalogs)
    {
        _localizer = localizer;
        foreach (var catalog in catalogs)
        {
            var translation = new Translation { Locale = catalog.Locale };
            foreach (var (key, message) in catalog.Messages)
            {
                translation.AddMessage(key, catalog.Format(message));
            }

            TranslationServer.AddTranslation(translation);
            _translations.Add(translation);
        }

        TranslationServer.SetLocale(localizer.CurrentLocale);
        localizer.LocaleChanged += OnLocaleChanged;
    }

    /// <summary>What Godot shows for a key in a translated control, in the current language.</summary>
    public static string Translate(string key) => TranslationServer.Translate(key).ToString();

    public void Dispose()
    {
        _localizer.LocaleChanged -= OnLocaleChanged;
        foreach (var translation in _translations)
        {
            TranslationServer.RemoveTranslation(translation);
        }

        _translations.Clear();
    }

    private static void OnLocaleChanged(string locale) => TranslationServer.SetLocale(locale);
}
