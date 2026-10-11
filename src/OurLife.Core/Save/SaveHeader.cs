namespace OurLife.Core.Save;

/// <summary>
/// What the presentation adds to a saved game: which game it belongs to, whether it is an
/// autosave, the version of the game and when it was saved. Core never reads the clock, so the
/// caller gives the time.
/// </summary>
public sealed record SaveInfo(string GameId, bool IsAutosave, string GameVersion, DateTimeOffset SavedAt);

/// <summary>
/// The small entry at the start of a saved game, enough to list the slots without reading the
/// state (ADR-010).
/// </summary>
public sealed class SaveHeader
{
    public int FormatVersion { get; set; }

    public string GameId { get; set; } = string.Empty;

    public bool IsAutosave { get; set; }

    public string GameVersion { get; set; } = string.Empty;

    public DateTimeOffset SavedAt { get; set; }

    public string ColonyName { get; set; } = string.Empty;

    /// <summary>The tick of the saved game; the date comes from the calendar of the content.</summary>
    public long Tick { get; set; }

    public string Era { get; set; } = string.Empty;

    /// <summary>Colonists alive when the game was saved.</summary>
    public int Colonists { get; set; }
}
