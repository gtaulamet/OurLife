namespace OurLife.Core.Map;

/// <summary>
/// The movement rules of the map (see <c>docs/gdd/mapa-y-generacion.md</c>), shared by the
/// connectivity regions and the pathfinding. The rules are symmetric: if B is a neighbour of A,
/// A is a neighbour of B.
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
    /// Writes into <paramref name="neighbours"/> the cells reachable in one step from a standable
    /// cell, and returns how many there are. The buffer needs room for <see cref="MaxNeighbours"/>.
    /// </summary>
    public static int GetNeighbours(GameMap map, int x, int y, int z, Span<TileCoord> neighbours)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (neighbours.Length < MaxNeighbours)
        {
            throw new ArgumentException($"The buffer needs room for {MaxNeighbours} cells.", nameof(neighbours));
        }

        var shape = map.ShapeOrEmpty(x, y, z);
        if (!shape.IsStandable())
        {
            return 0;
        }

        var count = 0;
        for (var i = 0; i < Horizontal.Length; i++)
        {
            var (dx, dy) = Horizontal[i];
            int nx = x + dx, ny = y + dy;
            if (!map.IsStandable(nx, ny, z))
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
            if (shape == TileShape.Ramp && openAbove && map.IsStandable(nx, ny, z + 1))
            {
                neighbours[count++] = new TileCoord(nx, ny, z + 1);
            }

            if (map.ShapeOrEmpty(nx, ny, z - 1) == TileShape.Ramp && map.ShapeOrEmpty(nx, ny, z) != TileShape.Wall)
            {
                neighbours[count++] = new TileCoord(nx, ny, z - 1);
            }
        }

        if (shape.GoesUp() && map.ShapeOrEmpty(x, y, z + 1).GoesDown())
        {
            neighbours[count++] = new TileCoord(x, y, z + 1);
        }

        if (shape.GoesDown() && map.ShapeOrEmpty(x, y, z - 1).GoesUp())
        {
            neighbours[count++] = new TileCoord(x, y, z - 1);
        }

        return count;
    }
}
