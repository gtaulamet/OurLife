namespace OurLife.Core.Sim;

/// <summary>Commands waiting for the next tick, in arrival order.</summary>
public sealed class CommandQueue
{
    private readonly Queue<ISimCommand> _pending = new();

    public int Count => _pending.Count;

    /// <summary>Pending commands in the order they will be applied.</summary>
    internal IEnumerable<ISimCommand> Pending => _pending;

    public void Enqueue(ISimCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _pending.Enqueue(command);
    }

    /// <summary>
    /// Applies the commands that were pending when the tick started. Commands enqueued while
    /// applying wait for the next tick.
    /// </summary>
    internal void ApplyPending(Simulation simulation)
    {
        for (var remaining = _pending.Count; remaining > 0; remaining--)
        {
            _pending.Dequeue().Apply(simulation);
        }
    }
}
