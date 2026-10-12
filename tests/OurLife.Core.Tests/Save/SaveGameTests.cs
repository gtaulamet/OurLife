using System.IO.Compression;
using System.Text;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Save;
using OurLife.Core.Sim;
using OurLife.Core.Tests.Colony;

namespace OurLife.Core.Tests.Save;

/// <summary>Saving and loading (ADR-010, <c>docs/gdd/guardado.md</c>) on the colony test bed.</summary>
public class SaveGameTests
{
    private static readonly SaveInfo Info = new("game-1", IsAutosave: false, "0.2.0-dev", new DateTimeOffset(2026, 10, 11, 18, 30, 0, TimeSpan.Zero));

    [Fact]
    public void Saving_halfway_and_going_on_gives_the_same_state_as_never_stopping()
    {
        var uninterrupted = BusyColony();
        RunUntilEverythingIsInPlay(uninterrupted);
        uninterrupted.Designate(DesignationKind.Stairs, new TileCoord(12, 8, 1), new TileCoord(12, 8, 1));
        var saved = Save(uninterrupted.Simulation);

        var loaded = Load(saved);
        loaded.Commands.Count.ShouldBe(1);
        loaded.ComputeStateHash().ShouldBe(uninterrupted.Simulation.ComputeStateHash());

        uninterrupted.Simulation.Run(3_000);
        loaded.Run(3_000);
        loaded.ComputeStateHash().ShouldBe(uninterrupted.Simulation.ComputeStateHash());
    }

    [Fact]
    public void The_header_describes_the_game_without_reading_the_state()
    {
        var bed = BusyColony();
        bed.Simulation.Run(100);

        var header = SaveGame.ReadHeader(new MemoryStream(Save(bed.Simulation)));

        header.FormatVersion.ShouldBe(SaveGame.FormatVersion);
        header.GameId.ShouldBe("game-1");
        header.IsAutosave.ShouldBeFalse();
        header.GameVersion.ShouldBe("0.2.0-dev");
        header.SavedAt.ShouldBe(Info.SavedAt);
        header.ColonyName.ShouldBe("Valdeolmos");
        header.Tick.ShouldBe(101);
        header.Era.ShouldBe(bed.Simulation.Progress.CurrentEra);
        header.Colonists.ShouldBe(5);
    }

    [Fact]
    public void Loading_keeps_the_colony_name_and_the_tick()
    {
        var bed = BusyColony();
        bed.Simulation.Run(10);

        var loaded = Load(Save(bed.Simulation));

        loaded.ColonyName.ShouldBe("Valdeolmos");
        loaded.CurrentTick.ShouldBe(11);
    }

    [Fact]
    public void A_newer_format_is_refused()
    {
        var file = Rewrite(Save(BusyColony().Simulation), "header.json", json => json.Replace(
            $"\"FormatVersion\":{SaveGame.FormatVersion}", $"\"FormatVersion\":{SaveGame.FormatVersion + 1}", StringComparison.Ordinal));

        LoadFails(file).Reason.ShouldBe(SaveLoadError.Newer);
    }

    [Fact]
    public void An_older_format_is_refused()
    {
        var file = Rewrite(Save(BusyColony().Simulation), "header.json", json => json.Replace(
            $"\"FormatVersion\":{SaveGame.FormatVersion}", "\"FormatVersion\":0", StringComparison.Ordinal));

        LoadFails(file).Reason.ShouldBe(SaveLoadError.Older);
    }

    [Fact]
    public void A_newer_format_with_new_header_fields_is_still_newer()
    {
        var file = Rewrite(Save(BusyColony().Simulation), "header.json", json => json.Replace(
            $"\"FormatVersion\":{SaveGame.FormatVersion}", $"\"Thumbnail\":\"abc\",\"FormatVersion\":{SaveGame.FormatVersion + 1}", StringComparison.Ordinal));

        LoadFails(file).Reason.ShouldBe(SaveLoadError.Newer);
        SaveGame.ReadHeader(new MemoryStream(file)).FormatVersion.ShouldBe(SaveGame.FormatVersion + 1);
    }

    [Fact]
    public void A_null_list_in_the_state_is_corrupt()
    {
        var file = Rewrite(Save(BusyColony().Simulation), "state.json", json => json.Replace("\"Knowledge\":[]", "\"Knowledge\":null", StringComparison.Ordinal));

        LoadFails(file).Reason.ShouldBe(SaveLoadError.Corrupt);
    }

    [Fact]
    public void A_null_list_in_a_component_is_corrupt()
    {
        var file = Rewrite(Save(BusyColony().Simulation), "state.json", json => json.Replace("\"Unreachable\":[]", "\"Unreachable\":null", StringComparison.Ordinal));

        var failure = LoadFails(file);

        failure.Reason.ShouldBe(SaveLoadError.Corrupt);
        failure.Detail.ShouldContain("Unreachable");
    }

    [Fact]
    public void An_entity_outside_the_map_is_corrupt()
    {
        var file = Rewrite(Save(BusyColony().Simulation), "state.json", json => json.Replace("\"Z\":1}", "\"Z\":99}", StringComparison.Ordinal));

        LoadFails(file).Detail.ShouldContain("outside the map");
    }

    [Fact]
    public void A_header_with_a_null_text_is_corrupt()
    {
        var file = Rewrite(Save(BusyColony().Simulation), "header.json", json => json.Replace("\"GameId\":\"game-1\"", "\"GameId\":null", StringComparison.Ordinal));

        Should.Throw<SaveLoadException>(() => SaveGame.ReadHeader(new MemoryStream(file))).Reason.ShouldBe(SaveLoadError.Corrupt);
        LoadFails(file).Reason.ShouldBe(SaveLoadError.Corrupt);
    }

    [Fact]
    public void An_enum_saved_as_a_number_is_corrupt()
    {
        var file = Rewrite(Save(BusyColony().Simulation), "state.json", json => json.Replace("\"Gender\":\"Female\"", "\"Gender\":99", StringComparison.Ordinal));

        LoadFails(file).Reason.ShouldBe(SaveLoadError.Corrupt);
    }

    [Fact]
    public void A_job_cell_outside_the_map_is_corrupt()
    {
        var bed = BusyColony();
        bed.Simulation.Run(20);
        var file = Rewrite(Save(bed.Simulation), "state.json", json => json.Replace("\"Cell\":{\"X\":", "\"Cell\":{\"X\":9", StringComparison.Ordinal));

        LoadFails(file).Detail.ShouldContain("outside the map");
    }

    [Fact]
    public void Loading_also_gives_the_header()
    {
        var bed = BusyColony();

        SaveGame.Load(new MemoryStream(Save(bed.Simulation)), ColonyTestBed.Content, out var header);

        header.ColonyName.ShouldBe("Valdeolmos");
        header.Tick.ShouldBe(1);
    }

    [Fact]
    public void A_null_item_in_a_list_is_corrupt()
    {
        var file = Rewrite(Save(BusyColony().Simulation), "state.json", json => json.Replace("\"Values\":[", "\"Values\":[null,", StringComparison.Ordinal));

        LoadFails(file).Detail.ShouldContain("holds a null");
    }

    [Fact]
    public void A_step_beyond_the_path_is_corrupt()
    {
        var file = Rewrite(Save(BusyColony().Simulation), "state.json", json => json.Replace("\"Next\":0", "\"Next\":99", StringComparison.Ordinal));

        LoadFails(file).Detail.ShouldContain("walks step 99");
    }

    [Fact]
    public void A_huge_map_size_is_corrupt_without_allocating_it()
    {
        var file = Rewrite(Save(BusyColony().Simulation), "state.json", json => json.Replace("\"Width\":16", "\"Width\":40000", StringComparison.Ordinal));

        LoadFails(file).Detail.ShouldContain("do not match its size");
    }

    [Fact]
    public void A_truncated_file_is_corrupt()
    {
        var file = Save(BusyColony().Simulation);

        LoadFails(file[..(file.Length / 2)]).Reason.ShouldBe(SaveLoadError.Corrupt);
        Should.Throw<SaveLoadException>(() => SaveGame.ReadHeader(new MemoryStream(file[..10]))).Reason.ShouldBe(SaveLoadError.Corrupt);
    }

    [Fact]
    public void A_file_that_is_not_a_saved_game_is_corrupt()
    {
        LoadFails(Encoding.UTF8.GetBytes("not a saved game")).Reason.ShouldBe(SaveLoadError.Corrupt);
    }

    [Fact]
    public void A_damaged_state_is_corrupt()
    {
        var file = Rewrite(Save(BusyColony().Simulation), "state.json", json => json.Replace("\"Tick\"", "\"Tock\"", StringComparison.Ordinal));

        LoadFails(file).Reason.ShouldBe(SaveLoadError.Corrupt);
    }

    [Fact]
    public void A_component_of_a_dead_entity_is_corrupt()
    {
        var file = Rewrite(Save(BusyColony().Simulation), "state.json", json => json.Replace(
            "\"Index\":0,\"Generation\":1", "\"Index\":0,\"Generation\":7", StringComparison.Ordinal));

        LoadFails(file).Reason.ShouldBe(SaveLoadError.Corrupt);
    }

    [Fact]
    public void A_plant_the_content_lacks_is_named()
    {
        var file = Rewrite(Save(BusyColony().Simulation), "state.json", json => json.Replace(
            "\"Def\":\"bush\"", "\"Def\":\"ghost_bush\"", StringComparison.Ordinal));

        var failure = LoadFails(file);

        failure.Reason.ShouldBe(SaveLoadError.MissingContent);
        failure.Detail.ShouldBe("ghost_bush");
    }

    [Fact]
    public void A_need_inside_a_list_the_content_lacks_is_named()
    {
        var file = Rewrite(Save(BusyColony().Simulation), "state.json", json => json.Replace(
            "\"Need\":\"rest\"", "\"Need\":\"ghost_need\"", StringComparison.Ordinal));

        LoadFails(file).Detail.ShouldBe("ghost_need");
    }

    [Fact]
    public void A_stockpile_category_the_content_lacks_is_named()
    {
        var file = Rewrite(Save(BusyColony().Simulation), "state.json", json => json.Replace(
            "\"Categories\":[\"food\"", "\"Categories\":[\"ghost_category\"", StringComparison.Ordinal));

        LoadFails(file).Detail.ShouldBe("ghost_category");
    }

    [Fact]
    public void A_material_the_content_lacks_is_named()
    {
        var file = Rewrite(Save(BusyColony().Simulation), "state.json", json => json.Replace(
            "\"Materials\":[\"masonry\",\"rock\"", "\"Materials\":[\"masonry\",\"ghost_rock\"", StringComparison.Ordinal));

        LoadFails(file).Detail.ShouldBe("ghost_rock");
    }

    [Fact]
    public void Saving_an_unregistered_component_fails_before_writing()
    {
        var bed = BusyColony();
        bed.World.Add(bed.World.CreateEntity(), new Unregistered());
        var output = new MemoryStream();

        Should.Throw<InvalidOperationException>(() => SaveGame.Write(output, bed.Simulation, Info));
        output.Length.ShouldBe(0);
    }

    [Fact]
    public void Every_command_of_the_core_is_registered()
    {
        var commands = typeof(Simulation).Assembly.GetTypes()
            .Where(type => typeof(ISimCommand).IsAssignableFrom(type) && type is { IsInterface: false, IsAbstract: false });

        SaveTypes.CommandTypes.ShouldBe(commands, ignoreOrder: true);
    }

    [Fact]
    public void Saved_types_are_registered_once()
    {
        var types = SaveTypes.ComponentTypes.Concat(SaveTypes.CommandTypes).ToList();

        types.Distinct().Count().ShouldBe(types.Count);
    }

    /// <summary>
    /// Five colonists with rock to dig, trees to chop and bushes to harvest, all ordered, plus an order nobody can reach. One
    /// tick has run.
    /// </summary>
    private static ColonyTestBed BusyColony()
    {
        var floor = ColonyTestBed.Level(16, 16, '.').ToCharArray();
        for (var y = 2; y < 10; y++)
        {
            for (var x = 2; x < 8; x++)
            {
                floor[(y * 17) + x] = '#';
            }
        }

        var bed = new ColonyTestBed(seed: 5, ColonyTestBed.Level(16, 16, '#'), new string(floor));
        ColonyStart.Settle(bed.Simulation, ColonyStart.TheOnlyScenario(bed.Simulation.Content), "Valdeolmos");
        for (var x = 9; x < 15; x += 2)
        {
            bed.AddPlant("tree", x, 3, 1);
            bed.AddPlant("bush", x, 12, 1);
        }

        bed.Designate(DesignationKind.Mine, new TileCoord(2, 2, 1), new TileCoord(7, 9, 1));
        bed.Designate(DesignationKind.Chop, new TileCoord(0, 0, 1), new TileCoord(15, 5, 1));
        bed.Designate(DesignationKind.Harvest, new TileCoord(0, 10, 1), new TileCoord(15, 15, 1));

        // Buried rock that nobody can reach: the order stays, and colonists remember it as unreachable.
        bed.Designate(DesignationKind.Mine, new TileCoord(14, 14, 0), new TileCoord(15, 15, 0));

        // A stockpile for wood and stone, so colonists haul what they dig and chop.
        bed.Designate(DesignationKind.Stockpile, new TileCoord(9, 14, 1), new TileCoord(12, 15, 1));

        // Walls to build from the logs, a channel to dig and a timber wall to take down.
        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand("wall", "timber", new TileCoord(13, 7, 1), new TileCoord(13, 9, 1)));
        bed.Designate(DesignationKind.Channel, new TileCoord(0, 14, 1), new TileCoord(1, 15, 1));
        bed.Map.SetTile(15, 0, 1, TileShape.Wall, (ushort)bed.Simulation.Content.IndexOf<MaterialDef>("timber"), TileFlags.None);
        bed.Designate(DesignationKind.Deconstruct, new TileCoord(15, 0, 1), new TileCoord(15, 0, 1));
        bed.Simulation.Run(1);
        var zones = new List<EntityId>();
        bed.World.Query<Stockpile>(zones);
        bed.Simulation.Commands.Enqueue(new SetStockpileFilterCommand(zones[0], "food", Allowed: false));
        return bed;
    }

    /// <summary>
    /// Runs until the colony has destroyed entities (free slots), colonists at work, harvested
    /// bushes, orders left and a colonist carrying a load: the continuity test proves little without all of them.
    /// </summary>
    private static void RunUntilEverythingIsInPlay(ColonyTestBed bed)
    {
        bool InPlay() =>
            bed.World.Entities.FreeSlotCount > 0
            && bed.World.Store<Job>().Count > 0
            && bed.World.Store<Harvested>().Count > 0
            && bed.Map.OrderedCells.Count > 0
            && bed.World.Store<Carried>().Count > 0;

        for (var tick = 0; tick < 10_000 && !InPlay(); tick++)
        {
            bed.Simulation.Tick();
        }

        InPlay().ShouldBeTrue("the busy colony never had everything in play at once");
    }

    private static byte[] Save(Simulation simulation)
    {
        var output = new MemoryStream();
        SaveGame.Write(output, simulation, Info);
        return output.ToArray();
    }

    private static Simulation Load(byte[] file)
    {
        var simulation = SaveGame.Load(new MemoryStream(file), ColonyTestBed.Content);
        ColonySystems.AddTo(simulation);
        return simulation;
    }

    private static SaveLoadException LoadFails(byte[] file) =>
        Should.Throw<SaveLoadException>(() => SaveGame.Load(new MemoryStream(file), ColonyTestBed.Content));

    /// <summary>Changes the text of one entry of a saved game.</summary>
    private static byte[] Rewrite(byte[] file, string entryName, Func<string, string> change)
    {
        var stream = new MemoryStream();
        stream.Write(file);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry(entryName)!;
            string text;
            using (var reader = new StreamReader(entry.Open()))
            {
                text = reader.ReadToEnd();
            }

            var changed = change(text);
            changed.ShouldNotBe(text, "the test must really change the file");
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
            writer.Write(changed);
        }

        return stream.ToArray();
    }

    private sealed class Unregistered
    {
    }
}
