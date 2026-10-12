using Godot;

namespace OurLife.Game.UI;

/// <summary>
/// A question with Yes and No over a screen. Esc answers No and stops there, so the screen
/// under it does not go back as well.
/// </summary>
public partial class ConfirmDialog : Control
{
    /// <summary>Adds the question over <paramref name="parent"/>: Yes runs <paramref name="yes"/>, both close it.</summary>
    /// <param name="question">Already formatted.</param>
    public static ConfirmDialog Show(Node parent, string question, Action yes)
    {
        ArgumentNullException.ThrowIfNull(parent);
        var dialog = new ConfirmDialog { Name = nameof(ConfirmDialog) };
        dialog.SetAnchorsPreset(LayoutPreset.FullRect);
        var (root, column) = UiBuilder.Screen("Root", dimmed: true);
        dialog.AddChild(root);

        column.AddChild(UiBuilder.Text(question));
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", UiBuilder.Gap);
        buttons.AddChild(UiBuilder.MenuButton("ui.common.yes", () =>
        {
            dialog.QueueFree();
            yes();
        }));
        buttons.AddChild(UiBuilder.MenuButton("ui.common.no", dialog.QueueFree));
        column.AddChild(buttons);
        parent.AddChild(dialog);
        return dialog;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            QueueFree();
            GetViewport().SetInputAsHandled();
        }
    }
}
