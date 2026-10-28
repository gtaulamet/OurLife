using System.Text;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
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
        public int Born;
        public int Pregnancies;
        public int Population;
        public int PeakPopulation;
        public int MinFoodDays = int.MaxValue;
        public int Rotten;
        public int RottenBread;
        public int RottenBerries;
        public int ToolsMade;
        public int ToolsEquipped;
        public int NightsInBed;
        public int NightsOnFloor;
        public int LateNightsInBed;
        public int LateNightsOnFloor;
        public int Beds;
        public double MsPerTick;
        public readonly StringBuilder Timeline = new();

        public override string ToString() =>
            $"starved {Starved}, born {Born}, pregnancies {Pregnancies}, population {Population} (peak {PeakPopulation}), beds {Beds}, rotten {Rotten} (bread {RottenBread}, berries {RottenBerries}), "
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

        outcome.Starved.ShouldBe(0, $"seed {seed}: {outcome}");
        outcome.Population.ShouldBeGreaterThan(8, $"seed {seed}: {outcome}");
        outcome.MsPerTick.ShouldBeLessThan(2.0, $"seed {seed}: {outcome}");
        outcome.ToolsMade.ShouldBeGreaterThanOrEqualTo(4, $"seed {seed}: {outcome}");
        outcome.ToolsEquipped.ShouldBeGreaterThanOrEqualTo(1, $"seed {seed}: {outcome}");

        // Not by the sackful: about a tenth of the ten-year harvest rots (the reserve for a birth is 30 days of food for every mouth, and berries last 60 days).
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
                    outcome.Rotten += units;
                    outcome.RottenBread += ((LocalizedItem(notice) == "item.bread") ? units : 0);
                    outcome.RottenBerries += ((LocalizedItem(notice) == "item.berries") ? units : 0);
                }
            }

            var population = simulation.World.Store<Colonist>().Count;
            outcome.PeakPopulation = Math.Max(outcome.PeakPopulation, population);
            if (day % 30 == 0)
            {
                Snapshot(simulation, player, outcome, day, population);
            }
        }

        outcome.Population = simulation.World.Store<Colonist>().Count;
        outcome.Beds = simulation.World.Store<Bed>().Count;

        // The village of year ten, with its workshops, orders, rooms, fields and piles: the cheapest of five half days.
        var half = simulation.Time.TicksPerDay / 2;
        outcome.MsPerTick = Timing.BestRoundMs(5, _ => simulation.Run(half)) / half;
        outcome.ToolsMade = Tools.Sum(tool => Stocks.Count(simulation, tool)) + outcome.ToolsEquipped;
        return outcome;
    }

    private static string LocalizedItem(NotificationEvent notice) =>
        notice.Text.Args["item"] is OurLife.Core.Text.LocalizedText item ? item.Key.Replace(".name", string.Empty, StringComparison.Ordinal).Replace("def.", string.Empty, StringComparison.Ordinal) : string.Empty;

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

        outcome.ToolsEquipped = equipped;
        var food = string.Join(' ', Foods.Select(item => $"{item}={Stocks.Count(simulation, item)}"));
        var stuff = string.Join(' ', new[] { "log", "plank", "stone", "iron_ore", "charcoal", "iron_ingot" }.Select(item => $"{item}={Stocks.Count(simulation, item)}"));
        outcome.Timeline.AppendLine(
            $"day {day,4} pop {population,2} beds {simulation.World.Store<Bed>().Count,2} houses {player.HousesPlaced} fields {player.FieldsPlaced} tools {equipped}+{Tools.Sum(tool => Stocks.Count(simulation, tool))} | {food} | {stuff} | rotten {outcome.Rotten}");
    }
}
