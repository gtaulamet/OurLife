using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Pathfinding;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>The recipes of the content (see "Recetas" in <c>docs/gdd/oficios.md</c>). Read-only queries.</summary>
public static class Recipes
{
    /// <summary>The recipes made at a workshop, in the order of its list: by <see cref="RecipeDef.Order"/>, then id.</summary>
    public static IReadOnlyList<RecipeDef> Of(DefDatabase content, string workshop)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrEmpty(workshop);
        return [.. content.All<RecipeDef>().Where(recipe => recipe.Workshop == workshop).OrderBy(recipe => recipe.Order)];
    }
}

/// <summary>
/// The rules of the ingredients of a workshop and of where a colonist fetches them from (see "El
/// trabajo craft" in <c>docs/gdd/oficios.md</c>), shared by the decisions and the jobs.
/// </summary>
public static class Crafting
{
    /// <summary>Whether a pile of an item and quality is what the input takes.</summary>
    public static bool Accepts(Simulation simulation, RecipeInput input, string item, string? quality)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(input);
        var matches = input.Item is not null
            ? item == input.Item
            : simulation.Content.Get<ItemDef>(item).Category == input.Category;
        return matches && (input.MinQuality is null || QualityRules.From(simulation.Content).AtLeast(quality, input.MinQuality));
    }

    /// <summary>Units of what the input takes that are at the workshop.</summary>
    public static int Held(Simulation simulation, Workshop workshop, RecipeInput input)
    {
        ArgumentNullException.ThrowIfNull(workshop);
        var held = 0;
        foreach (var stack in workshop.Inputs)
        {
            if (Accepts(simulation, input, stack.Def, stack.Quality))
            {
                held += stack.Count;
            }
        }

        return held;
    }

    /// <summary>Units of an input still to bring to the workshop.</summary>
    public static int Missing(Simulation simulation, Workshop workshop, RecipeInput input) =>
        Math.Max(0, input.Count - Held(simulation, workshop, input));

    /// <summary>Whether every input of the recipe is at the workshop.</summary>
    public static bool HasAll(Simulation simulation, RecipeDef recipe, Workshop workshop)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        foreach (var input in recipe.Inputs)
        {
            if (Missing(simulation, workshop, input) > 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The workshop entities of a buildable, by ascending id.</summary>
    public static void WorkshopsOf(Simulation simulation, string buildable, List<EntityId> buffer)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        world.Query<Workshop, Furniture>(buffer);
        buffer.RemoveAll(shop => world.Get<Furniture>(shop).Buildable != buildable);
    }

    /// <summary>Puts a load on the ingredients of a workshop: it joins the pile of its item and quality, if any.</summary>
    internal static void Deliver(Simulation simulation, EntityId entity, string item, int count, string? quality, int? freshness)
    {
        var shop = simulation.World.Get<Workshop>(entity);
        quality = QualityRules.Canonical(simulation.Content, quality);
        var spoils = simulation.Content.Get<ItemDef>(item).SpoilDays > 0;
        var fresh = spoils ? Math.Clamp(freshness ?? ItemStack.FullFreshness, 0, ItemStack.FullFreshness) : ItemStack.FullFreshness;
        var stack = shop.Inputs.Find(own => own.Def == item && own.Quality == quality);
        if (stack is null)
        {
            shop.Inputs.Add(new ItemStack { Def = item, Count = count, Quality = quality, Freshness = ItemPiles.AsStored(fresh) });
        }
        else
        {
            stack.Freshness = ItemPiles.AsStored(ItemPiles.Average(stack.Count, stack.FreshnessValue, count, fresh));
            stack.Count += count;
        }

        shop.InputsSinceTick = simulation.CurrentTick;
        simulation.Events.Emit(new EntityChangedEvent(entity));
    }

    /// <summary>Uses up the inputs of a recipe from the workshop, the first piles delivered first.</summary>
    internal static void Consume(Simulation simulation, RecipeDef recipe, Workshop workshop)
    {
        foreach (var input in recipe.Inputs)
        {
            var remaining = input.Count;
            foreach (var stack in workshop.Inputs)
            {
                if (remaining > 0 && stack.Count > 0 && Accepts(simulation, input, stack.Def, stack.Quality))
                {
                    var taken = Math.Min(stack.Count, remaining);
                    stack.Count -= taken;
                    remaining -= taken;
                }
            }
        }

        workshop.Inputs.RemoveAll(stack => stack.Count <= 0);
    }

    /// <summary>Leaves what a workshop holds on the ground, on its cell or by it, loose to be hauled.</summary>
    internal static void ReleaseInputs(Simulation simulation, EntityId entity)
    {
        var world = simulation.World;
        var shop = world.Get<Workshop>(entity);
        if (shop.Inputs.Count == 0)
        {
            return;
        }

        var cell = Construction.StandableNear(simulation.Map, world.Get<Position>(entity).Cell);
        foreach (var stack in shop.Inputs)
        {
            ItemPiles.Drop(simulation, stack.Def, stack.Count, cell, stack.Quality, stack.Freshness);
        }

        shop.Inputs.Clear();
        simulation.Events.Emit(new EntityChangedEvent(entity));
    }

    /// <summary>
    /// The pile nearest the colonist that an input takes, within the fetch radius of the workshop
    /// and reachable, that the predicate does not hold; none if there is no such pile. Ties go to
    /// the lowest id.
    /// </summary>
    internal static EntityId FindPile(
        Simulation simulation, RecipeInput input, TileCoord shopCell, TileCoord from, int radius, Predicate<EntityId> taken, List<EntityId> buffer)
    {
        var world = simulation.World;
        var connectivity = simulation.Connectivity;
        var reach = radius * PathFinder.StraightCost;
        var best = EntityId.None;
        var bestDistance = int.MaxValue;
        world.Query<ItemStack, Position>(buffer);
        foreach (var pile in buffer)
        {
            var stack = world.Get<ItemStack>(pile);
            var cell = world.Get<Position>(pile).Cell;
            if (stack.Count <= 0 || !Accepts(simulation, input, stack.Def, stack.Quality) || PathFinder.Estimate(cell, shopCell) > reach)
            {
                continue;
            }

            var distance = PathFinder.Estimate(from, cell);
            if (distance < bestDistance && !taken(pile) && connectivity.AreConnected(from, cell))
            {
                best = pile;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Units of what an input takes in the piles within the radius of the workshop that the predicate does not hold.</summary>
    internal static int Available(
        Simulation simulation, RecipeInput input, TileCoord shopCell, TileCoord from, int radius, Predicate<EntityId> taken, List<EntityId> buffer)
    {
        var world = simulation.World;
        var connectivity = simulation.Connectivity;
        var reach = radius * PathFinder.StraightCost;
        var total = 0;
        world.Query<ItemStack, Position>(buffer);
        foreach (var pile in buffer)
        {
            var stack = world.Get<ItemStack>(pile);
            var cell = world.Get<Position>(pile).Cell;
            if (stack.Count > 0 && Accepts(simulation, input, stack.Def, stack.Quality) && PathFinder.Estimate(cell, shopCell) <= reach
                && !taken(pile) && connectivity.AreConnected(from, cell))
            {
                total += stack.Count;
            }
        }

        return total;
    }
}
