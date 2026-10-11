using Godot;
using OurLife.Game.Bootstrap;
using OurLife.Game.Session;

namespace OurLife.Game.UI;

/// <summary>
/// Saving from the pause menu: a new slot, or a manual slot to replace after a question (see
/// "Pantallas" in <c>docs/gdd/guardado.md</c>). The game is stopped while it shows, and the
/// language cannot change, so its formatted texts are formatted once.
/// </summary>
public partial class SaveGameScreen : Control
{
    /// <summary>Raised with the id of the slot to save in: a new one, or one the player agreed to replace.</summary>
    public event Action<string>? SaveRequested;

    public event Action? BackRequested;

    public static SaveGameScreen Create(GameServices services, SaveSlots slots)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(slots);
        var screen = new SaveGameScreen { Name = nameof(SaveGameScreen) };
        screen.SetAnchorsPreset(LayoutPreset.FullRect);
        var (root, column) = UiBuilder.Screen("Root", dimmed: true);
        screen.AddChild(root);

        column.AddChild(UiBuilder.Title("ui.save.title"));
        column.AddChild(UiBuilder.MenuButton("ui.save.new_slot", () => screen.SaveRequested?.Invoke(slots.NewSlotId())));
        var list = SlotList.Create(column);
        foreach (var slot in slots.List().Where(slot => slot.Header is { IsAutosave: false }))
        {
            var row = SlotList.AddRow(list, services, slot);
            row.AddChild(SlotList.RowButton("ui.save.overwrite", () => UiBuilder.Confirm(
                screen,
                services.Text.Format("ui.save.confirm_overwrite", ("name", SlotTexts.Name(slot))),
                () => screen.SaveRequested?.Invoke(slot.Id))));
        }

        column.AddChild(UiBuilder.MenuButton("ui.common.back", () => screen.BackRequested?.Invoke()));
        return screen;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            BackRequested?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }
}
