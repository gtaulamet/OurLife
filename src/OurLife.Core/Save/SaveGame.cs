using System.Buffers.Binary;
using System.Collections;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Map;
using OurLife.Core.Sim;

namespace OurLife.Core.Save;

/// <summary>
/// Writes and reads saved games (ADR-010, <c>docs/gdd/guardado.md</c>): a ZIP with a small
/// <c>header.json</c> and the whole state in <c>state.json</c>. Loading gives back a simulation
/// with the same state fingerprint and no systems: the caller adds the same systems as when the
/// game started. Derived data (ADR-011) is rebuilt on first use. Core only sees streams; files
/// and slots belong to the presentation.
/// </summary>
public static class SaveGame
{
    /// <summary>Grows with every change to the saved state that older readers cannot handle.</summary>
    public const int FormatVersion = 6;

    /// <summary>
    /// The oldest format this game still reads. Older formats are migrated on loading, one step
    /// after another (ADR-013): format 1 (v0.2.x) gets ages and surnames, format 2 (H3.2)
    /// personalities, format 3 (H3.3) skills and format 4 (H3.4) relations; format 5 (H3.5) only
    /// lacks thoughts, desires and crises, which start empty (see "Guardado y migración" in <c>docs/gdd/vida-y-generaciones.md</c>).
    /// </summary>
    public const int OldestFormatVersion = 1;

    private const string HeaderEntry = "header.json";
    private const string StateEntry = "state.json";

    // ZIP entries carry a timestamp; a fixed one keeps the bytes of a save independent of the
    // clock. The real time of the save is in the header.
    private static readonly DateTimeOffset EntryTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        // Enums by name only: a number would deserialize to a value the enum does not define.
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
    };

    // The header is read leniently: a newer game may add fields to it, and its format version
    // must still be read to say the save is newer rather than damaged.
    private static readonly JsonSerializerOptions HeaderOptions = new(JsonOptions)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    private static readonly ConcurrentDictionary<Type, CheckedProperty[]> PropertiesByType = new();

    /// <summary>Writes the game to <paramref name="output"/>, which stays open.</summary>
    /// <exception cref="InvalidOperationException">A component or pending command is not registered in <see cref="SaveTypes"/>.</exception>
    public static void Write(Stream output, Simulation simulation, SaveInfo info)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(info);

        // Build both entries before writing, so an unregistered type fails with nothing written.
        var header = new SaveHeader
        {
            FormatVersion = FormatVersion,
            GameId = info.GameId,
            IsAutosave = info.IsAutosave,
            GameVersion = info.GameVersion,
            SavedAt = info.SavedAt,
            ColonyName = simulation.ColonyName,
            Tick = simulation.CurrentTick,
            Era = simulation.Progress.CurrentEra,
            Colonists = simulation.World.Store<Colonist>().Count,
        };
        var state = Capture(simulation);

        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        WriteEntry(archive, HeaderEntry, header);
        WriteEntry(archive, StateEntry, state);
    }

    /// <summary>Reads only the header, to list a slot.</summary>
    /// <exception cref="SaveLoadException">The file is damaged.</exception>
    public static SaveHeader ReadHeader(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Guard(() =>
        {
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            return ReadHeader(archive);
        });
    }

    /// <summary>Reads a saved game into a new simulation over <paramref name="content"/>, without systems.</summary>
    /// <exception cref="SaveLoadException">The file is damaged, of another format version, or uses defs the content lacks.</exception>
    public static Simulation Load(Stream input, DefDatabase content) => Load(input, content, out _);

    /// <summary>The same as <see cref="Load(Stream, DefDatabase)"/>, giving also the header, so the file is read once.</summary>
    /// <exception cref="SaveLoadException">The file is damaged, of another format version, or uses defs the content lacks.</exception>
    public static Simulation Load(Stream input, DefDatabase content, out SaveHeader header)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(content);
        (var simulation, header) = Guard(() =>
        {
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            var header = ReadHeader(archive);
            if (header.FormatVersion > FormatVersion)
            {
                throw new SaveLoadException(SaveLoadError.Newer, $"format {header.FormatVersion}, this game reads {FormatVersion}");
            }

            if (header.FormatVersion < OldestFormatVersion)
            {
                throw new SaveLoadException(
                    SaveLoadError.Older, $"format {header.FormatVersion}, this game reads {OldestFormatVersion} to {FormatVersion}");
            }

            var simulation = Restore(ReadEntry<SavedState>(archive, StateEntry, JsonOptions), content);
            if (header.FormatVersion <= 1)
            {
                MigrateFromVersion1(simulation);
            }

            if (header.FormatVersion <= 2)
            {
                ColonyStart.MigratePersonalities(simulation);
            }

            if (header.FormatVersion <= 3)
            {
                ColonyStart.MigrateSkills(simulation);
            }

            if (header.FormatVersion <= 4)
            {
                ColonyStart.MigrateRelations(simulation);
            }

            return (simulation, header);
        });
        return simulation;
    }

    /// <summary>The header, checked: valid JSON can still hold null texts or negative numbers.</summary>
    private static SaveHeader ReadHeader(ZipArchive archive)
    {
        var header = ReadEntry<SaveHeader>(archive, HeaderEntry, HeaderOptions);
        if (header.GameId is null || header.GameVersion is null || header.ColonyName is null || header.Era is null
            || header.Tick < 0 || header.Colonists < 0)
        {
            throw Corrupt("the header has a null text or a negative number");
        }

        return header;
    }

    private static SavedState Capture(Simulation simulation)
    {
        var state = new SavedState
        {
            Tick = simulation.CurrentTick,
            Seed = simulation.Seed,
            ColonyName = simulation.ColonyName,
            LifePace = simulation.LifePace,
            Era = simulation.Progress.CurrentEra,
            Knowledge = [.. simulation.Progress.Knowledge.Order(StringComparer.Ordinal)],
            Random = [.. simulation.Random.Streams.Select(entry => new SavedStream(entry.Key, entry.Value.State, entry.Value.Increment))],
            Generations = [.. simulation.World.Entities.Generations],
            FreeSlots = [.. simulation.World.Entities.FreeSlots],
        };

        if (simulation.HasMap)
        {
            var map = simulation.Map;
            var materials = new byte[map.MaterialIndexes.Length * sizeof(ushort)];
            for (var i = 0; i < map.MaterialIndexes.Length; i++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(materials.AsSpan(i * sizeof(ushort)), map.MaterialIndexes[i]);
            }

            state.Map = new SavedMap
            {
                Width = map.Size.Width,
                Height = map.Size.Height,
                Depth = map.Size.Depth,
                Materials = [.. map.Materials.Select(material => material.Id)],
                Shapes = Convert.ToBase64String(map.ShapeBytes),
                MaterialIndexes = Convert.ToBase64String(materials),
                Flags = Convert.ToBase64String(map.FlagBytes),
            };
        }

        var owners = new List<EntityId>();
        foreach (var store in simulation.World.StoresByTypeName())
        {
            if (store.Count == 0)
            {
                continue;
            }

            var saveType = SaveTypes.ComponentByType(store.ComponentType)
                ?? throw new InvalidOperationException($"The component {store.ComponentType.FullName} is not registered in SaveTypes.");
            var saved = new SavedStore { Type = saveType.Name };
            store.CopyEntitiesTo(owners);
            foreach (var owner in owners)
            {
                saved.Entities.Add(new SavedComponent
                {
                    Index = owner.Index,
                    Generation = owner.Generation,
                    Value = JsonSerializer.SerializeToElement(store.GetBoxed(owner), store.ComponentType, JsonOptions),
                });
            }

            state.Components.Add(saved);
        }

        foreach (var command in simulation.Commands.Pending)
        {
            var type = command.GetType();
            var saveType = SaveTypes.CommandByType(type)
                ?? throw new InvalidOperationException($"The command {type.FullName} is not registered in SaveTypes.");
            state.Commands.Add(new SavedCommand
            {
                Type = saveType.Name,
                Value = JsonSerializer.SerializeToElement(command, type, JsonOptions),
            });
        }

        return state;
    }

    private static Simulation Restore(SavedState state, DefDatabase content)
    {
        // Valid JSON can still hold null where a list belongs.
        if (state.ColonyName is null || state.LifePace is null || state.Era is null || state.Knowledge is null || state.Random is null
            || state.Generations is null || state.FreeSlots is null || state.Components is null || state.Commands is null
            || state.Components.Any(store => store?.Type is null || store.Entities is null || store.Entities.Any(saved => saved is null))
            || state.Commands.Any(saved => saved?.Type is null)
            || state.Random.Any(stream => stream?.Name is null)
            || state.Map is { Materials: null } or { Shapes: null } or { MaterialIndexes: null } or { Flags: null })
        {
            throw Corrupt("a list or text of the state is null");
        }

        var simulation = new Simulation(state.Seed, content) { ColonyName = state.ColonyName, CurrentTick = state.Tick };
        if (state.Tick < 0)
        {
            throw Corrupt($"negative tick {state.Tick}");
        }

        if (state.LifePace.Length > 0)
        {
            RequireDef<LifePaceDef>(content, state.LifePace);
            simulation.LifePace = state.LifePace;
        }

        RequireDef<EraDef>(content, state.Era);
        simulation.Progress.CurrentEra = state.Era;
        foreach (var knowledge in state.Knowledge)
        {
            RequireDef<KnowledgeDef>(content, knowledge);
            simulation.Progress.Learn(knowledge);
        }

        foreach (var stream in state.Random)
        {
            simulation.Random.Restore(stream.Name, Pcg32.FromState(stream.State, stream.Increment));
        }

        if (state.Map is not null)
        {
            simulation.AttachMap(RestoreMap(state.Map, content));
        }

        simulation.World.Entities.Restore(state.Generations, state.FreeSlots);

        var defIds = new HashSet<(Type, string)>(content.AllDefs().Select(def => (def.GetType(), def.Id)));
        foreach (var store in state.Components)
        {
            var saveType = SaveTypes.ComponentByName(store.Type) ?? throw Corrupt($"unknown component '{store.Type}'");
            foreach (var saved in store.Entities)
            {
                var entity = new EntityId(saved.Index, saved.Generation);
                if (!simulation.World.IsAlive(entity))
                {
                    throw Corrupt($"a {store.Type} belongs to {entity}, which is not alive");
                }

                var component = saved.Value.Deserialize(saveType.Type, JsonOptions) ?? throw Corrupt($"a null {store.Type}");
                CheckComponent(component, defIds, simulation);
                if (component is Position position && simulation.HasMap && !simulation.Map.Size.Contains(position.X, position.Y, position.Z))
                {
                    throw Corrupt($"{entity} stands outside the map, at {position.Cell}");
                }

                if (component is Movement movement && (movement.Next < 0 || movement.Next > movement.Path.Count || movement.Progress < 0))
                {
                    throw Corrupt($"{entity} walks step {movement.Next} of a path of {movement.Path.Count}");
                }

                if (component is Life { AgeDays: < 0 } life)
                {
                    throw Corrupt($"{entity} has lived {life.AgeDays} days");
                }

                if (component is Personality personality
                    && (personality.Traits.Any(trait => trait.Value is < 0 or > 100 || trait.MovedToday is < -100 or > 100)
                        || personality.Traits.Select(trait => trait.Trait).Distinct(StringComparer.Ordinal).Count() != personality.Traits.Count
                        || personality.DriftDay < 0))
                {
                    throw Corrupt($"{entity} has a trait out of range or twice");
                }

                if (component is Skills skills
                    && (skills.Values.Any(skill => skill.Level < 0 || skill.Experience < 0)
                        || skills.Values.Select(skill => skill.WorkType).Distinct(StringComparer.Ordinal).Count() != skills.Values.Count))
                {
                    throw Corrupt($"{entity} has a skill below zero or twice");
                }

                if (component is Relations relations
                    && (relations.Others.Any(relation => relation.Opinion is < -100 or > 100 || relation.Romance is < 0 or > 100 || relation.Other == entity)
                        || relations.Others.Zip(relations.Others.Skip(1)).Any(pair => pair.First.Other.CompareTo(pair.Second.Other) >= 0)))
                {
                    throw Corrupt($"{entity} has an opinion out of range, of itself, or out of order");
                }

                saveType.AddTo!(simulation.World, entity, component);
            }
        }

        foreach (var saved in state.Commands)
        {
            var saveType = SaveTypes.CommandByName(saved.Type) ?? throw Corrupt($"unknown command '{saved.Type}'");
            var command = (ISimCommand?)saved.Value.Deserialize(saveType.Type, JsonOptions) ?? throw Corrupt($"a null {saved.Type}");
            simulation.Commands.Enqueue(command);
        }

        return simulation;
    }

    /// <summary>
    /// A game of format 1 (v0.2.x), already restored, as if it had been saved in format 2: the
    /// walkers count their progress in thousandths of a tick instead of tenths, and the colonists
    /// get ages and surnames (ADR-013).
    /// </summary>
    private static void MigrateFromVersion1(Simulation simulation)
    {
        var walkers = new List<EntityId>();
        simulation.World.Query<Movement>(walkers);
        foreach (var walker in walkers)
        {
            simulation.World.Get<Movement>(walker).Progress *= 100;
        }

        ColonyStart.MigrateLives(simulation);
    }

    private static GameMap RestoreMap(SavedMap saved, DefDatabase content)
    {
        // The arrays are checked against the size before the map allocates it: a damaged size
        // could ask for gigabytes.
        var shapes = Convert.FromBase64String(saved.Shapes);
        var materials = Convert.FromBase64String(saved.MaterialIndexes);
        var flags = Convert.FromBase64String(saved.Flags);
        var cells = (long)saved.Width * saved.Height * saved.Depth;
        if (saved.Width <= 0 || saved.Height <= 0 || saved.Depth <= 0
            || shapes.Length != cells || flags.Length != cells || materials.Length != cells * sizeof(ushort))
        {
            throw Corrupt("the map arrays do not match its size");
        }

        var size = new MapSize(saved.Width, saved.Height, saved.Depth);
        var map = new GameMap(size, content.All<MaterialDef>());

        // Saved material indexes point into the saved table of ids; translate them to the content.
        var remap = new ushort[saved.Materials.Count];
        for (var i = 0; i < remap.Length; i++)
        {
            RequireDef<MaterialDef>(content, saved.Materials[i]);
            remap[i] = (ushort)content.IndexOf<MaterialDef>(saved.Materials[i]);
        }

        for (var index = 0; index < size.CellCount; index++)
        {
            var shape = (TileShape)shapes[index];
            if (!Enum.IsDefined(shape))
            {
                throw Corrupt($"unknown tile shape {shapes[index]}");
            }

            var material = BinaryPrimitives.ReadUInt16LittleEndian(materials.AsSpan(index * sizeof(ushort)));
            if (material != GameMap.NoMaterial)
            {
                material = material < remap.Length ? remap[material] : throw Corrupt($"unknown material index {material}");
            }

            var cell = size.CoordOf(index);
            map.SetTileQuietly(cell.X, cell.Y, cell.Z, shape, material, (TileFlags)flags[index]);
        }

        return map;
    }

    /// <summary>
    /// Checks the <see cref="DefRefAttribute"/> properties of a component and of the objects it
    /// holds, that none of its lists is null (components always start them empty), and that every
    /// cell it names is inside the map.
    /// </summary>
    private static void CheckComponent(object value, HashSet<(Type, string)> defIds, Simulation simulation)
    {
        if (value is TileCoord cell)
        {
            if (simulation.HasMap && !simulation.Map.Size.Contains(cell.X, cell.Y, cell.Z))
            {
                throw Corrupt($"the cell {cell} is outside the map");
            }

            return;
        }

        foreach (var property in PropertiesByType.GetOrAdd(value.GetType(), FindCheckedProperties))
        {
            var child = property.Info.GetValue(value);
            if (child is null)
            {
                if (property.Info.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(property.Info.PropertyType))
                {
                    throw Corrupt($"the list {value.GetType().Name}.{property.Info.Name} is null");
                }

                continue;
            }

            if (property.DefType is { } defType)
            {
                IEnumerable<string> ids = child is string single ? [single] : (IEnumerable<string>)child;
                foreach (var id in ids)
                {
                    if (id is null)
                    {
                        throw Corrupt($"the list {value.GetType().Name}.{property.Info.Name} holds a null");
                    }

                    if (!defIds.Contains((defType, id)))
                    {
                        throw new SaveLoadException(SaveLoadError.MissingContent, id);
                    }
                }
            }
            else if (child is IEnumerable items)
            {
                foreach (var item in items)
                {
                    if (item is null)
                    {
                        throw Corrupt($"the list {value.GetType().Name}.{property.Info.Name} holds a null");
                    }

                    if (item.GetType().Assembly == typeof(SaveGame).Assembly)
                    {
                        CheckComponent(item, defIds, simulation);
                    }
                }
            }
            else
            {
                CheckComponent(child, defIds, simulation);
            }
        }
    }

    /// <summary>The properties to check: marked strings and lists of strings, cells, Core classes and lists.</summary>
    private static CheckedProperty[] FindCheckedProperties(Type type) =>
        [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0)
            .Where(property =>
                ((property.PropertyType == typeof(string) || property.PropertyType == typeof(List<string>)) && property.IsDefined(typeof(DefRefAttribute)))
                || (property.PropertyType != typeof(string) && IsCoreClassOrList(property.PropertyType)))
            .Select(property => new CheckedProperty(property, property.GetCustomAttribute<DefRefAttribute>()?.DefType))];

    /// <summary>A property to check, with the def type its id names, if it is a <see cref="DefRefAttribute"/>.</summary>
    private sealed record CheckedProperty(PropertyInfo Info, Type? DefType);

    private static bool IsCoreClassOrList(Type type)
    {
        if ((type.IsClass || type == typeof(TileCoord)) && type.Assembly == typeof(SaveGame).Assembly)
        {
            return true;
        }

        // Every list, so a null one is caught; only Core classes inside it are walked.
        return type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type);
    }

    private static void RequireDef<T>(DefDatabase content, string id)
        where T : Def
    {
        if (!content.TryGet<T>(id, out _))
        {
            throw new SaveLoadException(SaveLoadError.MissingContent, id);
        }
    }

    private static SaveLoadException Corrupt(string detail) => new(SaveLoadError.Corrupt, detail);

    /// <summary>Turns every failure to read the file into a <see cref="SaveLoadError.Corrupt"/>.</summary>
    private static T Guard<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (SaveLoadException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or FormatException or ArgumentException
                                       or InvalidOperationException or NotSupportedException or EndOfStreamException
                                       or OverflowException or KeyNotFoundException)
        {
            throw new SaveLoadException(SaveLoadError.Corrupt, ex.Message, ex);
        }
    }

    private static void WriteEntry<T>(ZipArchive archive, string name, T value)
    {
        // Fastest would halve the size gain and save little time: most of it goes to the JSON.
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = EntryTimestamp;
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, value, JsonOptions);
    }

    private static T ReadEntry<T>(ZipArchive archive, string name, JsonSerializerOptions options)
    {
        var entry = archive.GetEntry(name) ?? throw Corrupt($"the entry {name} is missing");
        using var stream = entry.Open();
        return JsonSerializer.Deserialize<T>(stream, options) ?? throw Corrupt($"the entry {name} is empty");
    }

    private sealed class SavedState
    {
        public long Tick { get; set; }

        public ulong Seed { get; set; }

        public string ColonyName { get; set; } = string.Empty;

        /// <summary>Empty in format 1, which had no pace of life.</summary>
        public string LifePace { get; set; } = string.Empty;

        public string Era { get; set; } = string.Empty;

        public List<string> Knowledge { get; set; } = [];

        public List<SavedStream> Random { get; set; } = [];

        public SavedMap? Map { get; set; }

        public List<int> Generations { get; set; } = [];

        public List<int> FreeSlots { get; set; } = [];

        public List<SavedStore> Components { get; set; } = [];

        public List<SavedCommand> Commands { get; set; } = [];
    }

    private sealed record SavedStream(string Name, ulong State, ulong Increment);

    private sealed class SavedMap
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public int Depth { get; set; }

        public List<string> Materials { get; set; } = [];

        public string Shapes { get; set; } = string.Empty;

        public string MaterialIndexes { get; set; } = string.Empty;

        public string Flags { get; set; } = string.Empty;
    }

    private sealed class SavedStore
    {
        public string Type { get; set; } = string.Empty;

        public List<SavedComponent> Entities { get; set; } = [];
    }

    private sealed class SavedComponent
    {
        public int Index { get; set; }

        public int Generation { get; set; }

        public JsonElement Value { get; set; }
    }

    private sealed class SavedCommand
    {
        public string Type { get; set; } = string.Empty;

        public JsonElement Value { get; set; }
    }
}
