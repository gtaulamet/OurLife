using OurLife.Core.Defs;

namespace OurLife.Core.Tests.Defs;

public class WorldGenDefTests
{
    private const string Materials = """
        { "type": "Material", "id": "soil", "kind": "soil", "color": "#7A5A3A", "mineWorkTicks": 30 },
        { "type": "Material", "id": "granite", "kind": "stone", "color": "#8C7F7D", "mineWorkTicks": 180 },
        { "type": "Plant", "id": "bush", "visual": "plant.bush" }
        """;

    [Fact]
    public void A_valid_world_loads()
    {
        var defs = TestContent.WithDefs($"[{Materials}, {WorldGen()}]");

        defs.Get<WorldGenDef>("small").Strata.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("\"strata\": [{ \"material\": \"granite\", \"thickness\": 4 }, { \"material\": \"soil\", \"thickness\": 2 }]", "except the last one")]
    [InlineData("\"strata\": [{ \"material\": \"granite\" }, { \"material\": \"soil\" }]", "except the last one")]
    [InlineData("\"strata\": []", "at least one layer")]
    [InlineData("\"strata\": [{ \"material\": \"granite\", \"thickness\": 2 }, { \"material\": \"soil\" }]", "twice strataWobble")]
    [InlineData("\"strata\": [{ \"material\": \"marble\" }]", "strata[0].material points to 'marble'")]
    [InlineData("\"soilMaterial\": \"clay\"", "soilMaterial points to 'clay'")]
    [InlineData("\"vegetation\": [{ \"plant\": \"palm\", \"chance\": 10, \"minElevation\": 0, \"maxElevation\": 1 }]", "vegetation[0].plant points to 'palm'")]
    [InlineData("\"vegetation\": [{ \"plant\": \"bush\", \"chance\": 6000, \"minElevation\": 0, \"maxElevation\": 1 }]", "chance times groveFactorMax")]
    [InlineData("\"groveFactorMax\": 0", "at least 1")]
    [InlineData("\"valleyFloor\": 5", "rock below the deepest soil")]
    [InlineData("\"hillHeight\": 30", "two levels of air")]
    [InlineData("\"rockExposureElevation\": 3", "valley floor could be bare rock")]
    [InlineData("\"width\": 8", "16 to 512")]
    [InlineData("\"grassCoverage\": 20000", "grassCoverage")]
    public void Invalid_worlds_are_rejected_with_a_clear_message(string replacement, string expectedFragment)
    {
        var error = Should.Throw<DefValidationException>(() =>
            TestContent.WithDefs($"[{Materials}, {WorldGen(replacement)}]"));

        error.Message.ShouldContain(expectedFragment);
    }

    [Fact]
    public void Strata_problems_are_reported_together()
    {
        var error = Should.Throw<DefValidationException>(() => TestContent.WithDefs(
            $"[{Materials}, {WorldGen("\"strata\": [{ \"material\": \"granite\", \"thickness\": 2 }, { \"material\": \"soil\", \"thickness\": 3 }]")}]"));

        error.Message.ShouldContain("except the last one");
        error.Message.ShouldContain("twice strataWobble");
    }

    /// <summary>A valid small world; <paramref name="replacement"/> replaces the field with the same name.</summary>
    internal static string WorldGen(string? replacement = null)
    {
        var fields = new List<string>
        {
            "\"type\": \"WorldGen\"", "\"id\": \"small\"",
            "\"width\": 32", "\"height\": 32", "\"depth\": 16",
            "\"valleyFloor\": 8", "\"hillHeight\": 4", "\"valleyHalfWidth\": 4", "\"valleyMeander\": 2",
            "\"terrainNoiseScale\": 12", "\"terrainAmplitude\": 2", "\"detailNoiseScale\": 6", "\"forestNoiseScale\": 8",
            "\"soilMaterial\": \"soil\"", "\"soilDepthMin\": 1", "\"soilDepthMax\": 3", "\"rockExposureElevation\": 4", "\"rockExposureWobble\": 1",
            "\"strata\": [{ \"material\": \"granite\" }]", "\"strataWobble\": 1", "\"grassCoverage\": 5000", "\"groveFactorMax\": 2",
            "\"vegetation\": []",
        };

        if (replacement is not null)
        {
            var name = replacement[..(replacement.IndexOf(':') + 1)];
            fields[fields.FindIndex(field => field.StartsWith(name, StringComparison.Ordinal))] = replacement;
        }

        return "{ " + string.Join(", ", fields) + " }";
    }
}
