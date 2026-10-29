using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>What keeps a blueprint from advancing (see "Materiales y avisos" in <c>docs/gdd/construccion.md</c>).</summary>
public enum BuildBlock
{
    /// <summary>Nothing: somebody is bringing material or building, or will.</summary>
    None,

    /// <summary>The colony holds fewer units of the item than the blueprint still lacks.</summary>
    Material,

    /// <summary>No colonist does construction (priority never, or a stage of life that does not work).</summary>
    Builder,

    /// <summary>No colonist who builds can reach a cell next to the blueprint.</summary>
    Unreachable,

    /// <summary>The colony holds enough, but the other plans and jobs claim it first.</summary>
    Competing,
}

/// <summary>
/// Why a blueprint waits: the <see cref="Block"/>, the <see cref="Item"/> it is built of, how many
/// units it still <see cref="Missing"/> and how many the colony holds (<see cref="Available"/>).
/// </summary>
public readonly record struct BuildStatus(BuildBlock Block, string Item, int Missing, int Available);

/// <summary>Read-only queries about what is needed to build (the build bar and the inspector ask them).</summary>
public static class BuildQueries
{
    /// <summary>Units of an item the colony holds: piles lying anywhere, stored or not, and loads being carried.</summary>
    public static int Available(Simulation simulation, string item)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var total = 0;
        var entities = new List<EntityId>();
        world.Query<ItemStack, Position>(entities);
        foreach (var pile in entities)
        {
            var stack = world.Get<ItemStack>(pile);
            total += stack.Def == item ? stack.Count : 0;
        }

        world.Query<Carried>(entities);
        foreach (var colonist in entities)
        {
            var carried = world.Get<Carried>(colonist);
            total += carried.Def == item ? carried.Count : 0;
        }

        return total;
    }

    /// <summary>Units of an item the existing blueprints still lack.</summary>
    public static int Committed(Simulation simulation, string item)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var blueprints = new List<EntityId>();
        simulation.World.Query<Blueprint, Position>(blueprints);
        var total = 0;
        foreach (var blueprint in blueprints)
        {
            var (needed, missing) = Construction.Needs(simulation, simulation.World.Get<Blueprint>(blueprint));
            total += needed == item ? missing : 0;
        }

        return total;
    }

    /// <summary>The item a buildable made of a material takes, or null if the material is not built from an item.</summary>
    public static string? ItemOf(Simulation simulation, string material)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return simulation.Content.TryGet<MaterialDef>(material, out var def) ? def.BuiltFrom : null;
    }

    /// <summary>
    /// Why a blueprint is not advancing. Does not change anything. It follows the same sharing out of
    /// materials as the colonists (<see cref="SupplyBudget"/>), found again on each call over the
    /// piles, blueprints and colonists, with buffers kept per simulation.
    /// </summary>
    public static BuildStatus StatusOf(Simulation simulation, EntityId blueprint)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var (item, missing) = Construction.Needs(simulation, world.Get<Blueprint>(blueprint));
        var available = Available(simulation, item);
        if (missing > 0 && available < missing)
        {
            return new BuildStatus(BuildBlock.Material, item, missing, available);
        }

        var budget = SupplyBudget.For(simulation);
        budget.Compute(simulation, null, null);
        var block = budget.StateOf(blueprint) switch
        {
            SupplyState.Unreachable => budget.AnyBuilder ? BuildBlock.Unreachable : BuildBlock.Builder,
            SupplyState.Starved => BuildBlock.Competing,
            _ => BuildBlock.None,
        };
        return new BuildStatus(block, item, missing, available);
    }
}
