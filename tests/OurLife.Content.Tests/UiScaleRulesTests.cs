using OurLife.Game.UI;

namespace OurLife.Content.Tests;

/// <summary>The size of the interface for each window (<c>docs/gdd/tamano-de-la-interfaz.md</c>).</summary>
public class UiScaleRulesTests
{
    [Theory]
    [InlineData(1280, 720, 100)]
    [InlineData(1919, 1079, 100)]
    [InlineData(1920, 1080, 150)]
    [InlineData(2560, 1440, 200)]
    [InlineData(3200, 1800, 250)]
    [InlineData(3840, 2160, 300)]
    [InlineData(800, 600, 100)]
    public void Auto_keeps_a_canvas_of_the_design_size(int width, int height, int expected)
    {
        UiScaleRules.AutoFor(width, height).ShouldBe(expected);
        UiScaleRules.Effective(UiScaleRules.Auto, width, height).ShouldBe(expected);
    }

    [Fact]
    public void A_wide_but_short_window_takes_the_size_its_height_allows()
    {
        UiScaleRules.AutoFor(3840, 1080).ShouldBe(150);
    }

    [Fact]
    public void Fixed_sizes_leave_a_canvas_of_at_least_960_by_540()
    {
        UiScaleRules.Steps.Where(step => UiScaleRules.IsAvailable(step, 1280, 720)).ShouldBe([100]);
        UiScaleRules.Steps.Where(step => UiScaleRules.IsAvailable(step, 1920, 1080)).ShouldBe([100, 150, 200]);
        UiScaleRules.Steps.Where(step => UiScaleRules.IsAvailable(step, 3840, 2160)).ShouldBe(UiScaleRules.Steps);
    }

    [Fact]
    public void A_size_too_large_for_the_window_falls_back_to_the_largest_that_fits()
    {
        UiScaleRules.Effective(300, 1920, 1080).ShouldBe(200);
        UiScaleRules.Effective(250, 1280, 720).ShouldBe(100);
        UiScaleRules.Effective(150, 3840, 2160).ShouldBe(150);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(150, true)]
    [InlineData(125, false)]
    [InlineData(-1, false)]
    public void Only_auto_and_the_steps_are_valid(int setting, bool valid)
    {
        UiScaleRules.IsValid(setting).ShouldBe(valid);
    }
}
