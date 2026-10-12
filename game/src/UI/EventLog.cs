using Godot;
using OurLife.Core.Sim;
using OurLife.Core.Text;
using OurLife.Game.Bootstrap;

namespace OurLife.Game.UI;

/// <summary>
/// The last messages of the game, newest at the bottom, each with its time, and a line with the
/// date before the first message of each day (see "HUD de la partida" in
/// <c>docs/gdd/interfaz-ip1.md</c> and <c>docs/gdd/dia-y-noche.md</c>). It keeps the
/// <see cref="LocalizedText"/>, not the text, so changing the language translates the whole history.
/// </summary>
public partial class EventLog : PanelContainer
{
    // Lines of the panel, day lines included.
    private const int Shown = 7;

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
            // Only the last ones are ever shown: the log does not grow with the game.
            if (_entries.Count > Shown)
            {
                _entries.RemoveRange(0, _entries.Count - Shown);
            }

            Refresh();
        }
    }

    private void OnLocaleChanged(string locale) => Refresh();

    private static bool SameDay(GameDate a, GameDate b) => a.Year == b.Year && a.SeasonIndex == b.SeasonIndex && a.Day == b.Day;

    private void Refresh()
    {
        // From the newest back, day by day, until the lines fill the panel: each day shows its
        // date and its newest messages, and the panel never grows past its lines.
        var text = _services.Text;
        var lines = new List<string>();
        var end = _entries.Count;
        while (end > 0 && lines.Count + 2 <= Shown)
        {
            var date = _entries[end - 1].Date;
            var start = end;
            while (start > 0 && SameDay(_entries[start - 1].Date, date) && lines.Count + 1 + (end - start + 1) <= Shown)
            {
                start--;
            }

            var block = new List<string> { text.Format("ui.log.day", ("date", text.Date(date))) };
            for (var i = start; i < end; i++)
            {
                block.Add(text.Format("ui.log.entry", ("clock", text.Clock(_entries[i].Date)), ("text", text.Resolve(_entries[i].Text))));
            }

            lines.InsertRange(0, block);
            end = start;
            while (end > 0 && SameDay(_entries[end - 1].Date, date))
            {
                end--;
            }
        }

        _text.Text = string.Join("\n", lines);
        Visible = _entries.Count > 0;
    }
}
