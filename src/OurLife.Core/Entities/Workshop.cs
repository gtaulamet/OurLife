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

/// <summary>How a production order decides when it is done. O4 adds <c>Keep</c> and <c>Forever</c>.</summary>
public enum WorkOrderMode
{
    /// <summary>Make the recipe until the units of its main output sum <see cref="WorkOrder.Quantity"/>.</summary>
    Count,
}

/// <summary>
/// A production order (see "Órdenes de producción" in <c>docs/gdd/oficios.md</c>), its own entity
/// like a blueprint; the list of orders is the order of the entities. O3 only has the mode
/// <see cref="WorkOrderMode.Count"/>: pausing, the other modes and reordering come with O4, which
/// adds fields here without changing these.
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

    /// <summary>The workshop to use; none for any workshop of the recipe's kind.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public EntityId Workshop { get; set; }
}
