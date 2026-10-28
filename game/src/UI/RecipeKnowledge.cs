using OurLife.Core.Defs;
using OurLife.Core.Sim;
using OurLife.Game.Bootstrap;

namespace OurLife.Game.UI;

/// <summary>What the panels say of the knowledge a recipe still lacks, so they read it the same way.</summary>
public static class RecipeKnowledge
{
    /// <summary>The knowledge a recipe still lacks, empty when it is unlocked.</summary>
    public static IReadOnlyList<string> Missing(Simulation simulation, RecipeDef recipe)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(recipe);
        return simulation.Unlocks.IsUnlocked(recipe, simulation.Progress) ? [] : simulation.Unlocks.Missing(recipe, simulation.Progress);
    }

    /// <summary>The names of knowledge, translated and joined by commas.</summary>
    public static string Names(GameServices services, Simulation simulation, IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(ids);
        return string.Join(", ", ids.Select(id => services.Text.Format(simulation.Content.Get<KnowledgeDef>(id).NameKey!)));
    }
}
