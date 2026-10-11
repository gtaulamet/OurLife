using Godot;
using OurLife.Core.Map;
using OurLife.Game.Rendering;

namespace OurLife.Game.Input;

/// <summary>
/// Turns a point of the screen into the cell under it: the camera ray, in map coordinates, walked
/// through the grid by <see cref="MapRaycast"/>, skipping the levels the view hides: those above
/// it and those deeper than <see cref="LevelShading.MaxDepth"/>. Godot's X, Y and Z are the map's
/// x, z and y (ADR-009).
/// </summary>
public static class CellPicker
{
    public static TileCoord? Pick(Camera3D camera, Vector2 screenPoint, GameMap map, int viewLevel)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(map);
        var origin = camera.ProjectRayOrigin(screenPoint);
        var direction = camera.ProjectRayNormal(screenPoint);
        return MapRaycast.TryFirstHit(map, (origin.X, origin.Z, origin.Y), (direction.X, direction.Z, direction.Y), viewLevel, out var cell, viewLevel - LevelShading.MaxDepth)
            ? cell
            : null;
    }
}
