using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;
using OurLife.Core.WorldGen;

namespace OurLife.Content.Tests;

/// <summary>
/// A simulated player for the integration balance of O11: it plays a village for ten game years the
/// way a patient person would, only with commands. It keeps the bushes and the timber ordered, researches
/// what the field, the kitchen and the forge need, draws a field of wheat and two of vegetables, builds a
/// scriptorium, a carpenter's bench, a dining hall, houses with beds for everyone, a kitchen, a charcoal
/// kiln, a bloomery and a forge, keeps flour, bread, pottage, planks, charcoal and ingots in production,
/// digs the iron veins in sight and orders the four iron tools. Pure data and commands: it never touches
/// the simulation any other way. See <c>docs/ip/bitacora.md</c> for what it showed.
/// </summary>
internal sealed class VillagePlayer
{
    private const int GatherRadius = 25;
    private const int HouseSize = 5;
    private const int SearchRadius = 60;

    private static readonly string[] IronTools = ["iron_axe", "iron_pickaxe", "iron_hoe", "iron_hammer"];

    private static readonly int[] StockpileSizes = [6, 4, 3];

    private static readonly (int X, int Y)[] Steps = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    private static readonly string[] ResearchOrder =
        ["agriculture", "cereals", "milling", "stonecutting", "charcoal_burning", "metallurgy", "ironwork", "food_preservation", "joinery", "silviculture"];

    private readonly Simulation _simulation;
    private readonly TileCoord _arrival;
    private readonly int _top;
    private readonly List<(int X0, int Y0, int X1, int Y1)> _reserved = [];
    private readonly HashSet<string> _done = new(StringComparer.Ordinal);
    private int _lastHouseDay = -100;
    private int _bedsPlaced;
    private int _miningRadius = 2;

    public VillagePlayer(Simulation simulation, TileCoord arrival)
    {
        _simulation = simulation;
        _arrival = arrival;
        _top = simulation.Map.Size.Depth - 1;
    }

    public int HousesPlaced { get; private set; }

    public int FieldsPlaced { get; private set; }

    /// <summary>Starts a new village: a stockpile, and the first orders.</summary>
    public static (Simulation Simulation, VillagePlayer Player) Found(ulong seed)
    {
        var simulation = new Simulation(seed, GameContent.Defs);
        WorldGenerator.Generate(simulation, GameContent.Defs.All<WorldGenDef>()[0]);
        var colonists = ColonyStart.Settle(simulation, ColonyStart.TheOnlyScenario(GameContent.Defs), "Valdeolmos");
        ColonySystems.AddTo(simulation);
        var arrival = simulation.World.Get<Position>(colonists[0]).Cell;
        var player = new VillagePlayer(simulation, arrival);
        player.Reserve(arrival.X - 9, arrival.Y + 1, arrival.X - 2, arrival.Y + 6);
        simulation.Commands.Enqueue(new DesignateCommand(
            DesignationKind.Stockpile, arrival with { X = arrival.X - 9, Y = arrival.Y + 1 }, arrival with { X = arrival.X - 2, Y = arrival.Y + 6 }));
        player.Reserve(arrival.X - 1, arrival.Y - 1, arrival.X + 1, arrival.Y + 1);
        return (simulation, player);
    }

    /// <summary>What the player does at the start of a game day (the first is day 1).</summary>
    public void PlayDay(int day)
    {
        var simulation = _simulation;
        var world = simulation.World;
        // The bushes are gathered while the pantry is short of what a birth asks (30 days of food for every mouth, one more
        // than there is), with a margin: a player does not keep gathering to let it all rot.
        var mouths = world.Store<Colonist>().Count + 1;
        var needed = 4_800L * 30 * mouths * 130 / 100;
        if (day % 2 == 0 && ColonyStock.FoodReserve(simulation) < needed)
        {
            Designate(DesignationKind.Harvest, -GatherRadius, GatherRadius, 0, _top);
        }

        // More room for the stores as the village grows: a stockpile on flat open ground every twenty days, the biggest that fits.
        if (day % 20 == 10 && day <= 450)
        {
            foreach (var size in StockpileSizes)
            {
                if (FindSpot(size, 0) is { } room)
                {
                    Reserve(room.X, room.Y, room.X + size - 1, room.Y + size - 1);
                    simulation.Commands.Enqueue(new DesignateCommand(DesignationKind.Stockpile, room, room with { X = room.X + size - 1, Y = room.Y + size - 1 }));
                    break;
                }
            }
        }

        if (day % 6 == 1 && Stocks.Count(simulation, "log") + (2 * Stocks.Count(simulation, "plank")) < 60)
        {
            Designate(DesignationKind.Chop, -GatherRadius, GatherRadius, Math.Max(0, _arrival.Z - 3), Math.Min(_top, _arrival.Z + 3));
        }

        if (simulation.Progress.ResearchQueue.Count == 0)
        {
            var next = ResearchOrder.FirstOrDefault(knowledge => ResearchRules.State(simulation, knowledge) == KnowledgeState.Available);
            if (next is not null)
            {
                simulation.Commands.Enqueue(new SetResearchQueueCommand([next]));
            }
        }

        if (day % 5 == 1)
        {
            DigStone();
        }

        if (day % 10 == 3)
        {
            DigIron();
        }

        if (world.Store<Blueprint>().Count >= 3)
        {
            return;
        }

        PlaceNextProject(day);
    }

    private void PlaceNextProject(int day)
    {
        var planks = Stocks.Count(_simulation, "plank");
        var stone = Stocks.Count(_simulation, "stone");
        var known = _simulation.Progress.Knowledge;
        var population = _simulation.World.Store<Colonist>().Count;
        var beds = _simulation.World.Store<Bed>().Count;
        var commands = _simulation.Commands;

        // The first project that is ready and finds room goes up; the rest wait for another day. The work orders that go with a project
        // are given only once it is placed: a project that finds no room does not repeat its orders every day.
        bool Try(string id, bool ready, Func<bool> place, Action? orders = null)
        {
            if (_done.Contains(id) || !ready || !place())
            {
                return false;
            }

            orders?.Invoke();
            _done.Add(id);
            return true;
        }

        void Keep(string item, int count) => commands.Enqueue(new CreateWorkOrderCommand(item, WorkOrderMode.Keep, count));

        _ = Try("scriptorium", planks >= 6, () => Furniture("scriptorium", "planks"))
            || Try("bench", planks >= 6, () => Furniture("carpenter_bench", "planks"), () => Keep("plank", 30))
            || Try("house1", day > 3 && planks >= 12, () => House(day))
            || Try("kitchen", stone >= 8, () => Furniture("kitchen", "masonry"), () => Keep("pottage", 6))
            || Try("hall", day > 10 && planks >= 14, Hall)
            || Try("wheat", known.Contains("milling") && day > _simulation.Time.DaysPerYear, () => Field("wheat", 8, 8), () =>
            {
                Keep("flour", 12);
                Keep("bread", 20);
            })
            || Try("peas", known.Contains("agriculture") && _done.Contains("wheat"), () => Field("peas", 5, 6))
            || Try("cabbage", known.Contains("agriculture") && _done.Contains("peas"), () => Field("cabbage", 5, 6))
            || Try("kiln", known.Contains("charcoal_burning") && stone >= 8, () => Furniture("charcoal_kiln", "masonry"), () => Keep("charcoal", 6))
            || Try("bloomery", known.Contains("metallurgy") && stone >= 12, () => Furniture("bloomery", "masonry"), () => Keep("iron_ingot", 4))
            || Try("forge", known.Contains("ironwork") && stone >= 12, () => Furniture("forge", "masonry"), () =>
            {
                Keep("handle", 4);
                foreach (var tool in IronTools)
                {
                    commands.Enqueue(new CreateWorkOrderCommand(tool, WorkOrderMode.Count, 1));
                }
            })
            || (_done.Contains("house1") && day - _lastHouseDay >= 12 && _bedsPlaced < population + 2 && planks >= 13 && House(day));
    }

    private void Designate(DesignationKind kind, int from, int to, int fromZ, int toZ) =>
        _simulation.Commands.Enqueue(new DesignateCommand(
            kind, new TileCoord(_arrival.X + from, _arrival.Y + from, fromZ), new TileCoord(_arrival.X + to, _arrival.Y + to, toZ)));

    /// <summary>Digs the rock around the vein nearest to the village, for stone, one ring more each time.</summary>
    private void DigStone()
    {
        if (Stocks.Count(_simulation, "stone") >= 40 || NearestVein() is not { } vein)
        {
            return;
        }

        var radius = Math.Min(_miningRadius++, 16);
        var map = _simulation.Map;
        _simulation.Commands.Enqueue(new DesignateCommand(
            DesignationKind.Mine,
            new TileCoord(Math.Max(0, vein.X - radius), Math.Max(0, vein.Y - radius), Math.Max(0, vein.Z - 1)),
            new TileCoord(Math.Min(map.Size.Width - 1, vein.X + radius), Math.Min(map.Size.Height - 1, vein.Y + radius), Math.Min(_top, vein.Z + 2))));
    }

    /// <summary>Orders the iron cells in sight that have a floor beside them.</summary>
    private void DigIron()
    {
        if (Stocks.Count(_simulation, "iron_ore") + (2 * Stocks.Count(_simulation, "iron_ingot")) >= 14)
        {
            return;
        }

        var map = _simulation.Map;
        var ordered = 0;
        foreach (var index in SubsoilTests.Veins(map))
        {
            var cell = map.Size.CoordOf(index);
            if (!map.IsDiscovered(cell) || ordered >= 12)
            {
                continue;
            }

            if (Steps.Any(step => map.IsStandable(cell.X + step.X, cell.Y + step.Y, cell.Z)))
            {
                _simulation.Commands.Enqueue(new DesignateCommand(DesignationKind.Mine, cell, cell));
                ordered++;
            }
        }
    }

    private TileCoord? NearestVein()
    {
        var map = _simulation.Map;
        TileCoord? best = null;
        var bestDistance = long.MaxValue;
        foreach (var index in SubsoilTests.Veins(map))
        {
            var cell = map.Size.CoordOf(index);
            var distance = ((long)(cell.X - _arrival.X) * (cell.X - _arrival.X)) + ((long)(cell.Y - _arrival.Y) * (cell.Y - _arrival.Y));
            if (map.IsDiscovered(cell) && distance < bestDistance)
            {
                (best, bestDistance) = (cell, distance);
            }
        }

        return best;
    }

    private bool Furniture(string buildable, string material)
    {
        if (FindSpot(3, 1) is not { } spot)
        {
            return false;
        }

        Reserve(spot.X, spot.Y, spot.X + 2, spot.Y + 2);
        var cell = new TileCoord(spot.X + 1, spot.Y + 1, spot.Z);
        _simulation.Commands.Enqueue(new PlaceBlueprintCommand(buildable, material, cell, cell));
        return true;
    }

    private void Wall(TileCoord corner, int size = HouseSize)
    {
        var far = new TileCoord(corner.X + size - 1, corner.Y + size - 1, corner.Z);
        _simulation.Commands.Enqueue(new PlaceBlueprintCommand("wall", "timber", corner, far));
        var door = new TileCoord(corner.X + (size / 2), far.Y, corner.Z);
        _simulation.Commands.Enqueue(new DesignateCommand(DesignationKind.Cancel, door, door));
        _simulation.Commands.Enqueue(new PlaceBlueprintCommand("door", "timber", door, door));
    }

    private void Place(string buildable, TileCoord corner, int dx, int dy)
    {
        var cell = new TileCoord(corner.X + dx, corner.Y + dy, corner.Z);
        _simulation.Commands.Enqueue(new PlaceBlueprintCommand(buildable, "planks", cell, cell));
    }

    /// <summary>
    /// A house of 5 × 5 with a door in the middle of its south wall, two beds, a cradle, a table and a chair; where there is no room
    /// for it, a small one of 4 × 4 with two beds.
    /// </summary>
    private bool House(int day)
    {
        if (FindSpot(HouseSize, 1) is { } spot)
        {
            Reserve(spot.X - 1, spot.Y - 1, spot.X + HouseSize, spot.Y + HouseSize);
            Wall(spot);
            Place("bed", spot, 1, 1);
            Place("bed", spot, 3, 1);
            Place("cradle", spot, 2, 1);
            Place("table", spot, 3, 3);
            Place("chair", spot, 1, 3);
        }
        else if (FindSpot(4, 1) is { } small)
        {
            Reserve(small.X - 1, small.Y - 1, small.X + 4, small.Y + 4);
            Wall(small, 4);
            Place("bed", small, 1, 1);
            Place("bed", small, 2, 1);
        }
        else
        {
            return false;
        }

        _bedsPlaced += 2;
        _lastHouseDay = day;
        HousesPlaced++;
        return true;
    }

    /// <summary>A hall of 5 × 5 with two tables and four chairs.</summary>
    private bool Hall()
    {
        if (FindSpot(HouseSize, 1) is not { } spot)
        {
            return false;
        }

        Reserve(spot.X - 1, spot.Y - 1, spot.X + HouseSize, spot.Y + HouseSize);
        var corner = spot;
        Wall(corner);
        Place("table", corner, 1, 1);
        Place("table", corner, 3, 1);
        Place("chair", corner, 1, 2);
        Place("chair", corner, 3, 2);
        Place("chair", corner, 1, 3);
        Place("chair", corner, 3, 3);
        return true;
    }

    private bool Field(string crop, int width, int height)
    {
        TileCoord? best = null;
        var most = 0;
        for (var dy = -18; dy <= 10; dy++)
        {
            for (var dx = -18; dx <= 10; dx++)
            {
                var corner = new TileCoord(_arrival.X + dx, _arrival.Y + dy, _arrival.Z);
                if (Overlaps(corner.X, corner.Y, corner.X + width - 1, corner.Y + height - 1))
                {
                    continue;
                }

                var cells = FarmRules.CellsToZone(_simulation, corner, corner with { X = corner.X + width - 1, Y = corner.Y + height - 1 }).Count;
                if (cells > most)
                {
                    (best, most) = (corner, cells);
                }
            }
        }

        if (best is not { } origin)
        {
            return false;
        }

        Reserve(origin.X - 1, origin.Y - 1, origin.X + width, origin.Y + height);
        _simulation.Commands.Enqueue(new PlaceFarmZoneCommand(origin, origin with { X = origin.X + width - 1, Y = origin.Y + height - 1 }, crop));
        FieldsPlaced++;
        return true;
    }

    private void Reserve(int x0, int y0, int x1, int y1) => _reserved.Add((x0, y0, x1, y1));

    private bool Overlaps(int x0, int y0, int x1, int y1) =>
        _reserved.Any(box => x0 <= box.X1 && x1 >= box.X0 && y0 <= box.Y1 && y1 >= box.Y0);

    /// <summary>The nearest square of flat open floor, without plants, outside what is reserved; the search goes by rings of growing distance.</summary>
    private TileCoord? FindSpot(int size, int margin)
    {
        var map = _simulation.Map;
        var plants = Designations.PlantCells(_simulation);
        for (var distance = 2; distance <= SearchRadius; distance++)
        {
            for (var dy = -distance; dy <= distance; dy++)
            {
                for (var dx = -distance; dx <= distance; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != distance)
                    {
                        continue;
                    }

                    var x0 = _arrival.X + dx;
                    var y0 = _arrival.Y + dy;
                    if (Overlaps(x0 - margin, y0 - margin, x0 + size - 1 + margin, y0 + size - 1 + margin))
                    {
                        continue;
                    }

                    // The valley floor is not one level: try the arrival's and the two below.
                    for (var z = _arrival.Z; z >= Math.Max(0, _arrival.Z - 2); z--)
                    {
                        if (AllFlat(map, plants, x0, y0, size, z))
                        {
                            return new TileCoord(x0, y0, z);
                        }
                    }
                }
            }
        }

        return null;
    }

    private static bool AllFlat(GameMap map, HashSet<TileCoord> plants, int x0, int y0, int size, int z)
    {
        for (var y = y0; y < y0 + size; y++)
        {
            for (var x = x0; x < x0 + size; x++)
            {
                if (!map.Size.Contains(x, y, z) || map.ShapeAt(x, y, z) != TileShape.Floor || !map.IsStandable(x, y, z) || plants.Contains(new TileCoord(x, y, z)))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
