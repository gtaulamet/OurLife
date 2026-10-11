using System.Diagnostics;
using Godot;
using OurLife.Core.Sim;

namespace OurLife.Game.Session;

/// <summary>
/// Runs a game in real time: each frame it advances the ticks <see cref="TickPacer"/> asks for at
/// the current speed, then hands the events of those ticks to whoever listens, in emission order
/// (see "Tiempo" in <c>docs/gdd/interfaz-ip1.md</c>). Orders given while paused are applied at once,
/// so their marks show without waiting for time to run.
/// </summary>
public partial class GameSession : Node
{
    /// <summary>The speeds the player can choose, besides the pause.</summary>
    public static readonly IReadOnlyList<int> Speeds = [1, 2, 4];

    private readonly List<ISimEvent> _events = [];
    private TickPacer _pacer = null!;
    private bool _started;

    public Simulation Simulation { get; private set; } = null!;

    /// <summary>Speed while not paused: 1, 2 or 4.</summary>
    public int Speed { get; private set; } = 1;

    public bool Paused { get; private set; }

    /// <summary>True while a menu or the end screen holds the game: time stops and the player cannot pause it off.</summary>
    public bool Held { get; private set; }

    /// <summary>Real time spent running ticks in real time, for <c>--benchmark</c>. Measuring belongs here, outside the deterministic core.</summary>
    public TimeSpan TickTime { get; private set; }

    /// <summary>Raised once per frame with the events of the frame, in emission order. The list is reused.</summary>
    public event Action<IReadOnlyList<ISimEvent>>? EventsRead;

    /// <summary>Raised when the speed, the pause or the hold changes.</summary>
    public event Action? ClockChanged;

    public static GameSession Create(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return new GameSession
        {
            Name = nameof(GameSession),
            Simulation = simulation,
            _pacer = new TickPacer(simulation.Time.TicksPerSecond),
        };
    }

    public void SetSpeed(int speed)
    {
        if (!Speeds.Contains(speed))
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Speeds are 1, 2 and 4.");
        }

        Speed = speed;
        Paused = false;
        UpdatePacer();
    }

    public void TogglePause()
    {
        Paused = !Paused;
        UpdatePacer();
    }

    /// <summary>Stops time while a menu is open, without touching the player's own pause.</summary>
    public void SetHeld(bool held)
    {
        Held = held;
        UpdatePacer();
    }

    /// <summary>Gives an order: applied on the next tick, or at once while time is stopped.</summary>
    public void Enqueue(ISimCommand command)
    {
        Simulation.Commands.Enqueue(command);
        if (_pacer.Speed == 0)
        {
            Simulation.ApplyCommandsNow();
            Publish();
        }
    }

    /// <summary>Runs ticks at once, whatever the speed, and publishes their events.</summary>
    public void Advance(int ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);
        Simulation.Run(ticks);
        Publish();
    }

    public override void _Process(double delta)
    {
        // The first frame's delta holds the time it took to build the game, world generation
        // included: it would start with a burst of ticks before the player sees the map.
        if (!_started)
        {
            _started = true;
            Publish();
            return;
        }

        var ticks = _pacer.TicksFor(delta);
        var started = Stopwatch.GetTimestamp();
        for (var i = 0; i < ticks; i++)
        {
            Simulation.Tick();
        }

        TickTime += Stopwatch.GetElapsedTime(started);

        Publish();
    }

    private void Publish()
    {
        _events.Clear();
        Simulation.Events.DrainTo(_events);
        if (_events.Count > 0)
        {
            EventsRead?.Invoke(_events);
        }
    }

    private void UpdatePacer()
    {
        _pacer.Speed = Paused || Held ? 0 : Speed;
        ClockChanged?.Invoke();
    }
}
