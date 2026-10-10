using Godot;
using OurLife.Core.Sim;
using OurLife.Core.Text;
using OurLife.Game.Bootstrap;

namespace OurLife.Game.UI;

/// <summary>
/// The last messages of the game, newest at the bottom, each with its time (see "HUD de la
/// partida" in <c>docs/gdd/interfaz-ip1.md</c>). It keeps the <see cref="LocalizedText"/>, not the
/// text, so changing the language translates the whole history.
/// </summary>
public partial class EventLog : PanelContainer
{
    private const int Shown = 6;

    private readonly List<(GameDate Date, LocalizedText Text)> _entries = [];
    private GameServices _services = null!;
    private Label _text = null!;

    public static EventLog Create(GameServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var log = new EventLog { Name = nameof(EventLog), _services = services, MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        log._text = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, MouseFilter = MouseFilterEnum.Ignore };
        log.AddChild(log._text);
        return log;
    }

    public override void _EnterTree() => _services.Localizer.LocaleChanged += OnLocaleChanged;

    public override void _ExitTree() => _services.Localizer.LocaleChanged -= OnLocaleChanged;

    /// <summary>Adds the notifications among the events of a frame.</summary>
    public void Apply(IReadOnlyList<ISimEvent> events, Calendar calendar)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(calendar);
        var added = false;
        foreach (var simEvent in events)
        {
            if (simEvent is NotificationEvent notification)
            {
                _entries.Add((calendar.DateAt(notification.Tick), notification.Text));
                added = true;
            }
        }

        if (added)
        {
            Refresh();
        }
    }

    private void OnLocaleChanged(string locale) => Refresh();

    private void Refresh()
    {
        var text = _services.Text;
        var first = Math.Max(0, _entries.Count - Shown);
        _text.Text = string.Join(
            "\n",
            _entries.Skip(first).Select(entry => text.Format("ui.log.entry", ("clock", text.Clock(entry.Date)), ("text", text.Resolve(entry.Text)))));
        Visible = _entries.Count > 0;
    }
}
