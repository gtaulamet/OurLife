using System.Text;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
using OurLife.Core.Text;
using Xunit.Abstractions;

namespace OurLife.Content.Tests;

/// <summary>
/// The integration balance of O11 (see "Balance de integración" in <c>docs/ip/bitacora.md</c>): ten game
/// years at the fastest speed (the speed only changes how fast the clock runs, not what the simulation
/// does), played by a <see cref="VillagePlayer"/> who draws fields, builds houses with beds, a dining hall
/// and the workshops of fire and iron, and orders the four iron tools. Nobody starves, the village grows
/// past eight, food does not rot by the sackful, the iron tools are made and equipped and the colonists
/// sleep in beds. Slow: one game of ten years for each seed.
/// </summary>
[Collection(TimingCollection.Name)]
public class IntegrationBalanceTests(ITestOutputHelper output)
{
    private static readonly string[] Foods = ["berries", "wheat", "flour", "bread", "peas", "cabbage", "pottage", "pickled_cabbage"];
    private static readonly string[] Tools = ["iron_axe", "iron_pickaxe", "iron_hoe", "iron_hammer"];

    private sealed class Outcome
    {
        public int Starved;
        public int Lost;
        public int PopulationDrops;
        public int Born;
        public int Pregnancies;
        public int Population;
        public int PeakPopulation;
        public int MinFoodDays = int.MaxValue;
        public int Rotten;
        public readonly Dictionary<string, int> RottenByItem = new(StringComparer.Ordinal);
        public int ToolsMade;
        public int ToolsEquipped;
        public int NightsInBed;
        public int NightsOnFloor;
        public int LateNightsInBed;
        public int LateNightsOnFloor;
        public int Beds;
        public double MsPerTick;
        public readonly StringBuilder Timeline = new();

        public int RottenOf(string item) => RottenByItem.GetValueOrDefault(item);

        public override string ToString() =>
            $"starved {Starved}, colonists lost {Lost}, born {Born}, pregnancies {Pregnancies}, population {Population} (peak {PeakPopulation}), beds {Beds}, rotten {Rotten} (bread {RottenOf("bread")}, berries {RottenOf("berries")}), "
            + $"{MsPerTick:F3} ms per tick, tools made {ToolsMade}, equipped {ToolsEquipped}, sleepers in bed {NightsInBed}/{NightsInBed + NightsOnFloor} (last five years {LateNightsInBed}/{LateNightsInBed + LateNightsOnFloor})";
    }

    [Theory]
    [Trait("Category", "Slow")]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    public void In_ten_years_a_village_that_farms_builds_and_forges_never_starves_grows_and_sleeps_in_beds(ulong seed)
    {
        var outcome = Play(seed);
        output.WriteLine(outcome.Timeline.ToString());
        output.WriteLine($"seed {seed}: {outcome}");

        // Nobody dies of hunger (the notice) and no colonist is lost at all (a living entity that is gone), and the village never shrinks.
        outcome.Starved.ShouldBe(0, $"seed {seed}: {outcome}");
        outcome.Lost.ShouldBe(0, $"seed {seed}: {outcome}");
        outcome.PopulationDrops.ShouldBe(0, $"seed {seed}: {outcome}");
        outcome.Population.ShouldBeGreaterThanOrEqualTo(10, $"seed {seed}: {outcome}");

        // The village of year ten, over a whole game day (the night is cheap: nobody works), costs 0.06 to 0.106 ms a tick on these seeds, around
        // the 0.1 ms of the budget of an empty valley; the budget of this test is 0.15 ms. A tick that gets slower than that is a real regression.
        outcome.MsPerTick.ShouldBeLessThan(0.15, $"seed {seed}: {outcome}");
        outcome.ToolsMade.ShouldBeGreaterThanOrEqualTo(4, $"seed {seed}: {outcome}");
        outcome.ToolsEquipped.ShouldBeGreaterThanOrEqualTo(1, $"seed {seed}: {outcome}");

        // Not by the sackful: about a tenth of the ten-year harvest rots (the reserve for a birth is 30 days of food for every mouth, and berries last 60 days).
        outcome.Rotten.ShouldBe(outcome.RottenByItem.Values.Sum(), $"seed {seed}: every rotten unit is counted under its item");
        outcome.Rotten.ShouldBeLessThan(15_000, $"seed {seed}: {outcome}");
        (outcome.LateNightsInBed * 100).ShouldBeGreaterThanOrEqualTo(80 * (outcome.LateNightsInBed + outcome.LateNightsOnFloor), $"seed {seed}: {outcome}");
    }

    private static Outcome Play(ulong seed)
    {
        var (simulation, player) = VillagePlayer.Found(seed);
        var outcome = new Outcome();
        var content = GameContent.Defs;
        var deaths = NeedsSystem.DeathKey(content.Get<NeedDef>("hunger"));
        var events = new List<ISimEvent>();
        var daysPerYear = simulation.Time.DaysPerYear;
        var ticksPerHour = simulation.Time.TicksPerDay / 24;
        var colonists = new List<OurLife.Core.Ecs.EntityId>();
        var yesterday = new List<OurLife.Core.Ecs.EntityId>();
        var itemOfName = content.All<ItemDef>().Where(item => item.NameKey is not null).ToDictionary(item => item.NameKey!, item => item.Id, StringComparer.Ordinal);
        var previousPopulation = 0;

        for (var day = 1; day <= 10 * daysPerYear; day++)
        {
            player.PlayDay(day);
            for (var hour = 0; hour < 24; hour++)
            {
                simulation.Run(ticksPerHour);
                if (hour is >= 1 and <= 4)
                {
                    SampleSleepers(simulation, colonists, outcome, day > 5 * daysPerYear);
                }
            }

            events.Clear();
            simulation.Events.DrainTo(events);
            foreach (var notice in events.OfType<NotificationEvent>())
            {
                outcome.Starved += notice.Text.Key == deaths ? 1 : 0;
                outcome.Born += notice.Text.Key == BirthSystem.BirthKey ? 1 : 0;
                outcome.Pregnancies += notice.Text.Key == BirthSystem.PregnantKey ? 1 : 0;
                if (notice.Text.Key == SpoilageSystem.SpoiledKey)
                {
                    var units = Convert.ToInt32(notice.Text.Args["count"]);
                    var item = ItemOf(notice, itemOfName);
                    outcome.Rotten += units;
                    outcome.RottenByItem[item] = outcome.RottenByItem.GetValueOrDefault(item) + units;
                }
            }

            var world = simulation.World;
            outcome.Lost += yesterday.Count(colonist => !world.IsAlive(colonist) || !world.Has<Colonist>(colonist));
            world.Query<Colonist>(yesterday);
            var population = world.Store<Colonist>().Count;
            outcome.PopulationDrops += population < previousPopulation ? 1 : 0;
            previousPopulation = population;
            outcome.PeakPopulation = Math.Max(outcome.PeakPopulation, population);
            if (day % 30 == 0)
            {
                Snapshot(simulation, player, outcome, day, population);
            }
        }

        outcome.Population = simulation.World.Store<Colonist>().Count;
        outcome.Beds = simulation.World.Store<Bed>().Count;

        // Tools made: the ones in the stores and the ones the colonists carry (equipped ones are no longer in the stock, so nothing counts twice).
        CountTools(simulation, outcome);

        // The village of year ten, with its workshops, orders, rooms, fields and piles: the cheapest of four whole game days (each crosses a midnight).
        var window = simulation.Time.TicksPerDay;
        outcome.MsPerTick = Timing.BestRoundMs(4, _ => simulation.Run(window)) / window;
        return outcome;
    }

    /// <summary>
    /// The item a notice is about. The notice carries the item as a structured <see cref="LocalizedText"/> whose key is the item's name key
    /// (<c>docs/i18n.md</c>); it is matched to the item through the content, and an unknown name fails the test instead of counting for nothing.
    /// </summary>
    private static string ItemOf(NotificationEvent notice, Dictionary<string, string> itemOfName) =>
        notice.Text.Args["item"] is LocalizedText { Key: var key } && itemOfName.TryGetValue(key, out var item)
            ? item
            : throw new InvalidOperationException($"The item of the notice {notice.Text.Key} is not a known item name: {notice.Text.Args["item"]}.");

    private static void CountTools(Simulation simulation, Outcome outcome)
    {
        var colonists = new List<OurLife.Core.Ecs.EntityId>();
        simulation.World.Query<Colonist>(colonists);
        outcome.ToolsEquipped = colonists.Sum(colonist => OurLife.Core.Colony.Tools.Of(simulation, colonist).Count(tool => Tools.Contains(tool.Def)));
        outcome.ToolsMade = Tools.Sum(tool => Stocks.Count(simulation, tool)) + outcome.ToolsEquipped;
    }

    private static void SampleSleepers(Simulation simulation, List<OurLife.Core.Ecs.EntityId> colonists, Outcome outcome, bool late)
    {
        var world = simulation.World;
        world.Query<Colonist, Job>(colonists);
        foreach (var colonist in colonists)
        {
            var job = world.Get<Job>(colonist);
            if (job.Kind != JobKind.Sleep)
            {
                continue;
            }

            var inBed = !job.Target.IsNone && world.IsAlive(job.Target) && world.Has<Bed>(job.Target)
                        && world.Get<Position>(job.Target).Cell == world.Get<Position>(colonist).Cell;
            outcome.NightsInBed += inBed ? 1 : 0;
            outcome.NightsOnFloor += inBed ? 0 : 1;
            outcome.LateNightsInBed += late && inBed ? 1 : 0;
            outcome.LateNightsOnFloor += late && !inBed ? 1 : 0;
        }
    }

    private static void Snapshot(Simulation simulation, VillagePlayer player, Outcome outcome, int day, int population)
    {
        var equipped = 0;
        var colonists = new List<OurLife.Core.Ecs.EntityId>();
        simulation.World.Query<Colonist>(colonists);
        foreach (var colonist in colonists)
        {
            equipped += OurLife.Core.Colony.Tools.Of(simulation, colonist).Count;
        }

        var food = string.Join(' ', Foods.Select(item => $"{item}={Stocks.Count(simulation, item)}"));
        var stuff = string.Join(' ', new[] { "log", "plank", "stone", "iron_ore", "charcoal", "iron_ingot" }.Select(item => $"{item}={Stocks.Count(simulation, item)}"));
        outcome.Timeline.AppendLine(
            $"day {day,4} pop {population,2} beds {simulation.World.Store<Bed>().Count,2} houses {player.HousesPlaced} fields {player.FieldsPlaced} tools {equipped}+{Tools.Sum(tool => Stocks.Count(simulation, tool))} | {food} | {stuff} | rotten {outcome.Rotten}");
    }
}
