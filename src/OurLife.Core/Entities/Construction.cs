using OurLife.Core.Defs;

namespace OurLife.Core.Entities;

/// <summary>
/// A plan to build something on its cell (see <c>docs/gdd/construccion.md</c>), with the units of
/// the material's item brought so far.
/// </summary>
public sealed class Blueprint
{
    [DefRef(typeof(BuildableDef))]
    public string Buildable { get; set; } = string.Empty;

    [DefRef(typeof(MaterialDef))]
    public string Material { get; set; } = string.Empty;

    public int Delivered { get; set; }
}
