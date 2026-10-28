using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using Xunit.Abstractions;

namespace OurLife.Core.Tests.Colony;

/// <summary>The performance budget of rooms (criterion 15 of <c>docs/gdd/habitaciones.md</c>): 20 rooms, 15 colonists and the usual changes to the map.</summary>
[Collection(TimingCollection.Name)]
public class RoomBudgetTests(ITestOutputHelper output)
{
    private const int HousesAcross = 5;
    private const int HousesDown = 4;

    [Fact]
    public void Twenty_rooms_and_fifteen_colonists_with_changes_to_the_walls_take_less_than_a_tenth_of_a_millisecond_per_tick()
    {
        var village = TwentyHouses();
        village.Simulation.Commands.Enqueue(new SetFreeWillCommand(true));
        village.AddItems("provisions", 50, 20, 33, 1);
        village.Simulation.Run(600);
        var rock = (ushort)village.Simulation.Content.IndexOf<MaterialDef>("rock");

        // Ten stretches of half a game day; the cheapest counts, so a busy machine does not fail what the code does not cost.
        var tick = 0;
        var best = Timing.BestRoundMs(10, _ =>
        {
            for (var i = 0; i < 720; i++, tick++)
            {
                // Every two game hours a wall of some house falls and goes up again an hour later: the rooms are found again twice.
                if (tick % 120 is 0 or 60)
                {
                    var (x, y) = HouseOrigin((tick / 120) % (HousesAcross * HousesDown));
                    village.Map.SetTile(x, y + 2, 1, tick % 120 == 0 ? TileShape.Floor : TileShape.Wall, rock, TileFlags.None);
                }

                village.Simulation.Tick();
            }
        }) / 720;

        output.WriteLine($"{best:F4} ms per tick");
        best.ShouldBeLessThan(0.1);
        Rooms.All(village.Simulation).Count.ShouldBe(HousesAcross * HousesDown);
    }

    [Fact]
    public void Finding_twenty_rooms_again_after_a_change_takes_less_than_a_millisecond()
    {
        var village = TwentyHouses();
        Rooms.All(village.Simulation);
        var best = Timing.BestRoundMs(10, _ =>
        {
            for (var round = 0; round < 20; round++)
            {
                Rooms.Invalidate(village.Simulation);
                Rooms.All(village.Simulation).Count.ShouldBe(HousesAcross * HousesDown);
            }
        }) / 20;

        output.WriteLine($"{best:F4} ms per rebuild");
        best.ShouldBeLessThan(1.0);
    }

    [Fact]
    public void A_big_open_field_with_hundreds_of_pieces_of_furniture_does_not_search_for_each_one()
    {
        var village = new ColonyTestBed(RoomContent.Defs, 1, ColonyTestBed.Level(60, 60, '#'), ColonyTestBed.Level(60, 60, '.'), ColonyTestBed.Level(60, 60, '_'));
        for (var x = 0; x < 20; x++)
        {
            for (var y = 0; y < 15; y++)
            {
                var cell = new TileCoord(5 + (x * 2), 5 + (y * 2), 1);
                village.Simulation.Commands.Enqueue(new PlaceBlueprintCommand("chest", "timber", cell, cell));
                village.Simulation.ApplyCommandsNow();
            }
        }

        var blueprints = new List<EntityId>();
        village.World.Query<Blueprint, Position>(blueprints);
        foreach (var blueprint in blueprints)
        {
            Construction.Finish(village.Simulation, blueprint, village.World.Get<Position>(blueprint).Cell);
        }

        // The cheapest of several rounds counts, so a busy machine does not fail what the code does not cost.
        var best = Timing.BestRoundMs(8, _ =>
        {
            Rooms.Invalidate(village.Simulation);
            Rooms.All(village.Simulation).ShouldBeEmpty();
        });

        village.World.Store<Furniture>().Count.ShouldBe(300);
        output.WriteLine($"{best:F3} ms for 300 chests");
        best.ShouldBeLessThan(25);
    }

    private static (int X, int Y) HouseOrigin(int house) => (1 + (8 * (house % HousesAcross)), 1 + (8 * (house / HousesAcross)));

    private static ColonyTestBed TwentyHouses()
    {
        var village = new ColonyTestBed(RoomContent.Defs, 3, ColonyTestBed.Level(44, 40, '#'), ColonyTestBed.Level(44, 40, '.'), ColonyTestBed.Level(44, 40, '_'));
        var rock = (ushort)village.Simulation.Content.IndexOf<MaterialDef>("rock");
        for (var house = 0; house < HousesAcross * HousesDown; house++)
        {
            var (x0, y0) = HouseOrigin(house);
            for (var x = x0; x < x0 + 5; x++)
            {
                for (var y = y0; y < y0 + 5; y++)
                {
                    if (x == x0 || y == y0 || x == x0 + 4 || y == y0 + 4)
                    {
                        village.Map.SetTile(x, y, 1, TileShape.Wall, rock, TileFlags.None);
                    }
                }
            }

            village.Map.SetTile(x0 + 2, y0 + 4, 1, TileShape.Door, rock, TileFlags.None);
            Finish(village, "bed", x0 + 1, y0 + 1);
            Finish(village, "table", x0 + 2, y0 + 2);
            Finish(village, "chair", x0 + 1, y0 + 2);
        }

        for (var i = 0; i < 15; i++)
        {
            village.AddColonist(2 + (i * 2), 36, 1, name: $"Colonist {i}");
        }

        return village;
    }

    private static void Finish(ColonyTestBed village, string buildable, int x, int y)
    {
        var cell = new TileCoord(x, y, 1);
        village.Simulation.Commands.Enqueue(new PlaceBlueprintCommand(buildable, "timber", cell, cell));
        village.Simulation.ApplyCommandsNow();
        var blueprints = new List<EntityId>();
        village.World.Query<Blueprint, Position>(blueprints);
        Construction.Finish(village.Simulation, blueprints.Single(each => village.World.Get<Position>(each).Cell == cell), cell);
    }
}
