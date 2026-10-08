namespace OurLife.Core.Defs;

/// <summary>Reads the content folder from disk, for tests and tools.</summary>
public sealed class FileSystemContentSource : IContentSource
{
    private readonly string _root;

    public FileSystemContentSource(string rootDirectory)
    {
        _root = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(_root))
        {
            throw new DirectoryNotFoundException($"Content folder not found: {_root}");
        }
    }

    public IReadOnlyList<string> ListFiles(string directory, string extension)
    {
        var fullPath = Path.Combine(_root, directory);
        if (!Directory.Exists(fullPath))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(fullPath, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(extension, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(_root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<string> ListDirectories(string directory)
    {
        var fullPath = Path.Combine(_root, directory);
        if (!Directory.Exists(fullPath))
        {
            return [];
        }

        return Directory
            .EnumerateDirectories(fullPath)
            .Select(path => Path.GetFileName(path))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public string ReadText(string path) => File.ReadAllText(Path.Combine(_root, path));
}
