using Godot;

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
    public const string OrderMine = "order_mine";
    public const string OrderStairs = "order_stairs";
    public const string OrderChop = "order_chop";
    public const string OrderHarvest = "order_harvest";
    public const string OrderCancel = "order_cancel";
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
        (OrderMine, [Key.M]),
        (OrderStairs, [Key.B]),
        (OrderChop, [Key.T]),
        (OrderHarvest, [Key.R]),
        (OrderCancel, [Key.X]),
        (TogglePause, [Key.Space]),
        (Speed1, [Key.Key1]),
        (Speed2, [Key.Key2]),
        (Speed3, [Key.Key3]),
    ];

    /// <summary>Adds the actions that are missing; calling it again changes nothing.</summary>
    public static void Register()
    {
        foreach (var (action, keys) in Bindings)
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
