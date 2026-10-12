namespace OurLife.Core.Defs;

/// <summary>A def drawn on the map, whose model the presentation looks up by <see cref="Visual"/>.</summary>
public interface IHasVisual
{
    /// <summary>
    /// Id of its model in the visual catalog (<c>visuals.json</c>), like <c>plant.oak</c>; null for
    /// a def of a type that only sometimes has a model, like a buildable that is part of the terrain.
    /// </summary>
    string? Visual { get; }
}
