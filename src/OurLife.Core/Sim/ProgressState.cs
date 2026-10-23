namespace OurLife.Core.Sim;

/// <summary>
/// How far the colony has come in history: its current era and the knowledge it has gained.
/// Part of the simulation state, so it is included in the state fingerprint. Only Core changes
/// it (architecture rule 2): the presentation reads it and sends Commands.
/// </summary>
public sealed class ProgressState
{
    private readonly SortedSet<string> _knowledge = new(StringComparer.Ordinal);
    private readonly List<string> _queue = [];
    private readonly SortedDictionary<string, KnowledgePoints> _points = new(StringComparer.Ordinal);
    private string _currentEra = string.Empty;

    public ProgressState(string currentEra)
    {
        CurrentEra = currentEra;
    }

    /// <summary>Id of the current era def.</summary>
    public string CurrentEra
    {
        get => _currentEra;
        internal set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
            _currentEra = value;
        }
    }

    /// <summary>Ids of the knowledge defs the colony has, sorted.</summary>
    public IReadOnlySet<string> Knowledge => _knowledge;

    /// <summary>Ids of the knowledge in the research queue; only the first one advances.</summary>
    public IReadOnlyList<string> ResearchQueue => _queue;

    /// <summary>The points of the knowledge that is neither learned nor untouched, by id.</summary>
    public IReadOnlyDictionary<string, KnowledgePoints> Points => _points;

    internal void Learn(string knowledge)
    {
        ArgumentException.ThrowIfNullOrEmpty(knowledge);
        _knowledge.Add(knowledge);
    }

    internal void SetQueue(IEnumerable<string> queue)
    {
        _queue.Clear();
        _queue.AddRange(queue);
    }

    /// <summary>The points of a knowledge, created at zero the first time.</summary>
    internal KnowledgePoints PointsOf(string knowledge)
    {
        if (!_points.TryGetValue(knowledge, out var points))
        {
            points = new KnowledgePoints();
            _points[knowledge] = points;
        }

        return points;
    }

    internal void ForgetPoints(string knowledge) => _points.Remove(knowledge);
}

/// <summary>What the colony has gained toward a knowledge, in hundredths of a point (see <c>docs/gdd/conocimientos.md</c>).</summary>
public sealed class KnowledgePoints
{
    /// <summary>Gained by researching at a scriptorium.</summary>
    public int Research { get; set; }

    /// <summary>Gained by practicing, up to a share of the cost.</summary>
    public int Practice { get; set; }
}
