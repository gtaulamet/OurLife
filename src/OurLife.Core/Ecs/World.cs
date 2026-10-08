using System.Diagnostics.CodeAnalysis;

namespace OurLife.Core.Ecs;

/// <summary>
/// Entities and their components. Create and destroy entities here, never through
/// <see cref="EntityManager"/> directly, so their components are always cleaned up.
/// </summary>
public sealed class World
{
    private readonly Dictionary<Type, IComponentStore> _storesByType = [];

    // Registration order, used for iteration so no decision depends on dictionary order.
    private readonly List<IComponentStore> _stores = [];

    public EntityManager Entities { get; } = new();

    public EntityId CreateEntity() => Entities.Create();

    public void DestroyEntity(EntityId entity)
    {
        EnsureAlive(entity);
        foreach (var store in _stores)
        {
            store.Remove(entity);
        }

        Entities.Destroy(entity);
    }

    public bool IsAlive(EntityId entity) => Entities.IsAlive(entity);

    public ComponentStore<T> Store<T>()
        where T : class
    {
        if (!_storesByType.TryGetValue(typeof(T), out var store))
        {
            store = new ComponentStore<T>();
            _storesByType.Add(typeof(T), store);
            _stores.Add(store);
        }

        return (ComponentStore<T>)store;
    }

    public void Add<T>(EntityId entity, T component)
        where T : class
    {
        EnsureAlive(entity);
        Store<T>().Add(entity, component);
    }

    public T Get<T>(EntityId entity)
        where T : class => Store<T>().Get(entity);

    public bool TryGet<T>(EntityId entity, [NotNullWhen(true)] out T? component)
        where T : class => Store<T>().TryGet(entity, out component);

    public bool Has<T>(EntityId entity)
        where T : class => _storesByType.TryGetValue(typeof(T), out var store) && store.Has(entity);

    public bool Remove<T>(EntityId entity)
        where T : class => _storesByType.TryGetValue(typeof(T), out var store) && store.Remove(entity);

    /// <summary>
    /// Fills <paramref name="results"/> with the entities that have <typeparamref name="T"/>, by
    /// ascending id. Keep the list in a field and reuse it to avoid allocations. The list is a
    /// copy, so adding or removing components and entities while iterating it is safe.
    /// </summary>
    public void Query<T>(List<EntityId> results)
        where T : class => Store<T>().CopyEntitiesTo(results);

    /// <summary>Same as <see cref="Query{T}"/>, for entities that have both components.</summary>
    public void Query<T1, T2>(List<EntityId> results)
        where T1 : class
        where T2 : class
    {
        Store<T1>().CopyEntitiesTo(results);
        var second = Store<T2>();
        var write = 0;
        for (var read = 0; read < results.Count; read++)
        {
            if (second.Has(results[read]))
            {
                results[write++] = results[read];
            }
        }

        results.RemoveRange(write, results.Count - write);
    }

    /// <summary>Stores sorted by component type name, for state hashing and saving.</summary>
    internal IEnumerable<IComponentStore> StoresByTypeName() =>
        _stores.OrderBy(store => store.ComponentType.FullName, StringComparer.Ordinal);

    private void EnsureAlive(EntityId entity)
    {
        if (!Entities.IsAlive(entity))
        {
            throw new InvalidOperationException($"{entity} is not alive.");
        }
    }
}
