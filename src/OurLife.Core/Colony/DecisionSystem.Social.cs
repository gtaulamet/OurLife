using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// The company a colonist with free will seeks, and the rivals it keeps away from (see "Vida
/// social con intención" in <c>docs/gdd/libre-albedrio.md</c>).
/// </summary>
public sealed partial class DecisionSystem
{
    // Those a colonist may seek, with their weights: found at each decision.
    private readonly List<(EntityId Item, int Weight)> _liked = [];

    /// <summary>
    /// With free will on, a colonist with nothing else to do may seek someone it likes, with the
    /// chance its traits and mood give: it walks to it and stays by it a while, so the two interact
    /// by the rules of <see cref="SocialSystem"/>. The one it seeks is drawn, its partner, family
    /// and friends weighing more. True if it set off.
    /// </summary>
    private bool TrySeek(Simulation simulation, EntityId colonist)
    {
        if (!FreeWillRules.CanSeekCompany(simulation, colonist) || FreeWillRules.ConfigOf(simulation.Content) is not { } config)
        {
            return false;
        }

        // The roll comes first, so a colonist that will not seek anyone looks at nobody.
        var random = simulation.Random.Get(FreeWillStream);
        if (!random.Chance(FreeWillRules.SeekChance(simulation, colonist)))
        {
            return false;
        }

        FindLiked(simulation, colonist, config);
        for (var attempt = 0; attempt < PathAttempts && _liked.Count > 0; attempt++)
        {
            var index = SocialSystem.PickIndex(_liked, random);
            var other = _liked[index].Item;
            _liked.RemoveAt(index);
            if (TryVisit(simulation, colonist, other))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Sets off to keep <paramref name="other"/> company, after following it <paramref name="follows"/>
    /// times (kept in <see cref="Job.Count"/>); true if there is a way.
    /// </summary>
    private bool TryVisit(Simulation simulation, EntityId colonist, EntityId other, int follows = 0) =>
        TryGoTo(simulation, colonist, new Job { Kind = JobKind.Visit, Target = other, Cell = FreeWillRules.AimOf(simulation.World, other), Count = follows });

    /// <summary>
    /// A visitor on its way whose company will not be within reach of where it is going follows it
    /// to where it will be, while it is within reach for seeking, there is a way and it did not
    /// follow it <see cref="FreeWillConfigDef.SeekFollows"/> times yet; else the visit ends and it
    /// wanders. True if it got another job.
    /// </summary>
    private bool TryFollow(Simulation simulation, EntityId colonist, Job job)
    {
        // The cheap checks first: most of the time the other is still going where it was.
        var world = simulation.World;
        if (job.Stage != JobStage.Moving || !Relationships.IsLivingColonist(world, job.Target))
        {
            return false;
        }

        var aim = FreeWillRules.AimOf(world, job.Target);
        if (aim == job.Cell || FreeWillRules.WillBeNear(simulation, colonist, job.Target)
            || FreeWillRules.ConfigOf(simulation.Content) is not { } config || !FreeWillRules.StillWorthVisiting(simulation, colonist, job.Target))
        {
            return false;
        }

        if (job.Count >= config.SeekFollows || !FreeWillRules.IsNear(world.Get<Position>(colonist).Cell, config.SeekRadius, aim)
            || !TryVisit(simulation, colonist, job.Target, job.Count + 1))
        {
            // It gives up on the other for a while, as on a target it cannot reach, so it does not
            // set off after it again at once.
            world.Get<Colonist>(colonist).Unreachable.Add(new IgnoredTarget
            {
                Entity = job.Target,
                UntilTick = simulation.CurrentTick + _rules.Ai.UnreachableCooldownTicks,
            });
            Wander(simulation, colonist);
        }

        return true;
    }

    /// <summary>Sets off to a job next to its target or cell, by the way there; true if there is one.</summary>
    private bool TryGoTo(Simulation simulation, EntityId colonist, Job job)
    {
        var map = simulation.Map;
        var connectivity = simulation.Connectivity;
        var from = simulation.World.Get<Position>(colonist).Cell;
        FindSites(map, job.Kind, job.Cell);
        KeepInRegion(_sites, connectivity, connectivity.RegionAt(from));
        if (_sites.Contains(from))
        {
            Assign(simulation, colonist, job, []);
            return true;
        }

        if (_paths.TryFind(map, from, _sites, _path))
        {
            Assign(simulation, colonist, job, _path);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Those a colonist may seek, where they will be within the config's radius and in its region:
    /// its partner, family and friends, with the weight they have in the interactions; those it
    /// thinks well enough of and those it wishes to befriend or make up with. A child, who plays
    /// with the others, seeks only its family. Never a rival, not even one of the family, nor one
    /// who cannot interact now or is going to sleep, that the two met lately, that it gave up
    /// following a while ago or that is already coming to keep it company or to play.
    /// </summary>
    private void FindLiked(Simulation simulation, EntityId colonist, FreeWillConfigDef config)
    {
        _liked.Clear();
        var world = simulation.World;
        var connectivity = simulation.Connectivity;
        var social = Relationships.Config(simulation);
        var from = world.Get<Position>(colonist).Cell;
        var cooldown = (long)social.PairCooldownHours * simulation.Time.TicksPerHour;
        var onlyFamily = LifeRules.Stage(simulation, colonist)?.PlayRadius is not null;
        world.TryGet<Desires>(colonist, out var desires);
        var person = world.Get<Colonist>(colonist);
        foreach (var other in _colonists)
        {
            if (other == colonist || !world.IsAlive(other))
            {
                continue;
            }

            // Where it will be: one on its way far off is not sought.
            var aim = FreeWillRules.AimOf(world, other);
            if (!FreeWillRules.IsNear(from, config.SeekRadius, aim))
            {
                continue;
            }

            // The weight first, which leaves out most; then the dearer checks.
            var kind = Relationships.Kind(simulation, colonist, other);
            var weight = kind is RelationKind.Partner or RelationKind.Family ? social.CloseWeightPercent
                : onlyFamily ? 0
                : kind == RelationKind.Friend ? social.CloseWeightPercent
                : Relationships.Opinion(simulation, colonist, other) >= config.SeekMinOpinion || Wishes(simulation, desires, other)
                    ? SocialSystem.OtherWeightPercent
                    : 0;
            // A weight of 0, as in the interactions, leaves one out.
            if (weight == 0 || IsIgnored(person, other, aim) || !FreeWillRules.CanVisit(simulation, colonist, other)
                || SocialSystem.Met(simulation, colonist, other, cooldown) || SocialSystem.Met(simulation, other, colonist, cooldown)
                || !connectivity.AreConnected(from, aim)
                || (world.TryGet<Job>(other, out var theirs) && theirs.Kind is JobKind.Visit or JobKind.Play && theirs.Target == colonist))
            {
                continue;
            }

            _liked.Add((other, weight));
        }
    }

    /// <summary>Whether a colonist wishes to befriend another or to make up with it.</summary>
    private static bool Wishes(Simulation simulation, Desires? desires, EntityId other)
    {
        if (desires is null)
        {
            return false;
        }

        foreach (var active in desires.Active)
        {
            if (active.Target == other && simulation.Content.TryGet<DesireDef>(active.Desire, out var desire)
                && desire.Kind is DesireKind.Befriend or DesireKind.Reconcile)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// With free will on, where the rivals of a colonist near it on its level are, and where those
    /// walking are going, into <see cref="_avoided"/>: so two rivals do not wander off the same way.
    /// True if there is any. Its own level only, as it wanders away on it.
    /// </summary>
    private bool FindRivalsNear(Simulation simulation, EntityId colonist, TileCoord from)
    {
        _avoided.Clear();
        if (!simulation.FreeWill || FreeWillRules.ConfigOf(simulation.Content) is not { } config)
        {
            return false;
        }

        var world = simulation.World;
        foreach (var other in _colonists)
        {
            if (other == colonist || !world.IsAlive(other))
            {
                continue;
            }

            var cell = world.Get<Position>(other).Cell;
            if (cell.Z != from.Z || !FreeWillRules.IsNear(from, config.AvoidRadius, cell) || !Relationships.IsRival(simulation, colonist, other))
            {
                continue;
            }

            _avoided.Add(cell);
            if (FreeWillRules.AimOf(world, other) is var aim && aim != cell && aim.Z == from.Z)
            {
                _avoided.Add(aim);
            }
        }

        return _avoided.Count > 0;
    }
}
