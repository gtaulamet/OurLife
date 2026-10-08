namespace OurLife.Core.Sim;

/// <summary>
/// How far the colony has come in history: its current era and the knowledge it has gained.
/// Part of the simulation state, so it is included in the state fingerprint.
/// </summary>
public sealed class ProgressState
{
    public ProgressState(string currentEra)
    {
        ArgumentException.ThrowIfNullOrEmpty(currentEra);
        CurrentEra = currentEra;
    }

    /// <summary>Id of the current era def.</summary>
    public string CurrentEra { get; set; }

    /// <summary>Ids of the knowledge defs the colony has, sorted.</summary>
    public SortedSet<string> Knowledge { get; } = new(StringComparer.Ordinal);
}
