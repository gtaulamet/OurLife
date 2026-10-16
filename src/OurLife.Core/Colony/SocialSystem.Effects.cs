using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>What an interaction does, and how opinions fade between colonists who do not meet.</summary>
public sealed partial class SocialSystem
{
    private void Apply(Simulation simulation, InteractionDef interaction, EntityId initiator, EntityId receiver, Pcg32 random)
    {
        int towardsReceiver = 0, towardsInitiator = 0, romance = 0;
        var wentWell = true;
        var before = (Relationships.Opinion(simulation, initiator, receiver), Relationships.Opinion(simulation, receiver, initiator));
        var lesson = interaction.Teaches ? Lesson(simulation, initiator, receiver) : null;
        var gap = lesson is null
            ? 0
            : (SkillRules.Level(simulation, initiator, lesson.Id) ?? 0) - (SkillRules.Level(simulation, receiver, lesson.Id) ?? 0);
        if (interaction.Romantic)
        {
            // Courting works only if the other one is attracted back and does not dislike the one who courts.
            wentWell = Relationships.IsAttracted(simulation, receiver, initiator)
                       && Relationships.Opinion(simulation, receiver, initiator) >= interaction.RomanceMinOpinion;
            if (wentWell)
            {
                romance = Draw(interaction.Romance, random);
                towardsReceiver = Draw(interaction.Opinion, random);
                towardsInitiator = Draw(interaction.Opinion, random);
            }
        }
        else if (interaction.Backfire is { } backfire && PersonalityRules.Value(simulation, receiver, backfire.Trait) < backfire.Below
                 && random.Chance(backfire.ChancePercent * 100))
        {
            wentWell = false;
            towardsReceiver = Draw(backfire.Opinion, random);
            towardsInitiator = Draw(backfire.Opinion, random);
        }
        else
        {
            towardsReceiver = Draw(interaction.Opinion, random);
            towardsInitiator = Draw(interaction.Opinion, random);
        }

        towardsInitiator += Draw(interaction.ReceiverOpinion, random);
        if (lesson is not null)
        {
            _colony.Skills.Teach(simulation, initiator, receiver, lesson);
        }

        foreach (var drift in interaction.Drift)
        {
            foreach (var colonist in Sides(drift.Who, initiator, receiver))
            {
                if (random.Chance(drift.ChancePercent * 100))
                {
                    _personalities.Drift(simulation, colonist, drift.Trait, drift.Points);
                }
            }
        }

        _rules.Change(simulation, initiator, receiver, towardsReceiver, towardsInitiator, romance);
        Relationships.Ensure(simulation, initiator, receiver).LastInteractionTick = simulation.CurrentTick;
        Relationships.Ensure(simulation, receiver, initiator).LastInteractionTick = simulation.CurrentTick;
        Remember(simulation, initiator, receiver, interaction, started: true);
        Remember(simulation, receiver, initiator, interaction, started: false);
        if (wentWell)
        {
            MoodRules.Feel(simulation, initiator, interaction.InitiatorThought);
            MoodRules.Feel(simulation, receiver, interaction.ReceiverThought);
        }
        else if (interaction.Romantic)
        {
            MoodRules.Feel(simulation, initiator, interaction.RejectedThought);
        }

        if (wentWell)
        {
            WakeDesires(simulation, interaction, initiator, receiver, before, lesson, gap);
        }

        simulation.Events.Emit(new InteractionEvent(simulation.CurrentTick, initiator, receiver, interaction.Id));
    }

    /// <summary>
    /// The desires an interaction that went well may wake (see "Deseos" in
    /// <c>docs/gdd/vida-y-generaciones.md</c>): to learn from a teacher who knows enough more, to
    /// befriend someone it is warming to who is not family, and to make up with someone it liked
    /// before the argument, until it likes it as much again.
    /// </summary>
    private static void WakeDesires(
        Simulation simulation, InteractionDef interaction, EntityId initiator, EntityId receiver,
        (int OfReceiver, int OfInitiator) before, WorkTypeDef? lesson, int gap)
    {
        var family = Relationships.IsFamily(simulation, initiator, receiver);
        switch (interaction.Desire)
        {
            case DesireKind.LearnFrom when lesson is not null:
                DesireRules.Consider(simulation, receiver, DesireKind.LearnFrom, initiator, lesson.Id, desire => gap >= desire.MinLevels);
                break;
            case DesireKind.Befriend when !family:
                foreach (var (from, to) in new[] { (initiator, receiver), (receiver, initiator) })
                {
                    var opinion = Relationships.Opinion(simulation, from, to);
                    DesireRules.Consider(simulation, from, DesireKind.Befriend, to, eligible: desire => opinion >= desire.MinOpinion && opinion <= desire.MaxOpinion);
                }

                break;
            case DesireKind.Reconcile:
                foreach (var (from, to, opinionBefore) in new[] { (initiator, receiver, before.OfReceiver), (receiver, initiator, before.OfInitiator) })
                {
                    var opinion = Relationships.Opinion(simulation, from, to);
                    DesireRules.Consider(
                        simulation, from, DesireKind.Reconcile, to, eligible: desire => opinionBefore >= desire.MinOpinion && opinion < opinionBefore, level: opinionBefore);
                }

                break;
        }
    }

    private static int Draw(IntRange? range, Pcg32 random) => range is null ? 0 : random.NextInt(range.Min, range.Max + 1);

    private static EntityId[] Sides(InteractionSide who, EntityId initiator, EntityId receiver) => who switch
    {
        InteractionSide.Initiator => [initiator],
        InteractionSide.Receiver => [receiver],
        _ => [initiator, receiver],
    };

    /// <summary>The colonist keeps the interaction among its last ones, for its sheet.</summary>
    private void Remember(Simulation simulation, EntityId colonist, EntityId other, InteractionDef interaction, bool started)
    {
        var limit = _rules.Config.RecentInteractions;
        if (limit == 0)
        {
            return;
        }

        if (!simulation.World.TryGet<RecentInteractions>(colonist, out var recent))
        {
            recent = new RecentInteractions();
            simulation.World.Add(colonist, recent);
        }

        recent.Entries.Add(new InteractionRecord { Tick = simulation.CurrentTick, Interaction = interaction.Id, Other = other, Started = started });
        if (recent.Entries.Count > limit)
        {
            recent.Entries.RemoveRange(0, recent.Entries.Count - limit);
        }
    }

    /// <summary>
    /// Each opinion of someone the colonist did not meet in the last day comes back towards 0, by
    /// <see cref="SocialConfigDef.ForgetPerDay"/>; a good opinion of family or of a partner stops at
    /// <see cref="SocialConfigDef.FamilyFloor"/>. The dead were forgotten when they died (see <see cref="Relationships.ForgetTheDead"/>).
    /// </summary>
    private void Forget(Simulation simulation)
    {
        var world = simulation.World;
        var config = _rules.Config;
        var day = simulation.Time.TicksPerDay;
        foreach (var colonist in _colonists)
        {
            if (!world.TryGet<Relations>(colonist, out var relations))
            {
                continue;
            }

            foreach (var relation in relations.Others)
            {
                if (relation.LastInteractionTick is { } last && simulation.CurrentTick - last < day)
                {
                    continue;
                }

                if (relation.Opinion > 0)
                {
                    var floor = Relationships.IsFamily(simulation, colonist, relation.Other) ? Math.Min(relation.Opinion, config.FamilyFloor) : 0;
                    relation.Opinion = Math.Max(floor, relation.Opinion - config.ForgetPerDay);
                }
                else if (relation.Opinion < 0)
                {
                    relation.Opinion = Math.Min(0, relation.Opinion + config.ForgetPerDay);
                }
            }
        }
    }
}
