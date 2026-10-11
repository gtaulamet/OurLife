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

    /// <summary>
    /// Puts back the slots of a saved game: the generation of every slot (negated while free) and
    /// the free slots in reuse order. Only for an empty manager.
    /// </summary>
    internal void Restore(IReadOnlyList<int> generations, IReadOnlyList<int> freeSlots)
    {
        if (_generations.Count > 0)
        {
            throw new InvalidOperationException("Slots can only be restored into an empty manager.");
        }

        var free = new bool[generations.Count];
        foreach (var slot in freeSlots)
        {
            if ((uint)slot >= (uint)generations.Count || generations[slot] >= 0 || free[slot])
            {
                throw new ArgumentException($"Free slot {slot} is not a free slot, or appears twice.", nameof(freeSlots));
            }

            free[slot] = true;
        }

        for (var slot = 0; slot < generations.Count; slot++)
        {
            if (generations[slot] == 0 || (generations[slot] < 0) != free[slot])
            {
                throw new ArgumentException($"Slot {slot} has generation {generations[slot]}, which does not match the free slots.", nameof(generations));
            }
        }

        _generations.AddRange(generations);
        foreach (var slot in freeSlots)
        {
            _freeSlots.Enqueue(slot);
        }

        AliveCount = generations.Count - freeSlots.Count;
    }

    internal EntityId Create()
    {
        EntityId id;
        if (_freeSlots.TryPeek(out var index))
        {
            // Compute the new generation before touching any state, so an overflow leaves it intact.
            var generation = checked(-_generations[index] + 1);
            _freeSlots.Dequeue();
            _generations[index] = generation;
            id = new EntityId(index, generation);
        }
        else
        {
            _generations.Add(1);
            id = new EntityId(_generations.Count - 1, 1);
        }

        AliveCount++;
        return id;
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
