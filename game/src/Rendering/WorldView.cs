using Godot;
using OurLife.Core.Sim;
using OurLife.Game.Input;

namespace OurLife.Game.Rendering;

/// <summary>
/// The 3D view of a game (ADR-009): sky colour, light, terrain, plants and camera. It starts at
/// the highest surface of the map, so the whole valley shows, unless a level is given.
/// </summary>
public partial class WorldView : Node3D
{
    private const float StartDistance = 85f;

    private MapRenderer _map = null!;
    private PlantRenderer _plants = null!;
    private CameraRig _camera = null!;

    /// <summary>The level the view cuts the map at.</summary>
    public int ViewLevel => _map.ViewLevel;

    /// <summary>Builds the view of a simulation whose world is already generated.</summary>
    public static WorldView Create(Simulation simulation, VisualCatalog catalog, int? level)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(catalog);
        var map = simulation.Map;
        var viewLevel = Math.Clamp(level ?? HighestSurface(simulation), 0, map.Size.Depth - 1);

        var view = new WorldView { Name = nameof(WorldView) };
        try
        {
            view.AddChild(CreateEnvironment());
            view.AddChild(new DirectionalLight3D
            {
                Name = "Sun",
                RotationDegrees = new Vector3(-50f, 30f, 0f),
                LightEnergy = 0.9f,
                ShadowEnabled = true,
            });

            view._map = MapRenderer.Create(map, catalog, viewLevel);
            view.AddChild(view._map);

            view._plants = PlantRenderer.Create(simulation, new ModelLibrary(catalog), viewLevel);
            view.AddChild(view._plants);
            view._camera = CameraRig.Create(new Vector2(map.Size.Width, map.Size.Height), viewLevel, map.Size.Depth - 1, StartDistance);
            view._camera.LevelChanged += view.OnLevelChanged;
            view.AddChild(view._camera);
            return view;
        }
        catch
        {
            // Frees whatever was built, and with it the meshes, materials and models the
            // renderers hold, so a failed start does not leak at exit.
            view.Free();
            throw;
        }
    }

    private static int HighestSurface(Simulation simulation)
    {
        var map = simulation.Map;
        var highest = 0;
        for (var y = 0; y < map.Size.Height; y++)
        {
            for (var x = 0; x < map.Size.Width; x++)
            {
                highest = Math.Max(highest, map.SurfaceZ(x, y));
            }
        }

        return highest;
    }

    private static WorldEnvironment CreateEnvironment()
    {
        using var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.62f, 0.76f, 0.86f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.85f, 0.88f, 0.95f),
            AmbientLightEnergy = 0.35f,
        };

        // The node keeps its own reference; the C# one is released (see MapRenderer.Fill).
        return new WorldEnvironment { Name = "Environment", Environment = environment };
    }

    private void OnLevelChanged(int level)
    {
        _map.SetViewLevel(level);
        _plants.SetViewLevel(level);
    }
}
