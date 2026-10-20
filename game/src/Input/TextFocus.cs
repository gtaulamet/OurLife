using Godot;

namespace OurLife.Game.Input;

/// <summary>
/// Whether the player is writing in a text field, like a stock target of the colony panel: then
/// the keys write, and the game's keys (speeds, orders, camera) wait until the field lets go. Also
/// in the frame the field lets go, as the keys of that frame went into it.
/// </summary>
public static class TextFocus
{
    private static ulong _releasedFrame = ulong.MaxValue;

    public static bool IsTyping(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.GetViewport()?.GuiGetFocusOwner() is LineEdit or TextEdit || _releasedFrame == Engine.GetProcessFrames();
    }

    /// <summary>Remembers that a field let go of the keys in this frame.</summary>
    public static void Released() => _releasedFrame = Engine.GetProcessFrames();

    /// <summary>
    /// A click with any button but the wheel, anywhere but on the field being written, lets go of
    /// it, which applies what it holds; the click goes on as usual. A field inside a
    /// <see cref="SpinBox"/> includes its arrows. For the game screen's <c>_Input</c>.
    /// </summary>
    public static void ReleaseOnClickOutside(Node node, InputEvent @event)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right or MouseButton.Middle }
            || node.GetViewport()?.GuiGetFocusOwner() is not { } focused || focused is not (LineEdit or TextEdit))
        {
            return;
        }

        var field = focused.GetParent() is SpinBox spin ? (Control)spin : focused;
        var hovered = node.GetViewport().GuiGetHoveredControl();
        if (hovered is null || (hovered != field && !field.IsAncestorOf(hovered)))
        {
            focused.ReleaseFocus();
        }
    }
}
