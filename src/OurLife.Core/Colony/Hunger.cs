using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>Read-only queries about the needs met by eating, shared by eating, feeding babies and conceiving.</summary>
public static class Hunger
{
    /// <summary>Whether a need met by eating is under its low threshold.</summary>
    public static bool IsLow(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (!simulation.World.TryGet<Needs>(colonist, out var needs))
        {
            return false;
        }

        foreach (var state in needs.Values)
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
    /// The fewest units of food of this nutrition that leave every need met by eating lacking at
    /// most half a unit; 0 for a colonist who is fed.
    /// </summary>
    public static int UnitsToFeed(Simulation simulation, EntityId colonist, int nutrition)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var perUnit = Math.Max(1, nutrition);
        var units = 0;
        foreach (var state in simulation.World.Get<Needs>(colonist).Values)
        {
            var need = simulation.Content.Get<NeedDef>(state.Need);
            var missing = need.Max - state.Value;
            if (need.SatisfiedBy == NeedSatisfaction.Eating && missing > perUnit / 2)
            {
                units = Math.Max(units, (missing - (perUnit / 2) + perUnit - 1) / perUnit);
            }
        }

        return units;
    }
}
