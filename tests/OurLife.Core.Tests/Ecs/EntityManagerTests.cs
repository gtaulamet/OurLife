using OurLife.Core.Ecs;

namespace OurLife.Core.Tests.Ecs;

public class EntityManagerTests
{
    [Fact]
    public void New_entities_get_consecutive_slots_with_generation_one()
    {
        var manager = new EntityManager();

        manager.Create().ShouldBe(new EntityId(0, 1));
        manager.Create().ShouldBe(new EntityId(1, 1));
        manager.AliveCount.ShouldBe(2);
    }

    [Fact]
    public void Freed_slots_are_reused_first_in_first_out_with_a_new_generation()
    {
        var manager = new EntityManager();
        var first = manager.Create();
        var second = manager.Create();
        manager.Create();

        manager.Destroy(second);
        manager.Destroy(first);

        manager.Create().ShouldBe(new EntityId(1, 2));
        manager.Create().ShouldBe(new EntityId(0, 2));
        manager.Create().ShouldBe(new EntityId(3, 1));
    }

    [Fact]
    public void A_stale_id_is_not_alive_after_its_slot_is_reused()
    {
        var manager = new EntityManager();
        var old = manager.Create();
        manager.Destroy(old);

        var reused = manager.Create();

        reused.Index.ShouldBe(old.Index);
        manager.IsAlive(old).ShouldBeFalse();
        manager.IsAlive(reused).ShouldBeTrue();
    }

    [Fact]
    public void None_is_never_alive()
    {
        new EntityManager().IsAlive(EntityId.None).ShouldBeFalse();
    }

    [Fact]
    public void Destroying_a_dead_entity_throws()
    {
        var manager = new EntityManager();
        var entity = manager.Create();
        manager.Destroy(entity);

        Should.Throw<InvalidOperationException>(() => manager.Destroy(entity));
    }
}
