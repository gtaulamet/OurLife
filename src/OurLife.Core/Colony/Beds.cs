using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// Which beds a colonist takes (see <c>docs/gdd/muebles-y-camas.md</c>): a bed for some stages of
/// life, like a cradle for babies, only takes colonists at those stages, and one whose owner grew
/// out of it is free. A colonist without a stage counts as <see cref="LifeRules.AdultOf"/>.
/// </summary>
public static class Beds
{
    /// <summary>Whether the bed is for a colonist at the colonist's stage of life.</summary>
    public static bool Fits(Simulation simulation, EntityId bed, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        if (!world.TryGet<Furniture>(bed, out var furniture))
        {
            return true;
        }

        var stages = simulation.Content.Get<BuildableDef>(furniture.Buildable).ForStages;
        if (stages.Count == 0)
        {
            return true;
        }

        var stage = LifeRules.Stage(simulation, colonist)?.Id
                    ?? LifeRules.AdultOf([.. simulation.Content.All<LifeStageDef>().OrderBy(each => each.FromYears)]).Id;
        return stages.Contains(stage);
    }

    /// <summary>Whether a bed belongs to a living colonist it is for.</summary>
    public static bool IsTaken(Simulation simulation, EntityId bed)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var owner = world.Get<Bed>(bed).Owner;
        return Relationships.IsLivingColonist(world, owner) && Fits(simulation, bed, owner);
    }

    /// <summary>Whether a colonist owns a bed it is still for; <paramref name="buffer"/>, if given, is reused for the query.</summary>
    public static bool HasBed(Simulation simulation, EntityId colonist, List<EntityId>? buffer = null)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var beds = buffer ?? [];
        simulation.World.Query<Bed>(beds);
        return beds.Exists(bed => simulation.World.Get<Bed>(bed).Owner == colonist && Fits(simulation, bed, colonist));
    }
}
