using Godot;

namespace OurLife.Game.UI;

/// <summary>
/// Small helpers to build menus in code with the global theme: scenes stay minimal and the UI is
/// built in C#. Texts passed as keys are translated by Godot and follow language changes on their own.
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

    /// <summary>
    /// A label with text already formatted, which Godot must not translate. Whoever builds it
    /// formats it again if the language can change while it shows.
    /// </summary>
    public static Label Text(string text, int size = MenuSize)
    {
        var label = new Label { Text = text, AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    /// <summary>A menu button whose text is a key. It keeps its width, centred, in a wider column.</summary>
    public static Button MenuButton(string key, Action pressed)
    {
        var button = new Button { Text = key, CustomMinimumSize = new Vector2(360, 0), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        button.AddThemeFontSizeOverride("font_size", MenuSize);
        button.Pressed += pressed;
        return button;
    }

    // What the bottom of the canvas keeps for itself: the event log and the orders bar.
    public const float BottomReserve = 205;

    /// <summary>
    /// Sizes a scroll to its content, up to the room a panel has: under the top bar and over the event log and the orders
    /// bar, and not past the right edge of the canvas. A panel that would reach the log scrolls instead
    /// (docs/gdd/tamano-de-la-interfaz.md). Call it again when the content changes or the panel is laid out anew.
    /// </summary>
    public static void FitToCanvas(ScrollContainer scroll, Control content)
    {
        var canvas = scroll.GetViewportRect().Size;
        var origin = scroll.GetGlobalRect().Position;
        var wanted = content.GetCombinedMinimumSize();
        var room = new Vector2(Mathf.Max(240f, canvas.X - origin.X - 24f), Mathf.Max(120f, canvas.Y - origin.Y - BottomReserve));

        // A scroll bar takes some room of its own.
        var size = new Vector2(Mathf.Min(wanted.X + (wanted.Y > room.Y ? 12f : 0f), room.X), Mathf.Min(wanted.Y + (wanted.X > room.X ? 12f : 0f), room.Y));
        if (scroll.CustomMinimumSize != size)
        {
            scroll.CustomMinimumSize = size;
        }
    }
}
