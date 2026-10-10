using Godot;
using OurLife.Core.Sim;
using OurLife.Game.Bootstrap;
using OurLife.Game.Input;
using OurLife.Game.Session;

namespace OurLife.Game.UI;

/// <summary>
/// The HUD of a game, drawn over the 3D view (see "HUD de la partida" in
/// <c>docs/gdd/interfaz-ip1.md</c>). For now it holds the inspector; the top bar, the orders bar
/// and the event log join it in the next step of H1.7.
/// </summary>
public partial class Hud : CanvasLayer
{
    private const int Margin = 12;

    public InspectorPanel Inspector { get; private set; } = null!;

    public static Hud Create(GameServices services, GameSession session, GameInput input)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(input);
        var hud = new Hud { Name = nameof(Hud) };

        // A full-screen control that lets the mouse through to the world, for the theme and anchors.
        var root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiTheme.Create() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hud.AddChild(root);

        hud.Inspector = InspectorPanel.Create(services, session.Simulation);
        hud.Inspector.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        hud.Inspector.Position = new Vector2(-Margin, 64);
        hud.Inspector.GrowHorizontal = Control.GrowDirection.Begin;
        root.AddChild(hud.Inspector);

        input.SelectionChanged += hud.Inspector.Inspect;
        return hud;
    }
}
