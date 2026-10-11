using Godot;
using OurLife.Game.Bootstrap;
using OurLife.Game.Session;

namespace OurLife.Game.UI;

/// <summary>The scrolling list of saved games shared by the load and save screens.</summary>
public static class SlotList
{
    private const int Width = 1000;
    private const int Height = 420;
    private const int DetailSize = 20;

    /// <summary>Adds an empty list, inside a scroll box, to <paramref name="column"/>.</summary>
    public static VBoxContainer Create(VBoxContainer column)
    {
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(Width, Height),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        column.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", UiBuilder.Gap);
        scroll.AddChild(list);
        return list;
    }

    public static void Clear(VBoxContainer list)
    {
        foreach (var child in list.GetChildren())
        {
            list.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>Adds the row of a slot, with its texts; returns the row, to add its buttons.</summary>
    public static HBoxContainer AddRow(VBoxContainer list, GameServices services, SaveSlot slot)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UiBuilder.Gap);
        var texts = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var summary = UiBuilder.Text(SlotTexts.Summary(services, slot));
        summary.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        texts.AddChild(summary);
        texts.AddChild(UiBuilder.Text(SlotTexts.Detail(services, slot), DetailSize));
        row.AddChild(texts);
        list.AddChild(row);
        return row;
    }

    /// <summary>A button of a row, whose text is a key.</summary>
    public static Button RowButton(string key, Action pressed)
    {
        var button = new Button { Text = key, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        button.AddThemeFontSizeOverride("font_size", UiBuilder.MenuSize);
        button.Pressed += pressed;
        return button;
    }
}
