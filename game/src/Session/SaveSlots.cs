using System.Globalization;
using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Save;
using OurLife.Core.Sim;

namespace OurLife.Game.Session;

/// <summary>A saved game on disk: its id, and its header or why the header cannot be read.</summary>
public sealed record SaveSlot(string Id, SaveHeader? Header, SaveLoadException? Problem)
{
    public bool IsAutosave => Header?.IsAutosave ?? false;
}

/// <summary>
/// The slots of saved games (see "Ranuras" in <c>docs/gdd/guardado.md</c>): one file per slot in
/// a folder, <c>user://saves</c> for the player. Writing goes to a temporary file that then
/// replaces the slot, so a crash never leaves a slot half written.
/// </summary>
public sealed class SaveSlots
{
    public const string Extension = ".olsave";
    private const string TemporaryExtension = ".tmp";

    public SaveSlots(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        Directory = directory;
    }

    public string Directory { get; }

    /// <summary>The player's slots, in the user data folder.</summary>
    public static SaveSlots ForPlayer() => new(ProjectSettings.GlobalizePath("user://saves"));

    /// <summary>A new id for a game, kept in its saves so its autosave slot follows it.</summary>
    public static string NewGameId() => Guid.NewGuid().ToString("N");

    /// <summary>The slot where a game autosaves, replaced every day.</summary>
    public static string AutosaveId(string gameId) => $"auto-{gameId}";

    /// <summary>The version of the game, from the project settings, written in every save.</summary>
    public static string GameVersion => ProjectSettings.GetSetting("application/config/version").AsString();

    public bool Any() => System.IO.Directory.Exists(Directory) && System.IO.Directory.EnumerateFiles(Directory, "*" + Extension).Any();

    /// <summary>Every slot, the most recent first; damaged slots go last, so the player can delete them.</summary>
    public IReadOnlyList<SaveSlot> List()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return [];
        }

        var slots = new List<SaveSlot>();
        foreach (var path in System.IO.Directory.EnumerateFiles(Directory, "*" + Extension))
        {
            var id = Path.GetFileNameWithoutExtension(path);
            try
            {
                using var file = File.OpenRead(path);
                slots.Add(new SaveSlot(id, SaveGame.ReadHeader(file), null));
            }
            catch (SaveLoadException ex)
            {
                slots.Add(new SaveSlot(id, null, ex));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                slots.Add(new SaveSlot(id, null, new SaveLoadException(SaveLoadError.Corrupt, ex.Message, ex)));
            }
        }

        return [.. slots
            .OrderBy(slot => slot.Header is null)
            .ThenByDescending(slot => slot.Header?.SavedAt)
            .ThenBy(slot => slot.Id, StringComparer.Ordinal)];
    }

    /// <summary>A free id for a new manual slot, from the current time.</summary>
    public string NewSlotId()
    {
        var stem = "save-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var id = stem;
        for (var n = 2; File.Exists(PathOf(id)); n++)
        {
            id = $"{stem}-{n}";
        }

        return id;
    }

    /// <summary>Writes the game in the slot, replacing what it held.</summary>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public void Write(string slotId, Simulation simulation, string gameId, bool isAutosave)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        System.IO.Directory.CreateDirectory(Directory);
        var path = PathOf(slotId);
        var temporary = path + TemporaryExtension;
        using (var file = File.Create(temporary))
        {
            SaveGame.Write(file, simulation, new SaveInfo(gameId, isAutosave, GameVersion, DateTimeOffset.Now));
        }

        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>The game of a slot, with the colony systems added, and its header.</summary>
    /// <exception cref="SaveLoadException">The slot cannot be loaded; a file that cannot be read counts as damaged.</exception>
    public (Simulation Simulation, SaveHeader Header) Load(string slotId, DefDatabase content)
    {
        try
        {
            using var file = File.OpenRead(PathOf(slotId));
            var header = SaveGame.ReadHeader(file);
            file.Position = 0;
            var simulation = SaveGame.Load(file, content);
            ColonySystems.AddTo(simulation);
            return (simulation, header);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SaveLoadException(SaveLoadError.Corrupt, ex.Message, ex);
        }
    }

    public void Delete(string slotId) => File.Delete(PathOf(slotId));

    private string PathOf(string slotId)
    {
        if (slotId.Length == 0 || slotId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || slotId.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Invalid slot id '{slotId}'.", nameof(slotId));
        }

        return Path.Combine(Directory, slotId + Extension);
    }
}
