namespace OurLife.Game.UI;

/// <summary>
/// How large the interface is drawn for a window (<c>docs/gdd/tamano-de-la-interfaz.md</c>).
/// Sizes are percentages of the design size; <see cref="Auto"/> picks one from the window.
/// Plain integers and no Godot types, so the content tests can check the rules.
/// </summary>
public static class UiScaleRules
{
    /// <summary>The setting that picks the size from the window.</summary>
    public const int Auto = 0;

    /// <summary>The sizes the player can pick, in percent. Steps of 50 keep the pixel font on whole pixels.</summary>
    public static readonly IReadOnlyList<int> Steps = [100, 150, 200, 250, 300];

    // The design size of the interface, which Auto keeps at least.
    private const int DesignWidth = 1280;
    private const int DesignHeight = 720;

    // The smallest canvas a fixed size may leave.
    private const int MinWidth = 960;
    private const int MinHeight = 540;

    /// <summary>Whether a setting is one the game knows: <see cref="Auto"/> or one of the <see cref="Steps"/>.</summary>
    public static bool IsValid(int setting) => setting == Auto || Steps.Contains(setting);

    /// <summary>The largest size that leaves a canvas of the design size, or 100 in a smaller window.</summary>
    public static int AutoFor(int width, int height) =>
        Steps.Where(step => Fits(width, height, step, DesignWidth, DesignHeight)).DefaultIfEmpty(Steps[0]).Max();

    /// <summary>Whether a fixed size leaves a canvas of at least 960×540. 100 % always can.</summary>
    public static bool IsAvailable(int step, int width, int height) =>
        step == Steps[0] || Fits(width, height, step, MinWidth, MinHeight);

    /// <summary>
    /// The size to draw at: Auto's, or the chosen size, or the largest available one below it
    /// when the window is too small for it.
    /// </summary>
    public static int Effective(int setting, int width, int height) =>
        setting == Auto
            ? AutoFor(width, height)
            : Steps.Where(step => step <= setting && IsAvailable(step, width, height)).DefaultIfEmpty(Steps[0]).Max();

    private static bool Fits(int width, int height, int step, int canvasWidth, int canvasHeight) =>
        (long)width * 100 >= (long)canvasWidth * step && (long)height * 100 >= (long)canvasHeight * step;
}
