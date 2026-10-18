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
    /// <param name="level">
    /// What the desire remembers: by default the level of the skill; for making up, the opinion
    /// before the argument; for a child, the children the colonist had.
    /// </param>
    internal static void Consider(
        Simulation simulation, EntityId colonist, DesireKind kind, EntityId target = default, string? workType = null,
        Func<DesireDef, bool>? eligible = null, int? level = null)
    {
        var content = simulation.Content;
        if (MoodRules.ConfigOf(content) is not { } config || (kind == DesireKind.OwnBed && Beds.HasBed(simulation, colonist)))
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
            if ((desires?.Active.Count ?? 0) >= config.MaxDesires
                || (desires?.LastBornDay is { } last && today - last < config.DaysBetweenDesires))
            {
                return;
            }

            if (desires?.Active.Any(active => active.Desire == desire.Id && active.Target == target && active.WorkType == workType) == true)
            {
                continue;
            }

            var scale = desire.ScaledBy is { } trait ? PersonalityRules.Value(simulation, colonist, trait) : PersonalityRules.Average;
            if (!simulation.Random.Get(RandomStream).Chance(desire.ChancePercent * 100 * scale / PersonalityRules.Average))
            {
                continue;
            }

            desires = DesiresOf(simulation.World, colonist);
            desires.LastBornDay = today;
            desires.Active.Add(new ActiveDesire
            {
                Desire = desire.Id,
                Target = target,
                WorkType = workType,
                Level = level ?? (workType is null ? 0 : SkillRules.Level(simulation, colonist, workType) ?? 0),
                UntilTick = simulation.CurrentTick + ((long)ExpireDays(simulation, desire, config) * simulation.Time.TicksPerDay),
            });
        }
    }

    /// <summary>
    /// The game days a desire waits: those of the mood config, or its years of life at the pace of
    /// the game (a year of life is 60 game days at Normal and 7 at Very fast), at least one.
    /// </summary>
    private static int ExpireDays(Simulation simulation, DesireDef desire, MoodConfigDef config)
    {
        if (desire.ExpireYears is not { } years)
        {
            return config.DesireExpireDays;
        }

        var content = simulation.Content;
        var pace = simulation.LifePace.Length > 0 && content.TryGet<LifePaceDef>(simulation.LifePace, out var chosen)
            ? chosen
            : content.All<LifePaceDef>().FirstOrDefault(each => each.Default);
        return Math.Max(1, years * simulation.Time.DaysPerYear / (pace?.LifeDaysPerDay ?? 1));
    }

    /// <summary>The desires of a colonist, created empty if it had none.</summary>
    internal static Desires DesiresOf(World world, EntityId colonist)
    {
        if (!world.TryGet<Desires>(colonist, out var desires))
        {
            desires = new Desires();
            world.Add(colonist, desires);
        }

        return desires;
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
            DesireKind.OwnBed => Beds.HasBed(simulation, colonist),
            DesireKind.Reconcile => Alive(simulation, active.Target) && Relationships.Opinion(simulation, colonist, active.Target) >= active.Level,
            DesireKind.HaveChild => ChildrenOf(simulation, colonist) > active.Level || Expecting(simulation, colonist),
            _ => false,
        };
    }

    /// <summary>How many children a colonist has had, alive or dead.</summary>
    public static int ChildrenOf(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return simulation.World.TryGet<Family>(colonist, out var family) ? family.Children.Count : 0;
    }

    /// <summary>Whether a colonist, or its partner, expects a child.</summary>
    private static bool Expecting(Simulation simulation, EntityId colonist) =>
        LifeRules.PregnancyOf(simulation, colonist) is not null
        || (Relationships.PartnerOf(simulation, colonist) is { IsNone: false } partner && LifeRules.PregnancyOf(simulation, partner) is not null);

    /// <summary>
    /// A desire about someone who is gone can no longer come true, nor be frustrated; nor can a
    /// wish for a child with a partner the colonist is no longer with.
    /// </summary>
    public static bool IsMoot(Simulation simulation, EntityId colonist, ActiveDesire active)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(active);
        if (!active.Target.IsNone && !Alive(simulation, active.Target))
        {
            return true;
        }

        return simulation.Content.TryGet<DesireDef>(active.Desire, out var desire) && desire.Kind == DesireKind.HaveChild
               && Relationships.PartnerOf(simulation, colonist) != active.Target;
    }

    private static bool Alive(Simulation simulation, EntityId colonist) => Relationships.IsLivingColonist(simulation.World, colonist);
}
