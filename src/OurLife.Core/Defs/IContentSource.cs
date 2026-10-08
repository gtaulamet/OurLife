namespace OurLife.Core.Defs;

/// <summary>
/// Read access to the content folder. The game reads <c>res://content</c> through Godot,
/// because an exported game cannot read <c>res://</c> with <c>System.IO</c>; tests and tools read
/// the folder on disk. Paths are relative to the content root and use forward slashes.
/// </summary>
public interface IContentSource
{
    /// <summary>Files with the extension under a directory and its subdirectories, sorted.</summary>
    IReadOnlyList<string> ListFiles(string directory, string extension);

    /// <summary>Names of the direct subdirectories of a directory, sorted; empty when it does not exist.</summary>
    IReadOnlyList<string> ListDirectories(string directory);

    string ReadText(string path);
}
