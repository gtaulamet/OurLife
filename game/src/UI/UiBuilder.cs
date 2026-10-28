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

    // Until the HUD has measured it: about what the event log and one bar keep for themselves.
    private const float DefaultBottomReserve = 205;

    /// <summary>
    /// Where the bottom of the canvas starts (the event log and the rows above it that come and go: hint, build bar, farm bar),
    /// measured by the HUD every frame. NaN until it has.
    /// </summary>
    public static float BottomEdge { get; set; } = float.NaN;

    /// <summary>
    /// Sizes a scroll to its content, up to the room a panel has: under the top bar and over the bottom bars and the event
    /// log (<see cref="BottomEdge"/>), and not past the right edge of the canvas. A panel that would not fit scrolls instead,
    /// down and, when the content is wider than the room, sideways too (docs/gdd/tamano-de-la-interfaz.md). Call it again when
    /// the content changes or the panel is laid out anew. <paramref name="below"/> is what the panel keeps under the scroll.
    /// </summary>
    public static void FitToCanvas(ScrollContainer scroll, Control content, float below = 0f)
    {
        var canvas = scroll.GetViewportRect().Size;
        var origin = scroll.GetGlobalRect().Position;
        var wanted = content.GetCombinedMinimumSize();
        var bottom = float.IsNaN(BottomEdge) ? canvas.Y - DefaultBottomReserve : BottomEdge;
        var room = new Vector2(Mathf.Max(240f, canvas.X - origin.X - 24f), Mathf.Max(120f, bottom - origin.Y - below));

        // A scroll bar takes some room of its own, and only the one that shows.
        var sideways = wanted.X > room.X;
        var down = wanted.Y > room.Y;
        var mode = sideways ? ScrollContainer.ScrollMode.Auto : ScrollContainer.ScrollMode.Disabled;
        if (scroll.HorizontalScrollMode != mode)
        {
            scroll.HorizontalScrollMode = mode;
        }

        var size = new Vector2(Mathf.Min(wanted.X + (down ? 12f : 0f), room.X), Mathf.Min(wanted.Y + (sideways ? 12f : 0f), room.Y));
        if (scroll.CustomMinimumSize != size)
        {
            scroll.CustomMinimumSize = size;
        }
    }
}
