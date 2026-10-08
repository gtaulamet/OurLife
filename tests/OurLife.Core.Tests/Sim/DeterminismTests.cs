using OurLife.Core.Ecs;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Sim;

/// <summary>
/// Milestone H1.2 acceptance: same seed and same commands give the same state.
/// A toy system exercises everything determinism depends on: random streams, entity
/// creation and destruction with slot reuse, and components changed in place.
/// </summary>
public class DeterminismTests
{
    [Fact]
    public void The_same_seed_gives_the_same_state_after_10000_ticks()
    {
        RunAndHash(seed: 42, ticks: 10_000).ShouldBe(RunAndHash(seed: 42, ticks: 10_000));
    }

    [Fact]
    public void Different_seeds_give_different_states()
    {
        RunAndHash(seed: 42, ticks: 2_000).ShouldNotBe(RunAndHash(seed: 43, ticks: 2_000));
    }

    [Fact]
    public void One_more_tick_changes_the_state()
    {
        RunAndHash(seed: 42, ticks: 1_000).ShouldNotBe(RunAndHash(seed: 42, ticks: 1_001));
    }

    [Fact]
    public void The_same_commands_at_the_same_ticks_give_the_same_state()
    {
        static void SpawnEvery500Ticks(Simulation simulation)
        {
            if (simulation.CurrentTick % 500 == 0)
            {
                simulation.Commands.Enqueue(new SpawnCommand((int)(simulation.CurrentTick % 96), 5));
            }
        }

        var withCommands = RunAndHash(seed: 7, ticks: 3_000, SpawnEvery500Ticks);

        withCommands.ShouldBe(RunAndHash(seed: 7, ticks: 3_000, SpawnEvery500Ticks));
        withCommands.ShouldNotBe(RunAndHash(seed: 7, ticks: 3_000));
    }

    [Fact]
    public void The_scenario_really_reuses_entity_slots()
    {
        // Guards the tests above: without slot reuse they would prove much less.
        var simulation = NewSimulation(seed: 42);

        simulation.Run(10_000);

        simulation.World.Entities.Generations.ShouldContain(generation => Math.Abs(generation) > 1);
        simulation.World.Entities.AliveCount.ShouldBeGreaterThan(0);
    }

    private static Simulation NewSimulation(ulong seed)
    {
        var simulation = new Simulation(seed, TestContent.Minimal);
        simulation.AddSystem(new ChurnSystem());
        return simulation;
    }

    private static ulong RunAndHash(ulong seed, int ticks, Action<Simulation>? beforeEachTick = null)
    {
        var simulation = NewSimulation(seed);
        for (var i = 0; i < ticks; i++)
        {
            beforeEachTick?.Invoke(simulation);
            simulation.Tick();
        }

        return simulation.ComputeStateHash();
    }

    private sealed class Position
    {
        public int X { get; set; }

        public int Y { get; set; }
    }

    private sealed class Age
    {
        public int Ticks { get; set; }
    }

    private sealed class ChurnSystem : ISimSystem
    {
        private readonly List<EntityId> _entities = [];

        public void Tick(Simulation simulation)
        {
            var world = simulation.World;
            var rng = simulation.Random.Get("test.churn");

            if (rng.Chance(3_000))
            {
                var entity = world.CreateEntity();
                world.Add(entity, new Position { X = rng.NextInt(96), Y = rng.NextInt(96) });
                world.Add(entity, new Age());
            }

            world.Query<Position, Age>(_entities);
            foreach (var entity in _entities)
            {
                var position = world.Get<Position>(entity);
                position.X = Math.Clamp(position.X + rng.NextInt(-1, 2), 0, 95);
                position.Y = Math.Clamp(position.Y + rng.NextInt(-1, 2), 0, 95);

                var age = world.Get<Age>(entity);
                age.Ticks++;
                if (age.Ticks > 200 && rng.Chance(100))
                {
                    world.DestroyEntity(entity);
                }
            }
        }
    }

    private sealed record SpawnCommand(int X, int Y) : ISimCommand
    {
        public void Apply(Simulation simulation)
        {
            var entity = simulation.World.CreateEntity();
            simulation.World.Add(entity, new Position { X = X, Y = Y });
            simulation.World.Add(entity, new Age());
        }
    }
}
