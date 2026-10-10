using OurLife.Core.Map;

namespace OurLife.Core.Pathfinding;

/// <summary>
/// A* over the cells of a <see cref="GameMap"/> with the rules of <see cref="MapNavigation"/>
/// (see <c>docs/gdd/colonos-e-ia.md</c>). A straight step, a ramp or stairs cost
/// <see cref="StraightCost"/>; a diagonal step, <see cref="DiagonalCost"/>. The heuristic is the
/// larger of the octile distance on the plane and the levels to climb, so it never overestimates.
/// Buffers are kept between searches; one instance serves one map at a time and is not thread-safe.
/// </summary>
public sealed class PathFinder
{
    public const int StraightCost = 10;
    public const int DiagonalCost = 14;

    private readonly PriorityQueue<int, long> _open = new();
    private int[] _cost = [];
    private int[] _parent = [];

    // A cell's entries are valid only when its stamp equals the current search, so the arrays
    // never need clearing.
    private int[] _seen = [];
    private int[] _closed = [];
    private int _search;

    /// <summary>Cost of a step between two neighbouring cells.</summary>
    public static int StepCost(TileCoord from, TileCoord to) =>
        from.X != to.X && from.Y != to.Y ? DiagonalCost : StraightCost;

    /// <summary>A lower bound of the cost between two cells.</summary>
    public static int Estimate(TileCoord from, TileCoord to)
    {
        var dx = Math.Abs(from.X - to.X);
        var dy = Math.Abs(from.Y - to.Y);
        var plane = (StraightCost * Math.Max(dx, dy)) + ((DiagonalCost - StraightCost) * Math.Min(dx, dy));
        return Math.Max(plane, StraightCost * Math.Abs(from.Z - to.Z));
    }

    /// <summary>
    /// Finds the cheapest route from <paramref name="start"/> to any of <paramref name="goals"/>.
    /// On success, <paramref name="path"/> holds the cells to walk in order, without the start
    /// (empty if the start is a goal). Call it only with goals in the start's connectivity region,
    /// or the search explores the whole region before failing.
    /// </summary>
    public bool TryFind(GameMap map, TileCoord start, IReadOnlyList<TileCoord> goals, List<TileCoord> path)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(goals);
        ArgumentNullException.ThrowIfNull(path);
        path.Clear();
        if (goals.Count == 0 || !map.IsStandable(start))
        {
            return false;
        }

        foreach (var goal in goals)
        {
            if (goal == start)
            {
                return true;
            }
        }

        Prepare(map.Size.CellCount);
        var size = map.Size;
        var startIndex = size.IndexOf(start.X, start.Y, start.Z);
        Visit(startIndex, 0, -1);
        _open.Enqueue(startIndex, Priority(0, Heuristic(start, goals)));

        Span<TileCoord> neighbours = stackalloc TileCoord[MapNavigation.MaxNeighbours];
        while (_open.TryDequeue(out var index, out _))
        {
            if (_closed[index] == _search)
            {
                continue;
            }

            _closed[index] = _search;
            var cell = size.CoordOf(index);
            if (IsGoal(cell, goals))
            {
                BuildPath(size, index, path);
                return true;
            }

            var count = MapNavigation.GetNeighbours(map, cell.X, cell.Y, cell.Z, neighbours);
            for (var i = 0; i < count; i++)
            {
                var next = neighbours[i];
                var nextIndex = size.IndexOf(next.X, next.Y, next.Z);
                if (_closed[nextIndex] == _search)
                {
                    continue;
                }

                var cost = _cost[index] + StepCost(cell, next);
                if (_seen[nextIndex] == _search && cost >= _cost[nextIndex])
                {
                    continue;
                }

                Visit(nextIndex, cost, index);
                var estimate = Heuristic(next, goals);
                _open.Enqueue(nextIndex, Priority(cost + estimate, estimate));
            }
        }

        return false;
    }

    /// <summary>Lower total first; on a tie, the cell nearer the goal, so searches run straight.</summary>
    private static long Priority(int total, int estimate) => ((long)total << 32) | (uint)estimate;

    private static int Heuristic(TileCoord cell, IReadOnlyList<TileCoord> goals)
    {
        var best = int.MaxValue;
        foreach (var goal in goals)
        {
            best = Math.Min(best, Estimate(cell, goal));
        }

        return best;
    }

    private static bool IsGoal(TileCoord cell, IReadOnlyList<TileCoord> goals)
    {
        foreach (var goal in goals)
        {
            if (goal == cell)
            {
                return true;
            }
        }

        return false;
    }

    private void Prepare(int cellCount)
    {
        _open.Clear();
        if (_cost.Length != cellCount)
        {
            _cost = new int[cellCount];
            _parent = new int[cellCount];
            _seen = new int[cellCount];
            _closed = new int[cellCount];
            _search = 0;
        }

        _search++;
    }

    private void Visit(int index, int cost, int parent)
    {
        _seen[index] = _search;
        _cost[index] = cost;
        _parent[index] = parent;
    }

    private void BuildPath(MapSize size, int goalIndex, List<TileCoord> path)
    {
        for (var index = goalIndex; _parent[index] >= 0; index = _parent[index])
        {
            path.Add(size.CoordOf(index));
        }

        path.Reverse();
    }
}
