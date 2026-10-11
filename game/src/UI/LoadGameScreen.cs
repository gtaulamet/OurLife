using Godot;
using OurLife.Game.Bootstrap;
using OurLife.Game.Session;

namespace OurLife.Game.UI;

/// <summary>
/// The saved games, the most recent first, each with Load and Delete (see "Pantallas" in
/// <c>docs/gdd/guardado.md</c>). The language cannot change while it shows, so its formatted
/// texts are formatted once.
/// </summary>
public partial class LoadGameScreen : Control
{
    private GameServices _services = null!;
    private SaveSlots _slots = null!;
    private VBoxContainer _list = null!;

    /// <summary>Raised with the id of the slot to load.</summary>
    public event Action<string>? LoadRequested;

    public event Action? BackRequested;

    public static LoadGameScreen Create(GameServices services, SaveSlots slots)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(slots);
        var screen = new LoadGameScreen { Name = nameof(LoadGameScreen), _services = services, _slots = slots };
        screen.SetAnchorsPreset(LayoutPreset.FullRect);
        var (root, column) = UiBuilder.Screen("Root", dimmed: false);
        screen.AddChild(root);

        column.AddChild(UiBuilder.Title("ui.load.title"));
        screen._list = SlotList.Create(column);
        column.AddChild(UiBuilder.MenuButton("ui.common.back", () => screen.BackRequested?.Invoke()));
        screen.Fill();
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

    private void Fill()
    {
        SlotList.Clear(_list);
        var slots = _slots.List();
        if (slots.Count == 0)
        {
            _list.AddChild(UiBuilder.Caption("ui.load.empty"));
            return;
        }

        foreach (var slot in slots)
        {
            var row = SlotList.AddRow(_list, _services, slot);
            var load = SlotList.RowButton("ui.load.load", () => LoadRequested?.Invoke(slot.Id));
            load.Disabled = slot.Header is null;
            row.AddChild(load);
            row.AddChild(SlotList.RowButton("ui.load.delete", () => UiBuilder.Confirm(
                this,
                _services.Text.Format("ui.load.confirm_delete", ("name", SlotTexts.Name(slot))),
                () =>
                {
                    _slots.Delete(slot.Id);
                    Fill();
                })));
        }
    }
}
