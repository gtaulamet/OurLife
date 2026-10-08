using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Sim;

public class TickPacerTests
{
    [Fact]
    public void One_second_at_normal_speed_runs_the_ticks_per_second()
    {
        new TickPacer(ticksPerSecond: 10).TicksFor(1.0).ShouldBe(10);
    }

    [Fact]
    public void Fractions_of_a_tick_carry_over_to_the_next_frame()
    {
        var pacer = new TickPacer(ticksPerSecond: 10);

        pacer.TicksFor(0.25).ShouldBe(2);
        pacer.TicksFor(0.25).ShouldBe(3);
    }

    [Fact]
    public void Speed_multiplies_the_ticks()
    {
        var pacer = new TickPacer(ticksPerSecond: 10) { Speed = 4 };

        pacer.TicksFor(0.5).ShouldBe(20);
    }

    [Fact]
    public void Pause_runs_no_ticks_and_forgets_the_pending_fraction()
    {
        var pacer = new TickPacer(ticksPerSecond: 10);
        pacer.TicksFor(0.25).ShouldBe(2);

        pacer.Speed = 0;
        pacer.TicksFor(1.0).ShouldBe(0);

        pacer.Speed = 1;
        pacer.TicksFor(0.25).ShouldBe(2);
    }

    [Fact]
    public void A_long_frame_is_capped_and_the_backlog_dropped()
    {
        var pacer = new TickPacer(ticksPerSecond: 10, maxTicksPerFrame: 40) { Speed = 4 };

        pacer.TicksFor(5.0).ShouldBe(40);
        pacer.TicksFor(0.0).ShouldBe(0);
    }

    [Fact]
    public void A_negative_speed_is_rejected()
    {
        var pacer = new TickPacer(ticksPerSecond: 10);

        Should.Throw<ArgumentOutOfRangeException>(() => pacer.Speed = -1);
    }
}
