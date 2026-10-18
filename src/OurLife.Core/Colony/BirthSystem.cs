using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
using OurLife.Core.Text;

namespace OurLife.Core.Colony;

/// <summary>
/// Runs at midnight, after <see cref="LifeSystem"/> (see "Parejas, embarazo y nacimiento" in
/// <c>docs/gdd/vida-y-generaciones.md</c>): pregnancies go on by the days of life of the pace and
/// end in a birth, and a fertile couple of a woman and a man who may have children can conceive,
/// unless one of them is hungry or the mother has a child too young. Conception is abstract: it is
/// not shown. Women are visited by ascending id. Draws from the random stream <c>life.birth</c>.
/// </summary>
public sealed class BirthSystem : ISimSystem
{
    /// <summary>The message when a woman expects a child, with <c>colonist</c>.</summary>
    public const string PregnantKey = "event.life.pregnant";

    /// <summary>The message of a birth, with <c>mother</c> and <c>child</c>.</summary>
    public const string BirthKey = "event.life.birth";

    private const string RandomStream = "life.birth";

    private readonly LifeRules _life;
    private readonly PersonalityRules _personalities;
    private readonly SkillRules _skills;
    private readonly List<EntityId> _colonists = [];

    public BirthSystem(LifeRules life, PersonalityRules personalities, SkillRules skills)
    {
        ArgumentNullException.ThrowIfNull(life);
        ArgumentNullException.ThrowIfNull(personalities);
        ArgumentNullException.ThrowIfNull(skills);
        _life = life;
        _personalities = personalities;
        _skills = skills;
    }

    public void Tick(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (_life.Config.Birth is not { } birth)
        {
            return;
        }

        var world = simulation.World;
        var pace = _life.PaceOf(simulation).LifeDaysPerDay;

        // A child born now is not in the list, so it does nothing until the next midnight.
        world.Query<Colonist, Life>(_colonists);
        foreach (var colonist in _colonists)
        {
            if (world.TryGet<Pregnancy>(colonist, out var pregnancy))
            {
                pregnancy.LifeDays = checked(pregnancy.LifeDays + pace);
                if (pregnancy.LifeDays >= birth.PregnancyLifeDays)
                {
                    GiveBirth(simulation, colonist, pregnancy, birth);
                }
            }
            else if (world.Get<Colonist>(colonist).Gender == Gender.Female)
            {
                TryConceive(simulation, colonist, birth, pace);
            }
        }
    }

    private void TryConceive(Simulation simulation, EntityId mother, BirthConfig birth, int pace)
    {
        var father = Relationships.PartnerOf(simulation, mother);
        if (father.IsNone || !_life.CanHaveChildren(simulation, mother, father) || Hungry(simulation, mother) || Hungry(simulation, father)
            || HasYoungChild(simulation, mother, birth))
        {
            return;
        }

        // The chance of a year of life, in millionths, spread over the days of life of a game day.
        var chance = (long)birth.ConceptionChancePerYear * (LifeRules.Certain / 100) * pace / simulation.Time.DaysPerYear;
        if (simulation.Random.Get(RandomStream).NextInt(LifeRules.Certain) >= chance)
        {
            return;
        }

        simulation.World.Add(mother, new Pregnancy { Father = Relationships.RelativeOf(simulation, father) });
        simulation.Events.Emit(new EntityChangedEvent(mother));
        Notify(simulation, LocalizedText.Of(PregnantKey).With("colonist", mother));
    }

    /// <summary>Hunger under its low threshold keeps a couple from conceiving: food is what limits the colony.</summary>
    private static bool Hungry(Simulation simulation, EntityId colonist)
    {
        if (!simulation.World.TryGet<Needs>(colonist, out var needs))
        {
            return false;
        }

        foreach (var state in needs.Values)
        {
            var need = simulation.Content.Get<NeedDef>(state.Need);
            if (need.SatisfiedBy == NeedSatisfaction.Eating && state.Value < need.LowThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasYoungChild(Simulation simulation, EntityId mother, BirthConfig birth)
    {
        var world = simulation.World;
        var young = (long)birth.YearsBetweenChildren * simulation.Time.DaysPerYear;
        return world.TryGet<Family>(mother, out var family)
               && family.Children.Exists(child => !child.Dead && world.IsAlive(child.Entity) && world.TryGet<Life>(child.Entity, out var life)
                                                  && life.AgeDays < young);
    }

    /// <summary>
    /// A baby is born on the mother's cell: of a sex drawn, with a name of the list no living
    /// colonist bears while there are some, the father's surname, traits taken from both parents,
    /// no skill and every need full. Parents, brothers and sisters start with their opinions of it.
    /// </summary>
    private void GiveBirth(Simulation simulation, EntityId mother, Pregnancy pregnancy, BirthConfig birth)
    {
        var world = simulation.World;
        var random = simulation.Random.Get(RandomStream);
        var person = world.Get<Colonist>(mother);
        var creature = simulation.Content.Get<CreatureDef>(person.Creature);
        var father = pregnancy.Father.Dead || !world.IsAlive(pregnancy.Father.Entity) ? EntityId.None : pregnancy.Father.Entity;
        var gender = random.NextBool() ? Gender.Female : Gender.Male;

        var baby = world.CreateEntity();
        var cell = world.Get<Position>(mother).Cell;
        world.Add(baby, new Position { X = cell.X, Y = cell.Y, Z = cell.Z });
        world.Add(baby, new Colonist
        {
            Name = PickName(simulation, creature, gender, random),
            Surname = pregnancy.Father.Surname,
            Gender = gender,
            Creature = creature.Id,
        });
        world.Add(baby, _life.LifeAt(0, simulation.Time.DaysPerYear));
        world.Add(baby, _personalities.Inherit(PersonalityOf(world, mother), PersonalityOf(world, father), random));
        world.Add(baby, _skills.Newborn());
        world.Add(baby, FullNeeds(simulation, creature));
        world.Add(baby, new Movement());
        world.Add(baby, new Family
        {
            Father = Copy(pregnancy.Father),
            Mother = Relationships.RelativeOf(simulation, mother),
        });
        world.Remove<Pregnancy>(mother);

        foreach (var parent in father.IsNone ? [mother] : new[] { mother, father })
        {
            FamilyOf(world, parent).Children.Add(Relationships.RelativeOf(simulation, baby));
            Meet(simulation, parent, baby, birth.ParentOpinion);
            MoodRules.Feel(simulation, parent, config => config.ChildBorn);
            if (MoodRules.ConfigOf(simulation.Content)?.Parenthood is { } shift)
            {
                _personalities.Drift(simulation, parent, shift.Trait, shift.Points);
            }

            simulation.Events.Emit(new EntityChangedEvent(parent));
        }

        foreach (var sibling in Relationships.SiblingsOf(simulation, baby))
        {
            Meet(simulation, sibling, baby, birth.SiblingOpinion);
        }

        simulation.Events.Emit(new EntitySpawnedEvent(baby));
        Notify(simulation, LocalizedText.Of(BirthKey).With("mother", mother).With("child", baby));
    }

    /// <summary>Two meet at the birth with this opinion of each other, so it does not fade on the night they met.</summary>
    private static void Meet(Simulation simulation, EntityId a, EntityId b, int opinion)
    {
        foreach (var (from, to) in new[] { (a, b), (b, a) })
        {
            var relation = Relationships.Ensure(simulation, from, to);
            relation.Opinion = opinion;
            relation.LastInteractionTick = simulation.CurrentTick;
        }
    }

    private static Personality PersonalityOf(World world, EntityId colonist) =>
        !colonist.IsNone && world.TryGet<Personality>(colonist, out var personality) ? personality : new Personality();

    /// <summary>A name of the creature's list for the sex that no living colonist bears, or any of them once all are taken.</summary>
    private static string PickName(Simulation simulation, CreatureDef creature, Gender gender, Pcg32 random)
    {
        if (creature.NameList is null)
        {
            return string.Empty;
        }

        var list = simulation.Content.Get<NameListDef>(creature.NameList);
        var names = gender == Gender.Female ? list.Female : list.Male;
        var colonists = new List<EntityId>();
        simulation.World.Query<Colonist>(colonists);
        var taken = colonists.Select(colonist => simulation.World.Get<Colonist>(colonist).Name).ToHashSet(StringComparer.Ordinal);
        var free = names.Where(name => !taken.Contains(name)).ToArray();
        return free.Length > 0 ? free[random.NextInt(free.Length)] : names[random.NextInt(names.Count)];
    }

    private static Needs FullNeeds(Simulation simulation, CreatureDef creature)
    {
        var needs = new Needs();
        foreach (var id in creature.Needs)
        {
            needs.Values.Add(new NeedState { Need = id, Value = simulation.Content.Get<NeedDef>(id).Max });
        }

        return needs;
    }

    private static Family FamilyOf(World world, EntityId colonist)
    {
        if (!world.TryGet<Family>(colonist, out var family))
        {
            family = new Family();
            world.Add(colonist, family);
        }

        return family;
    }

    private static Relative Copy(Relative relative) => new()
    {
        Entity = relative.Entity,
        Name = relative.Name,
        Surname = relative.Surname,
        Gender = relative.Gender,
        Dead = relative.Dead,
    };

    private static void Notify(Simulation simulation, LocalizedText text) =>
        simulation.Events.Emit(new NotificationEvent(simulation.CurrentTick, ColonyText.Freeze(text, simulation.World), NotificationSeverity.Info));
}
