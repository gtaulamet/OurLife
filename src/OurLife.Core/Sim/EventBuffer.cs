namespace OurLife.Core.Sim;

/// <summary>
/// Events emitted by the systems, kept in emission order until the presentation drains them,
/// usually once per frame.
/// </summary>
public sealed class EventBuffer
{
    private readonly List<ISimEvent> _events = [];

    public int Count => _events.Count;

    public void Emit(ISimEvent simEvent)
    {
        ArgumentNullException.ThrowIfNull(simEvent);
        _events.Add(simEvent);
    }

    /// <summary>Moves every buffered event to the end of <paramref name="destination"/>.</summary>
    public void DrainTo(List<ISimEvent> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.AddRange(_events);
        _events.Clear();
    }

    /// <summary>Drops every buffered event, for headless runs that never show them.</summary>
    public void Clear() => _events.Clear();
}
