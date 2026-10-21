using OurLife.Core.Defs;

namespace OurLife.Core.Entities;

/// <summary>A pile of one kind of item lying on a cell.</summary>
public sealed class ItemStack
{
    /// <summary>Id of its <see cref="Defs.ItemDef"/>.</summary>
    [DefRef(typeof(ItemDef))]
    public string Def { get; set; } = string.Empty;

    public int Count { get; set; }
}

/// <summary>The player ordered to chop down this plant.</summary>
public sealed class ChopOrder
{
}

/// <summary>The player ordered to gather from this plant.</summary>
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
