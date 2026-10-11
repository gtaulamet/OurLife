using OurLife.Core.Colony;
using OurLife.Core.Map;

namespace OurLife.Content.Tests;

/// <summary>Texts that the game's UI builds at run time from an id, checked in every language.</summary>
public class UiTextTests
{
    // The ids InspectorPanel gives each TileShape.
    private static readonly string[] ShapeIds = ["empty", "floor", "wall", "ramp", "stair_up", "stair_down", "stair_updown"];

    [Fact]
    public void Every_tile_shape_has_its_name_in_every_language()
    {
        ShapeIds.Length.ShouldBe(Enum.GetValues<TileShape>().Length, "a new shape needs its id in InspectorPanel and here");
        foreach (var catalog in GameContent.Catalogs.Values)
        {
            foreach (var id in ShapeIds)
            {
                catalog.Messages.ContainsKey($"ui.inspector.shape.{id}").ShouldBeTrue($"{catalog.Locale}: missing ui.inspector.shape.{id}");
            }
        }
    }

    [Fact]
    public void Every_order_has_its_button_and_its_name_in_every_language()
    {
        foreach (var catalog in GameContent.Catalogs.Values)
        {
            // The HUD has a button for every kind, with the ids of DesignationKinds.
            foreach (var id in Enum.GetValues<DesignationKind>().Select(DesignationKinds.Id))
            {
                catalog.Messages.ContainsKey($"ui.orders.{id}").ShouldBeTrue($"{catalog.Locale}: missing ui.orders.{id}");
                catalog.Messages.ContainsKey($"ui.orders.name.{id}").ShouldBeTrue($"{catalog.Locale}: missing ui.orders.name.{id}");
            }
        }
    }
}
