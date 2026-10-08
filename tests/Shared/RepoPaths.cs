namespace OurLife.Tests.Shared;

/// <summary>
/// Locates repository folders from the test output directory. Shared by every test project
/// through a linked file.
/// </summary>
internal static class RepoPaths
{
    private static readonly Lazy<string> RootLazy = new(FindRoot);

    public static string Root => RootLazy.Value;

    /// <summary>The game's content folder: <c>game/content</c>.</summary>
    public static string Content => Path.Combine(Root, "game", "content");

    public static string Combine(params string[] parts) => Path.Combine([Root, .. parts]);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OurLife.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Repository root (folder with OurLife.sln) not found above {AppContext.BaseDirectory}.");
    }
}
