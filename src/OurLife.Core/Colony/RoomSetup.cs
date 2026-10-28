using System.Runtime.CompilerServices;
using OurLife.Core.Defs;

namespace OurLife.Core.Colony;

/// <summary>
/// The room defs of the content, checked once: the config, the levels by score and the roles (see
/// <c>docs/gdd/habitaciones.md</c>). Derived from the content only, so it is built once per database
/// and is not part of the state. Content without a <see cref="RoomConfigDef"/> has no rooms.
/// </summary>
internal sealed class RoomSetup
{
    private static readonly ConditionalWeakTable<DefDatabase, RoomSetup?> Cache = new();

    private RoomSetup(RoomConfigDef config, IReadOnlyList<RoomLevelDef> levels, IReadOnlyList<RoomRoleDef> roles)
    {
        Config = config;
        Levels = levels;
        Roles = roles;
    }

    public RoomConfigDef Config { get; }

    /// <summary>The levels, lowest first.</summary>
    public IReadOnlyList<RoomLevelDef> Levels { get; }

    /// <summary>The roles, in the order of their defs.</summary>
    public IReadOnlyList<RoomRoleDef> Roles { get; }

    /// <summary>The setup of the content, or null when it defines no room config (then there are no rooms).</summary>
    /// <exception cref="InvalidOperationException">
    /// The content has several configs, no levels, levels that repeat an order, whose scores do not go up with the order
    /// or whose lowest does not start at 0, or two roles with the same order.
    /// </exception>
    public static RoomSetup? From(DefDatabase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (Cache.TryGetValue(content, out var cached))
        {
            return cached;
        }

        var setup = Build(content);
        Cache.AddOrUpdate(content, setup);
        return setup;
    }

    /// <summary>The highest level whose score the points reach; the lowest one for fewer points than all of them.</summary>
    public RoomLevelDef LevelFor(int score)
    {
        var level = Levels[0];
        foreach (var each in Levels)
        {
            if (score >= each.From)
            {
                level = each;
            }
        }

        return level;
    }

    private static RoomSetup? Build(DefDatabase content)
    {
        var configs = content.All<RoomConfigDef>();
        if (configs.Count == 0)
        {
            return null;
        }

        if (configs.Count > 1)
        {
            throw new InvalidOperationException($"The content has {configs.Count} RoomConfig defs; at most one is allowed.");
        }

        var levels = content.All<RoomLevelDef>().OrderBy(level => level.Order).ThenBy(level => level.Id, StringComparer.Ordinal).ToArray();
        if (levels.Length == 0)
        {
            throw new InvalidOperationException("The colony needs at least one RoomLevel def.");
        }

        if (levels[0].From != 0)
        {
            throw new InvalidOperationException($"The lowest room level, '{levels[0].Id}', must start at 0 points.");
        }

        for (var i = 1; i < levels.Length; i++)
        {
            if (levels[i].Order == levels[i - 1].Order)
            {
                throw new InvalidOperationException($"The room levels '{levels[i - 1].Id}' and '{levels[i].Id}' have the same order.");
            }

            if (levels[i].From <= levels[i - 1].From)
            {
                throw new InvalidOperationException($"The score of the room level '{levels[i].Id}' must be higher than the one of '{levels[i - 1].Id}'.");
            }
        }

        var roles = content.All<RoomRoleDef>().OrderBy(role => role.Order).ThenBy(role => role.Id, StringComparer.Ordinal).ToArray();
        for (var i = 1; i < roles.Length; i++)
        {
            if (roles[i].Order == roles[i - 1].Order)
            {
                throw new InvalidOperationException($"The room roles '{roles[i - 1].Id}' and '{roles[i].Id}' have the same order.");
            }
        }

        return new RoomSetup(configs[0], levels, roles);
    }
}
