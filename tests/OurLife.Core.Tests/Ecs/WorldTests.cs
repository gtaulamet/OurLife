using OurLife.Core.Ecs;

namespace OurLife.Core.Tests.Ecs;

public class WorldTests
{
    [Fact]
    public void Destroying_an_entity_removes_all_its_components()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.Add(entity, new Position());
        world.Add(entity, new Health());

        world.DestroyEntity(entity);

        world.IsAlive(entity).ShouldBeFalse();
        world.Store<Position>().Count.ShouldBe(0);
        world.Store<Health>().Count.ShouldBe(0);
    }

    [Fact]
    public void A_reused_slot_starts_without_components()
    {
        var world = new World();
        var old = world.CreateEntity();
        world.Add(old, new Health { Value = 3 });
        world.DestroyEntity(old);

        var reused = world.CreateEntity();

        reused.Index.ShouldBe(old.Index);
        world.Has<Health>(reused).ShouldBeFalse();
    }

    [Fact]
    public void Adding_a_component_to_a_dead_entity_throws()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.DestroyEntity(entity);

        Should.Throw<InvalidOperationException>(() => world.Add(entity, new Position()));
    }

    [Fact]
    public void Query_with_two_components_returns_entities_having_both_by_ascending_id()
    {
        var world = new World();
        var a = world.CreateEntity();
        var b = world.CreateEntity();
        var c = world.CreateEntity();
        world.Add(c, new Position());
        world.Add(c, new Health());
        world.Add(a, new Position());
        world.Add(a, new Health());
        world.Add(b, new Position());

        var results = new List<EntityId>();
        world.Query<Position, Health>(results);

        results.ShouldBe(new[] { a, c });
    }

    [Fact]
    public void Entities_can_be_destroyed_while_iterating_a_query()
    {
        var world = new World();
        for (var i = 0; i < 3; i++)
        {
            world.Add(world.CreateEntity(), new Position());
        }

        var results = new List<EntityId>();
        world.Query<Position>(results);
        foreach (var entity in results)
        {
            world.DestroyEntity(entity);
        }

        world.Entities.AliveCount.ShouldBe(0);
        world.Store<Position>().Count.ShouldBe(0);
    }

    [Fact]
    public void Components_are_changed_in_place()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.Add(entity, new Health { Value = 10 });

        world.Get<Health>(entity).Value -= 3;

        world.Get<Health>(entity).Value.ShouldBe(7);
    }

    private sealed class Position
    {
        public int X { get; set; }
    }

    private sealed class Health
    {
        public int Value { get; set; }
    }
}
