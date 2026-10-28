using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
using OurLife.Game.Bootstrap;
using OurLife.Game.Session;

namespace OurLife.Game.UI;

/// <summary>
/// The sheet of a colonist (see "Ficha del colono" in <c>docs/gdd/vida-y-generaciones.md</c>): its
/// stage, age, mood and crisis; its personality in words, its skills and its family on the left;
/// its thoughts, wishes, relations and last interactions on the right. A colonist in a couple of a
/// woman and a man has the choice of children at the top. It opens over the game with
/// F or the inspector's button, follows the selected colonist, refreshes twice a second and closes
/// with F, Esc (through <see cref="Input.GameInput.CloseFirst"/>), its button, or when the colonist
/// dies. It shows again only when time passed, the colonist changed or the language did.
/// </summary>
public partial class ColonistSheet : PanelContainer
{
    private const double RefreshSeconds = 0.5;
    private const string AllowedKey = "ui.sheet.children_allowed";
    private const string ForbiddenKey = "ui.sheet.children_forbidden";
    private const int ColumnWidth = 340;

    private readonly List<(Label Header, Label Body)> _sections = [];

    /// <summary>Index of the tools in <see cref="_sections"/>, which has no text when the colonist has none.</summary>
    private const int ToolsSection = 1;
    private GameServices _services = null!;
    private GameSession _session = null!;
    private Simulation _simulation = null!;
    private Label _title = null!;
    private Label _summary = null!;
    private Button _children = null!;
    private double _sinceRefresh;
    private long _refreshedTick = -1;

    /// <summary>The colonist shown, or none.</summary>
    public EntityId Colonist { get; private set; }

    /// <summary>Every line shown, as the player reads it (the headers translated), for the smoke.</summary>
    public IEnumerable<string> Texts =>
        new[] { _title.Text, _summary.Text }.Concat(_sections.SelectMany(section => new[] { (string)Tr(section.Header.Text), section.Body.Text }));

    /// <summary>The button of the choice of children, for the smoke.</summary>
    public Button ChildrenButton => _children;

    public static ColonistSheet Create(GameServices services, GameSession session)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(session);
        var sheet = new ColonistSheet
        {
            Name = nameof(ColonistSheet), _services = services, _session = session, _simulation = session.Simulation, Visible = false,
        };
        sheet.SetAnchorsPreset(LayoutPreset.Center);
        sheet.GrowHorizontal = GrowDirection.Both;
        sheet.GrowVertical = GrowDirection.Both;

        // Opaque, so the HUD under it does not show through the text.
        sheet.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.11f, 0.10f, 0.97f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
        });

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        sheet.AddChild(column);

        var top = new HBoxContainer();
        column.AddChild(top);
        sheet._title = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        sheet._title.AddThemeFontSizeOverride("font_size", 24);
        top.AddChild(sheet._title);
        sheet._children = new Button { FocusMode = FocusModeEnum.None, Visible = false };
        sheet._children.Pressed += sheet.ToggleChildren;
        top.AddChild(sheet._children);
        var close = new Button { Text = "ui.sheet.close", FocusMode = FocusModeEnum.None };
        close.Pressed += sheet.Close;
        top.AddChild(close);

        sheet._summary = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        column.AddChild(sheet._summary);

        // Two columns in a scroll: the sheet fits a canvas of 960 × 540 however much it says.
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2((2 * ColumnWidth) + 24, 340), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 24);
        scroll.AddChild(columns);
        var left = Column(columns);
        var right = Column(columns);
        foreach (var (key, parent) in new[]
                 {
                     ("ui.sheet.traits", left), ("ui.sheet.tools", left), ("ui.sheet.skills", left), ("ui.sheet.family", left),
                     ("ui.sheet.thoughts", right), ("ui.sheet.desires", right), ("ui.sheet.relations", right), ("ui.sheet.recent", right),
                 })
        {
            var header = new Label { Text = key, ThemeTypeVariation = "HeaderSmall" };
            header.AddThemeColorOverride("font_color", UiTheme.PressedColor.Lightened(0.4f));
            parent.AddChild(header);
            var body = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            parent.AddChild(body);
            sheet._sections.Add((header, body));
        }

        return sheet;
    }

    public override void _EnterTree() => _services.Localizer.LocaleChanged += OnLocaleChanged;

    public override void _ExitTree() => _services.Localizer.LocaleChanged -= OnLocaleChanged;

    /// <summary>Shows a colonist, or closes the sheet if it was showing that one.</summary>
    public void Toggle(EntityId colonist)
    {
        if (Visible && Colonist == colonist)
        {
            Close();
        }
        else
        {
            ShowColonist(colonist);
        }
    }

    public void ShowColonist(EntityId colonist)
    {
        if (!IsColonist(colonist))
        {
            return;
        }

        Colonist = colonist;
        Visible = true;
        Refresh();
    }

    public void Close()
    {
        Visible = false;
        Colonist = EntityId.None;
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _sinceRefresh += delta;
        if (_sinceRefresh >= RefreshSeconds && _simulation.CurrentTick != _refreshedTick)
        {
            Refresh();
        }
    }

    /// <summary>
    /// Allows or forbids children to the couple of the colonist shown (see <see cref="SetChildrenAllowedCommand"/>),
    /// the opposite of what the button shows. While time runs the choice applies on the next tick:
    /// the button shows it at once, so pressing twice before that gives the choice back, and the
    /// sheet refreshes as soon as the tick passed.
    /// </summary>
    private void ToggleChildren()
    {
        if (!IsColonist(Colonist))
        {
            return;
        }

        var allowed = _children.Text == AllowedKey;
        _session.Enqueue(new SetChildrenAllowedCommand(Colonist, !allowed));
        _children.Text = allowed ? ForbiddenKey : AllowedKey;
        _sinceRefresh = RefreshSeconds;
    }

    /// <summary>
    /// The choice shows for a colonist with a living partner of the other sex: only they can have
    /// children. It is the couple's: allowed only if both allow it, as a couple conceives.
    /// </summary>
    private void RefreshChildren()
    {
        var world = _simulation.World;
        var partner = Relationships.PartnerOf(_simulation, Colonist);
        _children.Visible = !partner.IsNone && world.Get<Colonist>(partner).Gender != world.Get<Colonist>(Colonist).Gender;
        _children.Text = !partner.IsNone && LifeRules.ChildrenAllowed(_simulation, Colonist, partner) ? AllowedKey : ForbiddenKey;
    }

    private static VBoxContainer Column(HBoxContainer columns)
    {
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(ColumnWidth, 0) };
        column.AddThemeConstantOverride("separation", 4);
        columns.AddChild(column);
        return column;
    }

    private bool IsColonist(EntityId colonist) => Relationships.IsLivingColonist(_simulation.World, colonist);

    private void OnLocaleChanged(string locale)
    {
        if (Visible)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        _sinceRefresh = 0;
        _refreshedTick = _simulation.CurrentTick;
        if (!IsColonist(Colonist))
        {
            Close();
            return;
        }

        _title.Text = TitleOf(Colonist);
        RefreshChildren();
        _summary.Text = string.Join("   ", SummaryOf(Colonist));
        var bodies = new[] { Traits(Colonist), ToolLines(Colonist), Skills(Colonist), Family(Colonist), Thoughts(Colonist), Desires(Colonist), Relations(Colonist), Recent(Colonist) };
        for (var i = 0; i < _sections.Count; i++)
        {
            var lines = bodies[i];
            // The tools (second section) show only for a colonist that has one.
            var shown = i != ToolsSection || lines.Count > 0;
            _sections[i].Header.Visible = shown;
            _sections[i].Body.Visible = shown;
            _sections[i].Body.Text = lines.Count == 0 ? _services.Text.Format("ui.sheet.none") : string.Join("\n", lines);
        }
    }
}
