using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// The desires of the colonists (see "Deseos" in <c>docs/gdd/vida-y-generaciones.md</c>): born at
/// their moment with a chance, at most <see cref="MoodConfigDef.MaxDesires"/> at once and one every
/// <see cref="MoodConfigDef.DaysBetweenDesires"/>, and fulfilled or frustrated later. Draws from the
/// random stream <c>social.desires</c>.
/// </summary>
public static class DesireRules
{
    private const string RandomStream = "social.desires";

    /// <summary>
    /// A moment for desires of a kind, like a lesson for a desire to learn: each desire of that
    /// kind, if <paramref name="eligible"/>, may be born, while the colonist has room for one.
    /// </summary>
    internal static void Consider(
        Simulation simulation, EntityId colonist, DesireKind kind, EntityId target = default, string? workType = null,
        Func<DesireDef, bool>? eligible = null)
    {
        var content = simulation.Content;
        if (MoodRules.ConfigOf(content) is not { } config)
        {
            return;
        }

        foreach (var desire in content.All<DesireDef>())
        {
            if (desire.Kind != kind || (eligible is not null && !eligible(desire)))
            {
                continue;
            }

            var desires = simulation.World.TryGet<Desires>(colonist, out var existing) ? existing : null;
            var today = simulation.Calendar.DayNumberAt(simulation.CurrentTick);
            if (desires is not null
                && (desires.Active.Count >= config.MaxDesires
                    || (desires.LastBornDay is { } last && today - last < config.DaysBetweenDesires)
                    || desires.Active.Any(active => active.Desire == desire.Id && active.Target == target && active.WorkType == workType)))
            {
                return;
            }

            var scale = desire.ScaledBy is { } trait ? PersonalityRules.Value(simulation, colonist, trait) : PersonalityRules.Average;
            if (!simulation.Random.Get(RandomStream).Chance(desire.ChancePercent * 100 * scale / PersonalityRules.Average))
            {
                continue;
            }

            if (desires is null)
            {
                desires = new Desires();
                simulation.World.Add(colonist, desires);
            }

            desires.LastBornDay = today;
            desires.Active.Add(new ActiveDesire
            {
                Desire = desire.Id,
                Target = target,
                WorkType = workType,
                Level = workType is null ? 0 : SkillRules.Level(simulation, colonist, workType) ?? 0,
                UntilTick = simulation.CurrentTick + ((long)config.DesireExpireDays * simulation.Time.TicksPerDay),
            });
        }
    }

    /// <summary>Whether a desire came true.</summary>
    public static bool IsFulfilled(Simulation simulation, EntityId colonist, ActiveDesire active)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(active);
        var desire = simulation.Content.Get<DesireDef>(active.Desire);
        return desire.Kind switch
        {
            DesireKind.LearnFrom => active.WorkType is { } type && SkillRules.Level(simulation, colonist, type) > active.Level,
            DesireKind.Befriend => Alive(simulation, active.Target) && Relationships.Kind(simulation, colonist, active.Target) == RelationKind.Friend,
            DesireKind.FindPartner => !Relationships.PartnerOf(simulation, colonist).IsNone,
            DesireKind.OwnBed => HasBed(simulation, colonist),
            DesireKind.Reconcile => Alive(simulation, active.Target) && Relationships.Opinion(simulation, colonist, active.Target) >= desire.MinOpinion,
            _ => false,
        };
    }

    /// <summary>A desire about someone who is gone can no longer come true, nor be frustrated.</summary>
    public static bool IsMoot(Simulation simulation, ActiveDesire active)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(active);
        return !active.Target.IsNone && !Alive(simulation, active.Target);
    }

    private static bool Alive(Simulation simulation, EntityId colonist) =>
        !colonist.IsNone && simulation.World.IsAlive(colonist) && simulation.World.Has<Colonist>(colonist);

    private static bool HasBed(Simulation simulation, EntityId colonist)
    {
        var beds = new List<EntityId>();
        simulation.World.Query<Bed>(beds);
        return beds.Exists(bed => simulation.World.Get<Bed>(bed).Owner == colonist);
    }
}
