using System.Globalization;

namespace OurLife.Game.Rendering;

/// <summary>A colour as #RRGGBB values from 0 to 1, without Godot so the blend of seasons is tested.</summary>
public readonly record struct Rgb(float R, float G, float B)
{
    public static Rgb FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        int Channel(int at) => int.Parse(hex.AsSpan(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Rgb(Channel(1) / 255f, Channel(3) / 255f, Channel(5) / 255f);
    }

    public Rgb Lerp(Rgb to, float weight) => new(
        R + ((to.R - R) * weight),
        G + ((to.G - G) * weight),
        B + ((to.B - B) * weight));
}

/// <summary>
/// How the leaves of one kind of foliage look: each tree takes a tone between <paramref name="Tint"/>
/// and <paramref name="TintB"/>, <paramref name="Amount"/> says how much of its own green that tone
/// replaces, and <paramref name="Snow"/> how much snow lies on its top.
/// </summary>
public readonly record struct FoliageTint(Rgb Tint, Rgb TintB, float Amount, float Snow)
{
    public FoliageTint Lerp(FoliageTint to, float weight) => new(
        Tint.Lerp(to.Tint, weight),
        TintB.Lerp(to.TintB, weight),
        Amount + ((to.Amount - Amount) * weight),
        Snow + ((to.Snow - Snow) * weight));
}

/// <summary>The grass colours of a season, as the terrain shader takes them.</summary>
public readonly record struct GrassLook(Rgb Lush, Rgb Dry, Rgb Deep)
{
    public GrassLook Lerp(GrassLook to, float weight) => new(
        Lush.Lerp(to.Lush, weight), Dry.Lerp(to.Dry, weight), Deep.Lerp(to.Deep, weight));
}

/// <summary>
/// What the valley looks like at one moment of the year (<c>docs/gdd/estaciones-visibles.md</c>):
/// the catalog's season, or the blend of two while one gives way to the next.
/// </summary>
/// <param name="Season">The season that counts for what changes at once, like the flowers: the one the calendar is in.</param>
public sealed record SeasonLook(
    string Season,
    GrassLook Grass,
    float GrassSnow,
    float PeakSnow,
    FoliageTint Deciduous,
    FoliageTint Evergreen,
    Rgb SnowColor,
    float SnowLine)
{
    /// <summary>The look of a season of the catalog.</summary>
    public static SeasonLook Of(SeasonCatalog catalog, string season)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var def = catalog.Seasons[season];
        return new SeasonLook(
            season,
            new GrassLook(Rgb.FromHex(def.Grass.Lush), Rgb.FromHex(def.Grass.Dry), Rgb.FromHex(def.Grass.Deep)),
            def.GrassSnow,
            def.PeakSnow,
            Foliage(def.Deciduous),
            Foliage(def.Evergreen),
            Rgb.FromHex(catalog.SnowColor),
            catalog.SnowLine);
    }

    /// <summary>
    /// The look at a point of the year. The colours of a season hold until the last
    /// <see cref="SeasonCatalog.TransitionDays"/> of it and then pass to those of the next, so the
    /// change shows over days and not at midnight.
    /// </summary>
    /// <param name="seasonIds">The seasons in calendar order.</param>
    /// <param name="daysPerSeason">The length of a season, in days.</param>
    /// <param name="seasonIndex">The season the calendar is in.</param>
    /// <param name="daysIn">How far into the season it is, in days with their fractions, from 0 at its first midnight.</param>
    public static SeasonLook At(SeasonCatalog catalog, IReadOnlyList<string> seasonIds, int daysPerSeason, int seasonIndex, double daysIn)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(seasonIds);
        var current = Of(catalog, seasonIds[seasonIndex]);
        var transition = Math.Min(catalog.TransitionDays, daysPerSeason);
        if (transition <= 0)
        {
            return current;
        }

        var into = (float)((daysIn - (daysPerSeason - transition)) / transition);
        if (into <= 0f)
        {
            return current;
        }

        var next = Of(catalog, seasonIds[(seasonIndex + 1) % seasonIds.Count]);
        var weight = Math.Clamp(into, 0f, 1f);
        weight = weight * weight * (3f - (2f * weight));
        return new SeasonLook(
            current.Season,
            current.Grass.Lerp(next.Grass, weight),
            Mix(current.GrassSnow, next.GrassSnow, weight),
            Mix(current.PeakSnow, next.PeakSnow, weight),
            current.Deciduous.Lerp(next.Deciduous, weight),
            current.Evergreen.Lerp(next.Evergreen, weight),
            current.SnowColor,
            current.SnowLine);
    }

    private static FoliageTint Foliage(FoliageDef def) =>
        new(Rgb.FromHex(def.Tint), Rgb.FromHex(def.TintB), def.Amount, def.Snow);

    private static float Mix(float from, float to, float weight) => from + ((to - from) * weight);
}
