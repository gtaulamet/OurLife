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
    /// The first cell along the ray that is not air, skipping every level above
    /// <paramref name="maxLevel"/>, as the view hides them. A wall is hit from above or from the
    /// side; a floor, ramp or stairs as soon as the ray enters its cell.
    /// </summary>
    public static bool TryFirstHit(
        GameMap map,
        (double X, double Y, double Z) origin,
        (double X, double Y, double Z) direction,
        int maxLevel,
        out TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(map);
        cell = default;
        var size = map.Size;
        var top = Math.Min(size.Depth, maxLevel + 1);
        if (top <= 0 || (direction.X == 0 && direction.Y == 0 && direction.Z == 0))
        {
            return false;
        }

        // Where the ray enters and leaves the box of the visible map.
        double enter = 0, leave = double.PositiveInfinity;
        if (!Clip(origin.X, direction.X, size.Width, ref enter, ref leave)
            || !Clip(origin.Y, direction.Y, size.Height, ref enter, ref leave)
            || !Clip(origin.Z, direction.Z, top, ref enter, ref leave))
        {
            return false;
        }

        // A nudge past the entry face, so the first cell is the one the ray goes into.
        var t = enter + 1e-9;
        var x = Math.Clamp((int)Math.Floor(origin.X + (direction.X * t)), 0, size.Width - 1);
        var y = Math.Clamp((int)Math.Floor(origin.Y + (direction.Y * t)), 0, size.Height - 1);
        var z = Math.Clamp((int)Math.Floor(origin.Z + (direction.Z * t)), 0, top - 1);

        var (stepX, nextX, deltaX) = Axis(origin.X, direction.X, x);
        var (stepY, nextY, deltaY) = Axis(origin.Y, direction.Y, y);
        var (stepZ, nextZ, deltaZ) = Axis(origin.Z, direction.Z, z);

        while (true)
        {
            if (map.ShapeAt(x, y, z) != TileShape.Empty)
            {
                cell = new TileCoord(x, y, z);
                return true;
            }

            if (nextX <= nextY && nextX <= nextZ)
            {
                x += stepX;
                nextX += deltaX;
            }
            else if (nextY <= nextZ)
            {
                y += stepY;
                nextY += deltaY;
            }
            else
            {
                z += stepZ;
                nextZ += deltaZ;
            }

            if ((uint)x >= (uint)size.Width || (uint)y >= (uint)size.Height || (uint)z >= (uint)top)
            {
                return false;
            }
        }
    }

    /// <summary>Narrows [enter, leave] to where the ray is between 0 and <paramref name="max"/> on one axis.</summary>
    private static bool Clip(double origin, double direction, double max, ref double enter, ref double leave)
    {
        if (direction == 0)
        {
            return origin >= 0 && origin < max;
        }

        var t0 = (0 - origin) / direction;
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
