namespace OurLife.Core.Save;

/// <summary>Why a saved game could not be loaded. The presentation picks a localized text for each.</summary>
public enum SaveLoadError
{
    /// <summary>The file cannot be read or lacks data.</summary>
    Corrupt,

    /// <summary>The file was written by a newer format version.</summary>
    Newer,

    /// <summary>The file was written by an older format version with no migration.</summary>
    Older,

    /// <summary>The game uses a def the current content lacks; <see cref="SaveLoadException.Detail"/> is its id.</summary>
    MissingContent,
}

/// <summary>A saved game that cannot be loaded, with the reason (ADR-010).</summary>
public sealed class SaveLoadException : Exception
{
    public SaveLoadException(SaveLoadError reason, string detail, Exception? innerException = null)
        : base($"The saved game cannot be loaded ({reason}): {detail}", innerException)
    {
        Reason = reason;
        Detail = detail;
    }

    public SaveLoadError Reason { get; }

    /// <summary>For <see cref="SaveLoadError.MissingContent"/>, the missing id; otherwise a description for the log.</summary>
    public string Detail { get; }
}
