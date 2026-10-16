using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// The content of personality (see "Personalidad" in <c>docs/gdd/vida-y-generaciones.md</c>),
/// checked once: how traits are drawn, inherited and moved by experiences, and the read-only
/// queries of what they change. A colonist without a trait counts as average in it.
/// </summary>
public sealed class PersonalityRules
{
    /// <summary>The value of a trait a colonist does not have.</summary>
    public const int Average = 50;

    private const int Max = 100;

    private PersonalityRules(PersonalityConfigDef config, IReadOnlyList<TraitDef> traits)
    {
        Config = config;
        Traits = traits;
    }

    public PersonalityConfigDef Config { get; }

    /// <summary>The traits of the content, by id.</summary>
    public IReadOnlyList<TraitDef> Traits { get; }

    /// <exception cref="InvalidOperationException">The content has no PersonalityConfig or several.</exception>
    public static PersonalityRules From(DefDatabase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var configs = content.All<PersonalityConfigDef>();
        if (configs.Count != 1)
        {
            throw new InvalidOperationException($"The colony needs exactly one PersonalityConfig def, found {configs.Count}.");
        }

        return new PersonalityRules(configs[0], content.All<TraitDef>());
    }

    /// <summary>A personality drawn at random: each trait the average of a few rolls, and an orientation by its weights.</summary>
    public Personality Draw(Pcg32 random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var personality = new Personality();
        foreach (var trait in Traits)
        {
            personality.Traits.Add(new TraitValue { Trait = trait.Id, Value = Roll(random) });
        }

        personality.Orientation = DrawOrientation(random);
        return personality;
    }

    /// <summary>
    /// A child's personality: each trait is part the average of its parents' and part drawn, and
    /// the orientation is drawn.
    /// </summary>
    public Personality Inherit(Personality mother, Personality father, Pcg32 random)
    {
        ArgumentNullException.ThrowIfNull(mother);
        ArgumentNullException.ThrowIfNull(father);
        ArgumentNullException.ThrowIfNull(random);
        var personality = new Personality();
        foreach (var trait in Traits)
        {
            var parents = ((mother.Find(trait.Id)?.Value ?? Average) + (father.Find(trait.Id)?.Value ?? Average)) / 2;
            var drawn = Roll(random);
            var value = ((parents * Config.InheritedPercent) + (drawn * (100 - Config.InheritedPercent))) / 100;
            personality.Traits.Add(new TraitValue { Trait = trait.Id, Value = value });
        }

        personality.Orientation = DrawOrientation(random);
        return personality;
    }

    /// <summary>
    /// An experience moves a trait of the colonist by <paramref name="points"/>, times the drift of
    /// its stage of life, within 0 and 100 and within what the trait may still move today. Returns
    /// how far it moved. A colonist without personality, or without the trait, does not change.
    /// </summary>
    public int Drift(Simulation simulation, EntityId colonist, string trait, int points)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (!simulation.World.TryGet<Personality>(colonist, out var personality) || personality.Find(trait) is not { } value)
        {
            return 0;
        }

        // The day of the calendar, from midnight to midnight.
        var time = simulation.Time;
        var day = ((time.StartHour * 60L) + (simulation.CurrentTick * time.MinutesPerTick)) / TimeSettings.MinutesPerDay;
        if (personality.DriftDay != day)
        {
            personality.DriftDay = day;
            foreach (var each in personality.Traits)
            {
                each.MovedToday = 0;
            }
        }

        var scaled = points * (LifeRules.Stage(simulation, colonist)?.TraitDriftPercent ?? 100) / 100;
        var cap = Config.MaxDriftPerDay;
        var allowed = Math.Clamp(value.MovedToday + scaled, -cap, cap) - value.MovedToday;
        var moved = Math.Clamp(value.Value + allowed, 0, Max) - value.Value;
        value.Value += moved;
        value.MovedToday += moved;
        return moved;
    }

    /// <summary>A trait of a colonist, from 0 to 100; average if it has none.</summary>
    public static int Value(Simulation simulation, EntityId colonist, string trait)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return simulation.World.TryGet<Personality>(colonist, out var personality) && personality.Find(trait) is { } value
            ? value.Value
            : Average;
    }

    /// <summary>How fast a colonist works by its traits, in percent: the product of every trait with a <see cref="TraitDef.WorkSpeed"/>.</summary>
    public static int WorkSpeedPercent(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var percent = 100;
        foreach (var trait in simulation.Content.All<TraitDef>())
        {
            if (trait.WorkSpeed is { } speed)
            {
                percent = percent * speed.At(Value(simulation, colonist, trait.Id)) / 100;
            }
        }

        return percent;
    }

    /// <summary>Whether a trait makes the colonist look after its low needs before working.</summary>
    public static bool NeedsBeforeWork(Simulation simulation, EntityId colonist)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        foreach (var trait in simulation.Content.All<TraitDef>())
        {
            if (trait.NeedsBeforeWorkBelow is { } below && Value(simulation, colonist, trait.Id) < below)
            {
                return true;
            }
        }

        return false;
    }

    private int Roll(Pcg32 random)
    {
        var sum = 0;
        for (var i = 0; i < Config.Rolls; i++)
        {
            sum += random.NextInt(Max + 1);
        }

        return sum / Config.Rolls;
    }

    private Orientation DrawOrientation(Pcg32 random)
    {
        var weights = Config.Orientations;
        var roll = random.NextInt(weights.Opposite + weights.Same + weights.Both);
        return roll < weights.Opposite ? Orientation.Opposite
            : roll < weights.Opposite + weights.Same ? Orientation.Same
            : Orientation.Both;
    }
}
