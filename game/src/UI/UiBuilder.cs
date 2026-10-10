using Godot;

namespace OurLife.Game.UI;

/// <summary>
/// Small helpers to build menus in code with the global theme (escenas mínimas, UI en C#). Texts
/// passed as keys are translated by Godot and follow language changes on their own.
/// </summary>
public static class UiBuilder
{
    public const int TitleSize = 48;
    public const int MenuSize = 28;
    public const int Gap = 16;

    /// <summary>
    /// A full-screen root with the theme, a dark backdrop when <paramref name="dimmed"/>, and a
    /// centred column for the content. Returns the root and the column.
    /// </summary>
    public static (Control Root, VBoxContainer Column) Screen(string name, bool dimmed)
    {
        var root = new Control { Name = name, Theme = UiTheme.Create() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var backdrop = new ColorRect { Color = dimmed ? new Color(0, 0, 0, 0.6f) : new Color(0.11f, 0.10f, 0.09f) };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(backdrop);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", Gap);
        center.AddChild(column);
        return (root, column);
    }

    /// <summary>A centred label whose text is a key.</summary>
    public static Label Title(string key, int size = TitleSize)
    {
        var label = new Label { Text = key, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    /// <summary>A label whose text is a key, at the menu size.</summary>
    public static Label Caption(string key)
    {
        var label = new Label { Text = key };
        label.AddThemeFontSizeOverride("font_size", MenuSize);
        return label;
    }

    /// <summary>A menu button whose text is a key.</summary>
    public static Button MenuButton(string key, Action pressed)
    {
        var button = new Button { Text = key, CustomMinimumSize = new Vector2(360, 0) };
        button.AddThemeFontSizeOverride("font_size", MenuSize);
        button.Pressed += pressed;
        return button;
    }
}
