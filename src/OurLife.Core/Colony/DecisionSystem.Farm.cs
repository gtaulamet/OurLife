using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

// Farming (sowing and reaping the farm zones, see docs/gdd/agricultura.md) and the choice of what
// to eat.
public sealed partial class DecisionSystem
{
    // Derived from the state, found the first time a tick needs them and only looked up, never
    // iterated: the crops and the plants by cell, the zones by cell, and where each sower goes.
    private readonly Dictionary<int, EntityId> _cropByCell = [];
    private readonly HashSet<int> _plantCells = [];
    private readonly Dictionary<int, EntityId> _farmZoneByCell = [];
    private readonly Dictionary<int, (EntityId Zone, string Crop)> _sowByCell = [];
    private readonly List<EntityId> _farmZones = [];
    private readonly List<EntityId> _farmEntities = [];
    private long _farmIndexTick = -1;

    private void RefreshFarmIndex(Simulation simulation)
    {
        if (_farmIndexTick == simulation.CurrentTick)
        {
            return;
        }

        _farmIndexTick = simulation.CurrentTick;
        var world = simulation.World;
        var size = simulation.Map.Size;
        _cropByCell.Clear();
        _plantCells.Clear();
        _farmZoneByCell.Clear();
        _sowByCell.Clear();
        world.Query<FarmZone>(_farmZones);
        if (_farmZones.Count == 0)
        {
            return;
        }

        foreach (var zone in _farmZones)
        {
            foreach (var cell in world.Get<FarmZone>(zone).Cells)
            {
                _farmZoneByCell[size.IndexOf(cell.X, cell.Y, cell.Z)] = zone;
            }
        }

        world.Query<Crop, Position>(_farmEntities);
        foreach (var crop in _farmEntities)
        {
            var cell = world.Get<Position>(crop).Cell;
            _cropByCell[size.IndexOf(cell.X, cell.Y, cell.Z)] = crop;
        }

        world.Query<Plant, Position>(_farmEntities);
        foreach (var plant in _farmEntities)
        {
            var cell = world.Get<Position>(plant).Cell;
            _plantCells.Add(size.IndexOf(cell.X, cell.Y, cell.Z));
        }
    }

    /// <summary>The ripe crops on a cell of a zone that nobody reaps.</summary>
    private void AddRipeCrops(Simulation simulation)
    {
        RefreshFarmIndex(simulation);
        var world = simulation.World;
        var size = simulation.Map.Size;
        world.Query<Crop, Position>(_farmEntities);
        foreach (var crop in _farmEntities)
        {
            var cell = world.Get<Position>(crop).Cell;
            if (_farmZoneByCell.ContainsKey(size.IndexOf(cell.X, cell.Y, cell.Z)) && FarmRules.IsRipe(simulation, world.Get<Crop>(crop)))
            {
                AddEntity(simulation, JobKind.Reap, crop, capacity: 1);
            }
        }
    }

    /// <summary>
    /// The empty cells of the zones that nobody sows: of a crop the colony knows and that is ripe
    /// before its seasons end, and, for a tree, those at the spacing of the zone.
    /// </summary>
    private void AddSowableCells(Simulation simulation)
    {
        RefreshFarmIndex(simulation);
        var world = simulation.World;
        var map = simulation.Map;
        foreach (var zone in _farmZones)
        {
            var state = world.Get<FarmZone>(zone);
            if (!simulation.Content.TryGet<CropDef>(state.Crop, out var crop) || !simulation.Unlocks.IsUnlocked(crop, simulation.Progress)
                || !FarmRules.HasTimeToMature(simulation, crop))
            {
                continue;
            }

            foreach (var cell in state.Cells)
            {
                var index = map.Size.IndexOf(cell.X, cell.Y, cell.Z);
                if (_cropByCell.ContainsKey(index) || _plantCells.Contains(index) || IsReserved(map, cell)
                    || (crop.IsTree && (cell.X % crop.Spacing != 0 || cell.Y % crop.Spacing != 0)))
                {
                    continue;
                }

                _sowByCell[index] = (zone, crop.Id);
                _candidates.Add(new Candidate(JobKind.Sow, EntityId.None, cell));
            }
        }
    }

    /// <summary>Whether the need is under its critical threshold: a colonist that starves eats from the nearest food.</summary>
    private static bool IsCritical(Simulation simulation, EntityId colonist, NeedDef need) =>
        simulation.World.Get<Needs>(colonist).Find(need.Id) is { } state && state.Value < need.CriticalThreshold;

    /// <summary>
    /// Ranks the piles of food a colonist can reach for one who is hungry but not starving (see "Qué
    /// comer" in <c>docs/gdd/agricultura.md</c>): of those within <see cref="AiConfigDef.EatChoiceRadius"/>
    /// cells of the nearest, a prepared dish first, then the one that is less fresh, then the
    /// nearest; the farther piles come after. So what is about to rot is eaten first, and the
    /// provisions and berries stay as a reserve.
    /// </summary>
    private void RankFoodChoice(Simulation simulation, List<Reach> reachable)
    {
        if (reachable.Count == 0)
        {
            return;
        }

        var nearest = 0;
        for (var i = 1; i < reachable.Count; i++)
        {
            if (CompareReach(reachable[i], reachable[nearest]) < 0)
            {
                nearest = i;
            }
        }

        var world = simulation.World;
        var origin = reachable[nearest].Candidate.Cell;
        var radius = _rules.Ai.EatChoiceRadius;
        for (var i = 0; i < reachable.Count; i++)
        {
            var entry = reachable[i];
            var apart = entry.Candidate.Cell;
            if (Math.Max(Math.Max(Math.Abs(apart.X - origin.X), Math.Abs(apart.Y - origin.Y)), Math.Abs(apart.Z - origin.Z)) > radius)
            {
                reachable[i] = entry with { Group = 1 };
                continue;
            }

            var stack = world.Get<ItemStack>(entry.Candidate.Target);
            reachable[i] = entry with { NotMeal = simulation.Content.Get<ItemDef>(stack.Def).Meal ? 0 : 1, Freshness = stack.FreshnessValue };
        }
    }

    /// <summary>The order of the candidates: the group, the dish and the freshness of a food choice, then nearest first; ties by cell, then by entity, so the choice never depends on chance.</summary>
    private static int CompareReach(Reach a, Reach b)
    {
        var order = a.Group.CompareTo(b.Group);
        order = order != 0 ? order : a.NotMeal.CompareTo(b.NotMeal);
        order = order != 0 ? order : a.Freshness.CompareTo(b.Freshness);
        order = order != 0 ? order : a.Distance.CompareTo(b.Distance);
        order = order != 0 ? order : a.CellIndex.CompareTo(b.CellIndex);
        return order != 0 ? order : a.Candidate.Target.CompareTo(b.Candidate.Target);
    }
}
