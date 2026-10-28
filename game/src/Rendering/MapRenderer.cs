using Godot;
using OurLife.Core.Defs;
using OurLife.Core.Map;

namespace OurLife.Game.Rendering;

/// <summary>
/// Draws the terrain of a <see cref="GameMap"/> (ADR-009). Each chunk of 16×16 cells of a level
/// has one <see cref="MultiMeshInstance3D"/> per <see cref="TerrainPiece"/>, with only the blocks
/// that touch an open cell, created only for pieces the chunk has. The view cuts the map at
/// <see cref="ViewLevel"/>: higher levels are hidden, blocks at the cut get a dark cap, and lower
/// levels darken with depth. Chunks the map reports as dirty are rebuilt every frame. Grass is
/// dressed with tufts, flowers and pebbles of the catalog's decoration, one mesh per chunk with
/// all of them, a single draw call: a hash of each cell chooses them, so a cell always looks the
/// same, and they are not entities of the simulation (ADR-012).
/// </summary>
public partial class MapRenderer : Node3D
{
    // A 3×4 transform, a colour and the custom data, in the layout of MultiMesh.Buffer.
    private const int FloatsPerInstance = 20;

    // How far a decoration may sit from the middle of its cell, and how much its size varies.
    private const float DecorationSpread = 0.3f;
    private const float DecorationSizeVariation = 0.25f;

    // Grass flags of an instance, for the terrain shader.
    private const int GrassTop = 1;
    private const int GrassAll = 2;
    private const int GrassFringe = 4;

    private const string ShaderPath = "res://assets/shaders/terrain.gdshader";
    private const string DecorationShaderPath = "res://assets/shaders/decoration.gdshader";

    private static readonly TerrainPiece[] ChunkPieces =
        [TerrainPiece.Block, TerrainPiece.Floor, TerrainPiece.Ramp, TerrainPiece.Steps, TerrainPiece.Hole, TerrainPiece.Door, TerrainPiece.WallPost, TerrainPiece.WallArm];

    // North, east, south and west in map coordinates, with the yaw that turns +X towards each.
    private static readonly (int Dx, int Dy, float Yaw)[] Directions =
        [(0, -1, Mathf.Pi / 2), (1, 0, 0), (0, 1, -Mathf.Pi / 2), (-1, 0, Mathf.Pi)];

    private readonly Dictionary<TerrainPiece, Mesh> _meshes = [];
    // Per chunk, a MultiMeshInstance3D per piece, then a MeshInstance3D with the decoration.
    private readonly Dictionary<ChunkKey, GeometryInstance3D?[]> _chunks = [];
    private readonly List<ChunkKey> _dirty = [];
    private readonly List<(Transform3D Transform, Color Color, int Pattern, int Grass)>[] _buffers =
        Enumerable.Range(0, Enum.GetValues<TerrainPiece>().Length).Select(_ => new List<(Transform3D, Color, int, int)>()).ToArray();

    private IReadOnlyList<DecorationModel> _decorations = [];

    // The sum of the chances of the catalog, in a thousand.
    private int _decorationTotal;

    // What the chances of the decoration are multiplied by (DecorationLevels).
    private float _decorationDensity = 1f;

    // The cells with a plant, which keep their decoration out; asked once per chunk built.
    private Func<IReadOnlySet<TileCoord>> _plantCells = null!;
    private IReadOnlySet<TileCoord> _plants = new HashSet<TileCoord>();
    private DecorationMesh?[] _decorationMeshes = [];
    private readonly List<(int Model, Transform3D Transform)> _decorationBuffer = [];
    private ShaderMaterial[] _decorationMaterials = [];

    // The seasons (docs/gdd/estaciones-visibles.md): the catalog's, the grass it is measured against,
    // the height of the highest surface of the valley, and the chance of each decoration in the
    // season of the look shown, in a thousand.
    private SeasonCatalog? _seasons;
    private Rgb _baseLush;
    private int _valleyHeight;
    private string? _decorationSeason;
    private int[] _chances = [];

    private GameMap _map = null!;
    private Color[] _materialColors = [];
    private int[] _materialPatterns = [];
    private bool[] _builtMaterials = [];
    private GrassColors _grass = null!;
    private Color _cutColor;
    private ShaderMaterial[] _levelMaterials = [];
    private MultiMeshInstance3D _cutLayer = null!;

    public int ViewLevel { get; private set; }

    /// <param name="plantCells">The cells with a plant: no decoration grows inside a bush or a tree.</param>
    /// <param name="decorationDensity">What the chances of the decoration are multiplied by (see <see cref="DecorationLevels"/>).</param>
    /// <param name="season">The look of the year at the start, so the decoration is built once, with that season's flowers.</param>
    public static MapRenderer Create(GameMap map, VisualCatalog catalog, int viewLevel, Func<IReadOnlySet<TileCoord>> plantCells, float decorationDensity, SeasonLook? season = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(plantCells);
        var renderer = new MapRenderer
        {
            Name = nameof(MapRenderer),
            _map = map,
            _plantCells = plantCells,
            _decorationDensity = decorationDensity,
            _materialColors = map.Materials.Select(material => Color.FromHtml(material.Color)).ToArray(),
            _builtMaterials = map.Materials.Select(material => material.IsBuilt).ToArray(),
            _materialPatterns = map.Materials.Select(material => (int)catalog.Patterns.GetValueOrDefault(material.Id)).ToArray(),
            _grass = catalog.Grass,
            _cutColor = Color.FromHtml(catalog.CutColor),
            _seasons = catalog.Seasons,
            _baseLush = Rgb.FromHex(catalog.Grass.Lush),
            _chances = catalog.Decorations.Select(decoration => decoration.Chance).ToArray(),
        };
        renderer._valleyHeight = renderer.HighestSurface() + 1;
        renderer._levelMaterials = Enumerable.Range(0, LevelShading.MaxDepth + 1).Select(renderer.TerrainMaterial).ToArray();
        renderer._decorationMaterials = Enumerable.Range(0, LevelShading.MaxDepth + 1).Select(DecorationMaterial).ToArray();
        renderer._decorations = catalog.Decorations;
        renderer._decorationTotal = catalog.Decorations.Sum(decoration => decoration.Chance);
        using (var library = new ModelLibrary(catalog))
        {
            // A model that cannot be loaded is left out, like the models of entities; the smoke checks them all.
            renderer._decorationMeshes = catalog.Decorations.Select(decoration => DecorationMesh.Bake(library, decoration)).ToArray();
            foreach (var missing in catalog.Decorations.Where((_, i) => renderer._decorationMeshes[i] is null))
            {
                GD.PushWarning($"The decoration '{missing.Id}' ({missing.ModelPath}) could not be loaded and is left out.");
            }
        }

        foreach (var piece in Enum.GetValues<TerrainPiece>())
        {
            renderer._meshes[piece] = TerrainMeshes.Create(piece);
        }

        renderer._cutLayer = new MultiMeshInstance3D { Name = "CutLayer", MaterialOverride = renderer._levelMaterials[0] };
        renderer.AddChild(renderer._cutLayer);

        renderer.ViewLevel = Math.Clamp(viewLevel, 0, map.Size.Depth - 1);
        if (season is not null)
        {
            renderer.ApplySeason(season);
        }

        renderer.BuildAllChunks();
        renderer.RebuildCutLayer();
        return renderer;
    }

    /// <summary>
    /// Dresses the grass with more or less decoration: only the decoration of each chunk is
    /// built again, the terrain and the chunks waiting to be rebuilt stay as they are.
    /// </summary>
    public void SetDecorationDensity(float density)
    {
        if (Mathf.IsEqualApprox(density, _decorationDensity))
        {
            return;
        }

        _decorationDensity = density;
        _plants = _plantCells();

        // Grass needs a floor, so every chunk with grass already has nodes.
        foreach (var chunk in _chunks.Keys.ToList())
        {
            var instances = _chunks[chunk];
            BuildDecoration(chunk, ref instances);
            Keep(chunk, instances);
        }
    }

    /// <summary>
    /// Dresses again the chunks that hold the given cells, after the cells that stop the decoration
    /// changed (a farm zone drawn or removed): only those chunks are built again, and nothing else.
    /// </summary>
    public void Redress(IEnumerable<TileCoord> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        _plants = _plantCells();
        var chunks = new HashSet<ChunkKey>();
        foreach (var cell in cells)
        {
            var chunk = new ChunkKey(cell.X / GameMap.ChunkSize, cell.Y / GameMap.ChunkSize, cell.Z);
            if (_chunks.ContainsKey(chunk))
            {
                chunks.Add(chunk);
            }
        }

        foreach (var chunk in chunks)
        {
            var instances = _chunks[chunk];
            BuildDecoration(chunk, ref instances);
            Keep(chunk, instances);
        }
    }

    /// <summary>
    /// Paints the grass, the decoration and the high rock for a moment of the year. The colours are
    /// shader parameters, so it costs the same whatever the map; the decoration is built again only
    /// when the season changes, because its flowers do.
    /// </summary>
    public void ApplySeason(SeasonLook look)
    {
        ArgumentNullException.ThrowIfNull(look);
        foreach (var material in _levelMaterials)
        {
            material.SetShaderParameter("grass_lush", Parameter(look.Grass.Lush));
            material.SetShaderParameter("grass_dry", Parameter(look.Grass.Dry));
            material.SetShaderParameter("grass_deep", Parameter(look.Grass.Deep));
            material.SetShaderParameter("snow_color", Parameter(look.SnowColor));
            material.SetShaderParameter("snow_line", look.SnowLine * _valleyHeight);
            material.SetShaderParameter("snow_amount", look.PeakSnow);
        }

        var ratio = new Vector3(
            look.Grass.Lush.R / Math.Max(_baseLush.R, 0.05f),
            look.Grass.Lush.G / Math.Max(_baseLush.G, 0.05f),
            look.Grass.Lush.B / Math.Max(_baseLush.B, 0.05f));
        foreach (var material in _decorationMaterials)
        {
            material.SetShaderParameter("season_ratio", ratio);
            material.SetShaderParameter("snow_color", Parameter(look.SnowColor));
            material.SetShaderParameter("snow_amount", look.GrassSnow);
        }

        if (_decorationSeason == look.Season || _seasons is null)
        {
            return;
        }

        _decorationSeason = look.Season;
        var flowers = _seasons.Seasons[look.Season].Flowers;
        _chances = _decorations.Select(decoration => flowers.GetValueOrDefault(decoration.Id, decoration.Chance)).ToArray();
        _decorationTotal = _chances.Sum();
        _plants = _plantCells();
        foreach (var chunk in _chunks.Keys.ToList())
        {
            var instances = _chunks[chunk];
            BuildDecoration(chunk, ref instances);
            Keep(chunk, instances);
        }
    }

    private static Vector3 Parameter(Rgb color) => new(color.R, color.G, color.B);

    private int HighestSurface()
    {
        var highest = 0;
        for (var y = 0; y < _map.Size.Height; y++)
        {
            for (var x = 0; x < _map.Size.Width; x++)
            {
                highest = Math.Max(highest, _map.SurfaceZ(x, y));
            }
        }

        return highest;
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

            foreach (var material in _decorationMaterials)
            {
                material.Dispose();
            }

            _meshes.Clear();
            _levelMaterials = [];
            _decorationMeshes = [];
            _decorationMaterials = [];
        }
    }

    private ShaderMaterial TerrainMaterial(int depth)
    {
        // Instance colours are #RRGGBB values, like the albedo colours of the models; the shader
        // converts them where the renderer needs it.
        using var shader = GD.Load<Shader>(ShaderPath);
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("brightness", LevelShading.Brightness(depth));
        material.SetShaderParameter("fringe", depth > 0);
        material.SetShaderParameter("grass_lush", ColorParameter(_grass.Lush));
        material.SetShaderParameter("grass_dry", ColorParameter(_grass.Dry));
        material.SetShaderParameter("grass_deep", ColorParameter(_grass.Deep));
        return material;
    }

    /// <summary>The decoration's colours are in its vertices, as #RRGGBB values like the albedo colours of the models.</summary>
    private static ShaderMaterial DecorationMaterial(int depth)
    {
        // Both faces: thin blades of grass are often single sheets (the shader draws them).
        using var shader = GD.Load<Shader>(DecorationShaderPath);
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("brightness", LevelShading.Brightness(depth));
        return material;
    }

    /// <summary>A #RRGGBB colour as the shader takes it: the same values, converted there (see TerrainMaterial).</summary>
    private static Vector3 ColorParameter(string html)
    {
        var color = Color.FromHtml(html);
        return new Vector3(color.R, color.G, color.B);
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

        _plants = _plantCells();

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

            instances ??= new GeometryInstance3D?[ChunkPieces.Length + 1];
            if (instances[i] is null)
            {
                instances[i] = new MultiMeshInstance3D { Name = $"Chunk_{chunk.X}_{chunk.Y}_{chunk.Z}_{piece}" };
                AddChild(instances[i]);
            }

            Fill((MultiMeshInstance3D)instances[i]!, piece);
        }

        BuildDecoration(chunk, ref instances);
        Keep(chunk, instances);
    }

    /// <summary>Keeps the nodes of a chunk, shaded for its level, or forgets a chunk that draws nothing.</summary>
    private void Keep(ChunkKey chunk, GeometryInstance3D?[]? instances)
    {
        if (instances is null || instances.All(instance => instance is null))
        {
            _chunks.Remove(chunk);
            return;
        }

        _chunks[chunk] = instances;
        ApplyLevel(chunk, instances);
    }

    /// <summary>The decoration of a chunk, in the slot after its pieces: one mesh with all of it.</summary>
    private void BuildDecoration(ChunkKey chunk, ref GeometryInstance3D?[]? instances)
    {
        _decorationBuffer.Clear();
        var x0 = chunk.X * GameMap.ChunkSize;
        var y0 = chunk.Y * GameMap.ChunkSize;
        for (var y = y0; y < Math.Min(y0 + GameMap.ChunkSize, _map.Size.Height); y++)
        {
            for (var x = x0; x < Math.Min(x0 + GameMap.ChunkSize, _map.Size.Width); x++)
            {
                AddDecoration(x, y, chunk.Z);
            }
        }

        var decoration = ChunkPieces.Length;
        using var decorationMesh = DecorationMesh.Combine(_decorationMeshes, _decorationBuffer);
        if (decorationMesh is null)
        {
            if (instances?[decoration] is { } emptied)
            {
                emptied.QueueFree();
                instances[decoration] = null;
            }

            return;
        }

        instances ??= new GeometryInstance3D?[ChunkPieces.Length + 1];

        // Tufts so close to the ground striped it with their shadows, and cost a pass.
        instances[decoration] ??= AddDecorationNode(chunk);
        ((MeshInstance3D)instances[decoration]!).Mesh = decorationMesh;
    }

    /// <summary>Shows a chunk darkened for its depth below the current level, or hides it.</summary>
    private void ApplyLevel(ChunkKey chunk, GeometryInstance3D?[] instances)
    {
        var visible = LevelShading.IsVisible(chunk.Z, ViewLevel);
        for (var i = 0; i < instances.Length; i++)
        {
            if (instances[i] is not { } instance)
            {
                continue;
            }

            instance.Visible = visible;
            if (visible)
            {
                var depth = ViewLevel - chunk.Z;
                instance.MaterialOverride = i < ChunkPieces.Length ? _levelMaterials[depth] : _decorationMaterials[depth];
            }
        }
    }

    /// <summary>
    /// A tuft, a flower or pebbles on a floor with grass, chosen by hashes of the cell and the
    /// chances of the catalog, or nothing. One hash says whether the cell is dressed, against the
    /// total chance times the density; another picks the piece by the chances alone. So more
    /// decoration adds pieces and keeps the ones there were, and the mix stays the same at every
    /// density. Furniture and plants keep their cell clear. A piece
    /// moves off the middle of its cell only towards neighbours that could take one too, so it
    /// does not reach into a wall, a piece of furniture or a plant, or hang over a drop.
    /// </summary>
    private void AddDecoration(int x, int y, int z)
    {
        if (_decorationTotal == 0 || _decorationDensity <= 0 || !Dressable(x, y, z))
        {
            return;
        }

        if (CellHash(x, y, z, 0) % 1000 >= Math.Min(1000f, _decorationTotal * _decorationDensity))
        {
            return;
        }

        var pick = (long)(CellHash(x, y, z, 5) % (uint)_decorationTotal);
        for (var i = 0; i < _decorations.Count; i++)
        {
            pick -= _chances[i];
            if (pick >= 0)
            {
                continue;
            }

            var yaw = Unit(CellHash(x, y, z, 1)) * Mathf.Tau;
            var size = 1f + ((Unit(CellHash(x, y, z, 2)) - 0.5f) * 2 * DecorationSizeVariation);
            var offset = new Vector3(
                Toward(Unit(CellHash(x, y, z, 3)) - 0.5f, Dressable(x - 1, y, z), Dressable(x + 1, y, z)),
                0,
                Toward(Unit(CellHash(x, y, z, 4)) - 0.5f, Dressable(x, y - 1, z), Dressable(x, y + 1, z))) * 2 * DecorationSpread;
            var origin = new Vector3(x + 0.5f, z + TerrainMeshes.FloorThickness, y + 0.5f) + offset;
            _decorationBuffer.Add((i, new Transform3D(new Basis(Vector3.Up, yaw).Scaled(new Vector3(size, size, size)), origin)));
            return;
        }
    }

    /// <summary>A floor with grass, without furniture or a plant: a cell the decoration may dress.</summary>
    private bool Dressable(int x, int y, int z) =>
        _map.ShapeOrEmpty(x, y, z) == TileShape.Floor
        && (_map.FlagsAt(x, y, z) & (TileFlags.Grass | TileFlags.Furniture | TileFlags.Blocked)) == TileFlags.Grass
        && !_plants.Contains(new TileCoord(x, y, z));

    /// <summary>An offset from -0.5 to 0.5 along one axis, kept at 0 or less towards a side that cannot be dressed.</summary>
    private static float Toward(float offset, bool lowSideOpen, bool highSideOpen) =>
        offset < 0 ? (lowSideOpen ? offset : 0) : (highSideOpen ? offset : 0);

    /// <summary>
    /// A hash of a cell and a salt, the same on every run and machine: the decoration of a cell
    /// never changes. The view's own; the simulation's random streams are not touched.
    /// </summary>
    private static uint CellHash(int x, int y, int z, uint salt)
    {
        var hash = ((uint)x * 73856093u) ^ ((uint)y * 19349663u) ^ ((uint)z * 83492791u) ^ (salt * 2654435761u);
        hash ^= hash >> 13;
        hash *= 0x5bd1e995u;
        hash ^= hash >> 15;
        return hash;
    }

    /// <summary>A hash as a number from 0 up to 1.</summary>
    private static float Unit(uint hash) => hash / 4294967296f;

    private void AddCell(int x, int y, int z)
    {
        var origin = new Vector3(x + 0.5f, z, y + 0.5f);
        switch (_map.ShapeAt(x, y, z))
        {
            case TileShape.Wall when IsBuiltWall(x, y, z):
                AddThinWall(x, y, z, origin);
                break;
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

    /// <summary>Whether the cell is a wall somebody built, which is drawn thin; the rock and soil of the valley are blocks.</summary>
    private bool IsBuiltWall(int x, int y, int z) =>
        _map.ShapeOrEmpty(x, y, z) == TileShape.Wall && _map.MaterialIndexAt(x, y, z) is var material && material != GameMap.NoMaterial && _builtMaterials[material];

    /// <summary>
    /// A built wall is a post in the middle of the cell with an arm towards each neighbour that
    /// joins it (a wall, a block or a door lying along the arm), so a row of them reads as one thin
    /// wall. Where the cell has no solid ground below (air, or another thin wall), a slab of its
    /// material holds the post; on rock or soil the ground shows, with grass if the cells around
    /// have it.
    /// </summary>
    private void AddThinWall(int x, int y, int z, Vector3 origin)
    {
        var color = MaterialColor(x, y, z);
        var pattern = Pattern(x, y, z);
        Add(TerrainPiece.WallPost, new Transform3D(Basis.Identity, origin), color, pattern);
        foreach (var (dx, dy, yaw) in Directions)
        {
            if (JoinsWall(x + dx, y + dy, z, dx))
            {
                Add(TerrainPiece.WallArm, new Transform3D(new Basis(Vector3.Up, yaw), origin), color, pattern);
            }
        }

        if (z == 0)
        {
            return;
        }

        var below = new TileCoord(x, y, z - 1);
        if (_map.ShapeOrEmpty(below.X, below.Y, below.Z) != TileShape.Wall || IsBuiltWall(below.X, below.Y, below.Z))
        {
            Add(TerrainPiece.Floor, new Transform3D(Basis.Identity, origin), color, pattern);
        }
        else if (Directions.Any(direction => HasGrass(x + direction.Dx, y + direction.Dy, z)))
        {
            // The ground the wall stands on keeps the grass of the ground around it.
            Add(TerrainPiece.Floor, new Transform3D(Basis.Identity, origin), MaterialColor(below.X, below.Y, below.Z), Pattern(below.X, below.Y, below.Z), GrassAll);
        }
    }

    /// <summary>
    /// Whether the cell next to a wall, to the side given by <paramref name="dx"/> (non-zero for
    /// east or west), joins it: a wall or a block, or a door that lies along the same line.
    /// </summary>
    private bool JoinsWall(int x, int y, int z, int dx) => _map.ShapeOrEmpty(x, y, z) switch
    {
        TileShape.Wall => true,
        TileShape.Door => (DoorYaw(x, y, z) == 0) == (dx != 0),
        _ => false,
    };

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

    // A built wall is thin, so it does not hide the faces of the blocks next to it.
    private bool IsWallOrBelowMap(int x, int y, int z) => z < 0 || (_map.ShapeOrEmpty(x, y, z) == TileShape.Wall && !IsBuiltWall(x, y, z));

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

    private MeshInstance3D AddDecorationNode(ChunkKey chunk)
    {
        var node = new MeshInstance3D
        {
            Name = $"Chunk_{chunk.X}_{chunk.Y}_{chunk.Z}_Decoration",
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(node);
        return node;
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
                    if (_map.ShapeAt(x, y, z) == TileShape.Wall && !IsBuiltWall(x, y, z) && _map.ShapeAt(x, y, z + 1) != TileShape.Empty)
                    {
                        Add(TerrainPiece.Cut, new Transform3D(Basis.Identity, new Vector3(x + 0.5f, z, y + 0.5f)), CutCapColor(x, y, z), Pattern(x, y, z));
                    }
                }
            }
        }

        Fill(_cutLayer, TerrainPiece.Cut);
    }
}
