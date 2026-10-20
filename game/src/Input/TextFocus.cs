using Godot;

namespace OurLife.Game.Input;

/// <summary>
/// Whether the player is writing in a text field, like a stock target of the colony panel: then
/// the keys write, and the game's keys (speeds, orders, camera) wait until the field lets go.
/// </summary>
public static class TextFocus
{
    public static bool IsTyping(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.GetViewport()?.GuiGetFocusOwner() is LineEdit or TextEdit;
    }
}
