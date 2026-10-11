using Godot;
using OurLife.Core.Colony;

namespace OurLife.Game.Input;

/// <summary>
/// Input actions of the game, registered in code so <c>project.godot</c> stays minimal. The keys
/// follow <c>docs/gdd/interfaz-ip1.md</c>: WASD or arrows to move, Q/E to rotate, Page Up/Down to change level,
/// M, B, T, R and X for orders, Space and 1 to 3 for time.
/// </summary>
public static class InputActions
{
    public const string CameraLeft = "camera_left";
    public const string CameraRight = "camera_right";
    public const string CameraForward = "camera_forward";
    public const string CameraBack = "camera_back";
    public const string RotateLeft = "camera_rotate_left";
    public const string RotateRight = "camera_rotate_right";
    public const string LevelUp = "view_level_up";
    public const string LevelDown = "view_level_down";
    public const string TogglePause = "time_pause";
    public const string Speed1 = "time_speed_1";
    public const string Speed2 = "time_speed_2";
    public const string Speed3 = "time_speed_3";

    private static readonly (string Action, Key[] Keys)[] Bindings =
    [
        (CameraLeft, [Key.A, Key.Left]),
        (CameraRight, [Key.D, Key.Right]),
        (CameraForward, [Key.W, Key.Up]),
        (CameraBack, [Key.S, Key.Down]),
        (RotateLeft, [Key.Q]),
        (RotateRight, [Key.E]),
        (LevelUp, [Key.Pageup]),
        (LevelDown, [Key.Pagedown]),
        (TogglePause, [Key.Space]),
        (Speed1, [Key.Key1]),
        (Speed2, [Key.Key2]),
        (Speed3, [Key.Key3]),
    ];

    /// <summary>The action that chooses an order, like <c>order_mine</c>.</summary>
    public static string Order(DesignationKind kind) => $"order_{DesignationKinds.Id(kind)}";

    /// <summary>The key of each order; the buttons show it in their text (<c>ui.orders.&lt;id&gt;</c>).</summary>
    private static Key OrderKey(DesignationKind kind) => kind switch
    {
        DesignationKind.Mine => Key.M,
        DesignationKind.Stairs => Key.B,
        DesignationKind.Chop => Key.T,
        DesignationKind.Harvest => Key.R,
        DesignationKind.Cancel => Key.X,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The order has no key."),
    };

    /// <summary>Adds the actions that are missing; calling it again changes nothing.</summary>
    public static void Register()
    {
        var orders = Enum.GetValues<DesignationKind>().Select(kind => (Order(kind), new[] { OrderKey(kind) }));
        foreach (var (action, keys) in Bindings.Concat(orders))
        {
            if (InputMap.HasAction(action))
            {
                continue;
            }

            InputMap.AddAction(action);
            foreach (var key in keys)
            {
                // The input map keeps its own reference (see MapRenderer.Fill).
                using var keyEvent = new InputEventKey { PhysicalKeycode = key };
                InputMap.ActionAddEvent(action, keyEvent);
            }
        }
    }
}
