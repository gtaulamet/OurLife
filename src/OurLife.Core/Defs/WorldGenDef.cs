namespace OurLife.Core.Defs;

/// <summary>A layer of stone, from the bottom of the map up. The last one has no thickness: it reaches the soil.</summary>
public sealed class StratumSpec
{
    public required string Material { get; init; }

    /// <summary>Levels of this stratum; zero for the last one.</summary>
    public int Thickness { get; init; }
}

/// <summary>Where and how often a plant grows. Elevations are levels above the valley floor.</summary>
public sealed class VegetationRule
{
    public required string Plant { get; init; }

    /// <summary>Chance per eligible cell, in basis points, before the grove factor.</summary>
    public required int Chance { get; init; }

    public required int MinElevation { get; init; }

    public required int MaxElevation { get; init; }
}

/// <summary>
/// Settings of a generated map: size, relief, soil, strata, grass and vegetation. See
/// <c>docs/gdd/mapa-y-generacion.md</c> for what each value does.
/// </summary>
public sealed class WorldGenDef : Def
{
    public required int Width { get; init; }

    public required int Height { get; init; }

    public required int Depth { get; init; }

    /// <summary>Level of the surface along the bottom of the valley.</summary>
    public required int ValleyFloor { get; init; }

    /// <summary>Levels the hills rise towards the edges of the map.</summary>
    public required int HillHeight { get; init; }

    /// <summary>Cells from the axis of the valley to where the hills start.</summary>
    public required int ValleyHalfWidth { get; init; }

    /// <summary>Cells the axis of the valley wanders east and west.</summary>
    public required int ValleyMeander { get; init; }

    /// <summary>Cells per feature of the relief noise.</summary>
    public required int TerrainNoiseScale { get; init; }

    /// <summary>Levels the relief noise adds or removes.</summary>
    public required int TerrainAmplitude { get; init; }

    /// <summary>Cells per feature of the soil, rock, strata and grass noise.</summary>
    public required int DetailNoiseScale { get; init; }

    /// <summary>Cells per feature of the noise that groups plants into groves.</summary>
    public required int ForestNoiseScale { get; init; }

    public required string SoilMaterial { get; init; }

    public required int SoilDepthMin { get; init; }

    public required int SoilDepthMax { get; init; }

    /// <summary>Levels above the valley floor from which the surface is bare rock.</summary>
    public required int RockExposureElevation { get; init; }

    /// <summary>Levels the edge of the bare rock moves up and down, so it is not a straight contour line.</summary>
    public required int RockExposureWobble { get; init; }

    /// <summary>Stone layers from the bottom of the map up.</summary>
    public required IReadOnlyList<StratumSpec> Strata { get; init; }

    /// <summary>Levels the boundaries between strata wave up and down.</summary>
    public required int StrataWobble { get; init; }

    /// <summary>Share of the soil floor covered with grass, in basis points.</summary>
    public required int GrassCoverage { get; init; }

    /// <summary>The grove noise multiplies plant chances between 0 and this value, which groups plants into groves.</summary>
    public required int GroveFactorMax { get; init; }

    /// <summary>Rules tried in order on each eligible cell; the first that succeeds places its plant.</summary>
    public required IReadOnlyList<VegetationRule> Vegetation { get; init; }

    internal override IEnumerable<DefReference> References()
    {
        yield return new DefReference(typeof(MaterialDef), SoilMaterial, "soilMaterial");
        for (var i = 0; i < Strata.Count; i++)
        {
            yield return new DefReference(typeof(MaterialDef), Strata[i].Material, $"strata[{i}].material");
        }

        for (var i = 0; i < Vegetation.Count; i++)
        {
            yield return new DefReference(typeof(PlantDef), Vegetation[i].Plant, $"vegetation[{i}].plant");
        }
    }

    /// <summary>
    /// Every problem of the def on its own and against a database: what the content loader checks.
    /// For callers that may get a def that did not come through the loader.
    /// </summary>
    internal IEnumerable<string> ProblemsWith(DefDatabase content)
    {
        foreach (var problem in Validate())
        {
            yield return problem;
        }

        foreach (var reference in References())
        {
            if ((reference.Target == typeof(MaterialDef) && !content.TryGet<MaterialDef>(reference.Id!, out _))
                || (reference.Target == typeof(PlantDef) && !content.TryGet<PlantDef>(reference.Id!, out _)))
            {
                yield return $"{reference.Field} points to '{reference.Id}', which is not in the content.";
            }
        }
    }

    internal override IEnumerable<string> Validate()
    {
        if (Width is < 16 or > 512 || Height is < 16 or > 512 || Depth is < 8 or > 128)
        {
            yield return "the map must be 16 to 512 cells wide and long, and 8 to 128 levels deep.";
        }

        if (SoilDepthMin < 0 || SoilDepthMax < SoilDepthMin)
        {
            yield return "soilDepthMin must be zero or positive and not above soilDepthMax.";
        }

        if (ValleyFloor - TerrainAmplitude <= SoilDepthMax)
        {
            yield return "valleyFloor minus terrainAmplitude must leave rock below the deepest soil.";
        }

        if (ValleyFloor + HillHeight + TerrainAmplitude > Depth - 2)
        {
            yield return "valleyFloor plus hillHeight plus terrainAmplitude must leave two levels of air at the top.";
        }

        if (HillHeight < 0 || TerrainAmplitude < 0 || ValleyHalfWidth < 0 || ValleyMeander < 0 || StrataWobble < 0 || RockExposureWobble < 0)
        {
            yield return "hillHeight, terrainAmplitude, valleyHalfWidth, valleyMeander, strataWobble and rockExposureWobble must be zero or positive.";
        }

        if (TerrainNoiseScale <= 0 || DetailNoiseScale <= 0 || ForestNoiseScale <= 0)
        {
            yield return "noise scales must be positive.";
        }

        if (RockExposureElevation <= TerrainAmplitude + RockExposureWobble)
        {
            yield return "rockExposureElevation must be above terrainAmplitude plus rockExposureWobble, or the valley floor could be bare rock.";
        }

        if (Strata.Count == 0)
        {
            yield return "strata needs at least one layer.";
        }
        else
        {
            var lower = Strata.Take(Strata.Count - 1).ToList();
            if (lower.Any(stratum => stratum.Thickness <= 0) || Strata[^1].Thickness != 0)
            {
                yield return "every stratum needs a positive thickness, except the last one, which has none.";
            }

            // Two boundaries can wave towards each other by strataWobble each.
            if (lower.Any(stratum => stratum.Thickness > 0 && stratum.Thickness <= 2 * StrataWobble))
            {
                yield return "every stratum thickness must be more than twice strataWobble, or a middle layer could vanish from a column.";
            }
        }

        if (GrassCoverage is < 0 or > 10_000)
        {
            yield return "grassCoverage must be between 0 and 10000.";
        }

        if (GroveFactorMax < 1)
        {
            yield return "groveFactorMax must be at least 1.";
        }

        foreach (var rule in Vegetation)
        {
            if (rule.Chance < 0 || rule.Chance * GroveFactorMax > 10_000 || rule.MinElevation > rule.MaxElevation)
            {
                yield return $"vegetation rule for '{rule.Plant}': chance times groveFactorMax must be between 0 and 10000, and minElevation not above maxElevation.";
            }
        }
    }
}
