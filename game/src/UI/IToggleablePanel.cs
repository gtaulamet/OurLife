using Godot;

namespace OurLife.Game.UI;

/// <summary>
/// A panel of the left column that the top bar and its key open and close, one at a time (see <see cref="Hud.TogglePanel"/>).
/// Every one of them is a control: its visibility and rectangle come from <see cref="Control"/>.
/// </summary>
public interface IToggleablePanel
{
    /// <summary>Whether the panel shows now.</summary>
    bool Visible { get; }

    /// <summary>The panel's rectangle on the canvas.</summary>
    Rect2 GetGlobalRect();

    /// <summary>Opens the panel (building what it shows) or closes it.</summary>
    void Toggle();
}
