using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Sim;

public class RngStreamsTests
{
    [Fact]
    public void The_same_seed_and_name_give_the_same_numbers()
    {
        var first = new RngStreams(99).Get("ai");
        var second = new RngStreams(99).Get("ai");

        for (var i = 0; i < 50; i++)
        {
            first.NextUInt().ShouldBe(second.NextUInt());
        }
    }

    [Fact]
    public void Different_names_give_different_numbers()
    {
        var streams = new RngStreams(99);

        Draw(streams.Get("ai"), 8).ShouldNotBe(Draw(streams.Get("combat"), 8));
    }

    [Fact]
    public void Different_seeds_give_different_numbers()
    {
        Draw(new RngStreams(1).Get("ai"), 8).ShouldNotBe(Draw(new RngStreams(2).Get("ai"), 8));
    }

    [Fact]
    public void Drawing_from_another_stream_does_not_change_a_stream()
    {
        var quiet = new RngStreams(99);
        var busy = new RngStreams(99);
        busy.Get("worldgen").NextUInt();
        busy.Get("worldgen").NextUInt();

        Draw(quiet.Get("ai"), 20).ShouldBe(Draw(busy.Get("ai"), 20));
    }

    [Fact]
    public void Get_returns_the_same_generator_each_time()
    {
        var streams = new RngStreams(5);

        streams.Get("plants").ShouldBeSameAs(streams.Get("plants"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("AI")]
    [InlineData("with space")]
    [InlineData("guión")]
    public void Invalid_names_are_rejected(string name)
    {
        Should.Throw<ArgumentException>(() => new RngStreams(5).Get(name));
    }

    private static uint[] Draw(Pcg32 rng, int count) =>
        Enumerable.Range(0, count).Select(_ => rng.NextUInt()).ToArray();
}
