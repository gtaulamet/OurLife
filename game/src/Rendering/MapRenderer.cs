using Godot;
using OurLife.Core.Defs;
using OurLife.Core.Map;

namespace OurLife.Game.Rendering;

/// <summary>
/// Draws the terrain of a <see cref="GameMap"/> (ADR-009). Each chunk of 16×16 cells of a level
/// has one <see cref="MultiMeshInstance3D"/> per <see cref="TerrainPiece"/>, with only the blocks
/// that touch an open cell, created only for pieces the chunk has. The view cuts the map at
/// <see cref="ViewLevel"/>: higher levels are hidden, blocks at the cut get a dark cap, and lower
/// levels darken with depth. Chunks the map reports as dirty are rebuilt every frame.
/// </summary>
public partial class MapRenderer : Node3D
{
    // A 3×4 transform, a colour and the custom data, in the layout of MultiMesh.Buffer.
    private const int FloatsPerInstance = 20;

    // Grass flags of an instance, for the terrain shader.
    private const int GrassTop = 1;
    private const int GrassAll = 2;
    private const int GrassFringe = 4;

    private const string ShaderPath = "res://assets/shaders/terrain.gdshader";

    private static readonly TerrainPiece[] ChunkPieces =
        [TerrainPiece.Block, TerrainPiece.Floor, TerrainPiece.Ramp, TerrainPiece.Steps, TerrainPiece.Hole, TerrainPiece.Door];

    // North, east, south and west in map coordinates, with the yaw that turns +X towards each.
    private static readonly (int Dx, int Dy, float Yaw)[] Directions =
        [(0, -1, Mathf.Pi / 2), (1, 0, 0), (0, 1, -Mathf.Pi / 2), (-1, 0, Mathf.Pi)];

    private readonly Dictionary<TerrainPiece, Mesh> _meshes = [];
    private readonly Dictionary<ChunkKey, MultiMeshInstance3D?[]> _chunks = [];
    private readonly List<ChunkKey> _dirty = [];
    private readonly List<(Transform3D Transform, Color Color, int Pattern, int Grass)>[] _buffers =
        Enumerable.Range(0, Enum.GetValues<TerrainPiece>().Length).Select(_ => new List<(Transform3D, Color, int, int)>()).ToArray();

    private GameMap _map = null!;
    private Color[] _materialColors = [];
    private int[] _materialPatterns = [];
    private Color _grassColor;
    private Color _cutColor;
    private ShaderMaterial[] _levelMaterials = [];
    private MultiMeshInstance3D _cutLayer = null!;

    public int ViewLevel { get; private set; }

    public static MapRenderer Create(GameMap map, VisualCatalog catalog, int viewLevel)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(catalog);
        var renderer = new MapRenderer
        {
            Name = nameof(MapRenderer),
            _map = map,
            _materialColors = map.Materials.Select(material => Color.FromHtml(material.Color)).ToArray(),
            _materialPatterns = map.Materials.Select(material => TerrainStyle.PatternOf(material.Id)).ToArray(),
            _grassColor = Color.FromHtml(catalog.GrassColor),
            _cutColor = Color.FromHtml(catalog.CutColor),
        };
        renderer._levelMaterials = Enumerable.Range(0, LevelShading.MaxDepth + 1).Select(renderer.TerrainMaterial).ToArray();

        foreach (var piece in Enum.GetValues<TerrainPiece>())
        {
            renderer._meshes[piece] = TerrainMeshes.Create(piece);
        }

        renderer._cutLayer = new MultiMeshInstance3D { Name = "CutLayer", MaterialOverride = renderer._levelMaterials[0] };
        renderer.AddChild(renderer._cutLayer);

        renderer.ViewLevel = Math.Clamp(viewLevel, 0, map.Size.Depth - 1);
        renderer.BuildAllChunks();
        renderer.RebuildCutLayer();
        return renderer;
    }

    /// <summary>Moves the cut to another level, clamped to the map.</summary>
    public void SetViewLevel(int level)
    {
        ViewLevel = Math.Clamp(level, 0, _map.Size.Depth - 1);
        foreach (var (chunk, instances) in _chunks)
        {
            ApplyLevel(chunk, instances);
        }

        RebuildCutLayer();
    }

    public override void _Process(double delta)
    {
        RebuildDirtyChunks();
    }

    public override void _Notification(int what)
    {
        // On deletion, not on leaving the tree: the node may be freed without ever entering it,
        // and may leave and enter again. Releases the C# references to the shared meshes and
        // materials (see Fill).
        if (what == NotificationPredelete)
        {
            foreach (var mesh in _meshes.Values)
            {
                mesh.Dispose();
            }

            foreach (var material in _levelMaterials)
            {
                material.Dispose();
            }

            _meshes.Clear();
            _levelMaterials = [];
        }
    }

    private ShaderMaterial TerrainMaterial(int depth)
    {
        // Instance colours are #RRGGBB values, like the albedo colours of the models; the shader
        // converts them where the renderer needs it.
        using var shader = GD.Load<Shader>(ShaderPath);
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("style", TerrainStyle.Current);
        material.SetShaderParameter("brightness", LevelShading.Brightness(depth));
        material.SetShaderParameter("fringe", depth > 0);
        material.SetShaderParameter("grass_flat", new Vector3(_grassColor.R, _grassColor.G, _grassColor.B));
        return material;
    }

    /// <summary>
    /// Builds every chunk of the map, whatever the map reports as dirty: whoever drained the
    /// queue before, this view starts complete. Later changes arrive as dirty chunks.
    /// </summary>
    private void BuildAllChunks()
    {
        for (var z = 0; z < _map.Size.Depth; z++)
        {
            for (var cy = 0; cy < _map.ChunksY; cy++)
            {
                for (var cx = 0; cx < _map.ChunksX; cx++)
                {
                    BuildChunk(new ChunkKey(cx, cy, z));
                }
            }
        }

        _dirty.Clear();
        _map.DrainDirtyChunks(_dirty);
        _dirty.Clear();
    }

    private void RebuildDirtyChunks()
    {
        _dirty.Clear();
        _map.DrainDirtyChunks(_dirty);
        if (_dirty.Count == 0)
        {
            return;
        }

        var cutChanged = false;
        foreach (var chunk in _dirty)
        {
            BuildChunk(chunk);
            cutChanged |= chunk.Z == ViewLevel || chunk.Z == ViewLevel + 1;
        }

        if (cutChanged)
        {
            RebuildCutLayer();
        }
    }

    private void BuildChunk(ChunkKey chunk)
    {
        foreach (var buffer in _buffers)
        {
            buffer.Clear();
        }

        var x0 = chunk.X * GameMap.ChunkSize;
        var y0 = chunk.Y * GameMap.ChunkSize;
        var x1 = Math.Min(x0 + GameMap.ChunkSize, _map.Size.Width);
        var y1 = Math.Min(y0 + GameMap.ChunkSize, _map.Size.Height);
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                AddCell(x, y, chunk.Z);
            }
        }

        // Most chunks of the sky and of solid rock draw nothing, and get no nodes at all.
        _chunks.TryGetValue(chunk, out var instances);
        for (var i = 0; i < ChunkPieces.Length; i++)
        {
            var piece = ChunkPieces[i];
            if (_buffers[(int)piece].Count == 0)
            {
                if (instances?[i] is { } emptied)
                {
                    emptied.QueueFree();
                    instances[i] = null;
                }

                continue;
            }

            instances ??= new MultiMeshInstance3D?[ChunkPieces.Length];
            if (instances[i] is null)
            {
                instances[i] = new MultiMeshInstance3D { Name = $"Chunk_{chunk.X}_{chunk.Y}_{chunk.Z}_{piece}" };
                AddChild(instances[i]);
            }

            Fill(instances[i]!, piece);
        }

        if (instances is null || instances.All(instance => instance is null))
        {
            _chunks.Remove(chunk);
            return;
        }

        _chunks[chunk] = instances;
        ApplyLevel(chunk, instances);
    }

    /// <summary>Shows a chunk darkened for its depth below the current level, or hides it.</summary>
    private void ApplyLevel(ChunkKey chunk, MultiMeshInstance3D?[] instances)
    {
        var visible = LevelShading.IsVisible(chunk.Z, ViewLevel);
        foreach (var instance in instances)
        {
            if (instance is null)
            {
                continue;
            }

            instance.Visible = visible;
            if (visible)
            {
                instance.MaterialOverride = _levelMaterials[ViewLevel - chunk.Z];
            }
        }
    }

    private void AddCell(int x, int y, int z)
    {
        var origin = new Vector3(x + 0.5f, z, y + 0.5f);
        switch (_map.ShapeAt(x, y, z))
        {
            case TileShape.Wall when IsExposed(x, y, z):
                Add(TerrainPiece.Block, new Transform3D(Basis.Identity, origin), MaterialColor(x, y, z), Pattern(x, y, z), HasGrass(x, y, z + 1) ? GrassFringe : 0);
                break;
            case TileShape.Floor:
                Add(TerrainPiece.Floor, new Transform3D(Basis.Identity, origin), MaterialColor(x, y, z), Pattern(x, y, z), HasGrass(x, y, z) ? GrassAll : 0);
                break;
            case TileShape.Ramp:
                Add(TerrainPiece.Ramp, new Transform3D(new Basis(Vector3.Up, RampYaw(x, y, z)), origin), MaterialColor(x, y, z), Pattern(x, y, z), HasGrass(x, y, z) ? GrassTop : 0);
                break;
            case TileShape.StairUp:
                Add(TerrainPiece.Steps, new Transform3D(Basis.Identity, origin), MaterialColor(x, y, z), Pattern(x, y, z));
                break;
            case TileShape.StairUpDown:
                // Steps up on one half of the cell, the opening of the way down on the other.
                var half = Basis.Identity.Scaled(new Vector3(0.5f, 1f, 1f));
                Add(TerrainPiece.Steps, new Transform3D(half, origin + new Vector3(0.25f, 0, 0)), MaterialColor(x, y, z), Pattern(x, y, z));
                Add(TerrainPiece.Hole, new Transform3D(half, origin - new Vector3(0.25f, 0, 0)), _cutColor);
                break;
            case TileShape.StairDown:
                Add(TerrainPiece.Hole, new Transform3D(Basis.Identity, origin), _cutColor);
                break;
            case TileShape.Door:
                Add(TerrainPiece.Door, new Transform3D(new Basis(Vector3.Up, DoorYaw(x, y, z)), origin), MaterialColor(x, y, z), Pattern(x, y, z));
                break;
        }
    }

    /// <summary>
    /// A door lies in the line of its walls: across X unless the walls are only to its north or
    /// south, which turns it a quarter.
    /// </summary>
    private float DoorYaw(int x, int y, int z)
    {
        bool IsWall(int dx, int dy) => _map.ShapeOrEmpty(x + dx, y + dy, z) == TileShape.Wall;
        var alongX = IsWall(-1, 0) || IsWall(1, 0);
        var alongY = IsWall(0, -1) || IsWall(0, 1);
        return alongY && !alongX ? Mathf.Pi / 2 : 0;
    }

    /// <summary>
    /// A block is drawn when a face touches an open cell. The sides of the map count as open, so
    /// its edges show the strata; the bottom of the map does not.
    /// </summary>
    private bool IsExposed(int x, int y, int z) =>
        !IsWallOrBelowMap(x - 1, y, z) || !IsWallOrBelowMap(x + 1, y, z)
        || !IsWallOrBelowMap(x, y - 1, z) || !IsWallOrBelowMap(x, y + 1, z)
        || !IsWallOrBelowMap(x, y, z - 1) || !IsWallOrBelowMap(x, y, z + 1);

    private bool IsWallOrBelowMap(int x, int y, int z) => z < 0 || _map.ShapeOrEmpty(x, y, z) == TileShape.Wall;

    /// <summary>
    /// A ramp rises towards the neighbour whose surface is one level higher: a block on the ramp's
    /// level with open space above it. A cliff, several levels high, is a block too, so it only
    /// counts when no neighbour is one level higher.
    /// </summary>
    private float RampYaw(int x, int y, int z)
    {
        float? cliff = null;
        foreach (var (dx, dy, yaw) in Directions)
        {
            if (_map.ShapeOrEmpty(x + dx, y + dy, z) != TileShape.Wall)
            {
                continue;
            }

            if (_map.ShapeOrEmpty(x + dx, y + dy, z + 1) != TileShape.Wall)
            {
                return yaw;
            }

            cliff ??= yaw;
        }

        return cliff ?? 0;
    }

    private Color MaterialColor(int x, int y, int z)
    {
        var material = _map.MaterialIndexAt(x, y, z);
        return material == GameMap.NoMaterial ? _cutColor : _materialColors[material];
    }

    /// <summary>A darker tone of the block's material, so the strata still read at the cut.</summary>
    private Color CutCapColor(int x, int y, int z) =>
        MaterialColor(x, y, z) * new Color(LevelShading.CutBrightness, LevelShading.CutBrightness, LevelShading.CutBrightness);

    /// <summary>Whether the cell is open ground with grass; a block below it gets a fringe of grass.</summary>
    private bool HasGrass(int x, int y, int z) =>
        z < _map.Size.Depth && _map.ShapeOrEmpty(x, y, z) is TileShape.Floor or TileShape.Ramp && _map.FlagsAt(x, y, z).HasFlag(TileFlags.Grass);

    private int Pattern(int x, int y, int z)
    {
        var material = _map.MaterialIndexAt(x, y, z);
        return material == GameMap.NoMaterial ? 0 : _materialPatterns[material];
    }

    private void Add(TerrainPiece piece, Transform3D transform, Color color, int pattern = 0, int grass = 0) =>
        _buffers[(int)piece].Add((transform, color, pattern, grass));

    private void Fill(MultiMeshInstance3D instance, TerrainPiece piece)
    {
        var buffer = _buffers[(int)piece];
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            UseCustomData = true,
            Mesh = _meshes[piece],
            InstanceCount = buffer.Count,
        };

        // One call for the whole chunk instead of three per instance. Each instance is the 3×4
        // transform by rows, with the origin at the end of each row, its colour and its custom data.
        var data = new float[buffer.Count * FloatsPerInstance];
        for (var i = 0; i < buffer.Count; i++)
        {
            var (transform, color, pattern, grass) = buffer[i];
            var basis = transform.Basis;
            var origin = transform.Origin;
            var at = i * FloatsPerInstance;
            data[at] = basis.Row0.X;
            data[at + 1] = basis.Row0.Y;
            data[at + 2] = basis.Row0.Z;
            data[at + 3] = origin.X;
            data[at + 4] = basis.Row1.X;
            data[at + 5] = basis.Row1.Y;
            data[at + 6] = basis.Row1.Z;
            data[at + 7] = origin.Y;
            data[at + 8] = basis.Row2.X;
            data[at + 9] = basis.Row2.Y;
            data[at + 10] = basis.Row2.Z;
            data[at + 11] = origin.Z;
            data[at + 12] = color.R;
            data[at + 13] = color.G;
            data[at + 14] = color.B;
            data[at + 15] = color.A;
            data[at + 16] = pattern;
            data[at + 17] = grass;
            data[at + 18] = 0;
            data[at + 19] = 0;
        }

        multimesh.Buffer = data;
        instance.Multimesh = multimesh;

        // The node keeps its own reference. Godot .NET crashes on exit if C# still holds
        // references to resources it created, so the C# one is released right away.
        multimesh.Dispose();
    }

    /// <summary>Dark caps on the blocks of the current level that have something above them.</summary>
    private void RebuildCutLayer()
    {
        var cut = _buffers[(int)TerrainPiece.Cut];
        cut.Clear();
        var z = ViewLevel;
        if (z + 1 < _map.Size.Depth)
        {
            for (var y = 0; y < _map.Size.Height; y++)
            {
                for (var x = 0; x < _map.Size.Width; x++)
                {
                    if (_map.ShapeAt(x, y, z) == TileShape.Wall && _map.ShapeAt(x, y, z + 1) != TileShape.Empty)
                    {
                        Add(TerrainPiece.Cut, new Transform3D(Basis.Identity, new Vector3(x + 0.5f, z, y + 0.5f)), CutCapColor(x, y, z), Pattern(x, y, z));
                    }
                }
            }
        }

        Fill(_cutLayer, TerrainPiece.Cut);
    }
}
