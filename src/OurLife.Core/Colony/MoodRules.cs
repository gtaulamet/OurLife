using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// The mood of the colonists (see "Pensamientos y ánimo" in <c>docs/gdd/vida-y-generaciones.md</c>):
/// read-only queries for the systems and the presentation, and the thoughts events leave. Content
/// without a MoodConfig has no moods: everyone is at 50 and thinks nothing.
/// </summary>
public static class MoodRules
{
    private const int Neutral = 50;
    private const int Max = 100;

    /// <summary>The mood config of the content, or null if it has none.</summary>
    public static MoodConfigDef? ConfigOf(DefDatabase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var configs = content.All<MoodConfigDef>();
        return configs.Count == 1 ? configs[0] : null;
    }

    /// <summary>A colonist's mood, from 0 to 100: the base plus its thoughts of events and of its situation.</summary>
    public static int Value(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (ConfigOf(simulation.Content) is not { } config)
        {
            return Neutral;
        }

        var mood = config.Base;
        foreach (var thought in Thoughts(simulation, colonist))
        {
            mood += thought.Mood;
        }

        return Math.Clamp(mood, 0, Max);
    }

    /// <summary>Every thought a colonist has now: of events still under way, then of its situation.</summary>
    public static IEnumerable<ThoughtDef> Thoughts(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var content = simulation.Content;
        if (simulation.World.TryGet<Mood>(colonist, out var mood))
        {
            foreach (var active in mood.Thoughts)
            {
                if (active.UntilTick > simulation.CurrentTick && content.TryGet<ThoughtDef>(active.Thought, out var thought))
                {
                    yield return thought;
                }
            }
        }

        foreach (var thought in Situation(simulation, colonist))
        {
            yield return thought;
        }
    }

    /// <summary>The thoughts of the colonist's situation: needs under their thresholds, without those another stands in for.</summary>
    public static IEnumerable<ThoughtDef> Situation(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (!simulation.World.TryGet<Needs>(colonist, out var needs))
        {
            return [];
        }

        var holding = new List<ThoughtDef>();
        foreach (var thought in simulation.Content.All<ThoughtDef>())
        {
            if (thought.Need is { } id && needs.Find(id) is { } state && simulation.Content.TryGet<NeedDef>(id, out var need)
                && state.Value < (thought.Below == NeedLevel.Critical ? need.CriticalThreshold : need.LowThreshold))
            {
                holding.Add(thought);
            }
        }

        holding.RemoveAll(thought => holding.Any(other => other.Replaces == thought.Id));
        return holding;
    }

    /// <summary>How fast a colonist works by its mood, in percent.</summary>
    public static int WorkSpeedPercent(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (ConfigOf(simulation.Content) is not { } config)
        {
            return 100;
        }

        var mood = Value(simulation, colonist);
        return config.WorkSpeed.First(band => mood <= band.UpTo).Percent;
    }

    /// <summary>The crisis a colonist is in, or null.</summary>
    public static CrisisDef? CrisisOf(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return simulation.World.TryGet<Crisis>(colonist, out var crisis) && crisis.UntilTick > simulation.CurrentTick
               && simulation.Content.TryGet<CrisisDef>(crisis.Kind, out var kind)
            ? kind
            : null;
    }

    /// <summary>Whether a colonist is in a crisis that keeps it from interacting with others.</summary>
    public static bool Isolates(Simulation simulation, EntityId colonist) => CrisisOf(simulation, colonist)?.Behavior == CrisisBehavior.Isolate;

    /// <summary>
    /// An event leaves a thought, for its hours. One beyond its stack limit replaces the oldest.
    /// Nothing happens for a null id, or in content without the thought.
    /// </summary>
    public static void Feel(Simulation simulation, EntityId colonist, string? thought)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (thought is null || !simulation.Content.TryGet<ThoughtDef>(thought, out var def) || def.DurationHours == 0
            || !simulation.World.IsAlive(colonist))
        {
            return;
        }

        if (!simulation.World.TryGet<Mood>(colonist, out var mood))
        {
            mood = new Mood();
            simulation.World.Add(colonist, mood);
        }

        mood.Thoughts.RemoveAll(active => active.UntilTick <= simulation.CurrentTick);
        var same = mood.Thoughts.FindAll(active => active.Thought == thought);
        if (same.Count >= def.StackLimit)
        {
            mood.Thoughts.Remove(same[0]);
        }

        var ticksPerHour = simulation.Time.TicksPerDay / 24;
        mood.Thoughts.Add(new ActiveThought { Thought = thought, UntilTick = simulation.CurrentTick + ((long)def.DurationHours * ticksPerHour) });
    }

    /// <summary>The same, with the thought the mood config gives an event.</summary>
    public static void Feel(Simulation simulation, EntityId colonist, Func<MoodConfigDef, string?> thoughtOf)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(thoughtOf);
        if (ConfigOf(simulation.Content) is { } config)
        {
            Feel(simulation, colonist, thoughtOf(config));
        }
    }
}
