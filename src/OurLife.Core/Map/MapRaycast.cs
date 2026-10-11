namespace OurLife.Core.Map;

/// <summary>
/// Finds the cell a ray points at, walking the grid one cell at a time (Amanatides and Woo). The
/// cell <c>(x, y, z)</c> fills the box from <c>(x, y, z)</c> to <c>(x + 1, y + 1, z + 1)</c> in map
/// coordinates, with <c>z</c> up. A read-only query for the presentation, which turns the camera
/// ray into map coordinates; it is not part of the simulation, so doubles are fine here.
/// </summary>
public static class MapRaycast
{
    /// <summary>
    /// The first cell along the ray that is not air, between <paramref name="minLevel"/> and
    /// <paramref name="maxLevel"/>: the view hides the levels above it and those too deep. A wall,
    /// ramp or stairs up is hit as soon as the ray enters its cell. A floor or the opening of
    /// stairs down lies on the bottom of its cell, so it is hit only where the ray leaves the cell
    /// through the bottom: a slanted ray that only crosses the top of the cell points at the cell
    /// behind it.
    /// </summary>
    public static bool TryFirstHit(
        GameMap map,
        (double X, double Y, double Z) origin,
        (double X, double Y, double Z) direction,
        int maxLevel,
        out TileCoord cell,
        int minLevel = 0) =>
        TryFirstHit(map, origin, direction, maxLevel, minLevel, out cell, out _);

    /// <summary>
    /// Like the other overload, and also how far along the ray the hit is, in lengths of
    /// <paramref name="direction"/>: where the ray enters the cell, or for a floor where it reaches
    /// the ground. With a unit direction it is a distance, to compare with things drawn on the map.
    /// </summary>
    public static bool TryFirstHit(
        GameMap map,
        (double X, double Y, double Z) origin,
        (double X, double Y, double Z) direction,
        int maxLevel,
        int minLevel,
        out TileCoord cell,
        out double distance)
    {
        ArgumentNullException.ThrowIfNull(map);
        cell = default;
        distance = 0;
        var size = map.Size;
        var top = Math.Min(size.Depth, maxLevel + 1);
        var bottom = Math.Max(0, minLevel);
        if (bottom >= top || (direction.X == 0 && direction.Y == 0 && direction.Z == 0))
        {
            return false;
        }

        // Where the ray enters and leaves the box of the visible map.
        double enter = 0, leave = double.PositiveInfinity;
        if (!Clip(origin.X, direction.X, 0, size.Width, ref enter, ref leave)
            || !Clip(origin.Y, direction.Y, 0, size.Height, ref enter, ref leave)
            || !Clip(origin.Z, direction.Z, bottom, top, ref enter, ref leave))
        {
            return false;
        }

        // A nudge past the entry face, so the first cell is the one the ray goes into.
        var t = enter + 1e-9;
        var x = Math.Clamp((int)Math.Floor(origin.X + (direction.X * t)), 0, size.Width - 1);
        var y = Math.Clamp((int)Math.Floor(origin.Y + (direction.Y * t)), 0, size.Height - 1);
        var z = Math.Clamp((int)Math.Floor(origin.Z + (direction.Z * t)), bottom, top - 1);

        var (stepX, nextX, deltaX) = Axis(origin.X, direction.X, x);
        var (stepY, nextY, deltaY) = Axis(origin.Y, direction.Y, y);
        var (stepZ, nextZ, deltaZ) = Axis(origin.Z, direction.Z, z);

        // Where the ray entered the current cell.
        var entered = enter;
        while (true)
        {
            // The same choice as the step below: the ray leaves this cell through the bottom.
            var leavesThroughBottom = stepZ < 0 && nextZ < nextX && nextZ < nextY;
            var shape = map.ShapeAt(x, y, z);
            var thin = shape is TileShape.Floor or TileShape.StairDown;
            if (thin ? leavesThroughBottom : shape != TileShape.Empty)
            {
                cell = new TileCoord(x, y, z);
                distance = thin ? nextZ : entered;
                return true;
            }

            if (nextX <= nextY && nextX <= nextZ)
            {
                x += stepX;
                entered = nextX;
                nextX += deltaX;
            }
            else if (nextY <= nextZ)
            {
                y += stepY;
                entered = nextY;
                nextY += deltaY;
            }
            else
            {
                z += stepZ;
                entered = nextZ;
                nextZ += deltaZ;
            }

            if ((uint)x >= (uint)size.Width || (uint)y >= (uint)size.Height || z < bottom || z >= top)
            {
                return false;
            }
        }
    }

    /// <summary>Narrows [enter, leave] to where the ray is between <paramref name="min"/> and <paramref name="max"/> on one axis.</summary>
    private static bool Clip(double origin, double direction, double min, double max, ref double enter, ref double leave)
    {
        if (direction == 0)
        {
            return origin >= min && origin < max;
        }

        var t0 = (min - origin) / direction;
        var t1 = (max - origin) / direction;
        enter = Math.Max(enter, Math.Min(t0, t1));
        leave = Math.Min(leave, Math.Max(t0, t1));
        return enter < leave;
    }

    /// <summary>Step direction, the ray parameter at the next cell boundary, and the parameter per cell.</summary>
    private static (int Step, double Next, double Delta) Axis(double origin, double direction, int cell)
    {
        if (direction > 0)
        {
            return (1, (cell + 1 - origin) / direction, 1 / direction);
        }

        if (direction < 0)
        {
            return (-1, (cell - origin) / direction, -1 / direction);
        }

        return (0, double.PositiveInfinity, double.PositiveInfinity);
    }
}
