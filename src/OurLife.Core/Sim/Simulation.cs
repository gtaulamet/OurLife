using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Map;

namespace OurLife.Core.Sim;

/// <summary>
/// The whole simulation: content, world, clock, random streams, progress, commands, events and
/// systems. Each <see cref="Tick"/> applies the pending commands, runs the scheduled systems in
/// registration order and advances the clock. Same content, seed and commands at the same ticks
/// always give the same state (see <see cref="ComputeStateHash"/>).
/// </summary>
public sealed class Simulation
{
    private readonly List<ScheduledSystem> _systems = [];
    private GameMap? _map;
    private readonly List<int> _navigationChanges = [];
    private Connectivity? _connectivity;
    private int _connectivityVersion;

    public Simulation(ulong seed, DefDatabase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Seed = seed;
        Content = content;
        Calendar = new Calendar(content.Time);
        Random = new RngStreams(seed);
        Progress = new ProgressState(content.FirstEra.Id);
    }

    public ulong Seed { get; }

    /// <summary>Validated game content: materials, plants, items, eras, time settings…</summary>
    public DefDatabase Content { get; }

    /// <summary>Current era and known knowledge of the colony.</summary>
    public ProgressState Progress { get; }

    public Calendar Calendar { get; }

    public TimeSettings Time => Calendar.Settings;

    public World World { get; } = new();

    public bool HasMap => _map is not null;

    /// <summary>The terrain, once <see cref="WorldGen.WorldGenerator"/> has generated it.</summary>
    public GameMap Map => _map ?? throw new InvalidOperationException("The map has not been generated yet.");

    /// <summary>
    /// Connectivity regions of the map, brought up to date on first use after a change to where
    /// creatures can walk (<see cref="GameMap.NavigationVersion"/>): merged in place when the
    /// changes only opened ways, computed again otherwise. Derived data: not part of the state.
    /// </summary>
    public Connectivity Connectivity
    {
        get
        {
            var map = Map;
            if (_connectivity is not null && _connectivityVersion == map.NavigationVersion)
            {
                return _connectivity;
            }

            _navigationChanges.Clear();
            var onlyOpened = map.DrainNavigationChanges(_navigationChanges);
            if (_connectivity is not null && onlyOpened)
            {
                _connectivity.OpenWays(map, _navigationChanges);
            }
            else
            {
                _connectivity = Connectivity.Compute(map);
            }

            _connectivityVersion = map.NavigationVersion;
            return _connectivity;
        }
    }

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

    /// <summary>Gives the simulation its map, and optionally the connectivity already computed for it.</summary>
    internal void AttachMap(GameMap map, Connectivity? connectivity = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (_map is not null)
        {
            throw new InvalidOperationException("The simulation already has a map.");
        }

        _map = map;
        if (connectivity is not null)
        {
            _navigationChanges.Clear();
            map.DrainNavigationChanges(_navigationChanges);
            _connectivity = connectivity;
            _connectivityVersion = map.NavigationVersion;
        }
    }

    /// <summary>64-bit fingerprint of the current state. Costly: meant for tests and debugging.</summary>
    public ulong ComputeStateHash() => StateHasher.Compute(this);

    private readonly record struct ScheduledSystem(ISimSystem System, int Interval, int Offset);
}
