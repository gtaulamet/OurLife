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
/// unless one of them is hungry (food is what limits the colony) or the mother has a child too young. Conception is abstract: it is
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
    private readonly IReadOnlySet<string> _foodPlantDefs;
    private readonly List<EntityId> _colonists = [];
    private readonly List<EntityId> _plants = [];

    // Neither the colony's food nor its plants change while the women are visited: each is
    // counted once per midnight, when a woman first gets that far.
    private long? _foodNutrition;
    private long? _foodPlants;

    /// <param name="foodPlants">Ids of the plant defs that give food (see <see cref="ColonyRules.FoodPlants"/>).</param>
    public BirthSystem(LifeRules life, PersonalityRules personalities, SkillRules skills, IReadOnlySet<string> foodPlants)
    {
        ArgumentNullException.ThrowIfNull(life);
        ArgumentNullException.ThrowIfNull(personalities);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(foodPlants);
        _life = life;
        _personalities = personalities;
        _skills = skills;
        _foodPlantDefs = foodPlants;
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
        _foodNutrition = null;
        _foodPlants = null;

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
        if (father.IsNone || !_life.CouldConceive(simulation, mother, father) || TooHungry(simulation, mother, birth) || TooHungry(simulation, father, birth)
            || !HasFoodReserve(simulation, mother, birth) || !LandFeedsOneMore(simulation, birth))
        {
            return;
        }

        if (simulation.Random.Get(RandomStream).NextInt(LifeRules.Certain) >= _life.ConceptionChance(pace, simulation.Time.DaysPerYear))
        {
            return;
        }

        simulation.World.Add(mother, new Pregnancy { Father = Relationships.RelativeOf(simulation, father) });
        simulation.Events.Emit(new EntityChangedEvent(mother));
        Notify(simulation, LocalizedText.Of(PregnantKey).With("colonist", mother));
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
        var father = !pregnancy.Father.Dead && Relationships.IsLivingColonist(world, pregnancy.Father.Entity) ? pregnancy.Father.Entity : EntityId.None;
        var gender = random.NextBool() ? Gender.Female : Gender.Male;

        var baby = world.CreateEntity();
        var cell = world.Get<Position>(mother).Cell;
        world.Add(baby, new Position { X = cell.X, Y = cell.Y, Z = cell.Z });
        world.Add(baby, new Colonist
        {
            Name = ColonyStart.PickName(simulation.Content, creature, gender, random, LivingNames(world)),
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
            Father = pregnancy.Father,
            Mother = Relationships.RelativeOf(simulation, mother),
        });
        world.Remove<Pregnancy>(mother);

        foreach (var parent in father.IsNone ? [mother] : new[] { mother, father })
        {
            Relationships.FamilyOf(world, parent).Children.Add(Relationships.RelativeOf(simulation, baby));
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

    /// <summary>
    /// The colonists, the children on the way and the one to be conceived: the mouths the colony
    /// must feed if this woman conceives. A birth earlier in the same midnight leaves it as it was.
    /// </summary>
    private static long MouthsWithOneMore(World world) =>
        (long)world.Store<Colonist>().Count + world.Store<Pregnancy>().Count + 1;

    /// <summary>
    /// Hunger keeps a couple from conceiving: under <see cref="BirthConfig.HungryBelowPercent"/> of
    /// the need, or under its low threshold without it.
    /// </summary>
    private static bool TooHungry(Simulation simulation, EntityId colonist, BirthConfig birth)
    {
        if (birth.HungryBelowPercent is not { } percent)
        {
            return Hunger.IsLow(simulation, colonist);
        }

        foreach (var state in simulation.World.Get<Needs>(colonist).Values)
        {
            var need = simulation.Content.Get<NeedDef>(state.Need);
            if (need.SatisfiedBy == NeedSatisfaction.Eating && (long)state.Value * 100 < (long)need.Max * percent)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the colony has food for <see cref="BirthConfig.FoodReserveDays"/> for each mouth
    /// (see <see cref="MouthsWithOneMore"/>), at the appetite of an adult like the mother: a child
    /// is not born to starve in the next winter.
    /// </summary>
    private bool HasFoodReserve(Simulation simulation, EntityId mother, BirthConfig birth)
    {
        if (birth.FoodReserveDays == 0 || !simulation.World.TryGet<Needs>(mother, out var needs))
        {
            return true;
        }

        // One unit of food fills every need eating satisfies (see Hunger.UnitsToFeed): the
        // fastest of them sets the appetite.
        long perDay = 0;
        foreach (var state in needs.Values)
        {
            var need = simulation.Content.Get<NeedDef>(state.Need);
            perDay = need.SatisfiedBy == NeedSatisfaction.Eating ? Math.Max(perDay, (long)need.DecayPerHour * 24) : perDay;
        }

        _foodNutrition ??= ColonyStock.FoodNutrition(simulation);
        return _foodNutrition >= perDay * birth.FoodReserveDays * MouthsWithOneMore(simulation.World);
    }

    /// <summary>
    /// Whether the map has <see cref="BirthConfig.FoodPlantsPerColonist"/> plants that give food
    /// for each mouth (see <see cref="MouthsWithOneMore"/>).
    /// </summary>
    private bool LandFeedsOneMore(Simulation simulation, BirthConfig birth)
    {
        if (birth.FoodPlantsPerColonist == 0)
        {
            return true;
        }

        var world = simulation.World;
        if (_foodPlants is null)
        {
            world.Query<Plant>(_plants);
            var count = 0;
            foreach (var plant in _plants)
            {
                count += _foodPlantDefs.Contains(world.Get<Plant>(plant).Def) ? 1 : 0;
            }

            _foodPlants = count;
        }

        return _foodPlants >= birth.FoodPlantsPerColonist * MouthsWithOneMore(world);
    }

    private static Personality PersonalityOf(World world, EntityId colonist) =>
        !colonist.IsNone && world.TryGet<Personality>(colonist, out var personality) ? personality : new Personality();

    /// <summary>The names the living colonists bear: a newborn gets another while the list has some.</summary>
    private static HashSet<string> LivingNames(World world)
    {
        var colonists = new List<EntityId>();
        world.Query<Colonist>(colonists);
        return colonists.Select(colonist => world.Get<Colonist>(colonist).Name).ToHashSet(StringComparer.Ordinal);
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

    private static void Notify(Simulation simulation, LocalizedText text) =>
        simulation.Events.Emit(new NotificationEvent(simulation.CurrentTick, ColonyText.Freeze(text, simulation.World), NotificationSeverity.Info));
}
