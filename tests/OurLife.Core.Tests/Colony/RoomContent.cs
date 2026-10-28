using OurLife.Core.Defs;

namespace OurLife.Core.Tests.Colony;

/// <summary>
/// The colony test content with the rooms of IP4: the beauty of materials and furniture, the tags, the thoughts of
/// sleeping and eating, and the room config, levels and roles with the numbers of <c>docs/gdd/habitaciones.md</c>.
/// </summary>
internal static class RoomContent
{
    private const string Extras = """
        { "type": "Material", "id": "planks", "kind": "wood", "color": "#A57A4B", "mineWorkTicks": 30, "builtFrom": "plank", "beauty": { "wall": 1, "floor": 2 } },
        { "type": "Material", "id": "ashlar", "kind": "stone", "color": "#B5B0A6", "mineWorkTicks": 30, "builtFrom": "stone_block", "beauty": { "wall": 2, "floor": 3 } },
        { "type": "Item", "id": "stone_block", "category": "stone", "kind": "raw", "visual": "item.stone_block", "stackLimit": 25 },
        { "type": "WorkType", "id": "stonecutting", "order": 6, "jobs": [], "workshop": true, "skill": true },
        { "type": "WorkType", "id": "smithing", "order": 7, "jobs": [], "workshop": true, "skill": true },
        { "type": "Buildable", "id": "chair", "shape": "furniture", "materials": ["timber"], "cost": 2, "workTicks": 20,
          "visual": "furniture.chair", "beauty": 2, "tags": ["seat"] },
        { "type": "Buildable", "id": "mason_bench", "shape": "furniture", "materials": ["timber"], "cost": 2, "workTicks": 20,
          "visual": "furniture.mason_bench", "beauty": -1, "workstation": { "workType": "stonecutting" }, "tags": ["workshop"] },
        { "type": "Buildable", "id": "forge", "shape": "furniture", "materials": ["timber"], "cost": 2, "workTicks": 20,
          "visual": "furniture.forge", "beauty": -4, "workstation": { "workType": "smithing" }, "tags": ["workshop"] },
        { "type": "Thought", "id": "bedroom_miserable", "mood": -2, "durationHours": 12 },
        { "type": "Thought", "id": "bedroom_decent", "mood": 1, "durationHours": 12 },
        { "type": "Thought", "id": "bedroom_comfortable", "mood": 3, "durationHours": 12 },
        { "type": "Thought", "id": "bedroom_luxurious", "mood": 5, "durationHours": 12 },
        { "type": "Thought", "id": "family_home", "mood": 2, "durationHours": 12 },
        { "type": "Thought", "id": "slept_crowded", "mood": -2, "durationHours": 12 },
        { "type": "Thought", "id": "ate_at_table", "mood": 2, "durationHours": 8 },
        { "type": "Thought", "id": "dining_miserable", "mood": -1, "durationHours": 8 },
        { "type": "Thought", "id": "dining_decent", "mood": 1, "durationHours": 8 },
        { "type": "Thought", "id": "dining_comfortable", "mood": 2, "durationHours": 8 },
        { "type": "Thought", "id": "dining_luxurious", "mood": 4, "durationHours": 8 },
        { "type": "RoomConfig", "id": "default", "minCells": 4, "maxCells": 150, "sizePointsPerCell": 1, "sizeCap": 36,
          "diningRadius": 25, "gatherRadius": 30, "gatherChancePercent": 50, "gatherFromHour": 18, "gatherToHour": 22, "gatherTicks": 180, "crowdedBeds": 3 },
        { "type": "RoomLevel", "id": "miserable", "order": 0, "from": 0, "bedroomThought": "bedroom_miserable", "diningThought": "dining_miserable" },
        { "type": "RoomLevel", "id": "poor", "order": 1, "from": 8 },
        { "type": "RoomLevel", "id": "decent", "order": 2, "from": 18, "bedroomThought": "bedroom_decent", "diningThought": "dining_decent" },
        { "type": "RoomLevel", "id": "comfortable", "order": 3, "from": 36, "bedroomThought": "bedroom_comfortable", "diningThought": "dining_comfortable" },
        { "type": "RoomLevel", "id": "luxurious", "order": 4, "from": 61, "bedroomThought": "bedroom_luxurious", "diningThought": "dining_luxurious" },
        { "type": "RoomRole", "id": "bedroom", "order": 0, "needs": [{ "tag": "bed", "count": 1 }] },
        { "type": "RoomRole", "id": "dining", "order": 1, "needs": [{ "tag": "table", "count": 1 }, { "tag": "seat", "count": 1 }] },
        { "type": "RoomRole", "id": "workshop", "order": 2, "needs": [{ "tag": "workshop", "count": 1 }] },
        { "type": "RoomRole", "id": "storage", "order": 3, "needs": [{ "tag": "chest", "count": 2 }], "storageCellsPercent": 50 }
        """;

    private static readonly Lazy<DefDatabase> DefsLazy = new(Build);

    public static DefDatabase Defs => DefsLazy.Value;

    private static DefDatabase Build()
    {
        var json = ColonyTestBed.ContentJson
            .Replace("\"builtFrom\": \"log\" }", "\"builtFrom\": \"log\", \"beauty\": { \"wall\": 0, \"floor\": 1 } }", StringComparison.Ordinal)
            .Replace("\"builtFrom\": \"stone\" }", "\"builtFrom\": \"stone\", \"beauty\": { \"wall\": 1, \"floor\": 1 } }", StringComparison.Ordinal)
            .Replace("\"visual\": \"furniture.bed\",", "\"visual\": \"furniture.bed\", \"beauty\": 4, \"tags\": [\"bed\"],", StringComparison.Ordinal)
            .Replace("\"visual\": \"furniture.cradle\",", "\"visual\": \"furniture.cradle\", \"beauty\": 3, \"tags\": [\"bed\"],", StringComparison.Ordinal)
            .Replace("\"visual\": \"furniture.table\" }", "\"visual\": \"furniture.table\", \"beauty\": 3, \"tags\": [\"table\"] }", StringComparison.Ordinal)
            .Replace("\"visual\": \"furniture.chest\",", "\"visual\": \"furniture.chest\", \"beauty\": 2, \"tags\": [\"chest\"],", StringComparison.Ordinal)
            .Replace(
                "\"childBorn\": \"child_born\"}",
                "\"childBorn\": \"child_born\", \"ateAtTable\": \"ate_at_table\", \"familyHome\": \"family_home\", \"sleptCrowded\": \"slept_crowded\"}",
                StringComparison.Ordinal);
        return TestContent.WithDefs(string.Concat(json.AsSpan(0, json.LastIndexOf(']')), ",", Extras, "]"));
    }
}
