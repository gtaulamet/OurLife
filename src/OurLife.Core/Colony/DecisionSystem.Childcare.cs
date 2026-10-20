using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Pathfinding;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// Raising children (see "Crianza" in <c>docs/gdd/vida-y-generaciones.md</c>): feeding the babies
/// who do not eat on their own, babies who crawl near their mother, and children who play.
/// </summary>
public sealed partial class DecisionSystem
{
    // The food found for feeding in the current search, if it was looked for.
    // A search limit, not balance: a baby crawls towards its mother only within this many times its radius.
    private const int CrawlReach = 4;

    private (EntityId Food, TileCoord Cell, int Free, bool Held)? _food;

    /// <summary>Babies hungry under the low threshold whom nobody is bringing food to yet.</summary>
    private void AddHungryBabies(Simulation simulation)
    {
        foreach (var baby in _colonists)
        {
            if (simulation.World.IsAlive(baby) && LifeRules.Stage(simulation, baby) is { EatsAlone: false } && Hunger.IsLow(simulation, baby))
            {
                AddEntity(simulation, JobKind.FeedBaby, baby, capacity: 1);
            }
        }
    }

    /// <summary>
    /// Whether a parent of the baby, other than this colonist, will feed it: the mother or the
    /// father go first while they are free (idle, wandering or playing), look for this work before
    /// any other and can reach the baby. A parent who failed to reach it, or to find food, leaves it to the others for a while.
    /// </summary>
    private bool ParentWillFeed(Simulation simulation, EntityId colonist, EntityId baby)
    {
        var world = simulation.World;
        if (!world.TryGet<Family>(baby, out var family))
        {
            return false;
        }

        var babyCell = world.Get<Position>(baby).Cell;
        foreach (var relative in new[] { family.Mother, family.Father })
        {
            if (relative is not { Dead: false } parent || !Relationships.IsLivingColonist(world, parent.Entity))
            {
                continue;
            }

            if (parent.Entity == colonist)
            {
                return false;
            }

            // Idle, or wandering, playing or keeping company and about to look again this tick: one
            // who waits for its next look would keep the others from the baby meanwhile.
            var free = !world.TryGet<Job>(parent.Entity, out var job)
                       || (job.Kind is JobKind.Wander or JobKind.Play or JobKind.Visit && (simulation.CurrentTick + parent.Entity.Index) % _rules.Ai.RecheckTicks == 0);
            if (free && FeedsBabies(simulation, parent.Entity) && !IsIgnored(world.Get<Colonist>(parent.Entity), baby, babyCell)
                && simulation.Connectivity.AreConnected(world.Get<Position>(parent.Entity).Cell, babyCell))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a colonist looks for babies to feed before any other work: its stage does this work,
    /// no crisis stops it and no work type it does has a higher priority (childcare goes first
    /// among equals).
    /// </summary>
    private bool FeedsBabies(Simulation simulation, EntityId colonist)
    {
        var stage = LifeRules.Stage(simulation, colonist);
        var priority = WorkPriorityRules.Of(simulation.World, colonist, _childcare.Id, _rules.Ai.DefaultWorkPriority);
        if (!LifeRules.DoesWork(stage, _childcare) || priority == WorkPriorityLevels.Never || MoodRules.CrisisOf(simulation, colonist) is not null)
        {
            return false;
        }

        foreach (var type in _rules.WorkTypes)
        {
            var other = WorkPriorityRules.Of(simulation.World, colonist, type.Id, _rules.Ai.DefaultWorkPriority);
            if (other != WorkPriorityLevels.Never && other < priority && LifeRules.DoesWork(stage, type))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A baby someone now brings food to stops crawling and waits where it is.</summary>
    private void WaitForFood(Simulation simulation, EntityId baby)
    {
        if (simulation.World.TryGet<Job>(baby, out var job) && job.Kind == JobKind.Wander)
        {
            Assign(simulation, baby, new Job { Kind = JobKind.Wander, Cell = simulation.World.Get<Position>(baby).Cell }, []);
        }
    }

    /// <summary>
    /// The food a feeder takes to a baby: the pile nearest the colonist, in its region, with a unit
    /// nobody else holds, not about to be hauled and not one it failed to reach; ties to the lowest
    /// id. It does not depend on the baby, so it is looked for once per decision. Without any food,
    /// the colonist leaves the baby alone for a while, so a parent who cannot feed it does not keep
    /// the others from it; food that others only hold for now does not count as none.
    /// </summary>
    private bool TryFood(Simulation simulation, EntityId colonist, Candidate baby, int region, out EntityId food, out TileCoord cell, out int free)
    {
        var person = simulation.World.Get<Colonist>(colonist);
        if (_food is not { } found || (!found.Food.IsNone && IsIgnored(person, found.Food, found.Cell)))
        {
            found = NearestFood(simulation, person, simulation.World.Get<Position>(colonist).Cell, region);
            _food = found;
        }

        (food, cell, free) = (found.Food, found.Cell, found.Free);
        if (!food.IsNone)
        {
            return true;
        }

        if (!found.Held)
        {
            person.Unreachable.Add(new IgnoredTarget
            {
                Entity = baby.Target,
                Cell = baby.Cell,
                UntilTick = simulation.CurrentTick + _rules.Ai.UnreachableCooldownTicks,
            });
        }

        return false;
    }

    /// <summary>The free food nearest <paramref name="from"/>, with its units nobody holds, and whether some food in reach was only held by others.</summary>
    private (EntityId Food, TileCoord Cell, int Free, bool Held) NearestFood(Simulation simulation, Colonist person, TileCoord from, int region)
    {
        var world = simulation.World;
        var (food, cell, free, held) = (EntityId.None, default(TileCoord), 0, false);
        var best = int.MaxValue;
        world.Query<ItemStack, Position>(_entities);
        foreach (var pile in _entities)
        {
            var stack = world.Get<ItemStack>(pile);
            var at = world.Get<Position>(pile).Cell;
            if (simulation.Content.Get<ItemDef>(stack.Def).Kind != ItemKind.Food || IsIgnored(person, pile, at))
            {
                continue;
            }

            var distance = PathFinder.Estimate(from, at);
            var units = stack.Count - _entityUses.GetValueOrDefault(pile);
            var taken = _hauledPiles.Contains(pile) || units <= 0;
            if ((!taken && distance >= best) || (taken && held))
            {
                continue;
            }

            WorkSites.Find(simulation.Map, JobKind.FeedBaby, at, _recipientSites);
            if (!AnyInRegion(_recipientSites, simulation.Connectivity, region))
            {
                continue;
            }

            if (taken)
            {
                held = true;
            }
            else
            {
                (best, food, cell, free) = (distance, pile, at, units);
            }
        }

        return (food, cell, free, held);
    }

    /// <summary>
    /// A colonist at a stage that plays, like a child, goes to play with someone on its level
    /// within the stage's radius: another who plays, or family, who can interact now and was not met
    /// lately by the rules of <see cref="SocialSystem"/>, so the two do interact when they meet. The playmate is drawn, so a
    /// child does not follow the same one around. True if it set off.
    /// </summary>
    private bool TryPlay(Simulation simulation, EntityId colonist)
    {
        if (LifeRules.Stage(simulation, colonist)?.PlayRadius is not { } radius || MoodRules.Isolates(simulation, colonist))
        {
            return false;
        }

        var world = simulation.World;
        var map = simulation.Map;
        var connectivity = simulation.Connectivity;
        var from = world.Get<Position>(colonist).Cell;
        var cooldown = (long)Relationships.Config(simulation).PairCooldownHours * simulation.Time.TicksPerHour;
        _playmates.Clear();
        foreach (var other in _colonists)
        {
            if (other == colonist || !world.IsAlive(other))
            {
                continue;
            }

            var cell = world.Get<Position>(other).Cell;
            if (cell.Z != from.Z || Math.Max(Math.Abs(cell.X - from.X), Math.Abs(cell.Y - from.Y)) > radius
                || !SocialSystem.CanInteract(simulation, other)
                || !(LifeRules.Stage(simulation, other)?.PlayRadius is not null || Relationships.IsFamily(simulation, colonist, other))
                || SocialSystem.Met(simulation, colonist, other, cooldown) || SocialSystem.Met(simulation, other, colonist, cooldown)
                || !connectivity.AreConnected(from, cell))
            {
                continue;
            }

            _playmates.Add(other);
        }

        var region = connectivity.RegionAt(from);
        var random = simulation.Random.Get("colony.decision");
        for (var attempt = 0; attempt < PathAttempts && _playmates.Count > 0; attempt++)
        {
            var index = random.NextInt(_playmates.Count);
            var playmate = _playmates[index];
            _playmates.RemoveAt(index);
            var cell = world.Get<Position>(playmate).Cell;
            FindSites(map, JobKind.Play, cell);
            KeepInRegion(_sites, connectivity, region);
            var job = new Job { Kind = JobKind.Play, Target = playmate, Cell = cell };
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
    /// Where a baby crawls around: its mother, if she is alive, it can reach her and she is near
    /// (<see cref="CrawlReach"/> times its radius); else where it is, so a baby left behind does not
    /// look for long paths across the map.
    /// </summary>
    private static TileCoord CrawlCentre(Simulation simulation, EntityId baby, TileCoord from, int crawl)
    {
        var world = simulation.World;
        if (world.TryGet<Family>(baby, out var family) && family.Mother is { Dead: false } mother && world.IsAlive(mother.Entity)
            && world.TryGet<Position>(mother.Entity, out var position) && PathFinder.Estimate(from, position.Cell) <= CrawlReach * crawl * PathFinder.StraightCost
            && simulation.Connectivity.AreConnected(from, position.Cell))
        {
            return position.Cell;
        }

        return from;
    }
}
