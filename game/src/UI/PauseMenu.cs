using Godot;

namespace OurLife.Game.UI;

/// <summary>The pause menu, over the game: time stays stopped while it is open.</summary>
public partial class PauseMenu : Control
{
    public event Action? ResumeRequested;

    public event Action? OptionsRequested;

    public event Action? MainMenuRequested;

    public static PauseMenu Create()
    {
        var menu = new PauseMenu { Name = nameof(PauseMenu) };
        menu.SetAnchorsPreset(LayoutPreset.FullRect);
        var (root, column) = UiBuilder.Screen("Root", dimmed: true);
        menu.AddChild(root);

        column.AddChild(UiBuilder.Title("ui.pause.title"));
        column.AddChild(UiBuilder.MenuButton("ui.pause.resume", () => menu.ResumeRequested?.Invoke()));
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
