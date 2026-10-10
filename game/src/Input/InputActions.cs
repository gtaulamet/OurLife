using Godot;

namespace OurLife.Game.Input;

/// <summary>
/// Input actions of the game, registered in code so <c>project.godot</c> stays minimal. The keys
/// follow the plan: WASD or arrows to move, Q/E to rotate, Page Up/Down to change level.
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
