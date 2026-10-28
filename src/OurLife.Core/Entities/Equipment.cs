using OurLife.Core.Defs;

namespace OurLife.Core.Entities;

/// <summary>
/// The tools a colonist has equipped, at most one per work type (see "Herramientas" in
/// <c>docs/gdd/oficios.md</c>). Each is a stack of one unit with the quality it was made with. An
/// equipped tool is out of the world: it is not hauled and does not count in the stocks. A colonist
/// without the component has no tools; it is added with the first one, so a game with no tools is
/// saved and fingerprinted as before them.
/// </summary>
public sealed class Equipment
{
    public List<ItemStack> Tools { get; set; } = [];
}
