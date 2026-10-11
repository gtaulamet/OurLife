using System.Globalization;
using Godot;
using OurLife.Game.Session;

namespace OurLife.Game.Bootstrap;

/// <summary>
/// <c>--benchmark</c>: plays a game for a while at the fastest speed and checks the budget of IP1,
/// 60 frames per second and under 2 ms per tick (see "Definición de hecho de IP1" in
/// <c>docs/ip/IP1.md</c>). Vsync is off, so the frame rate shows the headroom and not the
/// monitor's refresh. The first seconds, while the game settles, are not counted.
/// </summary>
public partial class Benchmark : Node
{
    private const double WarmUpSeconds = 2;
    private const double MinFramesPerSecond = 60;
    private const double MaxMsPerTick = 2;

    private readonly List<double> _frames = [];
    private GameScreen _screen = null!;
    private double _seconds;
    private double _elapsed;
    private long _startTick;
    private TimeSpan _startTickTime;

    /// <summary>Raised with the exit code once the report is printed: 0 within the budget, 1 over it.</summary>
    public event Action<int>? Finished;

    public static Benchmark Create(GameScreen screen, int seconds)
    {
        ArgumentNullException.ThrowIfNull(screen);
        return new Benchmark { Name = nameof(Benchmark), _screen = screen, _seconds = seconds };
    }

    public override void _Ready()
    {
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        _screen.Session.SetSpeed(GameSession.Speeds[GameSession.Speeds.Count - 1]);
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed < WarmUpSeconds)
        {
            _startTick = _screen.Session.Simulation.CurrentTick;
            _startTickTime = _screen.Session.TickTime;
            return;
        }

        _frames.Add(delta);
        if (_elapsed >= WarmUpSeconds + _seconds && _frames.Count > 0)
        {
            SetProcess(false);
            Finished?.Invoke(Report());
        }
    }

    private int Report()
    {
        var sorted = _frames.Order().ToList();
        var total = sorted.Sum();
        var averageFps = sorted.Count / total;

        // The slowest 1 % of frames, as a frame rate.
        var slowest = sorted[(int)Math.Min(sorted.Count - 1, Math.Floor(sorted.Count * 0.99))];
        var ticks = _screen.Session.Simulation.CurrentTick - _startTick;
        var tickMs = (_screen.Session.TickTime - _startTickTime).TotalMilliseconds;
        var msPerTick = ticks == 0 ? 0 : tickMs / ticks;
        var size = DisplayServer.WindowGetSize();

        GD.Print(string.Create(
            CultureInfo.InvariantCulture,
            $"[benchmark] {RenderingServer.GetVideoAdapterName()} | window {size.X}x{size.Y} | {sorted.Count} frames in {total:F1} s"));
        GD.Print(string.Create(
            CultureInfo.InvariantCulture,
            $"[benchmark] average {averageFps:F0} FPS ({total * 1000 / sorted.Count:F2} ms) | slowest 1 % {1 / slowest:F0} FPS ({slowest * 1000:F2} ms) | worst {sorted[^1] * 1000:F2} ms"));
        GD.Print(string.Create(
            CultureInfo.InvariantCulture,
            $"[benchmark] {ticks} ticks at {GameSession.Speeds[GameSession.Speeds.Count - 1]}x, {msPerTick:F4} ms per tick"));

        if (averageFps < MinFramesPerSecond || msPerTick >= MaxMsPerTick)
        {
            GD.PushError(string.Create(CultureInfo.InvariantCulture, $"[benchmark] over the budget: {MinFramesPerSecond} FPS and {MaxMsPerTick} ms per tick"));
            return 1;
        }

        GD.Print("[benchmark] within the budget");
        return 0;
    }
}
