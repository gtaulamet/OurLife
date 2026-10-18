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
    /// <summary>Babies hungry under the low threshold whom nobody is bringing food to yet.</summary>
    private void AddHungryBabies(Simulation simulation)
    {
        foreach (var baby in _colonists)
        {
            if (simulation.World.IsAlive(baby) && LifeRules.Stage(simulation, baby) is { EatsAlone: false } && HungerIsLow(simulation, baby))
            {
                AddEntity(simulation, JobKind.FeedBaby, baby, capacity: 1);
            }
        }
    }

    private static bool HungerIsLow(Simulation simulation, EntityId colonist)
    {
        foreach (var state in simulation.World.Get<Needs>(colonist).Values)
        {
            var need = simulation.Content.Get<NeedDef>(state.Need);
            if (need.SatisfiedBy == NeedSatisfaction.Eating && state.Value < need.LowThreshold)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a parent of the baby, other than this colonist, will feed it: the mother or the
    /// father go first while they are free (idle, wandering or playing), do this work and can reach
    /// the baby. A parent who failed to reach it, or to find food, leaves it to the others for a while.
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
            if (relative is not { Dead: false } parent || !world.IsAlive(parent.Entity) || !world.Has<Colonist>(parent.Entity))
            {
                continue;
            }

            if (parent.Entity == colonist)
            {
                return false;
            }

            var free = !world.TryGet<Job>(parent.Entity, out var job) || job.Kind is JobKind.Wander or JobKind.Play;
            if (free && FeedsBabies(simulation, parent.Entity) && !IsIgnored(world.Get<Colonist>(parent.Entity), baby, babyCell)
                && simulation.Connectivity.AreConnected(world.Get<Position>(parent.Entity).Cell, babyCell))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a colonist does the work of feeding babies: its stage, its priority and no crisis allow it.</summary>
    private bool FeedsBabies(Simulation simulation, EntityId colonist) =>
        LifeRules.DoesWork(LifeRules.Stage(simulation, colonist), _childcare)
        && WorkPriorityRules.Of(simulation.World, colonist, _childcare.Id, _rules.Ai.DefaultWorkPriority) != WorkPriorityLevels.Never
        && MoodRules.CrisisOf(simulation, colonist) is null;

    /// <summary>
    /// The food pile nearest the colonist, in its region, with a unit nobody else takes and not
    /// about to be hauled; ties to the lowest id. Without one, the colonist leaves the baby alone
    /// for a while, so a parent who cannot feed it does not keep the others from it.
    /// </summary>
    private bool TryFood(Simulation simulation, EntityId colonist, Candidate baby, int region, out EntityId food, out TileCoord cell)
    {
        var world = simulation.World;
        var person = world.Get<Colonist>(colonist);
        var from = world.Get<Position>(colonist).Cell;
        food = EntityId.None;
        cell = default;
        var best = int.MaxValue;
        world.Query<ItemStack, Position>(_entities);
        foreach (var pile in _entities)
        {
            var stack = world.Get<ItemStack>(pile);
            var at = world.Get<Position>(pile).Cell;
            if (simulation.Content.Get<ItemDef>(stack.Def).Kind != ItemKind.Food || _hauledPiles.Contains(pile)
                || _entityUses.GetValueOrDefault(pile) >= stack.Count || IsIgnored(person, pile, at))
            {
                continue;
            }

            var distance = PathFinder.Estimate(from, at);
            if (distance >= best)
            {
                continue;
            }

            WorkSites.Find(simulation.Map, JobKind.FeedBaby, at, _recipientSites);
            if (AnyInRegion(_recipientSites, simulation.Connectivity, region))
            {
                (best, food, cell) = (distance, pile, at);
            }
        }

        if (!food.IsNone)
        {
            return true;
        }

        person.Unreachable.Add(new IgnoredTarget
        {
            Entity = baby.Target,
            Cell = baby.Cell,
            UntilTick = simulation.CurrentTick + _rules.Ai.UnreachableCooldownTicks,
        });
        return false;
    }

    /// <summary>
    /// A colonist at a stage that plays, like a child, goes to play with someone on its level
    /// within the stage's radius: another who plays, or family, awake and not met lately (so the
    /// two can interact when they meet, see <see cref="SocialSystem"/>). The playmate is drawn, so a
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
            var stage = LifeRules.Stage(simulation, other);
            var asleep = world.TryGet<Job>(other, out var job) && job.Kind == JobKind.Sleep && job.Stage == JobStage.Working;
            if (cell.Z != from.Z || Math.Max(Math.Abs(cell.X - from.X), Math.Abs(cell.Y - from.Y)) > radius || asleep
                || stage?.Interacts == false || !(stage?.PlayRadius is not null || Relationships.IsFamily(simulation, colonist, other))
                || MetLately(simulation, colonist, other, cooldown) || !connectivity.AreConnected(from, cell))
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

    private static bool MetLately(Simulation simulation, EntityId a, EntityId b, long cooldown) =>
        Relationships.Of(simulation, a, b)?.LastInteractionTick is { } last && simulation.CurrentTick - last < cooldown;

    /// <summary>Where a baby crawls around: its mother, if she is alive and it can reach her; else where it is.</summary>
    private static TileCoord CrawlCentre(Simulation simulation, EntityId baby, TileCoord from)
    {
        var world = simulation.World;
        if (world.TryGet<Family>(baby, out var family) && family.Mother is { Dead: false } mother && world.IsAlive(mother.Entity)
            && world.TryGet<Position>(mother.Entity, out var position) && simulation.Connectivity.AreConnected(from, position.Cell))
        {
            return position.Cell;
        }

        return from;
    }
}
