using System.Text.Json.Serialization;
using OurLife.Core.Defs;

namespace OurLife.Core.Entities;

/// <summary>
/// A pile of one kind of item lying on a cell. A kind is an item plus a quality: piles of
/// different quality never mix (see "Pilas con calidad y frescura" in <c>docs/gdd/oficios.md</c>).
/// </summary>
public sealed class ItemStack
{
    /// <summary>Full freshness: the value of a pile that has not started to rot, and of one that never rots.</summary>
    public const int FullFreshness = 10_000;

    /// <summary>Id of its <see cref="Defs.ItemDef"/>.</summary>
    [DefRef(typeof(ItemDef))]
    public string Def { get; set; } = string.Empty;

    public int Count { get; set; }

    /// <summary>
    /// Id of its <see cref="QualityDef"/>; null for the default quality of <see cref="QualityConfigDef"/>,
    /// which is not written to the saved game or the fingerprint, so a pile of normal quality is
    /// saved as before this field existed.
    /// </summary>
    [DefRef(typeof(QualityDef))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Quality { get; set; }

    /// <summary>
    /// From 0 (rotten) to <see cref="FullFreshness"/>; null for full, not written either. Items that
    /// do not rot (<see cref="ItemDef.SpoilDays"/> of 0) always have null.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Freshness { get; set; }

    /// <summary>Freshness as a number: <see cref="FullFreshness"/> when not set.</summary>
    [JsonIgnore]
    public int FreshnessValue => Freshness ?? FullFreshness;
}

/// <summary>The player ordered to chop down this plant.</summary>
public sealed class ChopOrder
{
}

/// <summary>
/// The player ordered to gather from this plant. It has a second use: a ripe <see cref="Crop"/>
/// carries it when the player ordered it reaped (tool Cosechar, see <c>docs/gdd/agricultura.md</c>).
/// Whoever queries it tells them apart by <see cref="Plant"/> or <see cref="Crop"/>: gathering only
/// takes plants, reaping only crops.
/// </summary>
public sealed class HarvestOrder
{
}

/// <summary>The plant was gathered and has nothing to give until <see cref="RegrowsAtTick"/>.</summary>
public sealed class Harvested
{
    public long RegrowsAtTick { get; set; }
}

/// <summary>
/// A plant that is still a sapling: it blocks nothing, gives nothing and takes no order until
/// <see cref="GrowsAtTick"/>, when it becomes an adult (see <c>ForestSystem</c>).
/// </summary>
public sealed class Sapling
{
    public long GrowsAtTick { get; set; }
}
