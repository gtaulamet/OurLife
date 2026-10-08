using OurLife.Core.Sim;

namespace OurLife.Core.Tests.Sim;

public class Pcg32Tests
{
    [Fact]
    public void Matches_the_output_of_the_reference_implementation()
    {
        // pcg32-demo from pcg-c-basic: pcg32_srandom_r(&rng, 42u, 54u), first six numbers.
        var rng = new Pcg32(42, 54);
        uint[] expected = [0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e];

        foreach (var value in expected)
        {
            rng.NextUInt().ShouldBe(value);
        }
    }

    [Fact]
    public void A_restored_generator_continues_the_same_sequence()
    {
        var original = new Pcg32(7, 3);
        original.NextUInt();
        original.NextUInt();

        var restored = Pcg32.FromState(original.State, original.Increment);

        for (var i = 0; i < 100; i++)
        {
            restored.NextUInt().ShouldBe(original.NextUInt());
        }
    }

    [Fact]
    public void An_even_increment_is_rejected()
    {
        Should.Throw<ArgumentException>(() => Pcg32.FromState(1, 2));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(1_000)]
    public void Bounded_numbers_stay_in_range(int bound)
    {
        var rng = new Pcg32(1, 1);
        for (var i = 0; i < 10_000; i++)
        {
            rng.NextInt(bound).ShouldBeInRange(0, bound - 1);
        }
    }

    [Fact]
    public void Bounded_numbers_cover_the_whole_range()
    {
        var rng = new Pcg32(2, 2);
        var seen = new HashSet<int>();
        for (var i = 0; i < 6_000; i++)
        {
            seen.Add(rng.NextInt(-1, 5));
        }

        seen.Order().ShouldBe(new[] { -1, 0, 1, 2, 3, 4 });
    }

    [Fact]
    public void Chance_respects_the_extremes()
    {
        var rng = new Pcg32(3, 3);
        for (var i = 0; i < 1_000; i++)
        {
            rng.Chance(0).ShouldBeFalse();
            rng.Chance(10_000).ShouldBeTrue();
        }
    }

    [Fact]
    public void Chance_is_close_to_its_probability()
    {
        var rng = new Pcg32(4, 4);
        var hits = 0;
        for (var i = 0; i < 100_000; i++)
        {
            if (rng.Chance(2_500))
            {
                hits++;
            }
        }

        hits.ShouldBeInRange(24_000, 26_000);
    }

    [Fact]
    public void Empty_ranges_are_rejected()
    {
        var rng = new Pcg32(5, 5);

        Should.Throw<ArgumentOutOfRangeException>(() => rng.NextInt(0));
        Should.Throw<ArgumentOutOfRangeException>(() => rng.NextInt(5, 5));
        Should.Throw<ArgumentOutOfRangeException>(() => rng.NextUInt(0));
    }
}
