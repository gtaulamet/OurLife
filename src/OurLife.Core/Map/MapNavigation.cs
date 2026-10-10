namespace OurLife.Core.Map;

/// <summary>
/// The movement rules of the map (see <c>docs/gdd/mapa-y-generacion.md</c>), shared by the
/// connectivity regions and the pathfinding. Blocked cells are not standable. The rules are
/// symmetric: if B is a neighbour of A, A is a neighbour of B.
/// </summary>
public static class MapNavigation
{
    /// <summary>Most neighbours a cell can have: 8 on its level, 4 up a ramp, 4 down a ramp and 2 by stairs.</summary>
    public const int MaxNeighbours = 18;

    private static readonly (int Dx, int Dy)[] Horizontal =
    [
        (0, -1), (1, 0), (0, 1), (-1, 0),
        (1, -1), (1, 1), (-1, 1), (-1, -1),
    ];

    /// <summary>
    /// The 8 directions on a level, in the order every neighbour search uses: north, east, south,
    /// west, then the diagonals. North is towards lower <c>y</c>.
    /// </summary>
    public static IReadOnlyList<(int Dx, int Dy)> Directions => Horizontal;

    /// <summary>
    /// Writes into <paramref name="neighbours"/> the cells reachable in one step from a standable
    /// cell, and returns how many there are. The buffer needs room for <see cref="MaxNeighbours"/>.
    /// With <paramref name="throughBlocked"/>, blocked cells count as standable: the ways there
    /// would be if every tree were gone, for the generator to find which trees close off the surface.
    /// </summary>
    public static int GetNeighbours(GameMap map, int x, int y, int z, Span<TileCoord> neighbours, bool throughBlocked = false)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (neighbours.Length < MaxNeighbours)
        {
            throw new ArgumentException($"The buffer needs room for {MaxNeighbours} cells.", nameof(neighbours));
        }

        bool Standable(int cx, int cy, int cz) =>
            throughBlocked ? map.ShapeOrEmpty(cx, cy, cz).IsStandable() : map.IsStandable(cx, cy, cz);

        if (!Standable(x, y, z))
        {
            return 0;
        }

        var shape = map.ShapeAt(x, y, z);

        var count = 0;
        for (var i = 0; i < Horizontal.Length; i++)
        {
            var (dx, dy) = Horizontal[i];
            int nx = x + dx, ny = y + dy;
            if (!Standable(nx, ny, z))
            {
                continue;
            }

            var diagonal = dx != 0 && dy != 0;
            if (diagonal && (map.ShapeOrEmpty(nx, y, z) == TileShape.Wall || map.ShapeOrEmpty(x, ny, z) == TileShape.Wall))
            {
                continue;
            }

            neighbours[count++] = new TileCoord(nx, ny, z);
        }

        // Ramps: up from a ramp with open space above it, down onto a ramp with open space above it.
        var openAbove = map.ShapeOrEmpty(x, y, z + 1) != TileShape.Wall;
        for (var i = 0; i < 4; i++)
        {
            var (dx, dy) = Horizontal[i];
            int nx = x + dx, ny = y + dy;
            if (shape == TileShape.Ramp && openAbove && Standable(nx, ny, z + 1))
            {
                neighbours[count++] = new TileCoord(nx, ny, z + 1);
            }

            // Every target is checked as standable, so a blocked ramp or stair is never a
            // neighbour: the rules stay symmetric.
            if (map.ShapeOrEmpty(nx, ny, z - 1) == TileShape.Ramp && Standable(nx, ny, z - 1)
                && map.ShapeOrEmpty(nx, ny, z) != TileShape.Wall)
            {
                neighbours[count++] = new TileCoord(nx, ny, z - 1);
            }
        }

        if (shape.GoesUp() && map.ShapeOrEmpty(x, y, z + 1).GoesDown() && Standable(x, y, z + 1))
        {
            neighbours[count++] = new TileCoord(x, y, z + 1);
        }

        if (shape.GoesDown() && map.ShapeOrEmpty(x, y, z - 1).GoesUp() && Standable(x, y, z - 1))
        {
            neighbours[count++] = new TileCoord(x, y, z - 1);
        }

        return count;
    }
}
