using System.Text.RegularExpressions;
using OurLife.Localization;

namespace OurLife.Content.Tests;

/// <summary>
/// Code and scenes only show text through catalog keys, and every key they use exists.
/// Keys built at run time, like <c>time.season.{id}</c>, are checked by <see cref="DefTextTests"/>.
/// </summary>
public partial class CodeTextTests
{
    [Fact]
    public void Keys_used_in_code_and_scenes_exist_in_the_spanish_catalog()
    {
        var problems = new List<string>();
        foreach (var file in CodeFiles().Concat(SceneFiles()))
        {
            foreach (Match match in KeyLiteral().Matches(File.ReadAllText(file)))
            {
                var key = match.Groups[1].Value;
                if (!GameContent.Spanish.Messages.ContainsKey(key))
                {
                    problems.Add($"{Relative(file)}: '{key}' is not in the catalog");
                }
            }
        }

        problems.ShouldBeEmpty(string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Game_code_and_scenes_have_no_visible_text_outside_the_catalogs()
    {
        var problems = new List<string>();
        foreach (var file in GameCodeFiles())
        {
            problems.AddRange(FindHardcodedText(File.ReadAllText(file)).Select(text => $"{Relative(file)}: \"{text}\""));
        }

        foreach (var file in SceneFiles())
        {
            foreach (Match match in SceneText().Matches(File.ReadAllText(file)))
            {
                if (!IsAllowed(match.Groups[1].Value))
                {
                    problems.Add($"{Relative(file)}: \"{match.Groups[1].Value}\"");
                }
            }
        }

        problems.ShouldBeEmpty(
            $"use a key from game/content/locales instead:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
    }

    [Fact]
    public void The_scan_finds_the_game_code()
    {
        GameCodeFiles().ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("label.Text = \"Nueva partida\";")]
    [InlineData("new Button { Text = \"Salir\" }")]
    [InlineData("control.TooltipText = $\"Hola {name}\";")]
    public void The_scanner_flags_hardcoded_text(string line)
    {
        FindHardcodedText(line).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("label.Text = \"ui.mainmenu.new_game\";")]
    [InlineData("label.Text = \"\";")]
    [InlineData("label.Text = localizer.Format(\"ui.probe.title\");")]
    [InlineData("GD.Print(\"[smoke] OK\");")]
    public void The_scanner_allows_keys_and_computed_text(string line)
    {
        FindHardcodedText(line).ShouldBeEmpty();
    }

    private static IEnumerable<string> FindHardcodedText(string code) =>
        TextAssignment().Matches(code)
            .Select(match => match.Groups[1].Value)
            .Where(text => !IsAllowed(text));

    private static bool IsAllowed(string text) => text.Length == 0 || LocaleCatalog.IsValidKey(text);

    private static IEnumerable<string> CodeFiles() => SourceFiles("src", "*.cs").Concat(GameCodeFiles());

    private static IEnumerable<string> GameCodeFiles() => SourceFiles(Path.Combine("game", "src"), "*.cs");

    private static IEnumerable<string> SceneFiles() => SourceFiles(Path.Combine("game", "scenes"), "*.tscn");

    private static IEnumerable<string> SourceFiles(string relativeDirectory, string pattern)
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory
            .EnumerateFiles(RepoPaths.Combine(relativeDirectory), pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                           && !path.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
                           && !path.Contains($"{separator}.godot{separator}", StringComparison.Ordinal));
    }

    private static string Relative(string path) => Path.GetRelativePath(RepoPaths.Root, path);

    [GeneratedRegex("\"((?:ui|def|event|job|time|need)\\.[a-z0-9_]+(?:\\.[a-z0-9_]+)*)\"")]
    private static partial Regex KeyLiteral();

    [GeneratedRegex("\\b(?:Text|TooltipText|PlaceholderText)\\s*=\\s*\\$?\"([^\"]*)\"")]
    private static partial Regex TextAssignment();

    [GeneratedRegex("^\\s*text\\s*=\\s*\"(.*)\"\\s*$", RegexOptions.Multiline)]
    private static partial Regex SceneText();
}
