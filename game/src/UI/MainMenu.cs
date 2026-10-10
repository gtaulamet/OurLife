using Godot;

namespace OurLife.Game.UI;

/// <summary>The main menu (see "Pantallas" in <c>docs/gdd/interfaz-ip1.md</c>).</summary>
public partial class MainMenu : Control
{
    public event Action? NewGameRequested;

    public event Action? OptionsRequested;

    public event Action? QuitRequested;

    public static MainMenu Create()
    {
        var menu = new MainMenu { Name = nameof(MainMenu) };
        menu.SetAnchorsPreset(LayoutPreset.FullRect);
        var (root, column) = UiBuilder.Screen("Root", dimmed: false);
        menu.AddChild(root);

        column.AddChild(UiBuilder.Title("ui.game.title", 72));
        column.AddChild(UiBuilder.MenuButton("ui.mainmenu.new_game", () => menu.NewGameRequested?.Invoke()));

        // Saving and loading arrive in IP2: the button is there, but disabled.
        var load = UiBuilder.MenuButton("ui.mainmenu.load_game", () => { });
        load.Disabled = true;
        load.TooltipText = "ui.mainmenu.load_later";
        column.AddChild(load);

        column.AddChild(UiBuilder.MenuButton("ui.mainmenu.options", () => menu.OptionsRequested?.Invoke()));
        column.AddChild(UiBuilder.MenuButton("ui.mainmenu.quit", () => menu.QuitRequested?.Invoke()));
        return menu;
    }
}
