using OurLife.Core.Ecs;
using OurLife.Core.Map;

namespace OurLife.Core.Entities;

/// <summary>What a colonist can do. Each kind has its text in <c>job.&lt;id&gt;.report</c> (see <see cref="JobKinds"/>).</summary>
public enum JobKind
{
    Mine,
    DigStairs,
    Chop,
    Harvest,
    Eat,
    Sleep,
    Wander,

    /// <summary>Carrying a loose pile to a stockpile (see <c>docs/gdd/almacenes.md</c>).</summary>
    Haul,

    /// <summary>Bringing materials to a blueprint (see <c>docs/gdd/construccion.md</c>).</summary>
    Deliver,

    /// <summary>Building a blueprint that has all its materials.</summary>
    Build,

    /// <summary>Taking down what was built on a cell.</summary>
    Deconstruct,

    /// <summary>Opening a floor down to the level below.</summary>
    Channel,

    /// <summary>Bringing food to a baby who cannot eat on its own (see "Crianza" in <c>docs/gdd/vida-y-generaciones.md</c>).</summary>
    FeedBaby,

    /// <summary>A child going to play with another, then staying by it a while.</summary>
    Play,
}

public enum JobStage
{
    /// <summary>Walking to where the work is done.</summary>
    Moving,

    /// <summary>Doing the work where it is done.</summary>
    Working,

    /// <summary>Carrying what it picked up to <see cref="Job.Destination"/>.</summary>
    Carrying,
}

/// <summary>
/// The job a colonist is doing. The target is an entity (a plant, a pile of food) or a cell (a
/// wall to dig, the place to wander to). A colonist without this component is idle.
/// </summary>
public sealed class Job
{
    public JobKind Kind { get; set; }

    public JobStage Stage { get; set; }

    /// <summary>The plant or item worked on, or none.</summary>
    public EntityId Target { get; set; }

    /// <summary>The cell worked on, or walked to.</summary>
    public TileCoord Cell { get; set; }

    /// <summary>Work ticks done in the current stage.</summary>
    public int Progress { get; set; }

    /// <summary>Tick at which waiting ends, for jobs that wait.</summary>
    public long UntilTick { get; set; }

    /// <summary>True when the colonist gathers to eat, not because the player ordered it.</summary>
    public bool ForFood { get; set; }

    /// <summary>For hauling, the stockpile cell where the load goes; for delivering, the blueprint's cell.</summary>
    public TileCoord Destination { get; set; }

    /// <summary>For delivering, the blueprint the materials go to; for feeding, the baby.</summary>
    public EntityId Recipient { get; set; }
}

public static class JobKinds
{
    /// <summary>The id of a kind in texts, like <c>dig_stairs</c>.</summary>
    public static string Id(JobKind kind) => kind switch
    {
        JobKind.Mine => "mine",
        JobKind.DigStairs => "dig_stairs",
        JobKind.Chop => "chop",
        JobKind.Harvest => "harvest",
        JobKind.Eat => "eat",
        JobKind.Sleep => "sleep",
        JobKind.Wander => "wander",
        JobKind.Haul => "haul",
        JobKind.Deliver => "deliver",
        JobKind.Build => "build",
        JobKind.Deconstruct => "deconstruct",
        JobKind.Channel => "channel",
        JobKind.FeedBaby => "feed_baby",
        JobKind.Play => "play",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>Key of the text that says what a colonist is doing, like <c>job.chop.report</c>.</summary>
    public static string ReportKey(JobKind kind) => $"job.{Id(kind)}.report";
}
