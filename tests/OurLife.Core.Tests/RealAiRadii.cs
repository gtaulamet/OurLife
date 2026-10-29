using System.Text.Json;

namespace OurLife.Core.Tests;

/// <summary>
/// The fetch radii of the game's own <c>game/content/defs/ai.json</c>, so the fixtures with an <c>AiConfig</c> use the real numbers
/// instead of a copy that could drift from them. A fixture writes <c>@CRAFT_RADIUS@</c> and <c>@TOOL_RADIUS@</c> where the numbers go.
/// </summary>
internal static class RealAiRadii
{
    private static readonly Lazy<JsonElement> Config = new(() =>
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepoPaths.Combine("game", "content", "defs", "ai.json")));
        return document.RootElement.EnumerateArray().Single(def => def.GetProperty("type").GetString() == "AiConfig").Clone();
    });

    public static int Craft => Config.Value.GetProperty("craftFetchRadius").GetInt32();

    public static int Tool => Config.Value.GetProperty("toolFetchRadius").GetInt32();

    /// <summary>Puts the real radii where the fixture has its placeholders.</summary>
    public static string Fill(string json) => json
        .Replace("@CRAFT_RADIUS@", Craft.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("@TOOL_RADIUS@", Tool.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
}
