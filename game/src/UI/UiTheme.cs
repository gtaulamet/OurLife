using Godot;

namespace OurLife.Game.UI;

/// <summary>Global look of the interface: pixel font without smoothing.</summary>
public static class UiTheme
{
    public const string FontPath = "res://assets/fonts/tiny5/Tiny5.ttf";
    public const int BaseFontSize = 16;

    public static Theme Create()
    {
        var font = GD.Load<FontFile>(FontPath);
        font.Antialiasing = TextServer.FontAntialiasing.None;
        font.Hinting = TextServer.Hinting.None;
        font.SubpixelPositioning = TextServer.SubpixelPositioning.Disabled;
        return new Theme { DefaultFont = font, DefaultFontSize = BaseFontSize };
    }
}
