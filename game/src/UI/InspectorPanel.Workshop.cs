using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;

namespace OurLife.Game.UI;

/// <summary>
/// The part of the inspector for a workshop (see "Órdenes de producción" in <c>docs/gdd/oficios.md</c>):
/// a button per recipe that creates an order to make a few units, and the orders of the workshop's
/// kind with their state and a button to remove each. O4 replaces it with the production panel.
/// </summary>
public partial class InspectorPanel
{
    /// <summary>Units a button of a recipe orders.</summary>
    private const int OrderedUnits = 5;

    // The signature of what is shown, so the rows are only rebuilt when it changes and a button keeps the mouse.
    private string _workshopSignature = string.Empty;

    private void ShowWorkshop(EntityId shop)
    {
        var world = _simulation.World;
        if (shop.IsNone || !world.IsAlive(shop) || !world.Has<Workshop>(shop))
        {
            ClearWorkshop();
            return;
        }

        var content = _simulation.Content;
        var text = _services.Text;
        var kind = world.Get<Furniture>(shop).Buildable;
        var recipes = Recipes.Of(content, kind);
        var orders = new List<(EntityId Entity, WorkOrder Order, WorkOrderStatus Status)>();
        var entities = new List<EntityId>();
        world.Query<WorkOrder>(entities);
        foreach (var entity in entities)
        {
            var order = world.Get<WorkOrder>(entity);
            if (content.TryGet<RecipeDef>(order.Recipe, out var recipe) && recipe.Workshop == kind)
            {
                orders.Add((entity, order, WorkOrderRules.Status(_simulation, entity)));
            }
        }

        var signature = string.Join(
            ';',
            new[] { kind }
                .Concat(recipes.Select(recipe => $"{recipe.Id}:{_simulation.Unlocks.IsUnlocked(recipe, _simulation.Progress)}"))
                .Concat(orders.Select(item => $"{item.Entity.Index}:{item.Order.Done}:{item.Status}")));
        if (signature == _workshopSignature)
        {
            return;
        }

        ClearWorkshop();
        _workshopSignature = signature;
        _workshop.AddChild(new Label { Text = text.Format("ui.workshop.recipes"), AutoTranslateMode = AutoTranslateModeEnum.Disabled });
        foreach (var recipe in recipes)
        {
            var unlocked = _simulation.Unlocks.IsUnlocked(recipe, _simulation.Progress);
            var button = new Button
            {
                Text = text.Format(unlocked ? "ui.workshop.make" : "ui.workshop.make_locked", ("count", OrderedUnits), ("recipe", text.Format(recipe.NameKey!).ToLowerInvariant())),
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                FocusMode = FocusModeEnum.None,
                Alignment = HorizontalAlignment.Left,
            };
            var id = recipe.Id;
            button.Pressed += () => _enqueue(new CreateWorkOrderCommand(id, WorkOrderMode.Count, OrderedUnits));
            _workshop.AddChild(button);
        }

        if (orders.Count == 0)
        {
            return;
        }

        _workshop.AddChild(new Label { Text = text.Format("ui.workshop.orders"), AutoTranslateMode = AutoTranslateModeEnum.Disabled });
        foreach (var (entity, order, status) in orders)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label
            {
                Text = text.Format(
                    "ui.workshop.order",
                    ("recipe", text.Format(content.Get<RecipeDef>(order.Recipe).NameKey!)),
                    ("done", order.Done),
                    ("quantity", order.Quantity),
                    ("status", text.Format($"ui.workshop.status.{StatusId(status)}"))),
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            });
            var remove = new Button { Text = "ui.workshop.remove", FocusMode = FocusModeEnum.None };
            var target = entity;
            remove.Pressed += () => _enqueue(new RemoveWorkOrderCommand(target));
            row.AddChild(remove);
            _workshop.AddChild(row);
        }
    }

    private void ClearWorkshop()
    {
        _workshopSignature = string.Empty;
        foreach (var child in _workshop.GetChildren())
        {
            _workshop.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>The id of a state in the texts (<c>ui.workshop.status.&lt;id&gt;</c>).</summary>
    internal static string StatusId(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Running => "running",
        WorkOrderStatus.WaitingInputs => "waiting_inputs",
        WorkOrderStatus.NoWorkshop => "no_workshop",
        WorkOrderStatus.Locked => "locked",
        WorkOrderStatus.Paused => "paused",
        WorkOrderStatus.Satisfied => "satisfied",
        WorkOrderStatus.NoWorker => "no_worker",
        WorkOrderStatus.Done => "done",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
