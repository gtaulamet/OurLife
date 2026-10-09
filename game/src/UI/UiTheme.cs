using Godot;

namespace OurLife.Game.UI;

/// <summary>
/// Global look of the interface. The pixel font is imported without smoothing, hinting,
/// subpixel positioning or system fallback (see <c>Tiny5.ttf.import</c>), so it renders crisp everywhere.
/// </summary>
public static class UiTheme
{
    public const string FontPath = "res://assets/fonts/tiny5/Tiny5.ttf";
    public const int BaseFontSize = 16;

    public static Theme Create() =>
        new() { DefaultFont = GD.Load<FontFile>(FontPath), DefaultFontSize = BaseFontSize };

    /// <summary>
    /// Null when the imported font has the crisp settings. Only the editor and <c>--import</c>
    /// reimport assets, so after a change to the <c>.import</c> file the cache in
    /// <c>.godot/imported</c> can still hold the old, blurry settings, or be missing.
    /// </summary>
    public static string? FontImportProblem()
    {
        var font = GD.Load<FontFile>(FontPath);
        if (font is null)
        {
            return $"{FontPath} could not be loaded. Import the project: godot --headless --path game --import";
        }

        if (font.Antialiasing == TextServer.FontAntialiasing.None
            && font.Hinting == TextServer.Hinting.None
            && font.SubpixelPositioning == TextServer.SubpixelPositioning.Disabled
            && !font.AllowSystemFallback)
        {
            return null;
        }

        return $"{FontPath} was imported with old settings. Reimport it: godot --headless --path game --import";
    }
}
