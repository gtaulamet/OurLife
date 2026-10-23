using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Save;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Colony;

/// <summary>
/// Knowledge, research and the scriptorium (<c>docs/gdd/conocimientos.md</c>, criteria 1 to 7).
/// The test content has a small tree: <c>stonecutting</c> (120 points, practiced by mining),
/// <c>agriculture</c> (120, practiced by gathering), <c>cereals</c> (which needs agriculture) and
/// <c>carpentry</c>, which the village knows from the start.
/// </summary>
public class ResearchTests
{
    private const string Extras = """
        { "type": "WorkType", "id": "carpentry", "order": 5, "jobs": [], "workshop": true, "skill": true },
        { "type": "WorkType", "id": "research", "order": 6, "jobs": [], "workshop": true, "light": true, "skill": true },
        { "type": "KnowledgeConfig", "id": "knowledge", "practiceCapPercent": 60, "researchChunkTicks": 20, "queueMax": 3, "workType": "research" },
        { "type": "Knowledge", "id": "carpentry", "branch": "crafts", "cost": 0 },
        { "type": "Knowledge", "id": "stonecutting", "branch": "crafts", "cost": 120, "practice": { "workType": "mining", "centipointsPerTick": 50 } },
        { "type": "Knowledge", "id": "agriculture", "branch": "field", "cost": 120, "practice": { "workType": "gathering", "centipointsPerTick": 80 } },
        { "type": "Knowledge", "id": "cereals", "requires": ["agriculture"], "branch": "field", "tier": 2, "cost": 120,
          "practice": { "workType": "gathering", "centipointsPerTick": 80 } },
        { "type": "Knowledge", "id": "silviculture", "requires": ["agriculture"], "branch": "field", "tier": 2, "cost": 120 },
        { "type": "Buildable", "id": "scriptorium", "shape": "furniture", "materials": ["timber"], "cost": 2, "workTicks": 20,
          "visual": "furniture.scriptorium", "workstation": { "workType": "research" } },
        { "type": "Buildable", "id": "mason_bench", "requires": ["stonecutting"], "shape": "furniture", "materials": ["timber"], "cost": 2, "workTicks": 20,
          "visual": "furniture.mason_bench", "workstation": { "workType": "carpentry" } }
        """;

    private static readonly Lazy<DefDatabase> ContentLazy = new(() =>
        TestContent.WithDefs(string.Concat(ColonyTestBed.ContentJson.AsSpan(0, ColonyTestBed.ContentJson.LastIndexOf(']')), ",", Extras, "]")));

    private static DefDatabase Content => ContentLazy.Value;

    private static ColonyTestBed Village()
    {
        var bed = ColonyTestBed.Flat(16, 1, Content);
        bed.Simulation.Progress.Learn("carpentry");
        return bed;
    }

    private static EntityId Scriptorium(ColonyTestBed bed, int x, int y)
    {
        var cell = new TileCoord(x, y, 1);
        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand("scriptorium", "timber", cell, cell));
        bed.Simulation.ApplyCommandsNow();
        var blueprints = new List<EntityId>();
        bed.World.Query<Blueprint, Position>(blueprints);
        Construction.Finish(bed.Simulation, blueprints.Single(entity => bed.World.Get<Position>(entity).Cell == cell), cell);
        var shops = new List<EntityId>();
        bed.World.Query<Workshop, Position>(shops);
        return shops.Single(entity => bed.World.Get<Position>(entity).Cell == cell);
    }

    private static EntityId Researcher(ColonyTestBed bed, int x, int y, string name, int level)
    {
        var colonist = bed.AddColonist(x, y, 1, name: name);
        bed.World.Add(colonist, new Skills { Values = [new SkillValue { WorkType = "research", Level = level }] });
        return colonist;
    }

    private static void Queue(ColonyTestBed bed, params string[] knowledge)
    {
        bed.Simulation.Commands.Enqueue(new SetResearchQueueCommand(knowledge));
        bed.Simulation.ApplyCommandsNow();
    }

    /// <summary>Runs until the knowledge is learned, giving the ticks it took; the limit if never.</summary>
    private static int TicksToLearn(ColonyTestBed bed, string knowledge, int limit = 1_000)
    {
        for (var tick = 1; tick <= limit; tick++)
        {
            bed.Simulation.Run(1);
            if (bed.Simulation.Progress.Knowledge.Contains(knowledge))
            {
                return tick;
            }
        }

        return limit;
    }

    [Fact]
    public void A_researcher_at_a_scriptorium_learns_the_first_of_the_queue_which_leaves_it_and_says_so()
    {
        var bed = Village();
        Scriptorium(bed, 8, 8);
        Researcher(bed, 6, 6, "Scribe", 10);
        Queue(bed, "stonecutting", "agriculture");
        var events = new List<ISimEvent>();

        var ticks = TicksToLearn(bed, "stonecutting");

        // 120 points at 100 % are 120 ticks of work, and the walk to the desk.
        ticks.ShouldBeInRange(120, 150);
        bed.Simulation.Progress.ResearchQueue.ShouldBe(["agriculture"]);
        bed.Simulation.Progress.Points.ContainsKey("stonecutting").ShouldBeFalse();
        bed.Simulation.Events.DrainTo(events);
        events.OfType<KnowledgeLearnedEvent>().Select(learned => learned.Knowledge).ShouldBe(["stonecutting"]);
        events.OfType<NotificationEvent>().ShouldContain(notice => notice.Text.Key == ResearchRules.LearnedKey);
        ResearchRules.State(bed.Simulation, "stonecutting").ShouldBe(KnowledgeState.Learned);
        ResearchRules.State(bed.Simulation, "agriculture").ShouldBe(KnowledgeState.Queued);
    }

    [Fact]
    public void A_researcher_without_skill_is_slower_than_an_expert()
    {
        var slow = Village();
        Scriptorium(slow, 8, 8);
        Researcher(slow, 6, 6, "Novice", 0);
        Queue(slow, "stonecutting");
        var fast = Village();
        Scriptorium(fast, 8, 8);
        Researcher(fast, 6, 6, "Expert", 20);
        Queue(fast, "stonecutting");

        var novice = TicksToLearn(slow, "stonecutting");
        var expert = TicksToLearn(fast, "stonecutting");

        // 120 points at 80 % and at 120 %: 150 and 100 ticks of work.
        novice.ShouldBeInRange(150, 180);
        expert.ShouldBeInRange(100, 130);
    }

    [Fact]
    public void Practice_adds_up_to_its_share_of_the_cost_and_only_to_what_is_available()
    {
        var bed = Village();
        var rules = ColonyRules.From(Content);
        var config = rules.Knowledge!;

        for (var tick = 0; tick < 1_000; tick++)
        {
            ResearchRules.AddPractice(bed.Simulation, rules.PracticedBy("mining"), config);
            ResearchRules.AddPractice(bed.Simulation, rules.PracticedBy("gathering"), config);
        }

        // 60 % of 120 points, in hundredths; nothing is learned by practice alone.
        bed.Simulation.Progress.Points["stonecutting"].Practice.ShouldBe(7_200);
        bed.Simulation.Progress.Points["agriculture"].Practice.ShouldBe(7_200);
        bed.Simulation.Progress.Knowledge.ShouldBe(["carpentry"]);

        // Cereals needs agriculture, so gathering did not teach it.
        bed.Simulation.Progress.Points.ContainsKey("cereals").ShouldBeFalse();
        ResearchRules.Progress(bed.Simulation, "stonecutting").ShouldBe(6_000);
    }

    [Fact]
    public void Practice_and_research_together_complete_a_knowledge_and_open_the_next()
    {
        var bed = Village();
        var rules = ColonyRules.From(Content);
        for (var tick = 0; tick < 1_000; tick++)
        {
            ResearchRules.AddPractice(bed.Simulation, rules.PracticedBy("gathering"), rules.Knowledge!);
        }

        Queue(bed, "agriculture");
        ResearchRules.AddResearch(bed.Simulation, 4_800);

        bed.Simulation.Progress.Knowledge.ShouldContain("agriculture");
        ResearchRules.State(bed.Simulation, "cereals").ShouldBe(KnowledgeState.Available);
        bed.Simulation.Progress.ResearchQueue.ShouldBeEmpty();

        // Now cereals takes the practice of gathering.
        ResearchRules.AddPractice(bed.Simulation, rules.PracticedBy("gathering"), rules.Knowledge!);
        bed.Simulation.Progress.Points["cereals"].Practice.ShouldBe(80);
    }

    [Fact]
    public void The_queue_takes_only_available_knowledge_without_repeats_and_up_to_the_most_the_config_says()
    {
        var bed = Village();
        Queue(bed, "stonecutting", "agriculture");
        bed.Simulation.Progress.ResearchQueue.ShouldBe(["stonecutting", "agriculture"]);

        // Blocked, learned, repeated, unknown and too many: the queue stays as it was.
        foreach (var queue in new[]
        {
            new[] { "cereals" },
            ["carpentry"],
            ["stonecutting", "stonecutting"],
            ["nothing"],
            ["stonecutting", "agriculture", "cereals", "silviculture"],
        })
        {
            Queue(bed, queue);
            bed.Simulation.Progress.ResearchQueue.ShouldBe(["stonecutting", "agriculture"]);
        }

        // Reordering changes which one goes first, and an empty queue clears it.
        Queue(bed, "agriculture", "stonecutting");
        ResearchRules.Current(bed.Simulation).ShouldBe("agriculture");
        Queue(bed);
        ResearchRules.Current(bed.Simulation).ShouldBeNull();
    }

    [Fact]
    public void Nobody_researches_who_never_does_it_without_a_desk_or_without_a_queue()
    {
        var bed = Village();
        var scribe = Researcher(bed, 6, 6, "Scribe", 10);
        Queue(bed, "stonecutting");

        // No scriptorium.
        bed.Simulation.Run(200);
        bed.Simulation.Progress.Points.ContainsKey("stonecutting").ShouldBeFalse();

        // A scriptorium, but the colonist never does research.
        Scriptorium(bed, 8, 8);
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(scribe, "research", WorkPriorityLevels.Never));
        bed.Simulation.Run(200);
        bed.Simulation.Progress.Points.ContainsKey("stonecutting").ShouldBeFalse();

        // With the priority back, it does.
        bed.Simulation.Commands.Enqueue(new SetWorkPriorityCommand(scribe, "research", WorkPriorityLevels.Lowest));
        bed.Simulation.Run(200);
        bed.Simulation.Progress.Knowledge.ShouldContain("stonecutting");

        // Nothing is left to research: the scribe leaves the desk.
        bed.Simulation.Run(100);
        bed.World.TryGet<Job>(scribe, out var job).ShouldBeTrue("the scribe does something else");
        job!.Kind.ShouldNotBe(JobKind.Research);
    }

    [Fact]
    public void A_desk_has_one_researcher_at_a_time_and_two_desks_have_two()
    {
        var bed = Village();
        Scriptorium(bed, 8, 8);
        Researcher(bed, 6, 6, "First", 10);
        Researcher(bed, 6, 7, "Second", 10);
        Queue(bed, "stonecutting", "agriculture");
        var most = 0;
        for (var tick = 0; tick < 100; tick++)
        {
            bed.Simulation.Run(1);
            most = Math.Max(most, Researching(bed));
        }

        most.ShouldBe(1);

        Scriptorium(bed, 10, 10);
        for (var tick = 0; tick < 100; tick++)
        {
            bed.Simulation.Run(1);
            most = Math.Max(most, Researching(bed));
        }

        most.ShouldBe(2);
    }

    private static int Researching(ColonyTestBed bed)
    {
        var jobs = new List<EntityId>();
        bed.World.Query<Job>(jobs);
        return jobs.Count(colonist => bed.World.Get<Job>(colonist).Kind == JobKind.Research);
    }

    [Fact]
    public void What_a_knowledge_requires_blocks_its_content_until_it_is_learned()
    {
        var bed = Village();
        Scriptorium(bed, 8, 8);
        var cell = new TileCoord(3, 3, 1);
        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand("mason_bench", "timber", cell, cell));
        bed.Simulation.ApplyCommandsNow();
        bed.World.Store<Blueprint>().Count.ShouldBe(0);
        ResearchRules.Unlocks(bed.Simulation, "stonecutting").Select(def => def.Id).ShouldBe(["mason_bench"]);

        bed.Simulation.Progress.Learn("stonecutting");
        bed.Simulation.Commands.Enqueue(new PlaceBlueprintCommand("mason_bench", "timber", cell, cell));
        bed.Simulation.ApplyCommandsNow();

        bed.World.Store<Blueprint>().Count.ShouldBe(1);
    }

    [Fact]
    public void A_game_saved_with_a_queue_and_points_goes_on_as_if_never_stopped()
    {
        var bed = Village();
        Scriptorium(bed, 8, 8);
        Researcher(bed, 6, 6, "Scribe", 10);
        Queue(bed, "stonecutting", "agriculture");
        bed.Simulation.Run(60);
        bed.Simulation.Progress.Points["stonecutting"].Research.ShouldBeGreaterThan(0);

        var output = new MemoryStream();
        SaveGame.Write(output, bed.Simulation, new SaveInfo("research", IsAutosave: false, "test", DateTimeOffset.UnixEpoch));
        var loaded = SaveGame.Load(new MemoryStream(output.ToArray()), Content);
        ColonySystems.AddTo(loaded);

        loaded.ComputeStateHash().ShouldBe(bed.Simulation.ComputeStateHash());
        loaded.Progress.ResearchQueue.ShouldBe(["stonecutting", "agriculture"]);
        bed.Simulation.Run(300);
        loaded.Run(300);
        loaded.ComputeStateHash().ShouldBe(bed.Simulation.ComputeStateHash());
        loaded.Progress.Knowledge.ShouldContain("stonecutting");
    }
}
