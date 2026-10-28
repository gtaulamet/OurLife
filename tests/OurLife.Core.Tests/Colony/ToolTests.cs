using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Save;
using OurLife.Core.Sim;
using OurLife.Core.Text;

namespace OurLife.Core.Tests.Colony;

/// <summary>
/// Tools and the equip job (<c>docs/gdd/oficios.md</c>, "Herramientas" and criterion 10): a colonist
/// without a tool for its work fetches a free one from a stockpile, keeps it, works faster with it
/// by the value of its quality, and drops it where it dies.
/// </summary>
public class ToolTests
{
    private const string Extras = """
        { "type": "Item", "id": "iron_axe", "category": "misc", "kind": "raw", "visual": "item.iron_axe", "stackLimit": 1,
          "tool": { "workType": "woodcutting", "speedPercent": 30 } },
        { "type": "Item", "id": "iron_pickaxe", "category": "misc", "kind": "raw", "visual": "item.iron_pickaxe", "stackLimit": 1,
          "tool": { "workType": "mining", "speedPercent": 30 } },
        { "type": "Item", "id": "iron_cart", "category": "misc", "kind": "raw", "visual": "item.iron_cart", "stackLimit": 1,
          "tool": { "workType": "hauling", "speedPercent": 30 } }
        """;

    private static readonly Lazy<DefDatabase> ContentLazy = new(() =>
        TestContent.WithDefs(string.Concat(ColonyTestBed.ContentJson.AsSpan(0, ColonyTestBed.ContentJson.LastIndexOf(']')), ",", Extras, "]")));

    private static DefDatabase Content => ContentLazy.Value;

    private static ColonyTestBed Village(ulong seed = 1)
    {
        var bed = ColonyTestBed.Flat(16, seed, Content);
        bed.Designate(DesignationKind.Stockpile, new TileCoord(1, 1, 1), new TileCoord(3, 3, 1));
        bed.Simulation.ApplyCommandsNow();
        return bed;
    }

    private static List<JobKind> JobsUntilFelled(ColonyTestBed bed, EntityId colonist, EntityId tree, int limit = 600)
    {
        var kinds = new List<JobKind>();
        for (var tick = 0; tick < limit && bed.World.IsAlive(tree); tick++)
        {
            bed.Simulation.Tick();
            if (bed.JobOf(colonist) is { } job && (kinds.Count == 0 || kinds[^1] != job.Kind))
            {
                kinds.Add(job.Kind);
            }
        }

        return kinds;
    }

    private static ItemStack Axe(string? quality = null) => new() { Def = "iron_axe", Count = 1, Quality = quality };

    [Fact]
    public void A_colonist_with_a_tree_to_fell_fetches_the_axe_of_the_stockpile_equips_it_and_fells_the_tree()
    {
        var bed = Village();
        ItemPiles.Drop(bed.Simulation, "iron_axe", 1, new TileCoord(2, 2, 1), "good");
        var tree = bed.AddPlant("tree", 9, 9, 1);
        bed.Designate(DesignationKind.Chop, new TileCoord(9, 9, 1), new TileCoord(9, 9, 1));
        var woodcutter = bed.AddColonist(6, 6, 1);

        var kinds = JobsUntilFelled(bed, woodcutter, tree);

        kinds.ShouldBe([JobKind.Equip, JobKind.Chop]);
        bed.World.IsAlive(tree).ShouldBeFalse();
        var tools = Tools.Of(bed.Simulation, woodcutter);
        tools.Select(tool => (tool.Def, tool.Count, tool.Quality)).ShouldBe([("iron_axe", 1, "good")]);

        // An equipped tool is out of the world: no pile, and not in the stocks.
        bed.CountItems("iron_axe").ShouldBe(0);
        Stocks.Count(bed.Simulation, "iron_axe").ShouldBe(0);
        Tools.SpeedPercent(bed.Simulation, woodcutter, "woodcutting").ShouldBe(36);
        Tools.SpeedPercent(bed.Simulation, woodcutter, "mining").ShouldBe(0);
    }

    [Fact]
    public void A_colonist_who_has_the_tool_for_its_work_does_not_fetch_another()
    {
        var bed = Village();
        ItemPiles.Drop(bed.Simulation, "iron_axe", 1, new TileCoord(2, 2, 1));
        bed.AddPlant("tree", 9, 9, 1);
        bed.Designate(DesignationKind.Chop, new TileCoord(9, 9, 1), new TileCoord(9, 9, 1));
        var woodcutter = bed.AddColonist(6, 6, 1);
        bed.World.Add(woodcutter, new Equipment { Tools = [Axe("masterwork")] });

        bed.Simulation.Run(400);

        bed.CountItems("iron_axe").ShouldBe(1);
        Tools.Of(bed.Simulation, woodcutter).Single().Quality.ShouldBe("masterwork");
    }

    [Fact]
    public void A_tool_is_only_fetched_for_the_work_it_speeds_up()
    {
        var bed = Village();
        ItemPiles.Drop(bed.Simulation, "iron_pickaxe", 1, new TileCoord(2, 2, 1));
        bed.AddPlant("tree", 9, 9, 1);
        bed.Designate(DesignationKind.Chop, new TileCoord(9, 9, 1), new TileCoord(9, 9, 1));
        var woodcutter = bed.AddColonist(6, 6, 1);

        bed.Simulation.Run(400);

        Tools.Of(bed.Simulation, woodcutter).ShouldBeEmpty();
        bed.CountItems("iron_pickaxe").ShouldBe(1);
    }

    [Fact]
    public void A_miner_fetches_the_pickaxe()
    {
        var floor = ColonyTestBed.Level(16, 16, '.').ToCharArray();
        floor[(9 * 17) + 9] = '#';
        var bed = new ColonyTestBed(Content, 1, ColonyTestBed.Level(16, 16, '#'), new string(floor));
        bed.Designate(DesignationKind.Stockpile, new TileCoord(1, 1, 1), new TileCoord(3, 3, 1));
        bed.Simulation.ApplyCommandsNow();
        ItemPiles.Drop(bed.Simulation, "iron_pickaxe", 1, new TileCoord(2, 2, 1));
        var miner = bed.AddColonist(6, 6, 1);
        bed.Designate(DesignationKind.Mine, new TileCoord(9, 9, 1), new TileCoord(9, 9, 1));

        bed.Simulation.Run(200);

        Tools.Of(bed.Simulation, miner).Select(tool => tool.Def).ShouldBe(["iron_pickaxe"]);
    }

    [Fact]
    public void Of_two_axes_the_best_one_is_fetched_and_each_axe_goes_to_only_one_of_two_woodcutters()
    {
        var bed = Village();
        ItemPiles.Drop(bed.Simulation, "iron_axe", 1, new TileCoord(2, 2, 1), "poor");
        ItemPiles.Drop(bed.Simulation, "iron_axe", 1, new TileCoord(3, 3, 1), "masterwork");
        bed.AddPlant("tree", 9, 9, 1);
        bed.AddPlant("tree", 11, 9, 1);
        bed.Designate(DesignationKind.Chop, new TileCoord(9, 9, 1), new TileCoord(11, 9, 1));
        var first = bed.AddColonist(6, 6, 1, name: "First");
        var second = bed.AddColonist(6, 7, 1, name: "Second");

        bed.Simulation.Run(300);

        // The masterwork axe goes to the colonist that decides first, then the poor one to the other: never the same pile twice.
        Tools.Of(bed.Simulation, first).Select(tool => tool.Quality).ShouldBe(["masterwork"]);
        Tools.Of(bed.Simulation, second).Select(tool => tool.Quality).ShouldBe(["poor"]);
        bed.CountItems("iron_axe").ShouldBe(0);
    }

    [Fact]
    public void A_tool_lying_outside_a_stockpile_is_not_fetched()
    {
        var bed = Village();
        ItemPiles.Drop(bed.Simulation, "iron_axe", 1, new TileCoord(12, 12, 1));
        var tree = bed.AddPlant("tree", 9, 9, 1);
        bed.Designate(DesignationKind.Chop, new TileCoord(9, 9, 1), new TileCoord(9, 9, 1));
        var woodcutter = bed.AddColonist(6, 6, 1);

        var kinds = JobsUntilFelled(bed, woodcutter, tree);

        kinds.ShouldNotContain(JobKind.Equip);
        Tools.Of(bed.Simulation, woodcutter).ShouldBeEmpty();
    }

    [Fact]
    public void A_tool_beyond_the_radius_of_the_village_is_left_alone()
    {
        var bed = ColonyTestBed.Flat(48, seed: 1, Content);
        bed.Designate(DesignationKind.Stockpile, new TileCoord(44, 44, 1), new TileCoord(46, 46, 1));
        bed.Simulation.ApplyCommandsNow();
        ItemPiles.Drop(bed.Simulation, "iron_axe", 1, new TileCoord(45, 45, 1));
        bed.AddPlant("tree", 5, 5, 1);
        bed.Designate(DesignationKind.Chop, new TileCoord(5, 5, 1), new TileCoord(5, 5, 1));
        var woodcutter = bed.AddColonist(2, 2, 1);

        bed.Simulation.Run(300);

        Tools.Of(bed.Simulation, woodcutter).ShouldBeEmpty();
        bed.CountItems("iron_axe").ShouldBe(1);
    }

    [Fact]
    public void A_colonist_who_hauls_does_not_give_the_haul_up_to_fetch_a_tool_that_would_not_speed_it_up()
    {
        var bed = Village();
        ItemPiles.Drop(bed.Simulation, "iron_cart", 1, new TileCoord(2, 2, 1));
        bed.AddItems("log", 1, 12, 12, 1);
        var hauler = bed.AddColonist(6, 6, 1);
        var kinds = new List<JobKind>();
        for (var tick = 0; tick < 300; tick++)
        {
            bed.Simulation.Tick();
            if (bed.JobOf(hauler) is { } job && (kinds.Count == 0 || kinds[^1] != job.Kind))
            {
                kinds.Add(job.Kind);
            }
        }

        kinds.ShouldContain(JobKind.Haul);
        kinds.ShouldNotContain(JobKind.Equip);
        Tools.Of(bed.Simulation, hauler).ShouldBeEmpty();
    }

    [Fact]
    public void A_colonist_on_its_way_to_a_tool_gives_up_when_it_may_no_longer_do_that_work()
    {
        var bed = Village();
        ItemPiles.Drop(bed.Simulation, "iron_axe", 1, new TileCoord(2, 2, 1));
        bed.AddPlant("tree", 9, 9, 1);
        bed.Designate(DesignationKind.Chop, new TileCoord(9, 9, 1), new TileCoord(9, 9, 1));
        var woodcutter = bed.AddColonist(6, 6, 1);
        for (var tick = 0; tick < 100 && bed.JobOf(woodcutter)?.Kind != JobKind.Equip; tick++)
        {
            bed.Simulation.Tick();
        }

        bed.JobOf(woodcutter)?.Kind.ShouldBe(JobKind.Equip);
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(woodcutter, "woodcutting", 0));
        bed.Simulation.ApplyCommandsNow();

        bed.Simulation.Run(200);

        Tools.Of(bed.Simulation, woodcutter).ShouldBeEmpty();
        bed.CountItems("iron_axe").ShouldBe(1);
    }

    [Fact]
    public void A_better_quality_tool_adds_more_speed_to_the_work_and_fells_the_tree_in_fewer_ticks()
    {
        var rules = ColonyRules.From(Content);
        var woodcutting = Content.Get<WorkTypeDef>("woodcutting");
        var speeds = new Dictionary<string, int>();
        var ticks = new Dictionary<string, int>();
        foreach (var quality in new string?[] { null, "poor", "normal", "good", "masterwork" })
        {
            var bed = Village();
            var tree = bed.AddPlant("tree", 8, 8, 1);
            bed.Designate(DesignationKind.Chop, new TileCoord(8, 8, 1), new TileCoord(8, 8, 1));
            var woodcutter = bed.AddColonist(7, 7, 1);
            bed.World.Add(woodcutter, new Skills { Values = [new SkillValue { WorkType = "woodcutting", Level = 0 }] });
            var key = quality ?? "none";
            if (quality is not null)
            {
                bed.World.Add(woodcutter, new Equipment { Tools = [Axe(quality)] });
            }

            speeds[key] = WorkSpeed.PercentOf(bed.Simulation, woodcutter, woodcutting, rules.Skills);
            var tick = 0;
            for (; tick < 600 && bed.World.IsAlive(tree); tick++)
            {
                bed.Simulation.Tick();
            }

            ticks[key] = tick;
        }

        // 30 percent of speed for a tool of the default quality, times the value of each quality: 22, 30, 36 and 60.
        speeds["none"].ShouldBe(80);
        speeds["poor"].ShouldBe(80 * 122 / 100);
        speeds["normal"].ShouldBe(80 * 130 / 100);
        speeds["good"].ShouldBe(80 * 136 / 100);
        speeds["masterwork"].ShouldBe(80 * 160 / 100);
        ticks["masterwork"].ShouldBeLessThan(ticks["good"]);
        ticks["good"].ShouldBeLessThan(ticks["normal"]);
        ticks["normal"].ShouldBeLessThan(ticks["poor"]);
        ticks["poor"].ShouldBeLessThan(ticks["none"]);
    }

    [Fact]
    public void A_tool_does_not_speed_up_other_work()
    {
        var bed = Village();
        var worker = bed.AddColonist(7, 7, 1);
        var rules = ColonyRules.From(Content);
        var mining = Content.Get<WorkTypeDef>("mining");
        var before = WorkSpeed.PercentOf(bed.Simulation, worker, mining, rules.Skills);

        bed.World.Add(worker, new Equipment { Tools = [Axe("masterwork")] });

        WorkSpeed.PercentOf(bed.Simulation, worker, mining, rules.Skills).ShouldBe(before);
        WorkSpeed.PercentOf(bed.Simulation, worker, null, rules.Skills).ShouldBe(before);
    }

    [Fact]
    public void A_colonist_who_dies_leaves_its_tools_with_their_quality_on_its_cell()
    {
        var bed = Village();
        var worker = bed.AddColonist(7, 7, 1);
        bed.World.Add(worker, new Equipment { Tools = [Axe("excellent")] });

        ColonistDeath.Die(bed.Simulation, worker, LocalizedText.Of("event.need.hunger.death").With("colonist", worker));

        bed.World.IsAlive(worker).ShouldBeFalse();
        var piles = new List<EntityId>();
        bed.World.Query<ItemStack, Position>(piles);
        var axe = piles.Single(pile => bed.World.Get<ItemStack>(pile).Def == "iron_axe");
        bed.World.Get<Position>(axe).Cell.ShouldBe(new TileCoord(7, 7, 1));
        bed.World.Get<ItemStack>(axe).Quality.ShouldBe("excellent");
    }

    [Fact]
    public void The_same_seed_equips_the_same_way_and_saving_midway_gives_the_same_game_as_never_stopping()
    {
        ColonyTestBed Play()
        {
            var bed = Village(seed: 9);
            ItemPiles.Drop(bed.Simulation, "iron_axe", 1, new TileCoord(2, 2, 1), "good");
            ItemPiles.Drop(bed.Simulation, "iron_pickaxe", 1, new TileCoord(3, 2, 1));
            bed.AddPlant("tree", 9, 9, 1);
            bed.Designate(DesignationKind.Chop, new TileCoord(9, 9, 1), new TileCoord(9, 9, 1));
            bed.AddColonist(6, 6, 1, name: "First");
            bed.AddColonist(7, 6, 1, name: "Second");
            return bed;
        }

        var uninterrupted = Play();
        var again = Play();
        uninterrupted.Simulation.Run(900);
        again.Simulation.Run(900);
        again.Simulation.ComputeStateHash().ShouldBe(uninterrupted.Simulation.ComputeStateHash());

        // Saved with a colonist on its way to the axe, and carried on after loading.
        var halfway = Play();
        var equipping = false;
        for (var tick = 0; tick < 600 && !equipping; tick++)
        {
            halfway.Simulation.Tick();
            equipping = halfway.Colonists().Any(colonist => halfway.JobOf(colonist)?.Kind == JobKind.Equip);
        }

        equipping.ShouldBeTrue();
        var loaded = SaveAndLoad(halfway.Simulation);
        loaded.ComputeStateHash().ShouldBe(halfway.Simulation.ComputeStateHash());
        halfway.Simulation.Run(600);
        loaded.Run(600);
        loaded.ComputeStateHash().ShouldBe(halfway.Simulation.ComputeStateHash());
        var equipped = new List<EntityId>();
        loaded.World.Query<Equipment>(equipped);
        equipped.ShouldNotBeEmpty();
        loaded.World.Get<Equipment>(equipped[0]).Tools.ShouldNotBeEmpty();

        // And with the tool in hand.
        var saved = SaveAndLoad(halfway.Simulation);
        saved.ComputeStateHash().ShouldBe(halfway.Simulation.ComputeStateHash());
    }

    private static Simulation SaveAndLoad(Simulation simulation)
    {
        var output = new MemoryStream();
        SaveGame.Write(output, simulation, new SaveInfo("game-1", IsAutosave: false, "0.4.0-dev", DateTimeOffset.UnixEpoch));
        var loaded = SaveGame.Load(new MemoryStream(output.ToArray()), simulation.Content);
        ColonySystems.AddTo(loaded);
        return loaded;
    }
}
