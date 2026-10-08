using System.Buffers.Binary;
using System.IO.Hashing;
using System.Text;
using System.Text.Json;
using OurLife.Core.Ecs;

namespace OurLife.Core.Sim;

/// <summary>
/// 64-bit fingerprint (XxHash64) of the whole simulation state: tick, seed, random streams,
/// entity slots, every component and the pending commands. Two runs with the same seed and the
/// same commands must give the same fingerprint after the same number of ticks. Components and
/// commands are hashed through their JSON form, so they must be plain data without cycles.
/// </summary>
public static class StateHasher
{
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true };

    public static ulong Compute(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var hash = new XxHash64();

        AppendInt64(hash, simulation.CurrentTick);
        AppendUInt64(hash, simulation.Seed);

        foreach (var (name, stream) in simulation.Random.Streams)
        {
            AppendString(hash, name);
            AppendUInt64(hash, stream.State);
            AppendUInt64(hash, stream.Increment);
        }

        AppendString(hash, simulation.Progress.CurrentEra);
        AppendInt32(hash, simulation.Progress.Knowledge.Count);
        foreach (var knowledge in simulation.Progress.Knowledge)
        {
            AppendString(hash, knowledge);
        }

        var entities = simulation.World.Entities;
        AppendInt32(hash, entities.SlotCount);
        foreach (var generation in entities.Generations)
        {
            AppendInt32(hash, generation);
        }

        AppendInt32(hash, entities.FreeSlotCount);
        foreach (var slot in entities.FreeSlots)
        {
            AppendInt32(hash, slot);
        }

        var owners = new List<EntityId>();
        foreach (var store in simulation.World.StoresByTypeName())
        {
            // Empty stores are skipped: whether a store was ever created is not part of the state.
            if (store.Count == 0)
            {
                continue;
            }

            AppendString(hash, store.ComponentType.FullName ?? store.ComponentType.Name);
            store.CopyEntitiesTo(owners);
            AppendInt32(hash, owners.Count);
            foreach (var entity in owners)
            {
                AppendInt32(hash, entity.Index);
                AppendInt32(hash, entity.Generation);
                AppendJson(hash, store.GetBoxed(entity), store.ComponentType);
            }
        }

        AppendInt32(hash, simulation.Commands.Count);
        foreach (var command in simulation.Commands.Pending)
        {
            var type = command.GetType();
            AppendString(hash, type.FullName ?? type.Name);
            AppendJson(hash, command, type);
        }

        return hash.GetCurrentHashAsUInt64();
    }

    private static void AppendJson(XxHash64 hash, object value, Type type)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, type, JsonOptions);
        AppendInt32(hash, bytes.Length);
        hash.Append(bytes);
    }

    private static void AppendString(XxHash64 hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        AppendInt32(hash, bytes.Length);
        hash.Append(bytes);
    }

    private static void AppendInt32(XxHash64 hash, int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        hash.Append(buffer);
    }

    private static void AppendInt64(XxHash64 hash, long value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        hash.Append(buffer);
    }

    private static void AppendUInt64(XxHash64 hash, ulong value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        hash.Append(buffer);
    }
}
