using OurLife.Core.Colony;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Colony;

/// <summary>Work priorities and alerts (<c>docs/gdd/gestion.md</c>). Tick 0 is 06:00; an hour is 60 ticks.</summary>
public class ManagementTests
{
    private const int Hour = 60;

    [Theory]
    [InlineData(1, 3, JobKind.Mine)]
    [InlineData(3, 1, JobKind.Chop)]
    public void A_colonist_takes_the_work_of_its_highest_priority_first(int mining, int woodcutting, JobKind expected)
    {
        var bed = new ColonyTestBed(ColonyTestBed.Level(12, 12, '#'), MapWithWallAt(12, 5, 5));
        bed.Designate(DesignationKind.Mine, new TileCoord(5, 5, 1), new TileCoord(5, 5, 1));
        bed.AddPlant("tree", 8, 8, 1);
        bed.Designate(DesignationKind.Chop, new TileCoord(8, 8, 1), new TileCoord(8, 8, 1));
        var colonist = bed.AddColonist(6, 6, 1);
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "mining", mining));
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "woodcutting", woodcutting));

        bed.Simulation.Tick();

        bed.JobOf(colonist)!.Kind.ShouldBe(expected);
    }

    [Fact]
    public void A_colonist_never_hauls_with_hauling_off_and_does_with_the_lowest_priority()
    {
        var bed = ColonyTestBed.Flat(16);
        bed.AddItems("log", 10, 2, 2, 1);
        bed.Designate(DesignationKind.Stockpile, new TileCoord(10, 10, 1), new TileCoord(10, 10, 1));
        var colonist = bed.AddColonist(5, 5, 1);
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "hauling", 0));

        bed.Simulation.Run(200);
        bed.CountItemsAt("log", new TileCoord(2, 2, 1)).ShouldBe(10);

        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "hauling", 4));
        bed.Simulation.Run(200);
        bed.CountItemsAt("log", new TileCoord(10, 10, 1)).ShouldBe(10);
    }

    [Fact]
    public void Setting_the_default_priority_drops_the_entry_and_nonsense_is_ignored()
    {
        var bed = ColonyTestBed.Flat(8);
        var colonist = bed.AddColonist(2, 2, 1);

        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "mining", 1));
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "woodcutting", 0));
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "mining", 3));
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "cooking", 1));
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "hauling", 5));
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(EntityId.None, "hauling", 1));
        bed.Simulation.ApplyCommandsNow();

        var entries = bed.World.Get<WorkPriorities>(colonist).Entries;
        entries.Select(entry => (entry.WorkType, entry.Priority)).ShouldBe([("woodcutting", 0)]);
    }

    [Fact]
    public void A_colonist_whose_priorities_are_all_the_default_has_no_component()
    {
        var bed = ColonyTestBed.Flat(8);
        var colonist = bed.AddColonist(2, 2, 1);

        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "mining", 3));
        bed.Simulation.ApplyCommandsNow();
        bed.World.Has<WorkPriorities>(colonist).ShouldBeFalse();

        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "mining", 2));
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(colonist, "mining", 3));
        bed.Simulation.ApplyCommandsNow();
        bed.World.Has<WorkPriorities>(colonist).ShouldBeFalse();
    }

    [Fact]
    public void An_idle_colonist_alerts_once_and_again_after_it_worked()
    {
        var bed = ColonyTestBed.Flat(16);
        var colonist = bed.AddColonist(5, 5, 1);

        bed.Simulation.Run((2 * Hour) - 5);
        IdleAlerts(bed).ShouldBe(0);
        bed.Simulation.Run(10);
        IdleAlerts(bed).ShouldBe(1);
        ColonyAlerts.Active(bed.Simulation).Select(alert => (alert.Colonist, alert.Text.Key)).ShouldBe([(colonist, "alert.idle")]);

        bed.Simulation.Run(Hour);
        IdleAlerts(bed).ShouldBe(0);

        // Work ends the alert; idle again, it alerts again.
        bed.AddPlant("tree", 8, 8, 1);
        bed.Designate(DesignationKind.Chop, new TileCoord(8, 8, 1), new TileCoord(8, 8, 1));
        bed.Simulation.Run(Hour);
        bed.World.Get<Colonist>(colonist).IdleAlerted.ShouldBeFalse();
        ColonyAlerts.Active(bed.Simulation).ShouldBeEmpty();
        bed.Simulation.Run(3 * Hour);
        bed.World.Get<Colonist>(colonist).IdleAlerted.ShouldBeTrue();
    }

    [Fact]
    public void Critical_hunger_alerts_once_and_ends_when_the_colonist_has_eaten()
    {
        var bed = ColonyTestBed.Flat(16);
        var colonist = bed.AddColonist(5, 5, 1, hunger: 1400);
        bed.DrainEvents();

        bed.Simulation.Tick();
        var alerts = bed.DrainEvents().OfType<NotificationEvent>().Where(alert => alert.Text.Key == "event.need.hunger.critical").ToList();
        alerts.Count.ShouldBe(1);
        alerts[0].Severity.ShouldBe(NotificationSeverity.Danger);
        alerts[0].Text.Args["colonist"].ShouldBe("Elvira");
        alerts[0].Text.Args["colonist_gender"].ShouldBe("female");
        ColonyAlerts.Active(bed.Simulation).Single().Text.Key.ShouldBe("alert.need.hunger");

        bed.AddItems("provisions", 5, 6, 6, 1);
        bed.Simulation.Run(2 * Hour);

        bed.Need(colonist, "hunger").Value.ShouldBeGreaterThanOrEqualTo(4000);
        bed.Need(colonist, "hunger").CriticalAlerted.ShouldBeFalse();
        ColonyAlerts.Active(bed.Simulation).ShouldBeEmpty();
    }

    [Fact]
    public void A_text_fixes_a_colonists_name_and_gender_and_keeps_it_after_the_colonist_is_gone()
    {
        var bed = ColonyTestBed.Flat(8);
        var colonist = bed.AddColonist(2, 2, 1, name: "Jimena");
        var text = Core.Text.LocalizedText.Of("event.colonist.idle").With("colonist", colonist).With("count", 3);

        var frozen = ColonyText.Freeze(text, bed.World);
        bed.World.DestroyEntity(colonist);

        frozen.Args["colonist"].ShouldBe("Jimena");
        frozen.Args["colonist_gender"].ShouldBe("female");
        frozen.Args["count"].ShouldBe(3);
        ColonyText.Freeze(text, bed.World).Args["colonist"].ShouldBe(colonist);
    }

    /// <summary>Notifications that a colonist is idle, among the events since the last call.</summary>
    private static int IdleAlerts(ColonyTestBed bed) =>
        bed.DrainEvents().OfType<NotificationEvent>().Count(alert => alert.Text.Key == ColonyAlerts.IdleEventKey);

    private static string MapWithWallAt(int size, int x, int y)
    {
        var floor = ColonyTestBed.Level(size, size, '.').ToCharArray();
        floor[(y * (size + 1)) + x] = '#';
        return new string(floor);
    }
}
