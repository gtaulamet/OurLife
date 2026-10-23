using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
using OurLife.Core.Text;

namespace OurLife.Core.Colony;

/// <summary>The colony learned a knowledge: the panel, the log and what it unlocks read it.</summary>
public sealed record KnowledgeLearnedEvent(string Knowledge) : ISimEvent;

/// <summary>The research queue or the points changed: the panel of knowledge reads them again.</summary>
public sealed record ResearchChangedEvent : ISimEvent;

/// <summary>Where a knowledge is in its way to being learned.</summary>
public enum KnowledgeState
{
    /// <summary>The colony has it.</summary>
    Learned,

    /// <summary>It can be researched and practiced; it is not in the queue.</summary>
    Available,

    /// <summary>It is in the research queue (the first one is being researched).</summary>
    Queued,

    /// <summary>Something it requires is missing, or its era has not come.</summary>
    Blocked,
}

/// <summary>
/// Sets the research queue (see "Cola de investigación" in <c>docs/gdd/conocimientos.md</c>): the
/// knowledge to research, in order, of which only the first advances. Ignored whole if it has more
/// than the most the config allows, a knowledge twice, or one that is not available (learned,
/// blocked or not in the content).
/// </summary>
public sealed record SetResearchQueueCommand([property: DefRef(typeof(KnowledgeDef))] IReadOnlyList<string> Queue) : ISimCommand
{
    public void Apply(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (Queue is null || ResearchRules.ConfigOf(simulation) is not { } config || Queue.Count > config.QueueMax
            || Queue.Distinct(StringComparer.Ordinal).Count() != Queue.Count
            || Queue.Any(knowledge => !ResearchRules.IsAvailable(simulation, knowledge)))
        {
            return;
        }

        if (simulation.Progress.ResearchQueue.SequenceEqual(Queue, StringComparer.Ordinal))
        {
            return;
        }

        simulation.Progress.SetQueue(Queue);
        simulation.Events.Emit(new ResearchChangedEvent());
    }
}

/// <summary>Read-only queries and the rules that give points (see <c>docs/gdd/conocimientos.md</c>).</summary>
public static class ResearchRules
{
    /// <summary>Hundredths of a point in a point.</summary>
    public const int Centi = 100;

    /// <summary>The numbers of research, or null if the content has none and so nothing is researched.</summary>
    public static KnowledgeConfigDef? ConfigOf(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return simulation.Content.All<KnowledgeConfigDef>() is [{ } config] ? config : null;
    }

    /// <summary>Whether a knowledge exists, is not learned and has what it requires and its era: it can be researched and practiced.</summary>
    public static bool IsAvailable(Simulation simulation, string knowledge)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return simulation.Content.TryGet<KnowledgeDef>(knowledge, out var def)
               && !simulation.Progress.Knowledge.Contains(knowledge)
               && simulation.Unlocks.IsUnlocked(def, simulation.Progress);
    }

    public static KnowledgeState State(Simulation simulation, string knowledge)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (simulation.Progress.Knowledge.Contains(knowledge))
        {
            return KnowledgeState.Learned;
        }

        if (!IsAvailable(simulation, knowledge))
        {
            return KnowledgeState.Blocked;
        }

        return simulation.Progress.ResearchQueue.Contains(knowledge) ? KnowledgeState.Queued : KnowledgeState.Available;
    }

    /// <summary>How much of the cost is gained, in hundredths of a percent (10 000 is all); all of it for what is learned.</summary>
    public static int Progress(Simulation simulation, string knowledge)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (simulation.Progress.Knowledge.Contains(knowledge))
        {
            return 10_000;
        }

        var cost = (long)simulation.Content.Get<KnowledgeDef>(knowledge).Cost * Centi;
        if (cost == 0 || !simulation.Progress.Points.TryGetValue(knowledge, out var points))
        {
            return 0;
        }

        return (int)Math.Min(10_000, (points.Research + (long)points.Practice) * 10_000 / cost);
    }

    /// <summary>
    /// The knowledge being researched: the first of the queue, if it can be (a game saved with one
    /// that is blocked or learned does not research it); none if it is empty.
    /// </summary>
    public static string? Current(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return simulation.Progress.ResearchQueue is [{ } first, ..] && IsAvailable(simulation, first) ? first : null;
    }

    /// <summary>The content a knowledge opens: what lists it among its requirements.</summary>
    public static IReadOnlyList<Def> Unlocks(Simulation simulation, string knowledge)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var content = simulation.Content;
        return [.. content.All<BuildableDef>().Cast<Def>()
            .Concat(content.All<RecipeDef>())
            .Concat(content.All<KnowledgeDef>())
            .Where(def => def.Requires.Contains(knowledge))];
    }

    /// <summary>Adds research points, in hundredths, to the knowledge at the head of the queue; none if it is empty.</summary>
    internal static void AddResearch(Simulation simulation, int centipoints)
    {
        if (Current(simulation) is not { } knowledge)
        {
            return;
        }

        simulation.Progress.PointsOf(knowledge).Research += centipoints;
        LearnIfComplete(simulation, knowledge);
    }

    /// <summary>
    /// Adds what a tick of work of a type teaches to each available knowledge that it practices,
    /// up to the share of the cost the config gives to practice.
    /// </summary>
    internal static void AddPractice(Simulation simulation, IReadOnlyList<KnowledgeDef> practiced, KnowledgeConfigDef config)
    {
        foreach (var def in practiced)
        {
            if (!IsAvailable(simulation, def.Id))
            {
                continue;
            }

            var points = simulation.Progress.PointsOf(def.Id);
            var cap = (long)def.Cost * Centi * config.PracticeCapPercent / 100;
            var room = (int)Math.Min(def.Practice!.CentipointsPerTick, Math.Max(0, cap - points.Practice));
            if (room <= 0)
            {
                continue;
            }

            points.Practice += room;
            LearnIfComplete(simulation, def.Id);
        }
    }

    private static void LearnIfComplete(Simulation simulation, string knowledge)
    {
        var def = simulation.Content.Get<KnowledgeDef>(knowledge);
        var points = simulation.Progress.PointsOf(knowledge);
        if (points.Research + (long)points.Practice < (long)def.Cost * Centi)
        {
            return;
        }

        var progress = simulation.Progress;
        progress.Learn(knowledge);
        progress.ForgetPoints(knowledge);
        progress.SetQueue(progress.ResearchQueue.Where(queued => queued != knowledge).ToList());
        simulation.Events.Emit(new KnowledgeLearnedEvent(knowledge));
        simulation.Events.Emit(new ResearchChangedEvent());
        simulation.Events.Emit(new NotificationEvent(
            simulation.CurrentTick,
            ColonyText.Freeze(LocalizedText.Of(LearnedKey).With("knowledge", LocalizedText.Of(def.NameKey!)), simulation.World),
            NotificationSeverity.Info));
    }

    /// <summary>The message of a knowledge learned, with <c>knowledge</c>.</summary>
    public const string LearnedKey = "event.knowledge.learned";
}
