using Godot;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Game.Rendering;

/// <summary>
/// Draws the plants of the simulation with the model of their def's visual, standing on the
/// floor of their cell. Plants above the current level or too deep are hidden; the rest darken
/// with depth like the terrain. Each plant turns by an angle taken from its cell, so a grove does
/// not look copied and pasted, and the same map always looks the same.
/// </summary>
/// <remarks>
/// The plants are those of the simulation when the view is built: nothing creates or removes
/// plants yet. Following them as they are cut down and grow back comes with H1.6 and H1.7.
/// </remarks>
public partial class PlantRenderer : Node3D
{
    private readonly List<PlantView> _plants = [];
    private ModelLibrary _models = null!;

    /// <summary>Draws the plants of a simulation. The renderer owns the library from this call on.</summary>
    public static PlantRenderer Create(Simulation simulation, ModelLibrary models, int viewLevel)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(models);
        var renderer = new PlantRenderer { Name = nameof(PlantRenderer), _models = models };

        try
        {
            var entities = new List<EntityId>();
            simulation.World.Query<Plant, Position>(entities);
            foreach (var entity in entities)
            {
                var position = simulation.World.Get<Position>(entity);
                var def = simulation.Content.Get<PlantDef>(simulation.World.Get<Plant>(entity).Def);
                if (!models.Catalog.TryResolve(def.Visual, simulation.Progress.CurrentEra, out var model))
                {
                    throw new InvalidOperationException($"The visual catalog has no model for '{def.Visual}'.");
                }

                var node = models.Instantiate(model);
                node.Position = new Vector3(position.X + 0.5f, position.Z + TerrainMeshes.FloorThickness, position.Y + 0.5f);
                node.RotationDegrees = new Vector3(0, CellAngle(position.X, position.Y), 0);
                renderer.AddChild(node);
                renderer._plants.Add(new PlantView(node, model, position.Z, ModelLibrary.MeshesOf(node)));
            }

            renderer.SetViewLevel(viewLevel);
            return renderer;
        }
        catch
        {
            // Frees the plants built so far and, on deletion, the library.
            renderer.Free();
            throw;
        }
    }

    public override void _Notification(int what)
    {
        // On deletion, not on leaving the tree (see MapRenderer).
        if (what == NotificationPredelete)
        {
            _models?.Dispose();
        }
    }

    public void SetViewLevel(int viewLevel)
    {
        foreach (var plant in _plants)
        {
            plant.Node.Visible = LevelShading.IsVisible(plant.Level, viewLevel);
            var depth = viewLevel - plant.Level;
            if (plant.Node.Visible && plant.ShadedDepth != depth)
            {
                _models.Shade(plant.Model, plant.Meshes, depth);
                plant.ShadedDepth = depth;
            }
        }
    }

    private sealed class PlantView(Node3D node, VisualModel model, int level, IReadOnlyList<MeshInstance3D> meshes)
    {
        public Node3D Node { get; } = node;

        public VisualModel Model { get; } = model;

        public int Level { get; } = level;

        public IReadOnlyList<MeshInstance3D> Meshes { get; } = meshes;

        /// <summary>Depth its materials are shaded for; -1 before the first time.</summary>
        public int ShadedDepth { get; set; } = -1;
    }

    /// <summary>A fixed angle in [0, 360) for a cell, from a small integer hash.</summary>
    private static float CellAngle(int x, int y)
    {
        var hash = unchecked((uint)((x * 73856093) ^ (y * 19349663)));
        hash ^= hash >> 13;
        hash = unchecked(hash * 0x5bd1e995);
        return hash % 360;
    }
}
