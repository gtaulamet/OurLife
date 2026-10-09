namespace OurLife.Core.Defs;

/// <summary>
/// Read access to the content folder. The game reads <c>res://content</c> through Godot,
/// because an exported game cannot read <c>res://</c> with <c>System.IO</c>; tests and tools read
/// the folder on disk. Paths are relative to the content root and use forward slashes. Files and
/// folders that are hidden or whose name starts with a dot are ignored, so editor and system
/// leftovers like <c>._materials.json</c> never load, and the game and the tests see the same files.
/// </summary>
public interface IContentSource
{
    /// <summary>Files with the extension under a directory and its subdirectories, sorted.</summary>
    IReadOnlyList<string> ListFiles(string directory, string extension);

    /// <summary>Names of the direct subdirectories of a directory, sorted; empty when it does not exist.</summary>
    IReadOnlyList<string> ListDirectories(string directory);

    string ReadText(string path);

    /// <summary>The name rule every source applies, on any platform: names that start with a dot are skipped.</summary>
    static bool IsIgnoredName(string name) => name.StartsWith('.');

    /// <summary>Whether a relative path, with forward slashes, crosses a file or folder that is skipped.</summary>
    static bool IsIgnoredPath(string path) => path.Split('/').Any(IsIgnoredName);
}
