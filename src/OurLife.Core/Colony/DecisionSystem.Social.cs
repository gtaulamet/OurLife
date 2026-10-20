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
        if (!simulation.FreeWill || FreeWillRules.ConfigOf(simulation.Content) is not { } config
            || LifeRules.Stage(simulation, colonist)?.CrawlRadius is not null || !SocialSystem.CanInteract(simulation, colonist))
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

    /// <summary>Sets off to keep <paramref name="other"/> company where it is now; true if there is a way.</summary>
    private bool TryVisit(Simulation simulation, EntityId colonist, EntityId other)
    {
        var world = simulation.World;
        var map = simulation.Map;
        var connectivity = simulation.Connectivity;
        var from = world.Get<Position>(colonist).Cell;
        var region = connectivity.RegionAt(from);
        var cell = world.Get<Position>(other).Cell;
        FindSites(map, JobKind.Visit, cell);
        KeepInRegion(_sites, connectivity, region);
        var job = new Job { Kind = JobKind.Visit, Target = other, Cell = cell };
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
    /// A visitor on its way whose company moved out of reach of where it was going sets off again
    /// to where it is now. True if it did.
    /// </summary>
    private bool TryFollow(Simulation simulation, EntityId colonist, Job job)
    {
        var world = simulation.World;
        return job.Stage == JobStage.Moving && Relationships.IsLivingColonist(world, job.Target)
               && !SocialSystem.InReach(Relationships.Config(simulation), job.Cell, world.Get<Position>(job.Target).Cell)
               && TryVisit(simulation, colonist, job.Target);
    }

    /// <summary>
    /// Those a colonist may seek, within the config's radius and in its region: its partner, family
    /// and friends, with the weight they have in the interactions; those it thinks well enough of
    /// and those it wishes to befriend or make up with. A child, who plays with the others, seeks
    /// only its family. Never a rival, not even one of the family, nor one who cannot interact now
    /// or that the two met lately.
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
        foreach (var other in _colonists)
        {
            if (other == colonist || !world.IsAlive(other))
            {
                continue;
            }

            var cell = world.Get<Position>(other).Cell;
            if (!FreeWillRules.IsNear(from, config.SeekRadius, cell) || !SocialSystem.CanMeet(simulation, colonist, other, cooldown)
                || !connectivity.AreConnected(from, cell) || IsRival(simulation, social, colonist, other))
            {
                continue;
            }

            var kind = Relationships.Kind(simulation, colonist, other);
            var weight = kind is RelationKind.Partner or RelationKind.Family ? social.CloseWeightPercent
                : onlyFamily ? 0
                : kind == RelationKind.Friend ? social.CloseWeightPercent
                : Relationships.Opinion(simulation, colonist, other) >= config.SeekMinOpinion || Wishes(simulation, desires, other) ? 100
                : 0;
            if (weight > 0)
            {
                _liked.Add((other, weight));
            }
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
    /// Whether either of two colonists thinks of the other as a rival, whatever else ties them: a
    /// quarrel can sour family too, though forgetting does not.
    /// </summary>
    private static bool IsRival(Simulation simulation, SocialConfigDef social, EntityId a, EntityId b) =>
        Relationships.Opinion(simulation, a, b) <= social.RivalOpinion || Relationships.Opinion(simulation, b, a) <= social.RivalOpinion;

    /// <summary>
    /// With free will on, where the rivals of a colonist near it are, and where those walking are
    /// going, into <see cref="_avoided"/>: so two rivals do not wander off the same way. True if
    /// there is any.
    /// </summary>
    private bool FindRivalsNear(Simulation simulation, EntityId colonist, TileCoord from)
    {
        _avoided.Clear();
        if (!simulation.FreeWill || FreeWillRules.ConfigOf(simulation.Content) is not { } config)
        {
            return false;
        }

        var world = simulation.World;
        var social = Relationships.Config(simulation);
        foreach (var other in _colonists)
        {
            if (other == colonist || !world.IsAlive(other))
            {
                continue;
            }

            var cell = world.Get<Position>(other).Cell;
            if (!FreeWillRules.IsNear(from, config.AvoidRadius, cell) || !IsRival(simulation, social, colonist, other))
            {
                continue;
            }

            _avoided.Add(cell);
            if (world.TryGet<Job>(other, out var job) && job.Stage == JobStage.Moving && job.Cell != cell)
            {
                _avoided.Add(job.Cell);
            }
        }

        return _avoided.Count > 0;
    }
}
