namespace OurLife.Core.Defs;

/// <summary>A def drawn on the map, whose model the presentation looks up by <see cref="Visual"/>.</summary>
public interface IHasVisual
{
    /// <summary>Id of its model in the visual catalog (<c>visuals.json</c>), like <c>plant.oak</c>.</summary>
    string Visual { get; }
}

/// <summary>The models the defs name, for the checks of the visual catalog.</summary>
public static class DefVisuals
{
    /// <summary>
    /// The visual of a def: always for an <see cref="IHasVisual"/>, only for furniture among the
    /// buildables (the rest are part of the terrain); null for defs that are not drawn.
    /// </summary>
    public static string? Of(Def def) => def switch
    {
        IHasVisual drawn => drawn.Visual,
        BuildableDef buildable => buildable.Visual,
        CropDef crop => crop.Visual,
        _ => null,
    };
}
