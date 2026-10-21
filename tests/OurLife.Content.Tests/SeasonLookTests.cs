using OurLife.Core.Defs;
using OurLife.Game.Rendering;

namespace OurLife.Content.Tests;

/// <summary>The seasons of the view (<c>docs/gdd/estaciones-visibles.md</c>): the real catalog and the blend between seasons.</summary>
public class SeasonLookTests
{
    private static readonly VisualCatalog Catalog =
        VisualCatalog.Parse(File.ReadAllText(RepoPaths.Combine("game", "assets", "models", "visuals.json")));

    private static readonly string[] Ids = ["spring", "summer", "autumn", "winter"];

    private static SeasonLook At(int season, double daysIn) => SeasonLook.At(Catalog.Seasons!, Ids, 15, season, daysIn);

    [Fact]
    public void The_catalog_paints_every_season_of_the_calendar()
    {
        Catalog.Seasons.ShouldNotBeNull();
        GameContent.Defs.Time.Seasons.ShouldBe(Ids);
        Catalog.Seasons.Seasons.Keys.OrderBy(id => id).ShouldBe(Ids.OrderBy(id => id));
    }

    [Fact]
    public void Trees_and_bushes_that_change_have_leaves_to_paint()
    {
        var foliage = Catalog.Models.Where(model => model.Foliage is not null).Select(model => model.Id).ToList();
        foliage.ShouldContain("plant.oak");
        foliage.ShouldContain("plant.pine");
        Catalog.Models.Single(model => model.Id == "plant.pine").Foliage.ShouldBe(FoliageKinds.Evergreen);
        Catalog.Seasons!.FoliageMaterials.ShouldContain("leafsGreen");
    }

    [Fact]
    public void Autumn_is_yellow_and_red_in_oaks_and_pines_and_winter_is_white()
    {
        var autumn = SeasonLook.Of(Catalog.Seasons!, "autumn");
        autumn.Deciduous.Amount.ShouldBeGreaterThan(0.5f);
        autumn.Evergreen.Amount.ShouldBeGreaterThan(0f);
        autumn.Evergreen.Amount.ShouldBeLessThan(autumn.Deciduous.Amount);
        autumn.Deciduous.Tint.ShouldNotBe(autumn.Deciduous.TintB);

        var winter = SeasonLook.Of(Catalog.Seasons!, "winter");
        winter.Deciduous.Snow.ShouldBeGreaterThan(0f);
        winter.Evergreen.Snow.ShouldBeGreaterThan(0f);
        winter.PeakSnow.ShouldBe(1f);
        winter.Grass.Lush.R.ShouldBeGreaterThan(0.8f);
    }

    [Fact]
    public void A_season_holds_its_look_until_its_last_days()
    {
        At(2, 0).ShouldBe(SeasonLook.Of(Catalog.Seasons!, "autumn"));
        At(2, 11.9).ShouldBe(SeasonLook.Of(Catalog.Seasons!, "autumn"));
    }

    [Fact]
    public void The_look_changes_over_days_and_arrives_at_the_next_season()
    {
        var before = At(2, 12.5);
        var middle = At(2, 13.5);
        var next = SeasonLook.Of(Catalog.Seasons!, "winter");
        before.Grass.Lush.R.ShouldBeGreaterThan(SeasonLook.Of(Catalog.Seasons!, "autumn").Grass.Lush.R);
        middle.Grass.Lush.R.ShouldBeGreaterThan(before.Grass.Lush.R);
        middle.Grass.Lush.R.ShouldBeLessThan(next.Grass.Lush.R);

        // The step between one midnight and the next is a fraction of the whole change.
        var day = Math.Abs(At(2, 14.0).Grass.Lush.R - At(2, 13.0).Grass.Lush.R);
        var whole = Math.Abs(next.Grass.Lush.R - SeasonLook.Of(Catalog.Seasons!, "autumn").Grass.Lush.R);
        day.ShouldBeLessThan(whole * 0.8f);

        // The first moment of the next season is where the blend ended.
        var arrived = SeasonLook.At(Catalog.Seasons!, Ids, 15, 3, 0);
        arrived.Grass.ShouldBe(next.Grass);
        At(2, 14.999).Grass.Lush.R.ShouldBe(next.Grass.Lush.R, 0.01f);
    }

    [Fact]
    public void Winter_gives_way_to_spring_across_the_new_year()
    {
        SeasonLook.At(Catalog.Seasons!, Ids, 15, 3, 14.9).Grass.Lush.R.ShouldBeLessThan(0.5f);
    }

    [Theory]
    [InlineData("""{ "transitionDays": 3, "snowLine": 2, "snowColor": "#FFFFFF", "foliageMaterials": [] }""")]
    [InlineData("""{ "transitionDays": -1, "snowLine": 0.5, "snowColor": "#FFFFFF", "foliageMaterials": [] }""")]
    public void An_invalid_seasons_block_is_rejected(string seasons)
    {
        var json = $$"""{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "seasons": {{seasons}}, "models": {} }""";
        Should.Throw<FormatException>(() => VisualCatalog.Parse(json));
    }

    [Fact]
    public void A_model_with_an_unknown_foliage_is_rejected()
    {
        const string json = """{ "terrain": { "cutColor": "#000000", "grass": { "lush": "#00FF00", "dry": "#00FF00", "deep": "#00FF00" }, "materials": {} }, "models": { "a": { "model": "res://a.glb", "foliage": "palm" } } }""";
        Should.Throw<FormatException>(() => VisualCatalog.Parse(json));
    }
}
