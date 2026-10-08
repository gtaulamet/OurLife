namespace OurLife.Core.Ecs;

/// <summary>
/// Reference to an entity: a slot index plus a generation. When an entity is destroyed its
/// slot can be reused, but with a higher generation, so stale references never match the new
/// entity. <c>default</c> equals <see cref="None"/>.
/// </summary>
public readonly record struct EntityId(int Index, int Generation) : IComparable<EntityId>
{
    /// <summary>No entity. Generations start at 1, so generation 0 never refers to a live entity.</summary>
    public static readonly EntityId None = default;

    public bool IsNone => Generation == 0;

    public int CompareTo(EntityId other)
    {
        var byIndex = Index.CompareTo(other.Index);
        return byIndex != 0 ? byIndex : Generation.CompareTo(other.Generation);
    }

    public override string ToString() => IsNone ? "Entity(none)" : $"Entity({Index}#{Generation})";
}
