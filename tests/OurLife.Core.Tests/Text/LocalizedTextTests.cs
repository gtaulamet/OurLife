using OurLife.Core.Ecs;
using OurLife.Core.Text;

namespace OurLife.Core.Tests.Text;

public class LocalizedTextTests
{
    [Theory]
    [InlineData("ui.mainmenu.new_game")]
    [InlineData("event.colonist.starved")]
    [InlineData("def.plant.oak.name")]
    public void Keys_following_the_convention_are_accepted(string key)
    {
        LocalizedText.Of(key).Key.ShouldBe(key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nodots")]
    [InlineData("UI.MainMenu")]
    [InlineData("ui..double")]
    [InlineData("ui.with space")]
    [InlineData("ui.trailing.")]
    public void Keys_breaking_the_convention_are_rejected(string key)
    {
        Should.Throw<ArgumentException>(() => LocalizedText.Of(key));
    }

    [Fact]
    public void With_returns_a_new_text_and_leaves_the_original_untouched()
    {
        var original = LocalizedText.Of("event.need.exhausted");

        var withColonist = original.With("colonist", new EntityId(3, 1));

        original.Args.ShouldBeEmpty();
        withColonist.Args["colonist"].ShouldBe(new EntityId(3, 1));
        withColonist.Key.ShouldBe(original.Key);
    }

    [Fact]
    public void Arguments_are_sorted_by_name()
    {
        var text = LocalizedText.Of("ui.items.count").With("zeta", 1).With("alpha", 2);

        text.Args.Keys.ShouldBe(new[] { "alpha", "zeta" });
    }

    [Theory]
    [InlineData("Colonist")]
    [InlineData("1st")]
    [InlineData("with-dash")]
    public void Invalid_argument_names_are_rejected(string name)
    {
        Should.Throw<ArgumentException>(() => LocalizedText.Of("ui.items.count").With(name, 1));
    }
}
