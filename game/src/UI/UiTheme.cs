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

    /// <summary>Background of a pressed button, so the active option of a toggle row stands out.</summary>
    public static readonly Color PressedColor = new(0.62f, 0.45f, 0.20f);

    public static Theme Create()
    {
        var theme = new Theme { DefaultFont = GD.Load<FontFile>(FontPath), DefaultFontSize = BaseFontSize };

        // The default pressed look is only a shade darker than the normal one, too close on the 3D
        // view and on a dimmed backdrop. Copies keep the default margins, so sizes do not change.
        var defaults = ThemeDB.GetDefaultTheme();
        foreach (var style in new[] { "pressed", "hover_pressed" })
        {
            var box = (StyleBox)defaults.GetStylebox(style, "Button").Duplicate();
            if (box is StyleBoxFlat flat)
            {
                flat.BgColor = style == "pressed" ? PressedColor : PressedColor.Lightened(0.15f);
            }

            theme.SetStylebox(style, "Button", box);
        }

        theme.SetColor("font_pressed_color", "Button", Colors.White);
        theme.SetColor("font_hover_pressed_color", "Button", Colors.White);
        return theme;
    }

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
