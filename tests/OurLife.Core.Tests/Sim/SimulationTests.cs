using OurLife.Core.Sim;
using OurLife.Core.Text;

namespace OurLife.Core.Tests.Sim;

public class SimulationTests
{
    [Fact]
    public void Systems_run_in_registration_order_on_their_scheduled_ticks()
    {
        var log = new List<string>();
        var simulation = NewSimulation();
        simulation.AddSystem(new RecordingSystem("every", log));
        simulation.AddSystem(new RecordingSystem("third", log), interval: 3, offset: 1);

        simulation.Run(5);

        log.ShouldBe(new[] { "every@0", "every@1", "third@1", "every@2", "every@3", "every@4", "third@4" });
        simulation.CurrentTick.ShouldBe(5);
    }

    [Fact]
    public void Commands_apply_at_the_start_of_the_next_tick_in_arrival_order()
    {
        var log = new List<string>();
        var simulation = NewSimulation();
        simulation.AddSystem(new RecordingSystem("system", log));
        simulation.Commands.Enqueue(new RecordingCommand("first", log));
        simulation.Commands.Enqueue(new RecordingCommand("second", log));

        simulation.Tick();

        log.ShouldBe(new[] { "first@0", "second@0", "system@0" });
        simulation.Commands.Count.ShouldBe(0);
    }

    [Fact]
    public void A_command_enqueued_while_applying_waits_for_the_next_tick()
    {
        var log = new List<string>();
        var simulation = NewSimulation();
        simulation.Commands.Enqueue(new ChainingCommand(log));

        simulation.Tick();
        log.ShouldBe(new[] { "chain@0" });

        simulation.Tick();
        log.ShouldBe(new[] { "chain@0", "follow-up@1" });
    }

    [Fact]
    public void Events_are_kept_until_drained()
    {
        var simulation = NewSimulation();
        simulation.AddSystem(new NotifyingSystem());

        simulation.Run(3);

        var drained = new List<ISimEvent>();
        simulation.Events.DrainTo(drained);
        drained.Count.ShouldBe(3);
        drained.OfType<NotificationEvent>().Select(e => e.Tick).ShouldBe(new[] { 0L, 1L, 2L });
        simulation.Events.Count.ShouldBe(0);
    }

    [Fact]
    public void The_current_date_follows_the_clock()
    {
        var simulation = NewSimulation();

        simulation.Run(90);

        simulation.CurrentDate.ShouldBe(new GameDate(1200, 0, "spring", 1, 7, 30));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    [InlineData(3, -1)]
    public void Invalid_schedules_are_rejected(int interval, int offset)
    {
        var simulation = NewSimulation();

        Should.Throw<ArgumentOutOfRangeException>(
            () => simulation.AddSystem(new RecordingSystem("x", []), interval, offset));
    }

    private static Simulation NewSimulation() => new(seed: 1, TestContent.Minimal);

    private sealed class RecordingSystem(string name, List<string> log) : ISimSystem
    {
        public void Tick(Simulation simulation) => log.Add($"{name}@{simulation.CurrentTick}");
    }

    private sealed class NotifyingSystem : ISimSystem
    {
        public void Tick(Simulation simulation) =>
            simulation.Events.Emit(new NotificationEvent(
                simulation.CurrentTick, LocalizedText.Of("event.test.tick"), NotificationSeverity.Info));
    }

    private sealed class RecordingCommand(string name, List<string> log) : ISimCommand
    {
        public void Apply(Simulation simulation) => log.Add($"{name}@{simulation.CurrentTick}");
    }

    private sealed class ChainingCommand(List<string> log) : ISimCommand
    {
        public void Apply(Simulation simulation)
        {
            log.Add($"chain@{simulation.CurrentTick}");
            simulation.Commands.Enqueue(new RecordingCommand("follow-up", log));
        }
    }
}
