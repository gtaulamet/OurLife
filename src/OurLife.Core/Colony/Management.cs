using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
using OurLife.Core.Text;

namespace OurLife.Core.Colony;

/// <summary>
/// Sets a colonist's priority for a work type, from 0 (never) to 4 (see <c>docs/gdd/gestion.md</c>).
/// It rules the colonist's next choice of work, not the job under way. An unknown colonist or work
/// type, or a priority out of range, is ignored.
/// </summary>
public sealed record SetWorkPriorityCommand(
    EntityId Colonist,
    [property: DefRef(typeof(WorkTypeDef))] string WorkType,
    int Priority) : ISimCommand
{
    public void Apply(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        if (!world.IsAlive(Colonist) || !world.Has<Colonist>(Colonist) || !simulation.Content.TryGet<WorkTypeDef>(WorkType, out _)
            || Priority is < WorkPriorityLevels.Never or > WorkPriorityLevels.Lowest)
        {
            return;
        }

        if (!world.TryGet<WorkPriorities>(Colonist, out var priorities))
        {
            priorities = new WorkPriorities();
            world.Add(Colonist, priorities);
        }

        // Only priorities that differ from the default are kept, sorted by work type.
        var entries = priorities.Entries;
        var index = entries.FindIndex(entry => entry.WorkType == WorkType);
        var isDefault = Priority == ColonyRules.From(simulation.Content).Ai.DefaultWorkPriority;
        if (index >= 0 && isDefault)
        {
            entries.RemoveAt(index);
        }
        else if (index >= 0)
        {
            entries[index].Priority = Priority;
        }
        else if (!isDefault)
        {
            var at = entries.FindIndex(entry => string.CompareOrdinal(entry.WorkType, WorkType) > 0);
            entries.Insert(at < 0 ? entries.Count : at, new WorkPriority { WorkType = WorkType, Priority = Priority });
        }

        simulation.Events.Emit(new EntityChangedEvent(Colonist));
    }
}

/// <summary>Reading the work priorities of the colonists, for the colony systems and the presentation.</summary>
public static class WorkPriorityRules
{
    /// <summary>A colonist's priority for a work type: its own, or the default of the <see cref="AiConfigDef"/>.</summary>
    public static int Of(World world, EntityId colonist, string workType, int defaultPriority)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.TryGet<WorkPriorities>(colonist, out var priorities))
        {
            foreach (var entry in priorities.Entries)
            {
                if (entry.WorkType == workType)
                {
                    return entry.Priority;
                }
            }
        }

        return defaultPriority;
    }
}

/// <summary>
/// Keeps the state of the alerts of colonists (see <c>docs/gdd/gestion.md</c>) and tells the
/// player when one starts: a colonist wandering with nothing to do for
/// <see cref="AiConfigDef.IdleAlertHours"/>, and a need with <see cref="NeedDef.AlertWhenCritical"/>
/// below its critical threshold. Runs last in the tick, after the jobs have changed.
/// </summary>
public sealed class AlertSystem : ISimSystem
{
    private readonly ColonyRules _rules;
    private readonly List<EntityId> _colonists = [];

    public AlertSystem(ColonyRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules;
    }

    public void Tick(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var tick = simulation.CurrentTick;
        var idleTicks = (long)_rules.Ai.IdleAlertHours * simulation.Time.TicksPerDay / 24;
        world.Query<Colonist, Needs>(_colonists);
        foreach (var colonist in _colonists)
        {
            var person = world.Get<Colonist>(colonist);

            // Between two walks a wanderer has no job for a tick: that is idle too.
            if (!world.TryGet<Job>(colonist, out var job) || job.Kind == JobKind.Wander)
            {
                person.IdleSinceTick ??= tick;
                if (!person.IdleAlerted && tick - person.IdleSinceTick >= idleTicks)
                {
                    person.IdleAlerted = true;
                    Notify(simulation, ColonyAlerts.IdleEventKey, colonist, NotificationSeverity.Warning);
                }
            }
            else
            {
                person.IdleSinceTick = null;
                person.IdleAlerted = false;
            }

            foreach (var state in world.Get<Needs>(colonist).Values)
            {
                var need = simulation.Content.Get<NeedDef>(state.Need);
                if (!need.AlertWhenCritical)
                {
                    continue;
                }

                if (!state.CriticalAlerted && state.Value < need.CriticalThreshold)
                {
                    state.CriticalAlerted = true;
                    Notify(simulation, ColonyAlerts.CriticalEventKey(need), colonist, NotificationSeverity.Danger);
                }
                else if (state.CriticalAlerted && state.Value >= need.LowThreshold)
                {
                    state.CriticalAlerted = false;
                }
            }
        }
    }

    private static void Notify(Simulation simulation, string key, EntityId colonist, NotificationSeverity severity) =>
        simulation.Events.Emit(new NotificationEvent(simulation.CurrentTick, LocalizedText.Of(key).With("colonist", colonist), severity));
}

/// <summary>An alert under way: whom it is about, its line for the HUD and how serious it is.</summary>
public sealed record ColonyAlert(EntityId Colonist, LocalizedText Text, NotificationSeverity Severity);

/// <summary>The alerts under way, for the HUD, and the keys of their texts (see <c>docs/gdd/gestion.md</c>).</summary>
public static class ColonyAlerts
{
    public const string IdleEventKey = "event.colonist.idle";

    public const string IdleAlertKey = "alert.idle";

    /// <summary>The message when a need goes critical, like <c>event.need.hunger.critical</c>.</summary>
    public static string CriticalEventKey(NeedDef need)
    {
        ArgumentNullException.ThrowIfNull(need);
        return $"event.need.{need.Id}.critical";
    }

    /// <summary>The HUD line while a need is critical, like <c>alert.need.hunger</c>.</summary>
    public static string CriticalAlertKey(NeedDef need)
    {
        ArgumentNullException.ThrowIfNull(need);
        return $"alert.need.{need.Id}";
    }

    /// <summary>The alerts under way, by colonist id: dangers of each colonist before its warnings.</summary>
    public static IReadOnlyList<ColonyAlert> Active(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var colonists = new List<EntityId>();
        world.Query<Colonist, Needs>(colonists);
        var alerts = new List<ColonyAlert>();
        foreach (var colonist in colonists)
        {
            foreach (var state in world.Get<Needs>(colonist).Values.Where(state => state.CriticalAlerted))
            {
                var key = CriticalAlertKey(simulation.Content.Get<NeedDef>(state.Need));
                alerts.Add(new ColonyAlert(colonist, LocalizedText.Of(key).With("colonist", colonist), NotificationSeverity.Danger));
            }

            if (world.Get<Colonist>(colonist).IdleAlerted)
            {
                alerts.Add(new ColonyAlert(colonist, LocalizedText.Of(IdleAlertKey).With("colonist", colonist), NotificationSeverity.Warning));
            }
        }

        return alerts;
    }
}
