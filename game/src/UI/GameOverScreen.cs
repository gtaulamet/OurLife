using Godot;

namespace OurLife.Game.UI;

/// <summary>Shown when the last colonist dies: the game is over.</summary>
public partial class GameOverScreen : Control
{
    public event Action? MainMenuRequested;

    public static GameOverScreen Create()
    {
        var screen = new GameOverScreen { Name = nameof(GameOverScreen) };
        screen.SetAnchorsPreset(LayoutPreset.FullRect);
        var (root, column) = UiBuilder.Screen("Root", dimmed: true);
        screen.AddChild(root);

        column.AddChild(UiBuilder.Title("ui.gameover.title"));
        column.AddChild(UiBuilder.MenuButton("ui.common.main_menu", () => screen.MainMenuRequested?.Invoke()));
        return screen;
    }
}
