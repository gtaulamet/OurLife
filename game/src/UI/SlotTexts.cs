using OurLife.Core.Save;
using OurLife.Core.Sim;
using OurLife.Game.Bootstrap;
using OurLife.Game.Session;

namespace OurLife.Game.UI;

/// <summary>The texts of saved games: how a slot reads in a list, and why one cannot be loaded.</summary>
public static class SlotTexts
{
    /// <summary>Village, date and hour of the game, and colonists; for a damaged slot, why.</summary>
    public static string Summary(GameServices services, SaveSlot slot)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(slot);
        if (slot.Header is not { } header)
        {
            return Problem(services, slot.Problem!);
        }

        var date = new Calendar(services.Defs.Time).DateAt(header.Tick);
        return services.Text.Format(
            "ui.slot.summary",
            ("name", header.ColonyName),
            ("date", services.Text.Date(date)),
            ("clock", services.Text.Clock(date)),
            ("count", header.Colonists));
    }

    /// <summary>When the slot was saved, by the player's clock, and whether it is an autosave.</summary>
    public static string Detail(GameServices services, SaveSlot slot)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(slot);
        if (slot.Header is not { } header)
        {
            return string.Empty;
        }

        var culture = services.Localizer.Catalog(services.Localizer.CurrentLocale).Culture;
        var when = header.SavedAt.ToLocalTime().ToString("g", culture);
        return services.Text.Format(header.IsAutosave ? "ui.slot.autosaved_at" : "ui.slot.saved_at", ("when", when));
    }

    /// <summary>The name of the village of a slot, for questions about it.</summary>
    public static string Name(SaveSlot slot) => slot.Header?.ColonyName ?? slot.Id;

    /// <summary>Why a saved game cannot be loaded.</summary>
    public static string Problem(GameServices services, SaveLoadException problem)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(problem);
        return problem.Reason switch
        {
            SaveLoadError.Newer => services.Text.Format("ui.error.save_newer"),
            SaveLoadError.Older => services.Text.Format("ui.error.save_older"),
            SaveLoadError.MissingContent => services.Text.Format("ui.error.save_missing_content", ("id", problem.Detail)),
            _ => services.Text.Format("ui.error.save_corrupt"),
        };
    }
}
