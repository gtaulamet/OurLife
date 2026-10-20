using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>Whom a colonist interacts with, and which interactions it may choose.</summary>
public sealed partial class SocialSystem
{
    /// <summary>The others awake on the same level close enough, not met too recently, with family and friends weighing more.</summary>
    private void FindNear(Simulation simulation, EntityId colonist)
    {
        _near.Clear();
        var world = simulation.World;
        var config = _rules.Config;
        var cell = world.Get<Position>(colonist).Cell;
        var cooldown = (long)config.PairCooldownHours * simulation.Time.TicksPerHour;
        foreach (var other in _colonists)
        {
            if (other == colonist || !world.IsAlive(other))
            {
                continue;
            }

            var at = world.Get<Position>(other).Cell;
            if (!InReach(config, cell, at) || !CanMeet(simulation, colonist, other, cooldown))
            {
                continue;
            }

            var close = Relationships.Kind(simulation, colonist, other) is RelationKind.Partner or RelationKind.Family or RelationKind.Friend;
            _near.Add((other, close ? config.CloseWeightPercent : OtherWeightPercent));
        }
    }

    /// <summary>Whether two cells are near enough for an interaction: on the same level, within the config's radius.</summary>
    internal static bool InReach(SocialConfigDef config, TileCoord a, TileCoord b) => InReach(a, b, config.Radius);

    /// <summary>Whether two cells are on the same level, within <paramref name="radius"/> cells of each other in each direction.</summary>
    internal static bool InReach(TileCoord a, TileCoord b, int radius) =>
        a.Z == b.Z && Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) <= radius;

    /// <summary>
    /// Whether <paramref name="colonist"/> may interact with <paramref name="other"/> now: the other
    /// can, and the two did not meet less than <paramref name="cooldown"/> ticks ago.
    /// </summary>
    internal static bool CanMeet(Simulation simulation, EntityId colonist, EntityId other, long cooldown) =>
        CanInteract(simulation, other) && !Met(simulation, colonist, other, cooldown) && !Met(simulation, other, colonist, cooldown);

    /// <summary>Whether <paramref name="from"/> interacted with <paramref name="to"/> less than <paramref name="cooldown"/> ticks ago.</summary>
    internal static bool Met(Simulation simulation, EntityId from, EntityId to, long cooldown) =>
        Relationships.Of(simulation, from, to)?.LastInteractionTick is { } last && simulation.CurrentTick - last < cooldown;

    private bool Allowed(Simulation simulation, InteractionDef interaction, EntityId initiator, EntityId receiver)
    {
        var opinion = Relationships.Opinion(simulation, initiator, receiver);
        if (!InStages(simulation, initiator, interaction.InitiatorStages)
            || !(InStages(simulation, receiver, interaction.ReceiverStages)
                 || (interaction.FamilyReceivers && Relationships.IsFamily(simulation, initiator, receiver)))
            || (interaction.MinOpinion is { } min && opinion < min)
            || (interaction.MaxOpinion is { } max && opinion > max)
            || (interaction.ReceiverMoodBelow is { } mood && MoodRules.Value(simulation, receiver) >= mood)
            || (interaction.Teaches && Lesson(simulation, initiator, receiver) is null))
        {
            return false;
        }

        return !interaction.Romantic
               || (Relationships.IsFree(simulation, initiator) && Relationships.IsFree(simulation, receiver)
                   && Relationships.IsAttracted(simulation, initiator, receiver) && !Relationships.IsFamily(simulation, initiator, receiver));
    }

    /// <summary>The weight plus, for each trait, its coefficient for each 10 points over 50; never under 0.</summary>
    private static int WeightOf(Simulation simulation, InteractionDef interaction, EntityId initiator, EntityId receiver)
    {
        var weight = interaction.Weight;
        foreach (var (trait, coefficient) in interaction.InitiatorTraits.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            weight += coefficient * (PersonalityRules.Value(simulation, initiator, trait) - PersonalityRules.Average) / 10;
        }

        foreach (var (trait, coefficient) in interaction.ReceiverTraits.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            weight += coefficient * (PersonalityRules.Value(simulation, receiver, trait) - PersonalityRules.Average) / 10;
        }

        return Math.Max(0, weight);
    }

    /// <summary>The skill the teacher knows the most more of than the apprentice, if enough to teach it; ties to the lowest id.</summary>
    private WorkTypeDef? Lesson(Simulation simulation, EntityId teacher, EntityId apprentice)
    {
        WorkTypeDef? best = null;
        var bestGap = _colony.Skills.Config.TeachingMinLevels - 1;
        foreach (var type in _colony.Skills.WorkTypes)
        {
            if (SkillRules.Level(simulation, teacher, type.Id) is { } master && SkillRules.Level(simulation, apprentice, type.Id) is { } learner
                && master - learner > bestGap)
            {
                (best, bestGap) = (type, master - learner);
            }
        }

        return best;
    }
}
