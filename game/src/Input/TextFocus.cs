using Godot;

namespace OurLife.Game.Input;

/// <summary>
/// Whether the player is writing in a text field, like a stock target of the colony panel: then
/// the keys write, and the game's keys (speeds, orders, camera) wait until the field lets go. Also
/// in the frame after one with a field written in, as a field let go of by a key or a click of
/// this frame took the keys before it, whatever field it is.
/// </summary>
public static class TextFocus
{
    // The last frame a field was seen written in; the game asks every frame.
    private static ulong _typingFrame = ulong.MaxValue;

    public static bool IsTyping(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var frame = Engine.GetProcessFrames();
        if (node.GetViewport()?.GuiGetFocusOwner() is LineEdit or TextEdit)
        {
            _typingFrame = frame;
            return true;
        }

        return _typingFrame != ulong.MaxValue && frame - _typingFrame <= 1;
    }

    /// <summary>
    /// A click with any button but the wheel, anywhere but on the field being written, lets go of
    /// it, which applies what it holds; the click goes on as usual. For the game screen's <c>_Input</c>.
    /// </summary>
    public static void ReleaseOnClickOutside(Node node, InputEvent @event)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right or MouseButton.Middle }
            || node.GetViewport()?.GuiGetFocusOwner() is not { } focused || focused is not (LineEdit or TextEdit))
        {
            return;
        }

        var hovered = node.GetViewport().GuiGetHoveredControl();
        if (hovered is null || (hovered != focused && !focused.IsAncestorOf(hovered)))
        {
            focused.ReleaseFocus();
        }
    }
}
