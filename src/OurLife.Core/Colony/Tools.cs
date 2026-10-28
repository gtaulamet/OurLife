using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// The tools colonists equip (see "Herramientas" in <c>docs/gdd/oficios.md</c>): the item with a
/// <see cref="ItemDef.Tool"/> says the work type it speeds up and by how much, and its quality
/// scales that by <see cref="QualityDef.ValuePercent"/>. A colonist carries at most one tool per
/// work type, kept for good: it is neither swapped for a better one nor worn out, and falls on the
/// ground where its owner dies. Read-only queries, but for the equipping itself.
/// </summary>
public static class Tools
{
    /// <summary>The tools a colonist has equipped, in the order it got them; empty if none.</summary>
    public static IReadOnlyList<ItemStack> Of(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return simulation.World.TryGet<Equipment>(colonist, out var equipment) ? equipment.Tools : [];
    }

    /// <summary>The tool a colonist has equipped for a work type, or null.</summary>
    public static ItemStack? EquippedFor(Simulation simulation, EntityId colonist, string workType)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (!simulation.World.TryGet<Equipment>(colonist, out var equipment))
        {
            return null;
        }

        foreach (var tool in equipment.Tools)
        {
            if (simulation.Content.Get<ItemDef>(tool.Def).Tool?.WorkType == workType)
            {
                return tool;
            }
        }

        return null;
    }

    /// <summary>
    /// The speed a tool adds to a work, in percent: the tool's <see cref="ItemTool.SpeedPercent"/> times the
    /// <see cref="QualityDef.ValuePercent"/> of its quality (a masterwork axe of 30 gives 60).
    /// </summary>
    public static int SpeedPercent(Simulation simulation, ItemDef item, string? quality)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(item);
        if (item.Tool is not { } tool)
        {
            return 0;
        }

        var value = simulation.Content.All<QualityConfigDef>().Count == 1 ? QualityRules.From(simulation.Content).Of(quality).ValuePercent : 100;
        return tool.SpeedPercent * value / 100;
    }

    /// <summary>The speed the tool a colonist has for a work type adds to it, in percent; 0 without one.</summary>
    public static int SpeedPercent(Simulation simulation, EntityId colonist, string workType)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return EquippedFor(simulation, colonist, workType) is { } tool
            ? SpeedPercent(simulation, simulation.Content.Get<ItemDef>(tool.Def), tool.Quality)
            : 0;
    }

    /// <summary>Whether the content has some tool for a work type.</summary>
    public static bool Exists(DefDatabase content, string workType)
    {
        ArgumentNullException.ThrowIfNull(content);
        foreach (var item in content.All<ItemDef>())
        {
            if (item.Tool?.WorkType == workType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Keeps one unit of a pile as the colonist's tool for the work type of the item and takes it out
    /// of the world. Nothing happens if the pile is gone, is not a tool, or the colonist has one for
    /// that work already.
    /// </summary>
    internal static bool Equip(Simulation simulation, EntityId colonist, EntityId pile)
    {
        var world = simulation.World;
        if (!world.IsAlive(pile) || !world.TryGet<ItemStack>(pile, out var stack) || stack.Count <= 0
            || simulation.Content.Get<ItemDef>(stack.Def).Tool is not { } tool || EquippedFor(simulation, colonist, tool.WorkType) is not null)
        {
            return false;
        }

        if (!world.TryGet<Equipment>(colonist, out var equipment))
        {
            equipment = new Equipment();
            world.Add(colonist, equipment);
        }

        equipment.Tools.Add(new ItemStack { Def = stack.Def, Count = 1, Quality = stack.Quality });
        ItemPiles.Take(simulation, pile, 1);
        simulation.Events.Emit(new EntityChangedEvent(colonist));
        return true;
    }

    /// <summary>Leaves the tools of a colonist who dies on the ground where it stands.</summary>
    internal static void DropAll(Simulation simulation, EntityId colonist)
    {
        if (!simulation.World.TryGet<Equipment>(colonist, out var equipment))
        {
            return;
        }

        var cell = simulation.World.Get<Position>(colonist).Cell;
        foreach (var tool in equipment.Tools)
        {
            ItemPiles.Drop(simulation, tool.Def, tool.Count, cell, tool.Quality);
        }

        equipment.Tools.Clear();
    }
}
