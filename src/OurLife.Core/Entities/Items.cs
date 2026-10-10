namespace OurLife.Core.Entities;

/// <summary>A pile of one kind of item lying on a cell.</summary>
public sealed class ItemStack
{
    /// <summary>Id of its <see cref="Defs.ItemDef"/>.</summary>
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
