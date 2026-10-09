using System.Text.Json.Serialization;
using OurLife.Core.Map;

namespace OurLife.Core.Entities;

/// <summary>Cell an entity occupies.</summary>
public sealed class Position
{
    public int X { get; set; }

    public int Y { get; set; }

    public int Z { get; set; }

    [JsonIgnore]
    public TileCoord Cell => new(X, Y, Z);
}
