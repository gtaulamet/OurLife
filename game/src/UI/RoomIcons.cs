using Godot;
using OurLife.Core.Defs;

namespace OurLife.Game.UI;

/// <summary>
/// The pixel icons of the rooms layer (<c>assets/icons/rooms/&lt;id&gt;.png</c>, drawn by
/// <c>tools/make-room-icons.py</c>): one per function of a room and one per level of quality, and the
/// palette of the levels, one run of tones from a dull grey-brown to gold. Loaded once; the text next to
/// them stays as the accessible fallback.
/// </summary>
public static class RoomIcons
{
    private const string Path = "res://assets/icons/rooms/{0}.png";

    private static readonly Dictionary<string, Texture2D?> Cache = new(StringComparer.Ordinal);
    private static readonly Color Dull = new(118 / 255f, 108 / 255f, 100 / 255f);
    private static readonly Color Gold = new(246 / 255f, 198 / 255f, 48 / 255f);

    /// <summary>The icon of a function (a <see cref="RoomRoleDef"/> id), or null for one without it.</summary>
    public static Texture2D? Role(string role) => Load("role_" + role);

    /// <summary>The icon of a level of quality (a <see cref="RoomLevelDef"/> id), or null for one without it.</summary>
    public static Texture2D? Level(string level) => Load("level_" + level);

    /// <summary>The tone of a level, by its place in the ladder: the lowest is dull and the highest is gold.</summary>
    public static Color ColorOf(RoomLevelDef level, int levels)
    {
        ArgumentNullException.ThrowIfNull(level);
        return levels <= 1 ? Gold : Dull.Lerp(Gold, Math.Clamp(level.Order / (float)(levels - 1), 0f, 1f));
    }

    private static Texture2D? Load(string id)
    {
        if (!Cache.TryGetValue(id, out var texture))
        {
            var path = string.Format(System.Globalization.CultureInfo.InvariantCulture, Path, id);
            texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            Cache[id] = texture;
        }

        return texture;
    }
}
