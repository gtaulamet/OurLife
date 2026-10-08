namespace OurLife.Core.Tests;

/// <summary>Locates repository folders starting from the test output directory.</summary>
internal static class TestPaths
{
    private static readonly Lazy<string> RepoRootLazy = new(FindRepoRoot);

    public static string RepoRoot => RepoRootLazy.Value;

    public static string CoreSourceDir => Path.Combine(RepoRoot, "src", "OurLife.Core");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OurLife.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Repository root (folder with OurLife.sln) not found above {AppContext.BaseDirectory}.");
    }
}
