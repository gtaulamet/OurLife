using OurLife.Core.Ecs;

namespace OurLife.Core.Sim;

/// <summary>
/// The whole simulation: world, clock, random streams, commands, events and systems.
/// Each <see cref="Tick"/> applies the pending commands, runs the scheduled systems in
/// registration order and advances the clock. Same seed and same commands at the same
/// ticks always give the same state (see <see cref="ComputeStateHash"/>).
/// </summary>
public sealed class Simulation
{
    private readonly List<ScheduledSystem> _systems = [];

    public Simulation(ulong seed, TimeSettings time)
    {
        ArgumentNullException.ThrowIfNull(time);
        Seed = seed;
        Calendar = new Calendar(time);
        Random = new RngStreams(seed);
    }

    public ulong Seed { get; }

    public Calendar Calendar { get; }

    public TimeSettings Time => Calendar.Settings;

    public World World { get; } = new();

    public RngStreams Random { get; }

    public CommandQueue Commands { get; } = new();

    public EventBuffer Events { get; } = new();

    /// <summary>Ticks already simulated, which is also the number of the next tick to run.</summary>
    public long CurrentTick { get; private set; }

    public GameDate CurrentDate => Calendar.DateAt(CurrentTick);

    /// <summary>
    /// Registers a system that runs on the ticks where <c>tick % interval == offset</c>.
    /// Register every system before running; the order of registration is the order of execution.
    /// </summary>
    public void AddSystem(ISimSystem system, int interval = 1, int offset = 0)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentOutOfRangeException.ThrowIfLessThan(interval, 1);
        if (offset < 0 || offset >= interval)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "The offset must be in [0, interval).");
        }

        _systems.Add(new ScheduledSystem(system, interval, offset));
    }

    public void Tick()
    {
        Commands.ApplyPending(this);
        foreach (var scheduled in _systems)
        {
            if (CurrentTick % scheduled.Interval == scheduled.Offset)
            {
                scheduled.System.Tick(this);
            }
        }

        CurrentTick++;
    }

    public void Run(int ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);
        for (var i = 0; i < ticks; i++)
        {
            Tick();
        }
    }

    /// <summary>64-bit fingerprint of the current state. Costly: meant for tests and debugging.</summary>
    public ulong ComputeStateHash() => StateHasher.Compute(this);

    private readonly record struct ScheduledSystem(ISimSystem System, int Interval, int Offset);
}
