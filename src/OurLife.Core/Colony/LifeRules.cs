using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// The content of life (see <c>docs/gdd/vida-y-generaciones.md</c>), checked once, and the
/// read-only queries about the life of a colonist for the systems and the presentation. A colonist
/// without <see cref="Life"/> counts as an adult: it works at full speed and never ages.
/// </summary>
public sealed class LifeRules
{
    /// <summary>The chances of dying of old age are in millionths: this one is certain death.</summary>
    public const int Certain = 1_000_000;

    private const int PerMillionPerPercent = Certain / 100;

    private LifeRules(LifeConfigDef config, IReadOnlyList<LifeStageDef> stages, LifePaceDef defaultPace)
    {
        Config = config;
        Stages = stages;
        DefaultPace = defaultPace;
    }

    public LifeConfigDef Config { get; }

    /// <summary>The stages of life, from the youngest: the first starts at 0 years.</summary>
    public IReadOnlyList<LifeStageDef> Stages { get; }

    /// <summary>The pace of a new game unless the player picks another.</summary>
    public LifePaceDef DefaultPace { get; }

    /// <summary>The stage a colonist without <see cref="Life"/> counts as: the youngest that does every work.</summary>
    public LifeStageDef AdultStage => Stages.FirstOrDefault(stage => stage.Work == StageWork.All) ?? Stages[^1];

    /// <exception cref="InvalidOperationException">
    /// The content has no LifeConfig or several, not exactly one default pace, no stage from
    /// 0 years, or two stages from the same age.
    /// </exception>
    public static LifeRules From(DefDatabase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var configs = content.All<LifeConfigDef>();
        if (configs.Count != 1)
        {
            throw new InvalidOperationException($"The colony needs exactly one LifeConfig def, found {configs.Count}.");
        }

        var defaults = content.All<LifePaceDef>().Where(pace => pace.Default).ToArray();
        if (defaults.Length != 1)
        {
            throw new InvalidOperationException($"Exactly one LifePace def must be the default, found {defaults.Length}.");
        }

        var stages = content.All<LifeStageDef>().OrderBy(stage => stage.FromYears).ToArray();
        if (stages.Length == 0 || stages[0].FromYears != 0)
        {
            throw new InvalidOperationException("The youngest LifeStage def must start at 0 years.");
        }

        var clash = stages.GroupBy(stage => stage.FromYears).FirstOrDefault(group => group.Count() > 1);
        if (clash is not null)
        {
            throw new InvalidOperationException(
                $"LifeStage defs {string.Join(", ", clash.Select(stage => stage.Id))} all start at {clash.Key} years.");
        }

        return new LifeRules(configs[0], stages, defaults[0]);
    }

    /// <summary>The pace of the game: the one the player chose, or the default before the colony arrives.</summary>
    public LifePaceDef PaceOf(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return simulation.LifePace.Length == 0 ? DefaultPace : simulation.Content.Get<LifePaceDef>(simulation.LifePace);
    }

    /// <summary>The stage an age, in days of life, falls in.</summary>
    public LifeStageDef StageAt(int ageDays, int daysPerYear)
    {
        var current = Stages[0];
        foreach (var stage in Stages)
        {
            if ((long)stage.FromYears * daysPerYear <= ageDays)
            {
                current = stage;
            }
        }

        return current;
    }

    /// <summary>The life of a colonist of this age.</summary>
    public Life LifeAt(int ageDays, int daysPerYear) => new() { AgeDays = ageDays, Stage = StageAt(ageDays, daysPerYear).Id };

    /// <summary>
    /// The chance, in millionths, that a colonist of this age dies of old age on a game day: the
    /// chance per year of the table at its age, times the days of life of a game day over the days
    /// of a year. <see cref="Certain"/> where the table says 100 %.
    /// </summary>
    public int OldAgeChance(int ageDays, int lifeDaysPerDay, int daysPerYear)
    {
        var rows = Config.OldAge;
        if (ageDays < (long)rows[0].Years * daysPerYear)
        {
            return 0;
        }

        var perYear = (long)rows[^1].PercentPerYear * PerMillionPerPercent;
        for (var i = 0; i + 1 < rows.Count; i++)
        {
            var from = (long)rows[i].Years * daysPerYear;
            var to = (long)rows[i + 1].Years * daysPerYear;
            if (ageDays < to)
            {
                var low = (long)rows[i].PercentPerYear * PerMillionPerPercent;
                var high = (long)rows[i + 1].PercentPerYear * PerMillionPerPercent;
                perYear = low + ((high - low) * (ageDays - from) / (to - from));
                break;
            }
        }

        return perYear >= Certain ? Certain : (int)Math.Min(Certain, perYear * lifeDaysPerDay / daysPerYear);
    }

    /// <summary>The age of a colonist in whole years, or null if it does not age.</summary>
    public static int? AgeYears(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return simulation.World.TryGet<Life>(colonist, out var life) ? life.AgeDays / simulation.Time.DaysPerYear : null;
    }

    /// <summary>The stage of life of a colonist, or null if it does not age.</summary>
    public static LifeStageDef? Stage(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return simulation.World.TryGet<Life>(colonist, out var life) ? simulation.Content.Get<LifeStageDef>(life.Stage) : null;
    }

    /// <summary>How fast a colonist works, in percent of an adult.</summary>
    public static int WorkSpeedPercent(Simulation simulation, EntityId colonist) => Stage(simulation, colonist)?.WorkSpeedPercent ?? 100;

    /// <summary>How fast a colonist walks, in percent of its creature.</summary>
    public static int MoveSpeedPercent(Simulation simulation, EntityId colonist) => Stage(simulation, colonist)?.MoveSpeedPercent ?? 100;

    /// <summary>
    /// Whether a colonist can have children now: a living adult (of a stage that does every work)
    /// within the fertile years of its sex. Nobody can without the rules of birth.
    /// </summary>
    public bool IsFertile(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        if (Config.Birth is not { } birth || !world.IsAlive(colonist) || !world.TryGet<Colonist>(colonist, out var person)
            || Stage(simulation, colonist) is not { Work: StageWork.All } || AgeYears(simulation, colonist) is not { } years)
        {
            return false;
        }

        var range = person.Gender == Gender.Female ? birth.FemaleFertileYears : birth.MaleFertileYears;
        return years >= range.Min && years <= range.Max;
    }

    /// <summary>
    /// Whether a couple can have children together: a woman and a man, both fertile, who both
    /// allow it (the player's choice, see <see cref="SetChildrenAllowedCommand"/>).
    /// </summary>
    public bool CanHaveChildren(Simulation simulation, EntityId a, EntityId b)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var world = simulation.World;
        return IsFertile(simulation, a) && IsFertile(simulation, b)
               && world.Get<Colonist>(a).Gender != world.Get<Colonist>(b).Gender
               && ChildrenAllowed(simulation, a) && ChildrenAllowed(simulation, b);
    }

    /// <summary>Whether a colonist's couple may have children; on unless the player said no.</summary>
    public static bool ChildrenAllowed(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return !simulation.World.TryGet<Family>(colonist, out var family) || family.ChildrenAllowed;
    }

    /// <summary>The pregnancy of a colonist, or null if it expects no child.</summary>
    public static Pregnancy? PregnancyOf(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return simulation.World.TryGet<Pregnancy>(colonist, out var pregnancy) ? pregnancy : null;
    }

    /// <summary>How fast a colonist works for its pregnancy, in percent: slower in the last part of it.</summary>
    public static int PregnancyWorkSpeedPercent(Simulation simulation, EntityId colonist) =>
        PregnancyOf(simulation, colonist) is { } pregnancy && simulation.Content.All<LifeConfigDef>() is [{ Birth: { } birth }]
        && pregnancy.LifeDays >= birth.LateFromLifeDays
            ? birth.LateWorkSpeedPercent
            : 100;

    /// <summary>Whether a colonist at this stage of life does work of this type; without a stage, it does.</summary>
    public static bool DoesWork(LifeStageDef? stage, WorkTypeDef workType)
    {
        ArgumentNullException.ThrowIfNull(workType);
        return stage?.Work switch
        {
            null or StageWork.All => true,
            StageWork.Light => workType.Light,
            _ => false,
        };
    }
}
