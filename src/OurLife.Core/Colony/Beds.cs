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

    /// <summary>Whether the colonist is at the last stage of life (or has none): one that may leave its parents' room.</summary>
    public static bool IsGrownUp(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var stage = LifeRules.Stage(simulation, colonist);
        return stage is null || stage.Id == LifeRules.AdultOf([.. simulation.Content.All<LifeStageDef>().OrderBy(each => each.FromYears)]).Id;
    }

    /// <summary>Whether a bed belongs to a living colonist it is for.</summary>
    public static bool IsTaken(Simulation simulation, EntityId bed)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var owner = world.Get<Bed>(bed).Owner;
        return Relationships.IsLivingColonist(world, owner) && Fits(simulation, bed, owner);
    }

    /// <summary>The bed a colonist owns and is still for (the lowest id if somehow more than one), or none; <paramref name="buffer"/>, if given, is reused for the query.</summary>
    public static EntityId OwnedBy(Simulation simulation, EntityId colonist, List<EntityId>? buffer = null)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var beds = buffer ?? [];
        simulation.World.Query<Bed>(beds);
        foreach (var bed in beds)
        {
            if (simulation.World.Get<Bed>(bed).Owner == colonist && Fits(simulation, bed, colonist))
            {
                return bed;
            }
        }

        return EntityId.None;
    }

    /// <summary>Whether a colonist owns a bed it is still for; <paramref name="buffer"/>, if given, is reused for the query.</summary>
    public static bool HasBed(Simulation simulation, EntityId colonist, List<EntityId>? buffer = null) => !OwnedBy(simulation, colonist, buffer).IsNone;
}

/// <summary>
/// Reserves a bed for a colonist, or frees it when there is none (see "Dormitorios" in
/// <c>docs/gdd/habitaciones.md</c>). Only the owner of a reserved bed uses it. The colonist's other beds
/// are freed, and whoever had this one loses it. Ignored if the bed does not exist, the colonist is not
/// alive, or the bed is not for the colonist's stage of life (a cradle for an adult).
/// </summary>
public sealed record AssignBedCommand(EntityId Bed, EntityId Colonist) : ISimCommand
{
    public void Apply(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        if (!world.IsAlive(Bed) || !world.TryGet<Bed>(Bed, out var bed))
        {
            return;
        }

        if (Colonist.IsNone)
        {
            Free(simulation, Bed, bed);
            return;
        }

        if (!Relationships.IsLivingColonist(world, Colonist) || !Beds.Fits(simulation, Bed, Colonist))
        {
            return;
        }

        // A colonist has a single bed: the one it owned is freed.
        var previous = Beds.OwnedBy(simulation, Colonist);
        if (!previous.IsNone && previous != Bed)
        {
            Free(simulation, previous, world.Get<Bed>(previous));
        }

        bed.Owner = Colonist;
        simulation.Events.Emit(new EntityChangedEvent(Bed));
    }

    private static void Free(Simulation simulation, EntityId id, Bed bed)
    {
        bed.Owner = EntityId.None;
        simulation.Events.Emit(new EntityChangedEvent(id));
    }
}
