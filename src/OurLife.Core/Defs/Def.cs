using System.Text.Json.Serialization;

namespace OurLife.Core.Defs;

/// <summary>
/// Base of every piece of data-driven content. Ids are lowercase, like <c>berry_bush</c>, and
/// unique per def type. Player-facing texts come from keys like <c>def.plant.berry_bush.name</c>.
/// Nothing about a specific era lives in code: <see cref="Era"/> and <see cref="Requires"/>
/// decide when content becomes available (architecture rule 5).
/// </summary>
public abstract class Def
{
    /// <summary>Def type as written in JSON, like "Material" or "Plant".</summary>
    public string Type { get; init; } = string.Empty;

    public required string Id { get; init; }

    /// <summary>Earliest era in which this content exists. Null means from the first era.</summary>
    public string? Era { get; init; }

    /// <summary>Knowledge the colony needs before this content is available.</summary>
    public IReadOnlyList<string> Requires { get; init; } = [];

    /// <summary>Segment of the localization keys, as in <c>def.material.granite.name</c>. Null when the def has no texts.</summary>
    [JsonIgnore]
    public string? TextCategory => DefTypes.TextCategoryOf(GetType());

    /// <summary>Key of the player-facing name, or null when the def has no texts.</summary>
    [JsonIgnore]
    public string? NameKey => TextCategory is null ? null : $"def.{TextCategory}.{Id}.name";

    /// <summary>Content file the def was loaded from, for error messages.</summary>
    [JsonIgnore]
    public string SourceFile { get; internal set; } = string.Empty;

    /// <summary>Other defs this one points to; they are checked when the database is built.</summary>
    internal virtual IEnumerable<DefReference> References() => [];

    /// <summary>Rules about this def's own values, one message per problem.</summary>
    internal virtual IEnumerable<string> Validate() => [];
}

/// <summary>A pointer from one def to another, by type and id. A null id means "not set".</summary>
internal readonly record struct DefReference(Type Target, string? Id, string Field);
