using OurLife.Core.Ecs;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>A colonist, item or plant appeared: the presentation starts drawing it.</summary>
public sealed record EntitySpawnedEvent(EntityId Entity) : ISimEvent;

/// <summary>Something the presentation shows about an entity changed, like a pile's size or a gathered bush.</summary>
public sealed record EntityChangedEvent(EntityId Entity) : ISimEvent;

/// <summary>An entity is about to disappear; it is still alive while the event is emitted, but gone when it is read.</summary>
public sealed record EntityRemovedEvent(EntityId Entity) : ISimEvent;

/// <summary>The player's orders changed inside a box of cells, corners included.</summary>
public sealed record DesignationsChangedEvent(TileCoord From, TileCoord To) : ISimEvent;

/// <summary>The last colonist died: the game is over.</summary>
public sealed record ColonyLostEvent(long Tick) : ISimEvent;
