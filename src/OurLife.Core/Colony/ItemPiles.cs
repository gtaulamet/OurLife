using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>Items on the ground: they pile up by kind on a cell, up to the item's stack limit.</summary>
public static class ItemPiles
{
    /// <summary>
    /// Leaves <paramref name="count"/> units of an item on a cell: first on the piles of the same
    /// item already there, by ascending id, then on new piles. Returns the first pile that got units.
    /// Finding the piles of the cell looks at every pile: drops happen only when a job ends or a
    /// hauler delivers, a few times per tick at most.
    /// </summary>
    public static EntityId Drop(Simulation simulation, string item, int count, TileCoord cell)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var limit = simulation.Content.Get<ItemDef>(item).StackLimit;
        var world = simulation.World;
        var first = EntityId.None;

        var piles = new List<EntityId>();
        world.Query<ItemStack, Position>(piles);
        foreach (var pile in piles)
        {
            var stack = world.Get<ItemStack>(pile);
            if (count == 0 || stack.Def != item || stack.Count >= limit || world.Get<Position>(pile).Cell != cell)
            {
                continue;
            }

            var added = Math.Min(count, limit - stack.Count);
            stack.Count += added;
            count -= added;
            first = first.IsNone ? pile : first;
            simulation.Events.Emit(new EntityChangedEvent(pile));
        }

        while (count > 0)
        {
            var added = Math.Min(count, limit);
            var pile = world.CreateEntity();
            world.Add(pile, new Position { X = cell.X, Y = cell.Y, Z = cell.Z });
            world.Add(pile, new ItemStack { Def = item, Count = added });
            count -= added;
            first = first.IsNone ? pile : first;
            simulation.Events.Emit(new EntitySpawnedEvent(pile));
        }

        return first;
    }

    /// <summary>Takes one unit from a pile, which disappears when it runs out.</summary>
    public static void TakeOne(Simulation simulation, EntityId pile) => Take(simulation, pile, 1);

    /// <summary>Takes units from a pile, at most what it has; the pile disappears when it runs out.</summary>
    public static void Take(Simulation simulation, EntityId pile, int count)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var stack = simulation.World.Get<ItemStack>(pile);
        stack.Count -= Math.Min(count, stack.Count);
        if (stack.Count > 0)
        {
            simulation.Events.Emit(new EntityChangedEvent(pile));
            return;
        }

        simulation.Events.Emit(new EntityRemovedEvent(pile));
        simulation.World.DestroyEntity(pile);
    }
}
