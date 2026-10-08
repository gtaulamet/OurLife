namespace OurLife.Content.Tests;

/// <summary>The game content loads, and every def has its texts in every language.</summary>
public class DefTextTests
{
    private static readonly string[] TextSuffixes = ["name", "desc", "gender"];
    private static readonly string[] Genders = ["male", "female", "other"];

    [Fact]
    public void The_game_content_loads_without_problems()
    {
        GameContent.Defs.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Every_def_with_texts_has_name_description_and_gender_in_every_language()
    {
        var problems = new List<string>();
        foreach (var def in GameContent.Defs.AllDefs().Where(def => def.TextCategory is not null))
        {
            foreach (var catalog in GameContent.Catalogs.Values)
            {
                foreach (var suffix in TextSuffixes)
                {
                    var key = $"def.{def.TextCategory}.{def.Id}.{suffix}";
                    if (!catalog.Messages.ContainsKey(key))
                    {
                        problems.Add($"{catalog.Locale}: missing {key}");
                    }
                }
            }
        }

        problems.ShouldBeEmpty(string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Grammatical_genders_are_valid_select_values()
    {
        var problems = GameContent.Catalogs.Values
            .SelectMany(catalog => catalog.Messages
                .Where(entry => entry.Key.EndsWith(".gender", StringComparison.Ordinal) && !Genders.Contains(entry.Value.Pattern))
                .Select(entry => $"{catalog.Locale} '{entry.Key}' = '{entry.Value.Pattern}'"))
            .ToList();

        problems.ShouldBeEmpty($"genders must be one of {string.Join(", ", Genders)}:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
    }

    [Fact]
    public void There_are_no_texts_for_defs_that_do_not_exist()
    {
        var prefixes = GameContent.Defs.AllDefs()
            .Where(def => def.TextCategory is not null)
            .Select(def => $"def.{def.TextCategory}.{def.Id}.")
            .ToArray();

        var orphans = GameContent.Spanish.Messages.Keys
            .Where(key => key.StartsWith("def.", StringComparison.Ordinal))
            .Where(key => !prefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        orphans.ShouldBeEmpty("texts left over from renamed or deleted defs");
    }

    [Fact]
    public void Every_season_has_a_name_in_every_language()
    {
        foreach (var season in GameContent.Defs.Time.Seasons)
        {
            foreach (var catalog in GameContent.Catalogs.Values)
            {
                catalog.Messages.ContainsKey($"time.season.{season}")
                    .ShouldBeTrue($"{catalog.Locale}: missing time.season.{season}");
            }
        }
    }
}
