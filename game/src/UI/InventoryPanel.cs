using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Entities;
using OurLife.Game.Bootstrap;
using OurLife.Game.Session;

namespace OurLife.Game.UI;

/// <summary>
/// What the colony has (see "Panel de inventario" in <c>docs/gdd/oficios.md</c>): a block per category of
/// items, in the order of the content, with a row per item and quality: how many units there are and, for
/// what goes bad, how fresh they are on average. It reads <see cref="Stocks.Totals"/>, the query the HUD, the
/// stock targets and the production orders share. It refreshes a few times per second while open and is
/// built again only when something it shows changes. Long lists scroll instead of growing past the event log.
/// </summary>
public partial class InventoryPanel : PanelContainer
{
    private const double RefreshSeconds = 0.5;
    private const int NameWidth = 170;
    private const int CountWidth = 80;
    private const int QualityWidth = 100;
    private const int FreshWidth = 120;
    private const int ColumnGap = 8;

    // Under these shares of freshness the row is yellow, and red: the food is about to go bad.
    private const int WarnPercent = 40;
    private const int DangerPercent = 15;
    private static readonly Color WarningColor = new(1f, 0.85f, 0.3f);
    private static readonly Color DangerColor = new(1f, 0.4f, 0.35f);

    private readonly List<string> _rows = [];
    private GameServices _services = null!;
    private GameSession _session = null!;
    private VBoxContainer _list = null!;
    private ScrollContainer _scroll = null!;
    private string _signature = string.Empty;
    private double _sinceRefresh;

    public static InventoryPanel Create(GameServices services, GameSession session)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(session);
        var panel = new InventoryPanel { Name = nameof(InventoryPanel), _services = services, _session = session, Visible = false };
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        panel.AddChild(column);
        column.AddChild(new Label { Text = "ui.inventory.title" });

        // The columns' names, above the scroll so they stay in view.
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", ColumnGap);
        column.AddChild(head);
        foreach (var (key, width, right) in new[] { ("ui.inventory.col.item", NameWidth, false), ("ui.inventory.col.amount", CountWidth, true), ("ui.inventory.col.quality", QualityWidth, false), ("ui.inventory.col.freshness", FreshWidth, false) })
        {
            var label = new Label { Text = key, CustomMinimumSize = new Vector2(width, 0), HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left };
            label.AddThemeColorOverride("font_color", UiTheme.PressedColor.Lightened(0.4f));
            head.AddChild(label);
        }

        panel._scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(panel._scroll);
        panel._list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel._list.AddThemeConstantOverride("separation", 2);
        panel._scroll.AddChild(panel._list);
        return panel;
    }

    /// <summary>The rows as the player reads them, one line each (category headers included), for the smoke.</summary>
    public IReadOnlyList<string> RowTexts => _rows;

    public override void _EnterTree() => _services.Localizer.LocaleChanged += OnLocaleChanged;

    public override void _ExitTree() => _services.Localizer.LocaleChanged -= OnLocaleChanged;

    public void Toggle()
    {
        Visible = !Visible;
        if (Visible)
        {
            _signature = string.Empty;
            Refresh();
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        UiBuilder.FitToCanvas(_scroll, _list);
        _sinceRefresh += delta;
        if (_sinceRefresh >= RefreshSeconds)
        {
            Refresh();
        }
    }

    private void OnLocaleChanged(string locale)
    {
        _signature = string.Empty;
        if (Visible)
        {
            Refresh();
        }
    }

    /// <summary>Reads the stock now, without waiting for the next refresh. For the smoke.</summary>
    public void Refresh()
    {
        _sinceRefresh = 0;
        var simulation = _session.Simulation;
        var totals = Stocks.Totals(simulation);
        var signature = string.Join(';', totals.Select(entry => $"{entry.Item.Id}:{entry.Quality}:{entry.Count}:{entry.Freshness / 100}"));
        if (signature == _signature)
        {
            return;
        }

        _signature = signature;
        _rows.Clear();
        foreach (var child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }

        var text = _services.Text;
        var defaultQuality = QualityRules.From(simulation.Content).Default.Id;
        foreach (var category in ItemCategoryDef.InOrder(simulation.Content))
        {
            var entries = totals.Where(entry => entry.Item.Category == category.Id).ToList();
            if (entries.Count == 0)
            {
                continue;
            }

            var header = text.Format("ui.hud.stock", ("category", text.CategoryName(category)), ("count", entries.Sum(entry => entry.Count)));
            _rows.Add(header);
            var title = new Label { Text = header, AutoTranslateMode = AutoTranslateModeEnum.Disabled, ThemeTypeVariation = "HeaderSmall" };
            title.AddThemeColorOverride("font_color", UiTheme.PressedColor.Lightened(0.4f));
            _list.AddChild(title);
            var grid = new GridContainer { Columns = 4 };
            grid.AddThemeConstantOverride("h_separation", ColumnGap);
            grid.AddThemeConstantOverride("v_separation", 2);
            _list.AddChild(grid);
            foreach (var entry in entries)
            {
                AddRow(grid, entry, defaultQuality);
            }
        }

        if (_rows.Count == 0)
        {
            _rows.Add(text.Format("ui.inventory.empty"));
            _list.AddChild(new Label { Text = "ui.inventory.empty" });
        }

        UiBuilder.FitToCanvas(_scroll, _list);
    }

    private void AddRow(GridContainer grid, StockEntry entry, string? defaultQuality)
    {
        var text = _services.Text;
        var name = text.Capitalized(text.Format(entry.Item.NameKey!, ("count", entry.Count)));

        // Only a quality other than the usual one is worth a word (docs/gdd/oficios.md).
        var quality = entry.Quality is { } id && id != defaultQuality ? text.Capitalized(text.Format(_session.Simulation.Content.Get<QualityDef>(id).NameKey!)) : string.Empty;
        var count = entry.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var line = quality.Length == 0 ? $"{name}  {count}" : $"{name}  {count}  {quality}";
        grid.AddChild(new Label { Text = name, AutoTranslateMode = AutoTranslateModeEnum.Disabled, CustomMinimumSize = new Vector2(NameWidth, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart });
        grid.AddChild(new Label { Text = count, AutoTranslateMode = AutoTranslateModeEnum.Disabled, CustomMinimumSize = new Vector2(CountWidth, 0), HorizontalAlignment = HorizontalAlignment.Right });

        grid.AddChild(new Label { Text = quality, AutoTranslateMode = AutoTranslateModeEnum.Disabled, CustomMinimumSize = new Vector2(QualityWidth, 0) });

        // What does not rot has no freshness to show.
        var fresh = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, CustomMinimumSize = new Vector2(FreshWidth, 0) };
        if (entry.Item.SpoilDays > 0)
        {
            var percent = entry.Freshness * 100 / ItemStack.FullFreshness;
            fresh.Text = text.Format("ui.inspector.freshness", ("percent", percent));
            line += "  " + fresh.Text;
            if (percent < WarnPercent)
            {
                fresh.AddThemeColorOverride("font_color", percent < DangerPercent ? DangerColor : WarningColor);
            }
        }

        _rows.Add(line);
        grid.AddChild(fresh);
    }
}
