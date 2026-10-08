namespace OurLife.Core.Ecs;

/// <summary>Non-generic view of a component store, used by the world for bookkeeping.</summary>
public interface IComponentStore
{
    Type ComponentType { get; }

    int Count { get; }

    bool Has(EntityId entity);

    bool Remove(EntityId entity);

    object GetBoxed(EntityId entity);

    /// <summary>Clears <paramref name="results"/> and fills it with the owners, by ascending id.</summary>
    void CopyEntitiesTo(List<EntityId> results);
}
