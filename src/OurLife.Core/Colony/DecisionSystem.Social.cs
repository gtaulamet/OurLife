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
    private readonly List<(EntityId Other, int Weight)> _liked = [];

    /// <summary>
    /// With free will on, a colonist with nothing else to do may seek someone it likes, with the
    /// chance its traits and mood give: it walks to it and stays by it a while, so the two interact
    /// by the rules of <see cref="SocialSystem"/>. The one it seeks is drawn, the partner and the
    /// family weighing more. True if it set off.
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
        var world = simulation.World;
        var map = simulation.Map;
        var connectivity = simulation.Connectivity;
        var from = world.Get<Position>(colonist).Cell;
        var region = connectivity.RegionAt(from);
        for (var attempt = 0; attempt < PathAttempts && _liked.Count > 0; attempt++)
        {
            var index = PickIndex(_liked, random);
            var other = _liked[index].Other;
            _liked.RemoveAt(index);
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
        }

        return false;
    }

    /// <summary>
    /// Those a colonist may seek, within the config's radius and in its region: its partner and
    /// family, with the config's weight; its friends, those it thinks well enough of and those it
    /// wishes to befriend or make up with. Never a rival, nor one who cannot interact now or that
    /// the two met lately.
    /// </summary>
    private void FindLiked(Simulation simulation, EntityId colonist, FreeWillConfigDef config)
    {
        _liked.Clear();
        var world = simulation.World;
        var connectivity = simulation.Connectivity;
        var from = world.Get<Position>(colonist).Cell;
        var cooldown = (long)Relationships.Config(simulation).PairCooldownHours * simulation.Time.TicksPerHour;
        world.TryGet<Desires>(colonist, out var desires);
        foreach (var other in _colonists)
        {
            if (other == colonist || !world.IsAlive(other))
            {
                continue;
            }

            var cell = world.Get<Position>(other).Cell;
            if (!FreeWillRules.IsNear(from, config.SeekRadius, cell) || !SocialSystem.CanInteract(simulation, other)
                || SocialSystem.Met(simulation, colonist, other, cooldown) || SocialSystem.Met(simulation, other, colonist, cooldown)
                || !connectivity.AreConnected(from, cell))
            {
                continue;
            }

            var weight = Relationships.Kind(simulation, colonist, other) switch
            {
                RelationKind.Partner or RelationKind.Family => config.SeekCloseWeightPercent,
                RelationKind.Friend => 100,
                RelationKind.Rival => 0,
                _ => Relationships.Opinion(simulation, colonist, other) >= config.SeekMinOpinion || Wishes(simulation, desires, other) ? 100 : 0,
            };
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

    private static int PickIndex(List<(EntityId Other, int Weight)> weighted, Pcg32 random)
    {
        var total = 0;
        foreach (var (_, weight) in weighted)
        {
            total += weight;
        }

        var roll = random.NextInt(total);
        for (var i = 0; i < weighted.Count; i++)
        {
            if (roll < weighted[i].Weight)
            {
                return i;
            }

            roll -= weighted[i].Weight;
        }

        return weighted.Count - 1;
    }

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
        foreach (var other in _colonists)
        {
            var cell = world.Get<Position>(other).Cell;
            if (other == colonist || !world.IsAlive(other) || !FreeWillRules.IsNear(from, config.AvoidRadius, cell)
                || Relationships.Kind(simulation, colonist, other) != RelationKind.Rival)
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
