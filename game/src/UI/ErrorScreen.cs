using Godot;

namespace OurLife.Game.UI;

/// <summary>
/// Something went wrong: a saved game that cannot be loaded, or a game that cannot start (see
/// "Errores" in <c>docs/gdd/guardado.md</c>). The message comes formatted.
/// </summary>
public partial class ErrorScreen : Control
{
    // Wide enough for a long detail to wrap instead of stretching the screen.
    private const int MessageWidth = 900;

    public event Action? Closed;

    /// <param name="message">The reason, already formatted.</param>
    /// <param name="buttonKey">The key of the only button, like Back or Quit.</param>
    public static ErrorScreen Create(string message, string buttonKey)
    {
        var screen = new ErrorScreen { Name = nameof(ErrorScreen) };
        screen.SetAnchorsPreset(LayoutPreset.FullRect);
        var (root, column) = UiBuilder.Screen("Root", dimmed: false);
        screen.AddChild(root);

        column.AddChild(UiBuilder.Title("ui.error.title"));
        column.AddChild(Message(message));
        column.AddChild(UiBuilder.MenuButton(buttonKey, () => screen.Closed?.Invoke()));
        return screen;
    }

    /// <summary>
    /// The error screen when not even the texts could load: the detail as it is, and a button
    /// with no words, because there is nothing to translate them with.
    /// </summary>
    public static ErrorScreen CreateUntranslated(string detail)
    {
        var screen = new ErrorScreen { Name = nameof(ErrorScreen) };
        screen.SetAnchorsPreset(LayoutPreset.FullRect);
        var (root, column) = UiBuilder.Screen("Root", dimmed: false);
        screen.AddChild(root);

        column.AddChild(Message(detail));
        var close = UiBuilder.MenuButton("X", () => screen.Closed?.Invoke());
        close.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        column.AddChild(close);
        return screen;
    }

    private static Label Message(string text)
    {
        var label = UiBuilder.Text(text);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(MessageWidth, 0);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }
}
