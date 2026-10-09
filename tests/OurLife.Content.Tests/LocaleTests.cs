using System.Text.Json;
using OurLife.Core.Defs;
using OurLife.Localization;

namespace OurLife.Content.Tests;

/// <summary>Every language says the same things, with the same arguments, in valid files.</summary>
public class LocaleTests
{
    [Fact]
    public void Spanish_and_English_are_available_and_spanish_is_the_default()
    {
        GameContent.Catalogs.Keys.ShouldBe(new[] { "en", "es" }, ignoreOrder: true);
        Localizer.DefaultLocale.ShouldBe("es");
    }

    [Fact]
    public void Every_language_has_the_same_files_as_spanish()
    {
        var spanishFiles = FileNames("es");

        foreach (var locale in GameContent.Catalogs.Keys)
        {
            FileNames(locale).ShouldBe(spanishFiles, $"the files of '{locale}' differ from the Spanish ones");
        }
    }

    [Fact]
    public void Every_language_has_exactly_the_spanish_keys()
    {
        var spanishKeys = GameContent.Spanish.Messages.Keys.ToArray();

        foreach (var catalog in GameContent.Catalogs.Values)
        {
            var keys = catalog.Messages.Keys.ToArray();
            spanishKeys.Except(keys).ShouldBeEmpty($"keys missing in '{catalog.Locale}'");
            keys.Except(spanishKeys).ShouldBeEmpty($"keys in '{catalog.Locale}' that Spanish does not have");
        }
    }

    [Fact]
    public void Translations_use_the_same_arguments_as_spanish()
    {
        var problems = new List<string>();
        foreach (var catalog in GameContent.Catalogs.Values)
        {
            foreach (var (key, message) in catalog.Messages)
            {
                if (GameContent.Spanish.Messages.TryGetValue(key, out var spanish)
                    && !spanish.ArgumentNames.SetEquals(message.ArgumentNames))
                {
                    problems.Add(
                        $"{catalog.Locale} '{key}' uses [{string.Join(", ", message.ArgumentNames)}], Spanish uses [{string.Join(", ", spanish.ArgumentNames)}]");
                }
            }
        }

        problems.ShouldBeEmpty(string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Every_language_has_the_name_of_every_language()
    {
        // The language selector builds the key ui.language.<locale> at run time.
        foreach (var catalog in GameContent.Catalogs.Values)
        {
            foreach (var locale in GameContent.Catalogs.Keys)
            {
                catalog.Messages.ContainsKey($"ui.language.{locale}")
                    .ShouldBeTrue($"{catalog.Locale}: missing ui.language.{locale}");
            }
        }
    }

    [Fact]
    public void Every_json_file_in_the_content_folder_is_strict_json()
    {
        // The loaders already reject comments and trailing commas, but only in the files they
        // load. The Godot editor parses every .json in the project, except hidden ones.
        foreach (var path in Directory.EnumerateFiles(RepoPaths.Content, "*.json", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(RepoPaths.Content, path).Replace('\\', '/');
            if (IContentSource.IsIgnoredPath(relative))
            {
                continue;
            }

            Should.NotThrow(
                () => JsonDocument.Parse(File.ReadAllText(path)).Dispose(),
                $"{relative} must be strict JSON: no comments and no trailing commas");
        }
    }

    private static string[] FileNames(string locale) =>
        GameContent.Source.ListFiles($"locales/{locale}", ".json").Select(Path.GetFileName).ToArray()!;
}
