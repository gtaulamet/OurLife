using Godot;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Game.Rendering;

/// <summary>
/// Draws the entities of the simulation with the model of their def's visual: plants, piles of
/// items and colonists (see "Dibujo del mundo" in <c>docs/gdd/interfaz-ip1.md</c>). It follows
/// the entity events: what appears, changes or disappears. Entities above the current level or
/// too deep are hidden, the rest darken with depth like the terrain. A gathered plant uses the
/// visual <c>&lt;visual&gt;.harvested</c> when the catalog has one. Colonists glide between cells
/// with the progress of their <see cref="Movement"/> and face where they walk, and hold the model
/// of what they carry in front of them (see <c>docs/gdd/almacenes.md</c>). A sleeper lies on its
/// back over its cell, along its bed if it sleeps in one (<c>docs/gdd/muebles-y-camas.md</c>).
/// A colonist is one of the variants of its model for its gender, chosen by its id, and plays the
/// animation of what it does, at the speed of the game (ADR-012). At each stage of life it is drawn
/// with the visual <c>&lt;visual&gt;.&lt;stage&gt;</c> when the catalog has one, like a smaller
/// child or a grey-haired elder, and walks at the pace of its stage (<c>docs/gdd/vida-y-generaciones.md</c>).
/// Two colonists who interact show a balloon with the icon of the interaction over their heads
/// for a moment (<c>assets/icons/interactions/&lt;id&gt;.png</c>).
/// </summary>
public partial class EntityRenderer : Node3D
{
    // How fast a drawn colonist catches up with where the simulation says it is, per second.
    private const float GlideRate = 14f;

    // Size of a carried load against a pile on the ground, and where it is held in the world's
    // units, whatever the carrier's scale.
    private const float LoadSize = 0.7f;
    private static readonly Vector3 LoadOffset = new(0f, 0.45f, 0.3f);

    // How much a sleeper rises on a bed, to the mattress, for each unit of the bed model's scale:
    // 0.22 for the bed of the catalog, at scale 0.33, and less for the smaller cradle.
    private const float MattressPerScale = 0.22f / 0.33f;

    // The animations of the Mini Characters kit that loop, by what a colonist does.
    private const string Idle = "idle";
    private const string Walk = "walk";
    private const string Strike = "attack-melee-right";
    private const string Handle = "interact-right";
    private const string PickUp = "pick-up";
    private const string Hold = "holding-both";
    private const string Play = "jump";
    private static readonly string[] LoopingAnimations = [Idle, Walk, Strike, Handle, PickUp, Hold, Play];

    // The balloons of interactions: how long they show and fade, in real seconds, how big their
    // pixels are and how far over a head they float, in the world's units.
    private const string IconPath = "res://assets/icons/interactions/{0}.png";


    private const double BalloonSeconds = 2.5;
    private const double BalloonFadeSeconds = 0.5;
    private const float BalloonPixel = 0.02f;
    private const float BalloonGap = 0.35f;
    private readonly Dictionary<EntityId, Balloon> _balloons = [];

    // The model of each visual and stage in an era, found once: "<visual>.<stage>", or null if the catalog has none.
    private readonly Dictionary<(string Visual, string Stage, string? Era), VisualModel?> _staged = [];

    // The animations that loop: those the renderer asks for and those the catalog plays instead.
    private HashSet<string> _loopingAnimations = null!;
    private ColonyRules _rules = null!;
    private readonly List<EntityId> _expired = [];

    // The animation libraries whose animations were already set to loop, by their Godot id: they
    // are shared by every instance, and their C# wrappers are released after each use.
    private readonly HashSet<ulong> _looped = [];
    private Func<float> _animationSpeed = () => 1f;

    private readonly Dictionary<EntityId, EntityView> _views = [];

    // The views that move, a few among thousands of plants: the only ones updated every frame.
    private readonly Dictionary<EntityId, EntityView> _walkers = [];
    private Simulation _simulation = null!;
    private ModelLibrary _models = null!;
    private int _viewLevel;

    /// <summary>Draws the entities of a simulation. The renderer owns the library from this call on.</summary>
    /// <param name="animationSpeed">How fast the colonists move their bodies: the game's speed, 0 when it stands still. Read every frame.</param>
    public static EntityRenderer Create(Simulation simulation, ModelLibrary models, int viewLevel, Func<float> animationSpeed)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(animationSpeed);
        var renderer = new EntityRenderer
        {
            Name = nameof(EntityRenderer), _simulation = simulation, _models = models, _viewLevel = viewLevel, _animationSpeed = animationSpeed,
            _rules = ColonyRules.From(simulation.Content),
            _loopingAnimations = LoopingAnimations
                .Concat(models.Catalog.Models.SelectMany(model => model.Animations?.Values ?? []))
                .ToHashSet(StringComparer.Ordinal),
        };

        try
        {
            var entities = new List<EntityId>();
            simulation.World.Query<Position>(entities);
            foreach (var entity in entities)
            {
                renderer.Spawn(entity);
            }

            return renderer;
        }
        catch
        {
            // Frees the views built so far and, on deletion, the library.
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

    /// <summary>Follows what the simulation reports about its entities.</summary>
    public void Apply(ISimEvent simEvent)
    {
        switch (simEvent)
        {
            case EntitySpawnedEvent spawned:
                Spawn(spawned.Entity);
                break;
            case EntityChangedEvent changed:
                Refresh(changed.Entity);
                break;
            case EntityRemovedEvent removed:
                Remove(removed.Entity);
                break;
            case InteractionEvent interaction:
                ShowBalloon(interaction.Initiator, interaction.Interaction);
                ShowBalloon(interaction.Receiver, interaction.Interaction);
                break;
        }
    }

    /// <summary>The box a drawn entity fills in the world, or null if it is not drawn or hidden: what a click can hit.</summary>
    public Aabb? DrawnBounds(EntityId entity)
    {
        if (!_views.TryGetValue(entity, out var view) || !view.Node.Visible)
        {
            return null;
        }

        Aabb? bounds = null;
        foreach (var mesh in view.Meshes)
        {
            var box = mesh.GlobalTransform * mesh.GetAabb();
            bounds = bounds is { } merged ? merged.Merge(box) : box;
        }

        // Bones the catalog enlarges, like a child's head, reach above the meshes as modelled.
        return bounds is { } drawn && view.HeightFactor > 1f
            ? new Aabb(drawn.Position, drawn.Size with { Y = drawn.Size.Y * view.HeightFactor })
            : bounds;
    }

    public void SetViewLevel(int viewLevel)
    {
        _viewLevel = viewLevel;
        foreach (var view in _views.Values)
        {
            UpdateVisibility(view);
        }
    }

    public override void _Process(double delta)
    {
        var world = _simulation.World;
        var glide = Math.Min(1f, (float)delta * GlideRate);
        var speed = _animationSpeed();
        FadeBalloons(delta);
        foreach (var (entity, view) in _walkers)
        {
            if (!world.IsAlive(entity))
            {
                continue;
            }

            Animate(entity, view, speed);

            var target = WalkingPosition(entity, out var facing);
            if (SleepingPose(entity, view.Node.Rotation.Y, view.HalfHeight) is { } lying)
            {
                // A balloon would lie down with the sleeper, so it goes.
                if (_balloons.Remove(entity, out var balloon))
                {
                    balloon.Sprite.QueueFree();
                    view.Balloon = null;
                }

                // Laid down at once: a sleeper does not glide into the bed.
                view.Node.Position = target + lying.Offset;
                view.Node.Rotation = new Vector3(-Mathf.Pi / 2, lying.Yaw, 0);
            }
            else
            {
                view.Node.Position = view.Node.Position.Lerp(target, glide);
                view.Node.Rotation = new Vector3(0, facing ?? view.Node.Rotation.Y, 0);
            }

            var level = world.Get<Position>(entity).Z;
            if (level != view.Level)
            {
                view.Level = level;
                UpdateVisibility(view);
            }
        }
    }

    private void Spawn(EntityId entity)
    {
        if (_views.ContainsKey(entity) || !_simulation.World.IsAlive(entity) || ModelOf(entity) is not { } model)
        {
            return;
        }

        var node = _models.Instantiate(model, FileOf(entity, model));

        // Measured before it is placed: half its height is how far a sleeper's feet move back. The
        // meshes are measured as modelled, so bones the catalog enlarges, like a child's head, add on.
        var halfHeight = (ModelLibrary.BoundsOf(node)?.Size.Y ?? 0f) / 2 * HeightFactor(model);
        var position = _simulation.World.Get<Position>(entity);
        var moves = _simulation.World.Has<Movement>(entity);
        node.Position = moves ? WalkingPosition(entity, out _) : CellBase(position.X, position.Y, position.Z);
        if (!moves)
        {
            // Furniture faces where the player turned it; the rest gets a fixed angle per cell.
            var angle = _simulation.World.TryGet<Furniture>(entity, out var furniture) ? furniture.Rotation * 90f : CellAngle(position.X, position.Y);
            node.RotationDegrees = new Vector3(0, angle, 0);
        }

        AddChild(node);
        var animator = AnimatorOf(node);
        var view = new EntityView(node, model, position.Z, ModelLibrary.MeshesOf(node))
        {
            HeightFactor = HeightFactor(model),
            HalfHeight = halfHeight,
            Animator = animator,
            Animations = animator is null ? [] : animator.GetAnimationList().ToHashSet(StringComparer.Ordinal),
        };
        _views.Add(entity, view);
        if (moves)
        {
            _walkers.Add(entity, view);
        }

        UpdateLoad(entity, view);
        UpdateStride(entity, view);
        UpdateVisibility(view);
    }

    /// <summary>
    /// How fast a walker goes against full speed: its stage's and, while it carries, its skill's.
    /// Kept on the view, as it only changes with what the simulation reports as a change.
    /// </summary>
    private void UpdateStride(EntityId entity, EntityView view)
    {
        if (_simulation.World.Has<Movement>(entity))
        {
            view.Stride = (float)MovementSystem.ProgressOf(_simulation, entity, _rules) / MovementSystem.ProgressPerTick;
        }
    }

    /// <summary>Shows the model of what a colonist carries in its hands, or nothing.</summary>
    private void UpdateLoad(EntityId entity, EntityView view)
    {
        var era = _simulation.Progress.CurrentEra;
        VisualModel? wanted = null;
        if (_simulation.World.TryGet<Carried>(entity, out var carried)
            && _models.Catalog.TryResolve(_simulation.Content.Get<ItemDef>(carried.Def).Visual, era, out var model))
        {
            wanted = model;
        }

        if (view.LoadModel == wanted)
        {
            return;
        }

        view.Load?.QueueFree();
        view.Load = null;
        view.LoadModel = wanted;
        if (wanted is null)
        {
            return;
        }

        // A child of the carrier, so it walks and turns with it; sized against the carrier's scale,
        // the catalog's, as the node may be an unscaled holder.
        var scale = view.Model.Scale;
        var load = _models.Instantiate(wanted);
        load.Scale *= LoadSize / scale;
        load.Position = LoadOffset / scale;
        view.Node.AddChild(load);
        view.Load = load;
        view.ShadedDepth = -1;
    }

    /// <summary>A changed entity keeps its node unless its model changed, like a bush that was gathered.</summary>
    private void Refresh(EntityId entity)
    {
        if (!_views.TryGetValue(entity, out var view))
        {
            Spawn(entity);
            return;
        }

        if (ModelOf(entity) is { } model && model != view.Model)
        {
            // A balloon outlives the model, as when a colonist grows up while it chats.
            var balloon = _balloons.GetValueOrDefault(entity);
            Remove(entity);
            Spawn(entity);
            if (balloon is not null)
            {
                ShowBalloon(entity, balloon.Interaction, balloon.Left);
            }

            return;
        }

        UpdateLoad(entity, view);
        UpdateStride(entity, view);
        UpdateVisibility(view);
    }

    private void Remove(EntityId entity)
    {
        if (_views.Remove(entity, out var view))
        {
            _walkers.Remove(entity);
            _balloons.Remove(entity);
            view.Node.QueueFree();
        }
    }

    /// <summary>
    /// A balloon with the icon of an interaction over a colonist's head, replacing the one it had.
    /// It is a child of the colonist, so it walks with it and hides with it; nothing shows for an
    /// interaction without an icon.
    /// </summary>
    private void ShowBalloon(EntityId entity, string interaction, double seconds = BalloonSeconds)
    {
        var path = string.Format(System.Globalization.CultureInfo.InvariantCulture, IconPath, interaction);
        if (!_views.TryGetValue(entity, out var view) || _models.LoadTexture(path) is not { } icon)
        {
            return;
        }

        if (_balloons.Remove(entity, out var old))
        {
            old.Sprite.QueueFree();
        }

        // In the colonist's space, which its catalog scale shrinks or grows.
        var scale = view.Model.Scale;
        var sprite = new Sprite3D
        {
            Name = "Balloon",
            Texture = icon,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            Shaded = false,
            PixelSize = BalloonPixel / scale,
            Position = new Vector3(0, ((2 * view.HalfHeight) + BalloonGap) / scale, 0),
            Modulate = BalloonColor(view, 1f),
        };
        view.Node.AddChild(sprite);
        view.Balloon = _balloons[entity] = new Balloon(sprite, interaction) { Left = seconds };
    }

    /// <summary>The colour of a balloon: darkened with the colonist's depth below the current level, and this see-through.</summary>
    private Color BalloonColor(EntityView view, float alpha)
    {
        var brightness = LevelShading.Brightness(_viewLevel - view.Level);
        return new Color(brightness, brightness, brightness, alpha);
    }

    /// <summary>Balloons fade out at the end of their time, in real seconds, and go.</summary>
    private void FadeBalloons(double delta)
    {
        _expired.Clear();
        foreach (var (entity, balloon) in _balloons)
        {
            balloon.Left -= delta;
            if (balloon.Left <= 0)
            {
                balloon.Sprite.QueueFree();
                if (_views.TryGetValue(entity, out var owner))
                {
                    owner.Balloon = null;
                }

                _expired.Add(entity);
                continue;
            }

            // Godot is told only while it fades.
            if (balloon.Left < BalloonFadeSeconds && _views.TryGetValue(entity, out var view))
            {
                balloon.Sprite.Modulate = BalloonColor(view, (float)(balloon.Left / BalloonFadeSeconds));
            }
        }

        foreach (var entity in _expired)
        {
            _balloons.Remove(entity);
        }
    }

    /// <summary>The model of a plant, a pile, a creature or a piece of furniture, or null for an entity that is not drawn.</summary>
    private VisualModel? ModelOf(EntityId entity)
    {
        var world = _simulation.World;
        var content = _simulation.Content;
        string visual;
        string? harvestedVisual = null;
        if (world.TryGet<Plant>(entity, out var plant))
        {
            visual = content.Get<PlantDef>(plant.Def).Visual;
            if (world.Has<Harvested>(entity))
            {
                harvestedVisual = $"{visual}.harvested";
            }
        }
        else if (world.TryGet<ItemStack>(entity, out var stack))
        {
            visual = content.Get<ItemDef>(stack.Def).Visual;
        }
        else if (world.TryGet<Colonist>(entity, out var colonist))
        {
            visual = content.Get<CreatureDef>(colonist.Creature).Visual;
            if (LifeRules.Stage(_simulation, entity) is { } stage && StagedModel(visual, stage.Id) is { } staged)
            {
                return staged;
            }
        }
        else if (world.TryGet<Furniture>(entity, out var furniture) && content.Get<BuildableDef>(furniture.Buildable).Visual is { } furnitureVisual)
        {
            visual = furnitureVisual;
        }
        else
        {
            return null;
        }

        var era = _simulation.Progress.CurrentEra;
        if (harvestedVisual is not null && _models.Catalog.TryResolve(harvestedVisual, era, out var harvested))
        {
            return harvested;
        }

        return _models.Catalog.TryResolve(visual, era, out var model)
            ? model
            : throw new InvalidOperationException($"The visual catalog has no model for '{visual}'.");
    }

    /// <summary>The model of a visual for a stage of life, <c>&lt;visual&gt;.&lt;stage&gt;</c>, or null if the catalog has none; found once per era.</summary>
    private VisualModel? StagedModel(string visual, string stage)
    {
        var era = _simulation.Progress.CurrentEra;
        if (!_staged.TryGetValue((visual, stage, era), out var model))
        {
            model = _models.Catalog.TryResolve($"{visual}.{stage}", era, out var found) ? found : null;
            _staged.Add((visual, stage, era), model);
        }

        return model;
    }

    /// <summary>How much taller than its meshes as modelled a model is drawn: as much as its most enlarged bone, at least 1.</summary>
    private static float HeightFactor(VisualModel model) => model.BoneScales is { Count: > 0 } bones ? Math.Max(1f, bones.Values.Max()) : 1f;

    /// <summary>
    /// The file of a model a colonist is drawn with: one of the variants for its gender, picked by
    /// its id, so it keeps its look; the model's own file for anything else.
    /// </summary>
    private string? FileOf(EntityId entity, VisualModel model)
    {
        if (model.Variants is null || !_simulation.World.TryGet<Colonist>(entity, out var colonist)
            || !model.Variants.TryGetValue(colonist.Gender.ToString().ToLowerInvariant(), out var files))
        {
            return null;
        }

        // Mixed so that consecutive ids, and every other one, spread over all the files.
        var hash = unchecked((uint)entity.Index * 0x9E3779B1u);
        hash ^= hash >> 15;
        hash = unchecked(hash * 0x85EBCA77u);
        hash ^= hash >> 13;
        return files[(int)(hash % (uint)files.Count)];
    }

    /// <summary>
    /// The animation player of a model, with the animations it has set to loop: those the renderer
    /// asks for and every one the catalog plays instead (<see cref="VisualModel.Animations"/>), as
    /// the library is shared by every visual of the file. Null for a model without one.
    /// </summary>
    private AnimationPlayer? AnimatorOf(Node node)
    {
        var animator = FindAnimator(node);
        if (animator is null)
        {
            return null;
        }

        foreach (var name in animator.GetAnimationLibraryList())
        {
            using var library = animator.GetAnimationLibrary(name);
            if (!_looped.Add(library.GetInstanceId()))
            {
                continue;
            }

            foreach (var animation in _loopingAnimations.Where(animation => library.HasAnimation(animation)))
            {
                using var clip = library.GetAnimation(animation);
                clip.LoopMode = Animation.LoopModeEnum.Linear;
            }
        }

        return animator;
    }

    private static AnimationPlayer? FindAnimator(Node node)
    {
        if (node is AnimationPlayer player)
        {
            return player;
        }

        foreach (var child in node.GetChildren())
        {
            if (FindAnimator(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Plays the animation of what a colonist does: walking, striking to dig or chop, handling
    /// to build or to feed a baby, picking up to gather, jumping to play, holding what it carries,
    /// or standing; the model's own one when it has it, like a baby sitting. At the game's speed,
    /// and walking at the pace of its stage of life.
    /// </summary>
    private void Animate(EntityId entity, EntityView view, float speed)
    {
        if (view.Animator is not { } animator)
        {
            return;
        }

        var world = _simulation.World;
        var moving = world.TryGet<Movement>(entity, out var movement) && movement.IsMoving;
        var job = world.TryGet<Job>(entity, out var current) ? current : null;
        var working = job is { Stage: JobStage.Working };
        var name = moving ? Walk
            : working && job!.Kind is JobKind.Mine or JobKind.DigStairs or JobKind.Chop or JobKind.Deconstruct ? Strike
            : working && job!.Kind is JobKind.Build ? Handle
            : job is { Kind: JobKind.FeedBaby, Stage: JobStage.Carrying } ? Handle
            : working && job!.Kind is JobKind.Harvest ? PickUp
            : working && job!.Kind is JobKind.Play ? Play
            : world.Has<Carried>(entity) ? Hold
            : Idle;

        // A sleeper lies still with the plain pose: a baby's own one for standing is sitting.
        var asleep = job is { Kind: JobKind.Sleep, Stage: JobStage.Working };
        name = asleep ? Idle : view.Model.AnimationFor(name);
        if (!view.Animations.Contains(name))
        {
            name = asleep ? Idle : view.Model.AnimationFor(Idle);
        }

        // Walking at the pace it really goes: its stage's, and its skill's while it carries.
        if (moving)
        {
            speed *= view.Stride;
        }

        // Godot is asked only when something changes: its calls cost on every frame.

        if (view.Playing != name && view.Animations.Contains(name))
        {
            animator.Play(name);
            view.Playing = name;
        }

        if (view.Speed != speed)
        {
            animator.SpeedScale = speed;
            view.Speed = speed;
        }
    }

    /// <summary>
    /// How a sleeping colonist lies, or null if it is not asleep: on its back, its feet moved back
    /// half its height so its body lies centred on the cell. On the bed its job is for, it takes
    /// the bed's heading and lies on the mattress; on the ground it keeps the heading it had.
    /// </summary>
    private (Vector3 Offset, float Yaw)? SleepingPose(EntityId entity, float yaw, float halfHeight)
    {
        var world = _simulation.World;
        if (!world.TryGet<Job>(entity, out var job) || job.Kind != JobKind.Sleep || job.Stage != JobStage.Working)
        {
            return null;
        }

        var lift = 0f;
        if (!job.Target.IsNone && world.IsAlive(job.Target) && world.TryGet<Furniture>(job.Target, out var bed)
            && world.Get<Position>(job.Target).Cell == world.Get<Position>(entity).Cell)
        {
            yaw = Mathf.DegToRad(bed.Rotation * 90f);
            lift = MattressPerScale * (_views.TryGetValue(job.Target, out var bedView) ? bedView.Model.Scale : 0.33f);
        }

        // Lying back, the head points the way the feet had behind them: the feet go the other way.
        var feet = new Basis(Vector3.Up, yaw) * new Vector3(0, 0, halfHeight);
        return (feet + new Vector3(0, lift, 0), yaw);
    }

    /// <summary>Where a walker is drawn: between its cell and the next one of its route, by its progress.</summary>
    private Vector3 WalkingPosition(EntityId entity, out float? facing)
    {
        var world = _simulation.World;
        var position = world.Get<Position>(entity);
        var here = CellBase(position.X, position.Y, position.Z);
        facing = null;
        if (!world.TryGet<Movement>(entity, out var movement) || !movement.IsMoving)
        {
            return here;
        }

        var next = movement.Path[movement.Next];
        // The same cost the simulation walks by, so the drawn fraction matches its progress.
        var cost = MovementSystem.StepCost(_simulation, entity, position.Cell, next);
        var fraction = Math.Clamp((float)movement.Progress / cost, 0f, 1f);
        var there = CellBase(next.X, next.Y, next.Z);

        // A step straight up or down the stairs has no heading: the walker keeps the last one.
        if (next.X != position.X || next.Y != position.Y)
        {
            facing = Mathf.Atan2(there.X - here.X, there.Z - here.Z);
        }

        return here.Lerp(there, fraction);
    }

    private void UpdateVisibility(EntityView view)
    {
        view.Node.Visible = LevelShading.IsVisible(view.Level, _viewLevel);
        var depth = _viewLevel - view.Level;
        if (view.Node.Visible && view.ShadedDepth != depth)
        {
            _models.Shade(view.Model, view.Meshes, depth);
            if (view.Load is { } load)
            {
                _models.Shade(view.LoadModel!, ModelLibrary.MeshesOf(load), depth);
            }

            view.ShadedDepth = depth;
            if (view.Balloon is { } balloon)
            {
                balloon.Sprite.Modulate = BalloonColor(view, balloon.Sprite.Modulate.A);
            }
        }
    }

    /// <summary>The centre of a cell's floor, where entities stand.</summary>
    private static Vector3 CellBase(int x, int y, int z) => new(x + 0.5f, z + TerrainMeshes.FloorThickness, y + 0.5f);

    /// <summary>A fixed angle in [0, 360) for a cell, from a small integer hash, so a grove does not look copied.</summary>
    private static float CellAngle(int x, int y)
    {
        var hash = unchecked((uint)((x * 73856093) ^ (y * 19349663)));
        hash ^= hash >> 13;
        hash = unchecked(hash * 0x5bd1e995);
        return hash % 360;
    }

    /// <summary>A balloon over a colonist, of an interaction, and the real seconds it has left.</summary>
    private sealed class Balloon(Sprite3D sprite, string interaction)
    {
        public Sprite3D Sprite { get; } = sprite;

        public string Interaction { get; } = interaction;

        public double Left { get; set; }
    }

    private sealed class EntityView(Node3D node, VisualModel model, int level, IReadOnlyList<MeshInstance3D> meshes)
    {
        public Node3D Node { get; } = node;

        public VisualModel Model { get; } = model;

        public int Level { get; set; } = level;

        public IReadOnlyList<MeshInstance3D> Meshes { get; } = meshes;

        /// <summary>Depth its materials are shaded for; -1 before the first time.</summary>
        public int ShadedDepth { get; set; } = -1;

        /// <summary>The model of what a colonist carries, held in its hands, and its node.</summary>
        public VisualModel? LoadModel { get; set; }

        public Node3D? Load { get; set; }

        /// <summary>Half the height of its model as drawn, to lay a sleeper centred on its cell.</summary>
        public float HalfHeight { get; init; }

        /// <summary>How much taller than its meshes as modelled it is drawn, for enlarged bones.</summary>
        public float HeightFactor { get; init; } = 1f;

        /// <summary>How fast it walks against full speed, for its walking animation.</summary>
        public float Stride { get; set; } = 1f;

        /// <summary>The balloon over it, if one shows, to darken it with its depth.</summary>
        public Balloon? Balloon { get; set; }

        /// <summary>The player of the model's animations, for a colonist; null for a model without them.</summary>
        public AnimationPlayer? Animator { get; init; }

        /// <summary>The names of the model's animations.</summary>
        public IReadOnlySet<string> Animations { get; init; } = new HashSet<string>();

        /// <summary>The animation it plays and how fast, so Godot is only told when they change.</summary>
        public string? Playing { get; set; }

        public float Speed { get; set; } = -1f;
    }
}
