namespace OurLife.Localization.Tests;

public class LocaleCatalogTests
{
    [Fact]
    public void Messages_from_several_files_are_merged_and_sorted()
    {
        var catalog = LocaleCatalog.Parse("es", [
            new CatalogFile("ui.json", """{ "ui.b.second": "B", "ui.a.first": "A" }"""),
            new CatalogFile("content.json", """{ "def.item.log.name": "tronco" }"""),
        ]);

        catalog.Messages.Keys.ShouldBe(new[] { "def.item.log.name", "ui.a.first", "ui.b.second" });
        catalog.SourceOf("ui.a.first").ShouldBe("ui.json");
    }

    [Theory]
    [InlineData("""{ /* Menú principal */ "ui.mainmenu.new_game": "Nueva partida" }""")]
    [InlineData("""{ "ui.mainmenu.new_game": "Nueva partida", }""")]
    public void Comments_and_trailing_commas_are_rejected_because_godot_reads_the_files_too(string json)
    {
        var error = Should.Throw<LocalizationException>(() =>
            LocaleCatalog.Parse("es", [new CatalogFile("ui.json", json)]));

        error.Message.ShouldContain("invalid JSON");
    }

    [Fact]
    public void Every_problem_is_reported_at_once()
    {
        var error = Should.Throw<LocalizationException>(() => LocaleCatalog.Parse("es", [
            new CatalogFile("a.json", """{ "ui.menu.title": "Título", "Bad Key": "x", "ui.menu.count": 3 }"""),
            new CatalogFile("b.json", """{ "ui.menu.title": "Otro", "ui.menu.broken": "{count, plural, one {#}}" }"""),
        ]));

        error.Errors.Count.ShouldBe(4);
        error.Message.ShouldContain("Bad Key");
        error.Message.ShouldContain("must be a string");
        error.Message.ShouldContain("already defined in a.json");
        error.Message.ShouldContain("'other'");
    }

    [Fact]
    public void A_file_that_is_not_an_object_is_rejected()
    {
        Should.Throw<LocalizationException>(() =>
            LocaleCatalog.Parse("es", [new CatalogFile("ui.json", """["ui.menu.title"]""")]));
    }

    [Theory]
    [InlineData("ES")]
    [InlineData("spanish")]
    [InlineData("")]
    public void Invalid_locale_codes_are_rejected(string locale)
    {
        Should.Throw<ArgumentException>(() => LocaleCatalog.Parse(locale, []));
    }
}
