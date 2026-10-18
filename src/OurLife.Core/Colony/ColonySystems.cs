using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>Adds the colony systems to a simulation, in the order of a tick (see <c>docs/architecture/overview.md</c>).</summary>
public static class ColonySystems
{
    /// <exception cref="InvalidOperationException">
    /// The content lacks what the colony needs (see <see cref="ColonyRules.From"/> and <see cref="LifeRules.From"/>).
    /// </exception>
    public static void AddTo(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var rules = ColonyRules.From(simulation.Content);
        var life = LifeRules.From(simulation.Content);
        simulation.AddSystem(new NeedsSystem());
        simulation.AddSystem(new LifeSystem(life, rules), simulation.Time.TicksPerDay, LifeSystem.MidnightOffset(simulation.Time));
        var personalities = PersonalityRules.From(simulation.Content);
        simulation.AddSystem(
            new BirthSystem(life, personalities, rules.Skills, rules.FoodPlants), simulation.Time.TicksPerDay, LifeSystem.MidnightOffset(simulation.Time));
        var storage = new StorageIndex();
        simulation.AddSystem(new DecisionSystem(rules, storage));
        simulation.AddSystem(new JobSystem(rules, storage));
        simulation.AddSystem(new MovementSystem(rules));
        simulation.AddSystem(new SocialSystem(rules, SocialRules.From(simulation.Content), personalities, life));
        var mood = MoodRules.ConfigOf(simulation.Content)
                   ?? throw new InvalidOperationException("The colony needs exactly one MoodConfig def.");
        var (hour, offset) = MoodSystem.Schedule(simulation.Time);
        simulation.AddSystem(new MoodSystem(rules, personalities, life, mood), hour, offset);
        simulation.AddSystem(new PlantGrowthSystem());
        simulation.AddSystem(new AlertSystem(rules));
    }
}
