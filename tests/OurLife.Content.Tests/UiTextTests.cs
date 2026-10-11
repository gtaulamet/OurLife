using OurLife.Core.Colony;
using OurLife.Core.Map;

namespace OurLife.Content.Tests;

/// <summary>Texts that the game's UI builds at run time from an id, checked in every language.</summary>
public class UiTextTests
{
    [Fact]
    public void Every_tile_shape_has_its_name_in_every_language()
    {
        foreach (var catalog in GameContent.Catalogs.Values)
        {
            // The inspector names every shape, with the ids of TileShapes.
            foreach (var id in Enum.GetValues<TileShape>().Select(TileShapes.Id))
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
