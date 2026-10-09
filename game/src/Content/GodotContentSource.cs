using Godot;
using OurLife.Core.Defs;

namespace OurLife.Game.Content;

/// <summary>
/// Reads <c>res://content</c> through Godot, which also works inside an exported game, where
/// <c>System.IO</c> cannot see <c>res://</c>.
/// </summary>
public sealed class GodotContentSource : IContentSource
{
    private readonly string _root;

    public GodotContentSource(string root = "res://content")
    {
        _root = root.TrimEnd('/');
    }

    public IReadOnlyList<string> ListFiles(string directory, string extension)
    {
        var results = new List<string>();
        Collect(directory, extension, results);
        results.Sort(StringComparer.Ordinal);
        return results;
    }

    public IReadOnlyList<string> ListDirectories(string directory)
    {
        using var access = DirAccess.Open($"{_root}/{directory}");
        if (access is null)
        {
            return [];
        }

        var names = access.GetDirectories().Where(name => !IContentSource.IsIgnoredName(name)).ToList();
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    public string ReadText(string path)
    {
        using var file = Godot.FileAccess.Open($"{_root}/{path}", Godot.FileAccess.ModeFlags.Read)
            ?? throw new FileNotFoundException($"Cannot open {_root}/{path}: {Godot.FileAccess.GetOpenError()}");
        return file.GetAsText();
    }

    /// <summary>DirAccess already skips hidden entries; dot names are skipped on every platform too.</summary>
    private void Collect(string directory, string extension, List<string> results)
    {
        using var access = DirAccess.Open($"{_root}/{directory}");
        if (access is null)
        {
            return;
        }

        foreach (var file in access.GetFiles())
        {
            if (file.EndsWith(extension, StringComparison.Ordinal) && !IContentSource.IsIgnoredName(file))
            {
                results.Add($"{directory}/{file}");
            }
        }

        foreach (var subdirectory in access.GetDirectories())
        {
            if (!IContentSource.IsIgnoredName(subdirectory))
            {
                Collect($"{directory}/{subdirectory}", extension, results);
            }
        }
    }
}
