using OurLife.Core.Map;

namespace OurLife.Core.Entities;

/// <summary>
/// The route a creature walks, one cell at a time. <see cref="Progress"/> counts towards the next
/// step; the presentation can use it to draw the creature between two cells.
/// </summary>
public sealed class Movement
{
    /// <summary>Cells still to walk, in order; the creature's own cell is not included.</summary>
    public List<TileCoord> Path { get; set; } = [];

    /// <summary>Index in <see cref="Path"/> of the next cell.</summary>
    public int Next { get; set; }

    /// <summary>Progress towards the next cell, in thousandths of a tick of walking at full speed.</summary>
    public int Progress { get; set; }

    public bool IsMoving => Next < Path.Count;

    public void Stop()
    {
        Path.Clear();
        Next = 0;
        Progress = 0;
    }
}
