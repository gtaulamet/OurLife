namespace OurLife.Localization.Tests;

public class LocalizerTests
{
    private static Localizer NewLocalizer() => new([
        LocaleCatalog.Parse("es", [new CatalogFile("ui.json", """
            {
              "ui.mainmenu.new_game": "Nueva partida",
              "ui.only.spanish": "Sólo en español",
              "ui.items.count": "{count, plural, one {# objeto} other {# objetos}}"
            }
            """)]),
        LocaleCatalog.Parse("en", [new CatalogFile("ui.json", """
            {
              "ui.mainmenu.new_game": "New game",
              "ui.items.count": "{count, plural, one {# item} other {# items}}"
            }
            """)]),
    ]);

    [Fact]
    public void Spanish_is_the_starting_language()
    {
        var localizer = NewLocalizer();

        localizer.CurrentLocale.ShouldBe("es");
        localizer.Format("ui.mainmenu.new_game").ShouldBe("Nueva partida");
    }

    [Fact]
    public void Changing_the_language_changes_the_texts_and_notifies()
    {
        var localizer = NewLocalizer();
        var notified = new List<string>();
        localizer.LocaleChanged += notified.Add;

        localizer.SetLocale("en");

        localizer.Format("ui.mainmenu.new_game").ShouldBe("New game");
        notified.ShouldBe(new[] { "en" });
    }

    [Fact]
    public void Setting_the_current_language_again_does_not_notify()
    {
        var localizer = NewLocalizer();
        var notifications = 0;
        localizer.LocaleChanged += _ => notifications++;

        localizer.SetLocale("es");

        notifications.ShouldBe(0);
    }

    [Fact]
    public void A_key_missing_in_the_current_language_falls_back_to_spanish()
    {
        var localizer = NewLocalizer();
        localizer.SetLocale("en");

        localizer.Format("ui.only.spanish").ShouldBe("Sólo en español");
    }

    [Fact]
    public void A_key_missing_everywhere_is_visible_and_recorded()
    {
        var localizer = NewLocalizer();

        localizer.Format("ui.does.not_exist").ShouldBe("⟦ui.does.not_exist⟧");
        localizer.MissingKeys.ShouldContain("ui.does.not_exist");
    }

    [Fact]
    public void Plurals_follow_the_current_language()
    {
        var localizer = NewLocalizer();
        var one = new Dictionary<string, object?> { ["count"] = 1 };
        var five = new Dictionary<string, object?> { ["count"] = 5 };

        localizer.Format("ui.items.count", one).ShouldBe("1 objeto");
        localizer.SetLocale("en");
        localizer.Format("ui.items.count", five).ShouldBe("5 items");
    }

    [Fact]
    public void An_unknown_language_is_rejected()
    {
        Should.Throw<ArgumentException>(() => NewLocalizer().SetLocale("fr"));
    }

    [Fact]
    public void The_fallback_language_must_have_a_catalog()
    {
        Should.Throw<ArgumentException>(() =>
            new Localizer([LocaleCatalog.Parse("en", [])]));
    }
}
