namespace OurLife.Core.Sim;

/// <summary>
/// Turns real elapsed time into a whole number of ticks for the current speed. It only decides
/// how many ticks run in a frame, never what happens inside them, so it does not affect
/// determinism. When the game falls behind, the backlog is dropped instead of piling up.
/// </summary>
public sealed class TickPacer
{
    private readonly int _ticksPerSecond;
    private readonly int _maxTicksPerFrame;
    private double _pendingTicks;
    private int _speed = 1;

    public TickPacer(int ticksPerSecond, int maxTicksPerFrame = 40)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ticksPerSecond, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTicksPerFrame, 1);
        _ticksPerSecond = ticksPerSecond;
        _maxTicksPerFrame = maxTicksPerFrame;
    }

    /// <summary>Speed multiplier: 0 pauses, 1 is normal speed, 2 and 4 are faster.</summary>
    public int Speed
    {
        get => _speed;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _speed = value;
            if (value == 0)
            {
                _pendingTicks = 0;
            }
        }
    }

    /// <summary>Number of ticks to run for a frame that took <paramref name="deltaSeconds"/>.</summary>
    public int TicksFor(double deltaSeconds)
    {
        if (_speed == 0 || deltaSeconds <= 0)
        {
            return 0;
        }

        _pendingTicks += deltaSeconds * _ticksPerSecond * _speed;
        var ticks = (int)Math.Floor(_pendingTicks);
        if (ticks > _maxTicksPerFrame)
        {
            _pendingTicks = 0;
            return _maxTicksPerFrame;
        }

        _pendingTicks -= ticks;
        return ticks;
    }
}
