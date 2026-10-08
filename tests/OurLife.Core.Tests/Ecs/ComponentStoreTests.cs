using OurLife.Core.Ecs;

namespace OurLife.Core.Tests.Ecs;

public class ComponentStoreTests
{
    [Fact]
    public void Owners_are_listed_by_ascending_id_whatever_the_insertion_order()
    {
        var store = new ComponentStore<Tag>();
        store.Add(new EntityId(5, 1), new Tag());
        store.Add(new EntityId(1, 1), new Tag());
        store.Add(new EntityId(3, 2), new Tag());

        var owners = new List<EntityId>();
        store.CopyEntitiesTo(owners);

        owners.ShouldBe(new[] { new EntityId(1, 1), new EntityId(3, 2), new EntityId(5, 1) });
    }

    [Fact]
    public void A_stale_id_does_not_see_the_component_of_the_current_owner()
    {
        var store = new ComponentStore<Tag>();
        store.Add(new EntityId(2, 2), new Tag { Value = 7 });

        store.Has(new EntityId(2, 1)).ShouldBeFalse();
        store.TryGet(new EntityId(2, 1), out _).ShouldBeFalse();
        store.Get(new EntityId(2, 2)).Value.ShouldBe(7);
    }

    [Fact]
    public void Adding_a_second_component_of_the_same_type_throws()
    {
        var store = new ComponentStore<Tag>();
        store.Add(new EntityId(0, 1), new Tag());

        Should.Throw<InvalidOperationException>(() => store.Add(new EntityId(0, 1), new Tag()));
    }

    [Fact]
    public void Adding_to_none_throws()
    {
        Should.Throw<ArgumentException>(() => new ComponentStore<Tag>().Add(EntityId.None, new Tag()));
    }

    [Fact]
    public void Remove_reports_whether_something_was_removed_and_keeps_the_order()
    {
        var store = new ComponentStore<Tag>();
        store.Add(new EntityId(1, 1), new Tag());
        store.Add(new EntityId(2, 1), new Tag());
        store.Add(new EntityId(3, 1), new Tag());

        store.Remove(new EntityId(2, 1)).ShouldBeTrue();
        store.Remove(new EntityId(2, 1)).ShouldBeFalse();

        var owners = new List<EntityId>();
        store.CopyEntitiesTo(owners);
        owners.ShouldBe(new[] { new EntityId(1, 1), new EntityId(3, 1) });
        store.Count.ShouldBe(2);
    }

    [Fact]
    public void Getting_a_missing_component_throws_a_clear_error()
    {
        var error = Should.Throw<KeyNotFoundException>(() => new ComponentStore<Tag>().Get(new EntityId(4, 1)));

        error.Message.ShouldContain("Tag");
    }

    private sealed class Tag
    {
        public int Value { get; set; }
    }
}
