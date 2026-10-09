using OurLife.Core.Defs;

namespace OurLife.Core.Tests.Defs;

/// <summary>The disk source sees the same files as Godot's, which skips hidden entries.</summary>
public sealed class FileSystemContentSourceTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("ourlife-content-");

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void Files_and_folders_starting_with_a_dot_are_ignored()
    {
        Write("defs/materials.json");
        Write("defs/._materials.json");
        Write("defs/.backup/old.json");
        Write("locales/es/ui.json");
        Write("locales/.cache/ui.json");
        var source = new FileSystemContentSource(_root.FullName);

        source.ListFiles("defs", ".json").ShouldBe(new[] { "defs/materials.json" });
        source.ListDirectories("locales").ShouldBe(new[] { "es" });
    }

    private void Write(string relativePath)
    {
        var path = Path.Combine(_root.FullName, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "[]");
    }
}
