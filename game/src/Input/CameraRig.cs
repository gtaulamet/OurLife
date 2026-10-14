using Godot;

namespace OurLife.Game.Input;

/// <summary>
/// The camera of the world view (ADR-009): it looks down at a point of the current level with a
/// fixed tilt, turns in steps of 90°, moves with the keys, zooms with the wheel and changes level
/// with Page Up/Down. Moving is relative to where the camera faces.
/// </summary>
public partial class CameraRig : Node3D
{
    private const float PitchDegrees = 55f;
    private const float MinDistance = 8f;
    private const float MaxDistance = 140f;
    private const float ZoomStep = 1.15f;
    private const float PanSpeedPerDistance = 0.9f;

    private Camera3D _camera = null!;
    private Vector2 _focus;
    private Vector2 _mapSize;
    private int _quarterTurns;
    private float _distance;
    private int _level;
    private int _maxLevel;

    /// <summary>The camera itself, for turning screen points into rays.</summary>
    public Camera3D Camera => _camera;

    /// <summary>Raised with the new level when the player moves the view up or down.</summary>
    public event Action<int>? LevelChanged;

    public static CameraRig Create(Vector2 mapSize, int level, int maxLevel, float distance)
    {
        var rig = new CameraRig
        {
            Name = nameof(CameraRig),
            _mapSize = mapSize,
            _focus = mapSize / 2,
            _level = level,
            _maxLevel = maxLevel,
            _distance = Math.Clamp(distance, MinDistance, MaxDistance),
            _camera = new Camera3D { Name = "Camera", Fov = 45f, Far = 1000f },
        };
        rig.AddChild(rig._camera);
        return rig;
    }

    /// <summary>Centres the view on a cell of the map, for captures.</summary>
    public void Focus(Vector2 cell)
    {
        _focus = cell.Clamp(Vector2.Zero, _mapSize);
        if (IsInsideTree())
        {
            PlaceCamera();
        }
    }

    public override void _Ready()
    {
        InputActions.Register();
        _camera.MakeCurrent();
        PlaceCamera();
    }

    public override void _Process(double delta)
    {
        if (Godot.Input.IsActionJustPressed(InputActions.RotateLeft))
        {
            _quarterTurns = (_quarterTurns + 3) % 4;
        }

        if (Godot.Input.IsActionJustPressed(InputActions.RotateRight))
        {
            _quarterTurns = (_quarterTurns + 1) % 4;
        }

        if (Godot.Input.IsActionJustPressed(InputActions.LevelUp))
        {
            ChangeLevel(1);
        }

        if (Godot.Input.IsActionJustPressed(InputActions.LevelDown))
        {
            ChangeLevel(-1);
        }

        var move = Godot.Input.GetVector(InputActions.CameraLeft, InputActions.CameraRight, InputActions.CameraForward, InputActions.CameraBack);
        if (move != Vector2.Zero)
        {
            // Screen right and screen down, turned to where the camera faces.
            var yaw = Yaw();
            var right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
            var back = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            _focus += ((right * move.X) + (back * move.Y)) * (float)delta * _distance * PanSpeedPerDistance;
            _focus = _focus.Clamp(Vector2.Zero, _mapSize);
        }

        PlaceCamera();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp)
            {
                _distance = Math.Max(MinDistance, _distance / ZoomStep);
            }
            else if (button.ButtonIndex == MouseButton.WheelDown)
            {
                _distance = Math.Min(MaxDistance, _distance * ZoomStep);
            }
        }
    }

    private void ChangeLevel(int step)
    {
        var level = Math.Clamp(_level + step, 0, _maxLevel);
        if (level != _level)
        {
            _level = level;
            LevelChanged?.Invoke(level);
        }
    }

    /// <summary>Yaw of the camera around the focus: 45° plus the quarter turns.</summary>
    private float Yaw() => Mathf.DegToRad(45f + (90f * _quarterTurns));

    private void PlaceCamera()
    {
        var target = new Vector3(_focus.X, _level + 1, _focus.Y);
        var pitch = Mathf.DegToRad(PitchDegrees);
        var yaw = Yaw();
        var offset = new Vector3(
            Mathf.Sin(yaw) * Mathf.Cos(pitch),
            Mathf.Sin(pitch),
            Mathf.Cos(yaw) * Mathf.Cos(pitch)) * _distance;
        _camera.Position = target + offset;
        _camera.LookAt(target, Vector3.Up);
    }
}
