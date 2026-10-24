using OurLife.Core.Defs;
using OurLife.Core.Map;

namespace OurLife.Core.Entities;

/// <summary>
/// A farm zone (see "Zonas de cultivo" in <c>docs/gdd/agricultura.md</c>): its cells, sorted by map
/// index, and the crop sown from now on. A zone without cells does not exist.
/// </summary>
public sealed class FarmZone
{
    /// <summary>Id of the <see cref="CropDef"/> that is sown in the zone.</summary>
    [DefRef(typeof(CropDef))]
    public string Crop { get; set; } = string.Empty;

    public List<TileCoord> Cells { get; set; } = [];
}

/// <summary>
/// A crop sown on a cell of a farm zone, which grows a day at a time at midnight while the season
/// is one of its growing seasons, until it is ripe. It blocks nothing.
/// </summary>
public sealed class Crop
{
    /// <summary>Id of its <see cref="CropDef"/>.</summary>
    [DefRef(typeof(CropDef))]
    public string Def { get; set; } = string.Empty;

    /// <summary>Days of growth so far.</summary>
    public int Days { get; set; }
}
