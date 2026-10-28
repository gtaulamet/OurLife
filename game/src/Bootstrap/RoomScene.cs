using System.Reflection;
using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Game.Bootstrap;

/// <summary>
/// The scene of the <c>--rooms</c> capture mode (milestone O9): next to the first colonist, a bedroom with
/// beds and a chest, and a dining room with a table and chairs, flattened ground, planks for floors and
/// masonry for walls. Only for screenshots: it builds in a blink what a village takes days to build, and
/// for that it calls the methods only the Core may call, like the smoke of the farm tool does with the knowledge.
/// The game itself never does this.
/// </summary>
internal static class RoomScene
{
    private const int Width = 7;
    private const int Depth = 5;

    private static readonly MethodInfo SetTileMethod = typeof(GameMap).GetMethod("SetTile", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("GameMap.SetTile is gone: the --rooms scene needs another way to build.");

    private static readonly MethodInfo SetFlagsMethod = typeof(GameMap).GetMethod("SetFlags", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("GameMap.SetFlags is gone: the --rooms scene needs another way to mark furniture.");

    /// <summary>What was built, for the capture to point the camera at.</summary>
    public readonly record struct Built(TileCoord BedroomCell, TileCoord DiningCell);

    /// <summary>Builds the scene east of the first colonist and returns the cells inside each room.</summary>
    public static Built Build(Simulation simulation, TileCoord origin)
    {
        var map = simulation.Map;
        var z = origin.Z;
        var x0 = Math.Min(origin.X + 5, map.Size.Width - (2 * Width) - 4);
        var y0 = Math.Min(origin.Y - 2, map.Size.Height - Depth - 3);
        var soil = map.MaterialIndexAt(origin.X, origin.Y, z);
        Flatten(simulation, x0 - 1, y0 - 1, x0 + (2 * Width) + 2, y0 + Depth + 1, z, soil);

        // Bedroom: 5 x 3 inside, timber floor and walls; three beds along the north wall and a chest.
        House(simulation, x0, y0, Width, Depth, z, "timber", "timber");
        foreach (var (buildable, dx, dy) in new[] { ("bed", 1, 1), ("bed", 2, 1), ("bed", 3, 1), ("cradle", 4, 1), ("chest", 5, 1) })
        {
            Furnish(simulation, buildable, x0 + dx, y0 + dy, z, 0);
        }

        // Dining room next to it: a table with chairs on three sides, facing it. Bare earth floor and timber walls.
        var dx0 = x0 + Width + 1;
        House(simulation, dx0, y0, Width, Depth, z, "soil", "timber");
        Furnish(simulation, "table", dx0 + 3, y0 + 2, z, 0);
        Furnish(simulation, "table", dx0 + 2, y0 + 2, z, 0);
        Furnish(simulation, "chair", dx0 + 1, y0 + 2, z, 0);
        Furnish(simulation, "chair", dx0 + 4, y0 + 2, z, 0);
        Furnish(simulation, "chair", dx0 + 2, y0 + 1, z, 0);
        Furnish(simulation, "chair", dx0 + 3, y0 + 1, z, 0);
        Furnish(simulation, "chair", dx0 + 2, y0 + 3, z, 0);
        Furnish(simulation, "chair", dx0 + 3, y0 + 3, z, 0);
        return new Built(new TileCoord(x0 + 2, y0 + 2, z), new TileCoord(dx0 + 2, y0 + 2, z));
    }

    /// <summary>
    /// Sits the colonists, one per chair of the dining room, as if they had come to gather: the day a colony
    /// has idle colonists in the evening takes long to wait for, and what the screenshot shows is how they sit.
    /// Gives each the job the colonists give themselves (<see cref="JobKind.Gather"/>, working) and puts it on its chair.
    /// </summary>
    public static int Seat(Simulation simulation, IReadOnlyList<EntityId> colonists, TileCoord diningCell, int ticks)
    {
        var world = simulation.World;
        if (Rooms.At(simulation, diningCell) is not { } room)
        {
            return 0;
        }

        var seated = 0;
        foreach (var piece in room.Furniture)
        {
            if (seated == colonists.Count)
            {
                break;
            }

            if (simulation.Content.Get<BuildableDef>(world.Get<Furniture>(piece).Buildable).Tags.Contains(RoomRules.SeatTag))
            {
                var colonist = colonists[seated++];
                var cell = world.Get<Position>(piece).Cell;
                var at = world.Get<Position>(colonist);
                (at.X, at.Y, at.Z) = (cell.X, cell.Y, cell.Z);
                world.Get<Movement>(colonist).Stop();
                world.Remove<Job>(colonist);
                world.Add(colonist, new Job { Kind = JobKind.Gather, Stage = JobStage.Working, Recipient = piece, Cell = cell, UntilTick = simulation.CurrentTick + ticks });
                simulation.Events.Emit(new EntityChangedEvent(colonist));
            }
        }

        return seated;
    }

    private static void Flatten(Simulation simulation, int fromX, int fromY, int toX, int toY, int z, ushort soil)
    {
        var map = simulation.Map;
        var world = simulation.World;
        var everything = new List<EntityId>();
        world.Query<Position>(everything);
        foreach (var entity in everything)
        {
            var at = world.Get<Position>(entity);
            if (at.X >= fromX && at.X <= toX && at.Y >= fromY && at.Y <= toY && !world.Has<Colonist>(entity))
            {
                world.DestroyEntity(entity);
                simulation.Events.Emit(new EntityRemovedEvent(entity));
            }
        }

        for (var x = fromX; x <= toX; x++)
        {
            for (var y = fromY; y <= toY; y++)
            {
                for (var level = 0; level < map.Size.Depth; level++)
                {
                    var shape = level < z ? TileShape.Wall : level == z ? TileShape.Floor : TileShape.Empty;
                    var material = shape == TileShape.Empty ? GameMap.NoMaterial : soil;
                    Set(map, x, y, level, shape, material, map.FlagsAt(x, y, level) & TileFlags.Discovered);
                }
            }
        }
    }

    private static void House(Simulation simulation, int x0, int y0, int width, int depth, int z, string floor, string walls)
    {
        var map = simulation.Map;
        var floorMaterial = IndexOf(map, floor);
        var wallMaterial = IndexOf(map, walls);
        for (var x = x0; x < x0 + width; x++)
        {
            for (var y = y0; y < y0 + depth; y++)
            {
                var edge = x == x0 || y == y0 || x == x0 + width - 1 || y == y0 + depth - 1;
                var door = x == x0 + (width / 2) && y == y0 + depth - 1;
                var shape = door ? TileShape.Door : edge ? TileShape.Wall : TileShape.Floor;
                Set(map, x, y, z, shape, edge ? wallMaterial : floorMaterial, map.FlagsAt(x, y, z) & TileFlags.Discovered);
            }
        }
    }

    private static void Furnish(Simulation simulation, string buildable, int x, int y, int z, int rotation)
    {
        var world = simulation.World;
        var def = simulation.Content.Get<BuildableDef>(buildable);
        var cell = new TileCoord(x, y, z);
        SetFlagsMethod.Invoke(simulation.Map, [x, y, z, simulation.Map.FlagsAt(cell) | TileFlags.Furniture]);
        var piece = world.CreateEntity();
        world.Add(piece, new Position { X = x, Y = y, Z = z });
        world.Add(piece, new Furniture { Buildable = def.Id, Material = def.Materials.Contains("planks") ? "planks" : def.Materials[0], Rotation = rotation });
        if (def.RestRecoveryPercent is not null)
        {
            world.Add(piece, new Bed());
        }

        if (def.Storage)
        {
            world.Add(piece, new Stockpile { Cells = [cell], Categories = [.. simulation.Content.All<ItemCategoryDef>().Select(category => category.Id)] });
            simulation.Events.Emit(new StockpilesChangedEvent());
        }

        simulation.Events.Emit(new EntitySpawnedEvent(piece));
    }

    private static ushort IndexOf(GameMap map, string material)
    {
        for (var i = 0; i < map.Materials.Count; i++)
        {
            if (map.Materials[i].Id == material)
            {
                return (ushort)i;
            }
        }

        throw new InvalidOperationException($"The material '{material}' does not exist.");
    }

    private static void Set(GameMap map, int x, int y, int z, TileShape shape, ushort material, TileFlags flags) =>
        SetTileMethod.Invoke(map, [x, y, z, shape, material, flags]);
}
