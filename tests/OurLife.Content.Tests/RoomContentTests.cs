using OurLife.Core.Defs;

namespace OurLife.Content.Tests;

/// <summary>The real content of rooms (<c>docs/gdd/habitaciones.md</c>): the numbers of the spec, the tags of the furniture and the thoughts.</summary>
public class RoomContentTests
{
    [Fact]
    public void The_room_config_has_the_numbers_of_the_spec()
    {
        var config = GameContent.Defs.All<RoomConfigDef>().ShouldHaveSingleItem();

        (config.MinCells, config.MaxCells, config.SizePointsPerCell, config.SizeCap).ShouldBe((4, 150, 1, 36));
        (config.DiningRadius, config.GatherRadius, config.GatherChancePercent).ShouldBe((25, 30, 50));
        (config.GatherFromHour, config.GatherToHour, config.GatherTicks, config.CrowdedBeds).ShouldBe((18, 22, 180, 3));
    }

    [Fact]
    public void The_levels_start_where_the_spec_says_and_name_the_thoughts_they_leave()
    {
        var levels = GameContent.Defs.All<RoomLevelDef>().OrderBy(level => level.Order).ToList();

        levels.Select(level => (level.Id, level.From)).ShouldBe([("miserable", 0), ("poor", 8), ("decent", 18), ("comfortable", 36), ("luxurious", 61)]);
        levels.Select(level => level.BedroomThought).ShouldBe(["bedroom_miserable", null, "bedroom_decent", "bedroom_comfortable", "bedroom_luxurious"]);
        levels.Select(level => level.DiningThought).ShouldBe(["dining_miserable", null, "dining_decent", "dining_comfortable", "dining_luxurious"]);
    }

    [Fact]
    public void The_thoughts_of_rooms_have_the_moods_and_hours_of_the_spec()
    {
        var thoughts = GameContent.Defs.All<ThoughtDef>().ToDictionary(thought => thought.Id);
        var expected = new Dictionary<string, (int Mood, int Hours)>
        {
            ["bedroom_miserable"] = (-2, 12), ["bedroom_decent"] = (1, 12), ["bedroom_comfortable"] = (3, 12), ["bedroom_luxurious"] = (5, 12),
            ["family_home"] = (2, 12), ["slept_crowded"] = (-2, 12), ["ate_at_table"] = (2, 8),
            ["dining_miserable"] = (-1, 8), ["dining_decent"] = (1, 8), ["dining_comfortable"] = (2, 8), ["dining_luxurious"] = (4, 8),
        };

        foreach (var (id, numbers) in expected)
        {
            (thoughts[id].Mood, thoughts[id].DurationHours, thoughts[id].StackLimit).ShouldBe((numbers.Mood, numbers.Hours, 1), id);
        }
    }

    [Fact]
    public void The_best_home_adds_ten_to_the_mood_and_a_crowded_wretched_barracks_still_does_not_cost_more_than_it_gives()
    {
        var mood = GameContent.Defs.All<MoodConfigDef>().Single();
        var thoughts = GameContent.Defs.All<ThoughtDef>().ToDictionary(thought => thought.Id);
        int Of(string? id) => id is null ? 0 : thoughts[id].Mood;

        // Waking in a bed (3), in a luxurious room (5), among family (2).
        (Of(mood.SleptInBed) + Of("bedroom_luxurious") + Of(mood.FamilyHome)).ShouldBe(10);

        // The worst a bed gives: the wretched room (-2) and the strangers (-2) take the 3 of the bed down to -1.
        (Of(mood.SleptInBed) + Of("bedroom_miserable") + Of(mood.SleptCrowded)).ShouldBe(-1);
    }

    [Fact]
    public void Every_piece_of_furniture_has_tags_and_every_workshop_is_tagged_so()
    {
        var furniture = GameContent.Defs.All<BuildableDef>().Where(buildable => buildable.IsFurniture).ToList();

        furniture.ShouldAllBe(buildable => buildable.Tags.Count > 0);
        furniture.Where(buildable => buildable.Workstation is not null).ShouldAllBe(buildable => buildable.Tags.Contains("workshop"));
        furniture.Where(buildable => buildable.Workstation is null).ShouldNotContain(buildable => buildable.Tags.Contains("workshop"));
        furniture.Where(buildable => buildable.RestRecoveryPercent is not null).ShouldAllBe(buildable => buildable.Tags.Contains("bed"));
        furniture.Where(buildable => buildable.Storage).ShouldAllBe(buildable => buildable.Tags.Contains("chest"));
        GameContent.Defs.All<BuildableDef>().Where(buildable => !buildable.IsFurniture).ShouldAllBe(buildable => buildable.Tags.Count == 0);
    }

    [Fact]
    public void Every_tag_a_role_asks_for_exists_in_some_piece_of_furniture()
    {
        var tags = GameContent.Defs.All<BuildableDef>().SelectMany(buildable => buildable.Tags).ToHashSet();

        foreach (var role in GameContent.Defs.All<RoomRoleDef>())
        {
            role.Needs.Select(need => need.Tag).ShouldAllBe(tag => tags.Contains(tag), role.Id);
        }

        GameContent.Defs.All<RoomRoleDef>().Select(role => role.Id).Order().ShouldBe(["bedroom", "dining", "storage", "workshop"]);
    }

    [Fact]
    public void The_beauty_of_furniture_and_materials_is_the_one_of_the_spec()
    {
        var beauty = GameContent.Defs.All<BuildableDef>().Where(buildable => buildable.IsFurniture).ToDictionary(buildable => buildable.Id, buildable => buildable.Beauty);
        beauty.Select(pair => (pair.Key, pair.Value)).OrderBy(pair => pair.Key, StringComparer.Ordinal).ShouldBe(
        [
            ("bed", 4), ("bloomery", -4), ("carpenter_bench", -1), ("chair", 2), ("charcoal_kiln", -2), ("chest", 2), ("cradle", 3), ("forge", -4),
            ("kitchen", 0), ("mason_bench", -1), ("scriptorium", 1), ("table", 3),
        ]);

        var materials = GameContent.Defs.All<MaterialDef>().ToDictionary(material => material.Id, material => (material.Beauty.Wall, material.Beauty.Floor));
        materials["soil"].ShouldBe((0, 0));
        materials["granite"].ShouldBe((0, 0));
        materials["timber"].ShouldBe((0, 1));
        materials["masonry"].ShouldBe((1, 1));
        materials["planks"].ShouldBe((1, 2));
        materials["ashlar"].ShouldBe((2, 3));
    }

    [Fact]
    public void The_example_houses_of_the_spec_come_out_decent_comfortable_and_luxurious_with_the_real_numbers()
    {
        var defs = GameContent.Defs;
        int Furniture(params string[] ids) => ids.Sum(id => defs.Get<BuildableDef>(id).Beauty);
        int Wall(string id) => defs.Get<MaterialDef>(id).Beauty.Wall;
        int Floor(string id) => defs.Get<MaterialDef>(id).Beauty.Floor;
        var levels = defs.All<RoomLevelDef>().OrderBy(level => level.From).ToList();
        string LevelOf(int score) => levels.Last(level => score >= level.From).Id;

        // 3 × 3 room, 16 pieces of border, a bed, a table, a chair and a chest.
        var furniture = Furniture("bed", "table", "chair", "chest");
        (9 + furniture).ShouldBe(20);
        LevelOf(9 + furniture).ShouldBe("decent");
        (9 + furniture + (9 * Floor("planks")) + (16 * Wall("masonry"))).ShouldBe(54);
        LevelOf(54).ShouldBe("comfortable");
        (9 + furniture + (9 * Floor("ashlar")) + (16 * Wall("ashlar"))).ShouldBe(79);
        LevelOf(79).ShouldBe("luxurious");
    }
}
