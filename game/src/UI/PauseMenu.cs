using Godot;

namespace OurLife.Game.UI;

/// <summary>
/// The pause menu, over the game: time stays stopped while it is open. Save shows only where the
/// game can be saved, and a line under the title tells how the last save went.
/// </summary>
public partial class PauseMenu : Control
{
    public event Action? ResumeRequested;

    public event Action? SaveRequested;

    public event Action? OptionsRequested;

    public event Action? MainMenuRequested;

    /// <param name="canSave">Whether to show Save.</param>
    /// <param name="statusKey">The key of a line under the title, like "Game saved", or null.</param>
    public static PauseMenu Create(bool canSave, string? statusKey = null)
    {
        var menu = new PauseMenu { Name = nameof(PauseMenu) };
        menu.SetAnchorsPreset(LayoutPreset.FullRect);
        var (root, column) = UiBuilder.Screen("Root", dimmed: true);
        menu.AddChild(root);

        column.AddChild(UiBuilder.Title("ui.pause.title"));
        if (statusKey is not null)
        {
            var status = UiBuilder.Caption(statusKey);
            status.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(status);
        }

        column.AddChild(UiBuilder.MenuButton("ui.pause.resume", () => menu.ResumeRequested?.Invoke()));
        if (canSave)
        {
            column.AddChild(UiBuilder.MenuButton("ui.pause.save", () => menu.SaveRequested?.Invoke()));
        }

        column.AddChild(UiBuilder.MenuButton("ui.mainmenu.options", () => menu.OptionsRequested?.Invoke()));
        column.AddChild(UiBuilder.MenuButton("ui.common.main_menu", () => menu.MainMenuRequested?.Invoke()));
        return menu;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            ResumeRequested?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }
}
