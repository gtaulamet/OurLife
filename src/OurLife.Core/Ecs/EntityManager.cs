namespace OurLife.Core.Ecs;

/// <summary>
/// Allocates entity ids. Freed slots are reused in first-in, first-out order with a higher
/// generation, so the ids handed out depend only on the sequence of create and destroy calls.
/// Create and destroy entities through <see cref="World"/>, which also removes their components.
/// </summary>
public sealed class EntityManager
{
    // Generation of each slot: positive while the slot is alive, negated while it is free.
    private readonly List<int> _generations = [];
    private readonly Queue<int> _freeSlots = new();

    /// <summary>Number of living entities.</summary>
    public int AliveCount { get; private set; }

    /// <summary>Number of slots ever allocated, alive or free.</summary>
    public int SlotCount => _generations.Count;

    public bool IsAlive(EntityId id) =>
        id.Generation > 0
        && (uint)id.Index < (uint)_generations.Count
        && _generations[id.Index] == id.Generation;

    /// <summary>Slot generations, for state hashing and saving.</summary>
    internal IReadOnlyList<int> Generations => _generations;

    /// <summary>Free slots in reuse order, for state hashing and saving.</summary>
    internal IEnumerable<int> FreeSlots => _freeSlots;

    internal int FreeSlotCount => _freeSlots.Count;

    internal EntityId Create()
    {
        AliveCount++;
        if (_freeSlots.Count > 0)
        {
            var index = _freeSlots.Dequeue();
            var generation = checked(-_generations[index] + 1);
            _generations[index] = generation;
            return new EntityId(index, generation);
        }

        _generations.Add(1);
        return new EntityId(_generations.Count - 1, 1);
    }

    internal void Destroy(EntityId id)
    {
        if (!IsAlive(id))
        {
            throw new InvalidOperationException($"{id} is not alive.");
        }

        _generations[id.Index] = -id.Generation;
        _freeSlots.Enqueue(id.Index);
        AliveCount--;
    }
}
