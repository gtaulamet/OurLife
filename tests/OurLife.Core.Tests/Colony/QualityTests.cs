using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Colony;

/// <summary>The quality roll (<c>docs/gdd/oficios.md</c>, "Calidad"): the numbers are the table of the spec.</summary>
public class QualityTests
{
    private const string SkillConfig =
        """{ "type": "SkillConfig", "id": "default", "maxLevel": 20, "averageLevel": 10, "experienceBase": 600, "experiencePerLevel": 150, "speedBase": 80, "speedPerLevel": 2, "teachingMinLevels": 3, "teachingBase": 100, "teachingPerLevel": 30 }""";

    private static QualityRules Rules => QualityRules.From(ColonyTestBed.Content);

    [Theory]
    [InlineData(0, "poor")]
    [InlineData(29, "poor")]
    [InlineData(30, "normal")]
    [InlineData(99, "normal")]
    [InlineData(100, "good")]
    [InlineData(129, "good")]
    [InlineData(130, "excellent")]
    [InlineData(149, "excellent")]
    [InlineData(150, "masterwork")]
    [InlineData(5_000, "masterwork")]
    public void A_score_gives_the_first_quality_whose_bound_is_above_it(int score, string expected)
    {
        Rules.ForScore(score).Id.ShouldBe(expected);
    }

    [Fact]
    public void The_score_is_a_die_plus_three_points_per_level()
    {
        var rules = Rules;
        var random = new Pcg32(1, 1);
        var scores = Enumerable.Range(0, 2_000).Select(_ => rules.Score(random, skillLevel: 10)).ToList();

        scores.Min().ShouldBe(30);
        scores.Max().ShouldBe(129);
    }

    [Fact]
    public void A_smith_of_level_zero_makes_a_poor_tool_below_30_and_one_of_level_20_a_masterwork_from_150()
    {
        var rules = Rules;

        // With the die fixed at its lowest and at its highest values.
        rules.ForScore(0 + (3 * 0)).Id.ShouldBe("poor");
        rules.ForScore(99 + (3 * 0)).Id.ShouldBe("normal");
        rules.ForScore(90 + (3 * 20)).Id.ShouldBe("masterwork");
        rules.ForScore(89 + (3 * 20)).Id.ShouldBe("excellent");
    }

    [Theory]
    [InlineData(0, 30, 70, 0, 0, 0)]
    [InlineData(5, 15, 70, 15, 0, 0)]
    [InlineData(10, 0, 70, 30, 0, 0)]
    [InlineData(15, 0, 55, 30, 15, 0)]
    [InlineData(20, 0, 40, 30, 20, 10)]
    public void Every_value_of_the_die_gives_the_percentages_of_the_spec(int level, int poor, int normal, int good, int excellent, int masterwork)
    {
        var rules = Rules;

        var counts = Enumerable.Range(0, 100).GroupBy(die => rules.ForScore(die + (3 * level)).Id).ToDictionary(group => group.Key, group => group.Count());

        counts.GetValueOrDefault("poor").ShouldBe(poor);
        counts.GetValueOrDefault("normal").ShouldBe(normal);
        counts.GetValueOrDefault("good").ShouldBe(good);
        counts.GetValueOrDefault("excellent").ShouldBe(excellent);
        counts.GetValueOrDefault("masterwork").ShouldBe(masterwork);
    }

    [Fact]
    public void With_1000_rolls_at_level_10_there_are_no_poor_or_excellent_and_good_is_between_25_and_35_percent()
    {
        var rules = Rules;
        var random = new Pcg32(7, 3);

        var rolls = Enumerable.Range(0, 1_000).Select(_ => rules.Roll(random, skillLevel: 10).Id).ToList();

        rolls.ShouldNotContain("poor");
        rolls.ShouldNotContain("excellent");
        rolls.ShouldNotContain("masterwork");
        rolls.Count(id => id == "good").ShouldBeInRange(250, 350);
    }

    [Fact]
    public void A_colonist_rolls_with_the_level_of_the_skill_and_the_stream_craft_quality()
    {
        var bed = ColonyTestBed.Flat(8);
        var master = bed.AddColonist(2, 2, 1);
        bed.World.Add(master, new Skills { Values = [new SkillValue { WorkType = "mining", Level = 20 }] });
        var rules = Rules;

        var rolls = Enumerable.Range(0, 1_000).Select(_ => rules.Roll(bed.Simulation, master, "mining").Id).ToList();

        rolls.ShouldNotContain("poor");
        rolls.Count(id => id == "masterwork").ShouldBeInRange(70, 130);
        bed.Simulation.Random.Streams.Select(stream => stream.Key).ShouldContain(QualityRules.Stream);
        QualityRules.Stream.ShouldBe("craft.quality");
    }

    [Fact]
    public void A_colonist_without_that_skill_rolls_at_level_zero_and_one_without_skills_at_the_average_level()
    {
        var bed = ColonyTestBed.Flat(8);
        var novice = bed.AddColonist(2, 2, 1);
        bed.World.Add(novice, new Skills { Values = [new SkillValue { WorkType = "mining", Level = 20 }] });
        var average = bed.AddColonist(3, 2, 1);
        var rules = Rules;

        var novices = Enumerable.Range(0, 1_000).Select(_ => rules.Roll(bed.Simulation, novice, "woodcutting").Id).ToList();
        var averages = Enumerable.Range(0, 1_000).Select(_ => rules.Roll(bed.Simulation, average, "woodcutting").Id).ToList();

        novices.ShouldNotContain("good");
        novices.Count(id => id == "poor").ShouldBeInRange(240, 360);
        averages.ShouldNotContain("poor");
        averages.ShouldNotContain("excellent");
    }

    [Fact]
    public void The_same_seed_rolls_the_same_qualities()
    {
        static string Rolls()
        {
            var bed = ColonyTestBed.Flat(8, seed: 42);
            var colonist = bed.AddColonist(2, 2, 1);
            bed.World.Add(colonist, new Skills { Values = [new SkillValue { WorkType = "mining", Level = 12 }] });
            return string.Join(',', Enumerable.Range(0, 200).Select(_ => Rules.Roll(bed.Simulation, colonist, "mining").Id));
        }

        Rolls().ShouldBe(Rolls());
    }

    [Fact]
    public void The_default_quality_is_never_stored_and_the_others_are_ordered_in_the_ladder()
    {
        var rules = Rules;

        rules.Default.Id.ShouldBe("normal");
        rules.Store("normal").ShouldBeNull();
        rules.Store(null).ShouldBeNull();
        rules.Store("good").ShouldBe("good");
        rules.IdOf(null).ShouldBe("normal");
        rules.Of(null).Id.ShouldBe("normal");
        rules.Of("masterwork").ValuePercent.ShouldBe(200);
        rules.Of("poor").ComfortPercent.ShouldBe(90);
        rules.Ladder.Select(quality => quality.Id).ShouldBe(["poor", "normal", "good", "excellent", "masterwork"]);
        rules.RankOf(null).ShouldBe(1);
        rules.AtLeast("good", "normal").ShouldBeTrue();
        rules.AtLeast(null, "normal").ShouldBeTrue();
        rules.AtLeast("poor", null).ShouldBeTrue();
        rules.AtLeast("poor", "normal").ShouldBeFalse();
        Should.Throw<ArgumentException>(() => rules.RankOf("legendary"));
    }

    [Theory]
    [InlineData("""[{ "type": "QualityConfig", "id": "q", "defaultQuality": "a", "die": 100, "perLevel": 3 }, { "type": "Quality", "id": "a", "order": 0, "below": 50, "valuePercent": 100, "comfortPercent": 100 }]""", "must be below more than 159")]
    [InlineData("""[{ "type": "QualityConfig", "id": "q", "defaultQuality": "a", "die": 100, "perLevel": 3 }, { "type": "Quality", "id": "a", "order": 0, "below": 100, "valuePercent": 100, "comfortPercent": 100 }, { "type": "Quality", "id": "b", "order": 1, "below": 100, "valuePercent": 100, "comfortPercent": 100 }]""", "must be higher than")]
    [InlineData("""[{ "type": "QualityConfig", "id": "q", "defaultQuality": "a", "die": 100, "perLevel": 3 }, { "type": "Quality", "id": "a", "order": 0, "below": 100, "valuePercent": 100, "comfortPercent": 100 }, { "type": "Quality", "id": "b", "order": 0, "below": 9999, "valuePercent": 100, "comfortPercent": 100 }]""", "same order")]
    public void Content_with_a_broken_ladder_is_rejected(string defs, string expectedFragment)
    {
        var content = TestContent.WithDefs(defs.TrimEnd().TrimEnd(']') + ", " + SkillConfig + "]");

        var error = Should.Throw<InvalidOperationException>(() => QualityRules.From(content));

        error.Message.ShouldContain(expectedFragment);
    }

    [Fact]
    public void Content_without_the_quality_config_is_rejected()
    {
        var error = Should.Throw<InvalidOperationException>(() => QualityRules.From(TestContent.WithDefs($"[{SkillConfig}]")));

        error.Message.ShouldContain("exactly one QualityConfig");
    }
}
