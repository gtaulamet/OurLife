using System.Text.Json.Serialization;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;

namespace OurLife.Core.Entities;

/// <summary>
/// A workshop: the furniture entity of a buildable with a workstation, where colonists make what
/// recipes say (see "Talleres" and "El trabajo craft" in <c>docs/gdd/oficios.md</c>). It holds the
/// ingredients delivered to it, which stay there if the worker leaves, so nothing is lost.
/// </summary>
public sealed class Workshop
{
    /// <summary>The piles delivered, in the order of delivery: one per item, quality and freshness mix as in the world.</summary>
    public List<ItemStack> Inputs { get; set; } = [];

    /// <summary>Tick of the last delivery. Ingredients nobody uses for a day fall to the ground.</summary>
    public long InputsSinceTick { get; set; }
}

/// <summary>How a production order decides when it is done.</summary>
public enum WorkOrderMode
{
    /// <summary>Make the recipe until the units of its main output sum <see cref="WorkOrder.Quantity"/>.</summary>
    Count,

    /// <summary>Make the recipe while the stock of its main output, with what is being made, is under <see cref="WorkOrder.Quantity"/>.</summary>
    Keep,

    /// <summary>Make the recipe without stopping.</summary>
    Forever,
}

/// <summary>
/// A production order (see "Órdenes de producción" in <c>docs/gdd/oficios.md</c>), its own entity
/// like a blueprint; the list of orders is the order of <see cref="Sequence"/>, then of the entities.
/// </summary>
public sealed class WorkOrder
{
    [DefRef(typeof(RecipeDef))]
    public string Recipe { get; set; } = string.Empty;

    public WorkOrderMode Mode { get; set; }

    /// <summary>Units of the main output to make, in mode <see cref="WorkOrderMode.Count"/>.</summary>
    public int Quantity { get; set; }

    /// <summary>Units of the main output made so far.</summary>
    public int Done { get; set; }

    /// <summary>The player paused the order: nobody makes it until it is resumed.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Paused { get; set; }

    /// <summary>Place in the list of orders, lowest first (the player reorders them).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Sequence { get; set; }

    /// <summary>The workshop to use; none for any workshop of the recipe's kind.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public EntityId Workshop { get; set; }
}
