using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
using OurLife.Core.Text;

namespace OurLife.Core.Colony;

/// <summary>
/// Needs go down every tick, at the percent of the stage of life (see
/// <c>docs/gdd/vida-y-generaciones.md</c>); a need satisfied by sleeping goes up instead while its owner sleeps.
/// A need that kills ends the colonist's life after its hours at zero; when the last colonist
/// dies the colony is lost. Integer arithmetic: the fraction of each tick is carried over.
/// </summary>
public sealed class NeedsSystem : ISimSystem
{
    private const int MinutesPerHour = 60;

    // The remainder counts hundredths of a point per hour times minutes.
    private const int RemainderPerPoint = MinutesPerHour * 100;

    private readonly List<EntityId> _colonists = [];

    public void Tick(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        var minutes = simulation.Time.MinutesPerTick;
        var deaths = 0;

        world.Query<Colonist, Needs>(_colonists);
        foreach (var colonist in _colonists)
        {
            var asleep = world.TryGet<Job>(colonist, out var job) && job.Kind == JobKind.Sleep && job.Stage == JobStage.Working;
            var bedPercent = asleep ? BedRecoveryPercent(simulation, colonist, job!) : 100;
            var stage = LifeRules.Stage(simulation, colonist);
            NeedDef? killer = null;
            foreach (var state in world.Get<Needs>(colonist).Values)
            {
                var need = simulation.Content.Get<NeedDef>(state.Need);
                // In hundredths, so a bed's percent keeps its fraction in the remainder.
                var perHour = asleep && need.SatisfiedBy == NeedSatisfaction.Sleeping
                    ? need.RecoveryPerHour * bedPercent
                    : -need.DecayPerHour * (stage?.NeedPercent(need.Id) ?? 100);
                state.Remainder += perHour * minutes;
                var whole = state.Remainder / RemainderPerPoint;
                state.Remainder -= whole * RemainderPerPoint;
                state.Value = Math.Clamp(state.Value + whole, 0, need.Max);
                state.MinutesAtZero = state.Value == 0 ? state.MinutesAtZero + minutes : 0;
                if (killer is null && need.DeathAfterHoursAtZero is { } hours && state.MinutesAtZero >= hours * MinutesPerHour)
                {
                    killer = need;
                }
            }

            if (killer is not null)
            {
                ColonistDeath.Die(simulation, colonist, LocalizedText.Of(DeathKey(killer)).With("colonist", colonist));
                deaths++;
            }
        }

        if (deaths > 0)
        {
            ColonistDeath.CheckColonyLost(simulation);
        }
    }

    /// <summary>Key of the message that tells a colonist died of a need, like <c>event.need.hunger.death</c>.</summary>
    public static string DeathKey(NeedDef need)
    {
        ArgumentNullException.ThrowIfNull(need);
        return $"event.need.{need.Id}.death";
    }

    /// <summary>
    /// The rate a sleeper recovers at, in percent: the bed's when it lies on the bed its job is
    /// for, else the usual 100 (see <c>docs/gdd/muebles-y-camas.md</c>).
    /// </summary>
    private static int BedRecoveryPercent(Simulation simulation, EntityId colonist, Job job)
    {
        var world = simulation.World;
        if (job.Target.IsNone || !world.IsAlive(job.Target) || !world.TryGet<Furniture>(job.Target, out var furniture)
            || world.Get<Position>(job.Target).Cell != world.Get<Position>(colonist).Cell)
        {
            return 100;
        }

        // The comfort of the bed's quality scales its rest recovery (see "Calidad" in docs/gdd/oficios.md).
        var percent = simulation.Content.Get<BuildableDef>(furniture.Buildable).RestRecoveryPercent ?? 100;
        return furniture.Quality is null ? percent : percent * QualityRules.From(simulation.Content).Of(furniture.Quality).ComfortPercent / 100;
    }
}
