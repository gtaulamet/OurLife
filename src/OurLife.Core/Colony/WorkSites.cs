using OurLife.Core.Entities;
using OurLife.Core.Map;

namespace OurLife.Core.Colony;

/// <summary>Where a colonist stands to do a job (see the table of jobs in <c>docs/gdd/colonos-e-ia.md</c>).</summary>
public static class WorkSites
{
    /// <summary>
    /// Fills <paramref name="sites"/> with the standable cells from which a job on
    /// <paramref name="target"/> can be done, in a fixed order.
    /// </summary>
    public static void Find(GameMap map, JobKind kind, TileCoord target, List<TileCoord> sites)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(sites);
        sites.Clear();
        switch (kind)
        {
            case JobKind.Eat:
            case JobKind.Haul:
            case JobKind.Deliver:
            case JobKind.Sleep:
            case JobKind.Wander:
                if (map.IsStandable(target))
                {
                    sites.Add(target);
                }

                return;
            case JobKind.Mine:
            case JobKind.Channel:
            case JobKind.Build:
            case JobKind.Deconstruct:
                // The cell changes shape under the work, or nobody can stand on it yet.
                AddAround(map, target, sites);
                return;
            case JobKind.DigStairs:
            case JobKind.Chop:
            case JobKind.Harvest:
                // A blocking plant makes its own cell unstandable, so only the neighbours remain.
                if (map.IsStandable(target))
                {
                    sites.Add(target);
                }

                AddAround(map, target, sites);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    /// <summary>True if a colonist on <paramref name="cell"/> can do the job on <paramref name="target"/>.</summary>
    public static bool IsSite(GameMap map, JobKind kind, TileCoord target, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (kind is JobKind.Sleep or JobKind.Wander)
        {
            return true;
        }

        if (!map.IsStandable(cell) || cell.Z != target.Z)
        {
            return false;
        }

        var dx = Math.Abs(cell.X - target.X);
        var dy = Math.Abs(cell.Y - target.Y);
        if (dx == 1 && dy == 1 && !DiagonalIsOpen(map, target, cell))
        {
            return false;
        }

        return kind switch
        {
            JobKind.Eat or JobKind.Haul or JobKind.Deliver => dx == 0 && dy == 0,
            JobKind.Mine or JobKind.Channel or JobKind.Build or JobKind.Deconstruct => Math.Max(dx, dy) == 1,
            _ => Math.Max(dx, dy) <= 1,
        };
    }

    private static void AddAround(GameMap map, TileCoord target, List<TileCoord> sites)
    {
        foreach (var (dx, dy) in MapNavigation.Directions)
        {
            var cell = new TileCoord(target.X + dx, target.Y + dy, target.Z);
            if (map.IsStandable(cell) && (dx == 0 || dy == 0 || DiagonalIsOpen(map, target, cell)))
            {
                sites.Add(cell);
            }
        }
    }

    /// <summary>
    /// A job can be done across a diagonal only where a creature could step across it: neither of
    /// the two cells beside both ends is a wall (<see cref="MapNavigation"/>). Otherwise a dug wall
    /// or a felled tree would leave a cell, and what drops on it, that nobody can walk into.
    /// </summary>
    private static bool DiagonalIsOpen(GameMap map, TileCoord target, TileCoord cell) =>
        map.ShapeOrEmpty(target.X, cell.Y, cell.Z) != TileShape.Wall
        && map.ShapeOrEmpty(cell.X, target.Y, cell.Z) != TileShape.Wall;
}
