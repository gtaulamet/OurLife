using OurLife.Core.Colony;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Colony;

/// <summary>Interactions and relations (H3.5, <c>docs/gdd/vida-y-generaciones.md</c>).</summary>
public class SocialTests
{
    private const int TicksPerDay = 24 * 60;

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void Two_sociable_and_kind_colonists_who_live_together_become_friends_in_five_days(ulong seed)
    {
        // Two women attracted to men: friends, not a couple.
        var (bed, a, b) = TwoInAHut(seed, sociability: 90, kindness: 90, patience: 90, Gender.Female);

        var told = RunDays(bed, 5, SocialRules.FriendsKey);

        Relationships.Kind(bed.Simulation, a, b).ShouldBe(RelationKind.Friend);
        told.ShouldBe(1);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void Two_irritable_and_harsh_colonists_become_rivals_in_five_days(ulong seed)
    {
        var (bed, a, b) = TwoInAHut(seed, sociability: 50, kindness: 5, patience: 5);

        var told = RunDays(bed, 5, SocialRules.RivalsKey);

        Relationships.Kind(bed.Simulation, a, b).ShouldBe(RelationKind.Rival);
        told.ShouldBe(1);
    }

    /// <summary>Two colonists, a woman and a man unless told, in a small room with food: they are always within reach of each other.</summary>
    internal static (ColonyTestBed Bed, EntityId A, EntityId B) TwoInAHut(
        ulong seed, int sociability, int kindness, int patience, Gender second = Gender.Male)
    {
        var bed = ColonyTestBed.Flat(5, seed);
        var a = bed.AddColonist(1, 1, 1, name: "Elvira");
        var b = bed.AddColonist(3, 3, 1, name: second == Gender.Male ? "Rodrigo" : "Urraca");
        bed.World.Get<Colonist>(b).Gender = second;
        foreach (var colonist in new[] { a, b })
        {
            bed.World.Add(colonist, new Personality
            {
                Traits =
                [
                    new TraitValue { Trait = "kindness", Value = kindness },
                    new TraitValue { Trait = "patience", Value = patience },
                    new TraitValue { Trait = "sociability", Value = sociability },
                ],
            });
        }

        bed.AddItems("provisions", 50, 2, 2, 1);
        return (bed, a, b);
    }

    /// <summary>Runs whole days and counts the messages with the key.</summary>
    internal static int RunDays(ColonyTestBed bed, int days, string key)
    {
        var told = 0;
        for (var day = 0; day < days; day++)
        {
            bed.Simulation.Run(TicksPerDay);
            told += bed.DrainEvents().OfType<NotificationEvent>().Count(notice => notice.Text.Key == key);
        }

        return told;
    }
}
