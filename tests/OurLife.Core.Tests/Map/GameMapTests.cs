using OurLife.Core.Map;

namespace OurLife.Core.Tests.Map;

public class GameMapTests
{
    [Fact]
    public void Cell_indexes_and_coordinates_round_trip()
    {
        var size = new MapSize(5, 4, 3);

        for (var index = 0; index < size.CellCount; index++)
        {
            var cell = size.CoordOf(index);
            size.IndexOf(cell.X, cell.Y, cell.Z).ShouldBe(index);
        }

        size.CoordOf(size.IndexOf(4, 3, 2)).ShouldBe(new TileCoord(4, 3, 2));
    }

    [Fact]
    public void A_new_map_is_air_without_material()
    {
        var map = new GameMap(new MapSize(4, 4, 2), []);

        map.ShapeAt(3, 3, 1).ShouldBe(TileShape.Empty);
        map.MaterialIndexAt(3, 3, 1).ShouldBe(GameMap.NoMaterial);
        map.MaterialAt(3, 3, 1).ShouldBeNull();
        map.SurfaceZ(0, 0).ShouldBe(-1);
    }

    [Fact]
    public void Cells_outside_the_map_are_rejected_when_read_directly()
    {
        var map = new GameMap(new MapSize(4, 4, 2), []);

        Should.Throw<ArgumentOutOfRangeException>(() => map.ShapeAt(4, 0, 0));
        map.ShapeOrEmpty(4, 0, 0).ShouldBe(TileShape.Empty);
        map.ShapeOrEmpty(-1, 0, 0).ShouldBe(TileShape.Empty);
    }

    [Fact]
    public void An_unknown_material_index_is_rejected()
    {
        var map = new GameMap(new MapSize(4, 4, 2), []);

        Should.Throw<ArgumentOutOfRangeException>(() => map.SetTile(0, 0, 0, TileShape.Wall, 0, TileFlags.None));
    }

    [Fact]
    public void A_new_map_reports_every_chunk_once()
    {
        var map = new GameMap(new MapSize(40, 20, 3), []);
        var chunks = new List<ChunkKey>();

        map.DrainDirtyChunks(chunks);
        map.DrainDirtyChunks(chunks);

        chunks.Count.ShouldBe(3 * 2 * 3);
        chunks.Distinct().Count().ShouldBe(chunks.Count);
    }

    [Fact]
    public void Changing_a_cell_marks_its_chunk_and_those_of_its_neighbours()
    {
        var map = new GameMap(new MapSize(40, 40, 3), []);

        // All air: everything is discovered already, so only the change itself makes chunks dirty.
        map.ComputeDiscovered();
        map.DrainDirtyChunks([]);
        var chunks = new List<ChunkKey>();

        map.SetTile(16, 5, 1, TileShape.Floor, GameMap.NoMaterial, TileFlags.None);
        map.DrainDirtyChunks(chunks);

        chunks.ShouldBe(
            new[] { new ChunkKey(1, 0, 1), new ChunkKey(0, 0, 1), new ChunkKey(1, 0, 0), new ChunkKey(1, 0, 2) },
            ignoreOrder: true);
    }

    [Fact]
    public void The_surface_is_the_highest_cell_that_is_not_air()
    {
        var map = AsciiMap.Parse("#", ".", " ");

        map.SurfaceZ(0, 0).ShouldBe(1);
    }
}
