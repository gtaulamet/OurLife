using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;

namespace OurLife.Game.UI;

/// <summary>
/// The part of the inspector for a farm zone (see "Zonas de cultivo" in <c>docs/gdd/agricultura.md</c>):
/// what the cell under the selection holds (the zone's crop and how far the crop sown on it grew),
/// and a button per crop the colony knows to change what the zone sows from now on.
/// </summary>
public partial class InspectorPanel
{
    // The zone whose buttons are shown and what they were built for, so they are only rebuilt when it changes and keep the mouse.
    private string _farmSignature = string.Empty;
    private readonly List<(string Crop, Button Button)> _farmButtons = [];

    /// <summary>The lines about the farm zone on a cell: its crop, and the crop sown on it, ripe or by percent.</summary>
    private void DescribeFarm(TileCoord cell)
    {
        if (FarmRules.State(_simulation, cell) is not { } farm)
        {
            return;
        }

        var text = _services.Text;
        var content = _simulation.Content;
        _lines.Add(text.Capitalized(text.Format("ui.inspector.farm_zone", ("crop", NameOf(content.Get<CropDef>(farm.ZoneCrop))))));
        if (farm.CropDef is { } sown)
        {
            var name = NameOf(content.Get<CropDef>(sown));
            _lines.Add(text.Capitalized(farm.State == FarmCellState.Ripe
                ? text.Format("ui.inspector.crop_ripe", ("crop", name))
                : text.Format("ui.inspector.crop", ("crop", name), ("percent", farm.PercentGrown))));
            if (_simulation.World.IsAlive(farm.Crop) && _simulation.World.Has<HarvestOrder>(farm.Crop))
            {
                _lines.Add(text.Format("ui.inspector.order.reap"));
            }
        }
    }

    /// <summary>The order to reap a crop that stands off every farm zone (inside a zone, <see cref="DescribeFarm"/> says it).</summary>
    private void DescribeLooseCropOrder(TileCoord cell)
    {
        if (FarmRules.State(_simulation, cell) is not null)
        {
            return;
        }

        var crop = FarmRules.CropAt(_simulation, cell);
        if (!crop.IsNone && _simulation.World.Has<HarvestOrder>(crop))
        {
            _lines.Add(_services.Text.Format("ui.inspector.order.reap"));
        }
    }

    /// <summary>A button per crop the colony knows, for the zone under the selected cell; none off the farm zones.</summary>
    private void ShowFarm(EntityId zone)
    {
        var world = _simulation.World;
        if (zone.IsNone || !world.TryGet<FarmZone>(zone, out var farm))
        {
            ClearFarm();
            return;
        }

        var crops = _simulation.Content.All<CropDef>()
            .Where(crop => _simulation.Unlocks.IsUnlocked(crop, _simulation.Progress))
            .OrderBy(crop => crop.IsTree)
            .ThenBy(crop => crop.Id, StringComparer.Ordinal)
            .ToList();
        var signature = $"{zone.Index}#{zone.Generation}:{string.Join(',', crops.Select(crop => crop.Id))}";
        if (signature != _farmSignature)
        {
            ClearFarm();
            _farmSignature = signature;
            var text = _services.Text;
            _farm.AddChild(new Label { Text = text.Format("ui.farm.choose_crop"), AutoTranslateMode = AutoTranslateModeEnum.Disabled });
            foreach (var crop in crops)
            {
                var button = new Button
                {
                    Text = text.Capitalized(NameOf(crop)),
                    AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                    ToggleMode = true,
                    FocusMode = FocusModeEnum.None,
                    Alignment = HorizontalAlignment.Left,
                };
                var id = crop.Id;
                button.Pressed += () => _enqueue(new SetFarmZoneCropCommand(zone, id));
                _farm.AddChild(button);
                _farmButtons.Add((id, button));
            }
        }

        foreach (var (crop, button) in _farmButtons)
        {
            button.SetPressedNoSignal(crop == farm.Crop);
        }
    }

    private void ClearFarm()
    {
        _farmSignature = string.Empty;
        _farmButtons.Clear();
        foreach (var child in _farm.GetChildren())
        {
            _farm.RemoveChild(child);
            child.QueueFree();
        }
    }
}
