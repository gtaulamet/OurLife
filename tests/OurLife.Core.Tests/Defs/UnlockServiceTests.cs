using OurLife.Core.Defs;
using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Defs;

/// <summary>
/// Era hooks from IP1 (ADR-008): content of a later era stays locked in an earlier one, and
/// content that requires knowledge stays locked until the colony learns it.
/// </summary>
public class UnlockServiceTests
{
    private static readonly DefDatabase Content = TestContent.WithDefs("""
        [
          { "type": "Era", "id": "later_age", "order": 1 },
          { "type": "Knowledge", "id": "printing" },
          { "type": "Item", "id": "plank", "kind": "raw", "sprite": "item.plank", "stackLimit": 20 },
          { "type": "Item", "id": "musket", "kind": "raw", "sprite": "item.musket", "stackLimit": 1, "era": "later_age" },
          { "type": "Item", "id": "book", "kind": "raw", "sprite": "item.book", "stackLimit": 10, "requires": ["printing"] }
        ]
        """);

    private readonly UnlockService _unlocks = new(Content);

    [Fact]
    public void A_new_game_starts_in_the_first_era()
    {
        new Simulation(1, Content).Progress.CurrentEra.ShouldBe("test_age");
    }

    [Fact]
    public void Content_without_era_or_requirements_is_always_available()
    {
        _unlocks.IsUnlocked(Content.Get<ItemDef>("plank"), new ProgressState("test_age")).ShouldBeTrue();
    }

    [Fact]
    public void Content_of_a_later_era_is_locked_until_that_era()
    {
        var musket = Content.Get<ItemDef>("musket");

        _unlocks.IsUnlocked(musket, new ProgressState("test_age")).ShouldBeFalse();
        _unlocks.IsUnlocked(musket, new ProgressState("later_age")).ShouldBeTrue();
    }

    [Fact]
    public void Content_that_requires_knowledge_is_locked_until_it_is_learned()
    {
        var book = Content.Get<ItemDef>("book");
        var progress = new ProgressState("test_age");

        _unlocks.IsUnlocked(book, progress).ShouldBeFalse();
        progress.Knowledge.Add("printing");
        _unlocks.IsUnlocked(book, progress).ShouldBeTrue();
    }

    [Fact]
    public void Progress_is_part_of_the_state_fingerprint()
    {
        var before = new Simulation(1, Content);
        var after = new Simulation(1, Content);
        after.Progress.Knowledge.Add("printing");

        after.ComputeStateHash().ShouldNotBe(before.ComputeStateHash());
    }
}
