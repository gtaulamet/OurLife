using System.Diagnostics.CodeAnalysis;

namespace OurLife.Core.Ecs;

/// <summary>
/// Components of one type, indexed by entity slot. Owners are kept sorted by slot index, so the
/// iteration order is stable and identical after saving and loading a game. Components are
/// plain serializable classes and systems mutate them in place.
/// </summary>
public sealed class ComponentStore<T> : IComponentStore
    where T : class
{
    private readonly List<int> _sortedSlots = [];
    private T?[] _components = [];
    private EntityId[] _owners = [];

    public Type ComponentType => typeof(T);

    public int Count => _sortedSlots.Count;

    /// <summary>Goes up with every component added or removed: a cheap way to tell that the owners changed.</summary>
    public int Version { get; private set; }

    public bool Has(EntityId entity) =>
        !entity.IsNone
        && (uint)entity.Index < (uint)_owners.Length
        && _owners[entity.Index] == entity;

    public void Add(EntityId entity, T component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (entity.IsNone)
        {
            throw new ArgumentException("Cannot add a component to EntityId.None.", nameof(entity));
        }

        EnsureCapacity(entity.Index + 1);
        if (!_owners[entity.Index].IsNone)
        {
            throw new InvalidOperationException(
                $"{entity} cannot get a {typeof(T).Name}: the slot already holds one for {_owners[entity.Index]}.");
        }

        Version++;
        _owners[entity.Index] = entity;
        _components[entity.Index] = component;
        var position = _sortedSlots.BinarySearch(entity.Index);
        _sortedSlots.Insert(~position, entity.Index);
    }

    public T Get(EntityId entity) =>
        TryGet(entity, out var component)
            ? component
            : throw new KeyNotFoundException($"{entity} has no {typeof(T).Name}.");

    public bool TryGet(EntityId entity, [NotNullWhen(true)] out T? component)
    {
        if (Has(entity))
        {
            component = _components[entity.Index]!;
            return true;
        }

        component = null;
        return false;
    }

    public bool Remove(EntityId entity)
    {
        if (!Has(entity))
        {
            return false;
        }

        Version++;
        _owners[entity.Index] = EntityId.None;
        _components[entity.Index] = null;
        _sortedSlots.RemoveAt(_sortedSlots.BinarySearch(entity.Index));
        return true;
    }

    public void CopyEntitiesTo(List<EntityId> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        results.Clear();
        foreach (var slot in _sortedSlots)
        {
            results.Add(_owners[slot]);
        }
    }

    object IComponentStore.GetBoxed(EntityId entity) => Get(entity);

    private void EnsureCapacity(int size)
    {
        if (size <= _owners.Length)
        {
            return;
        }

        var newSize = Math.Max(size, Math.Max(16, _owners.Length * 2));
        Array.Resize(ref _owners, newSize);
        Array.Resize(ref _components, newSize);
    }
}
