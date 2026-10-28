using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;
using OurLife.Game.Localization;

namespace OurLife.Game.UI;

/// <summary>
/// The words that name a room for the inspector and the list of rooms: its main function and its
/// level, which agrees with the function in gender ("dormitorio decente", "habitación pobre"). A room
/// with no function is just a room. Texts are in <c>ui.rooms.*</c>, <c>ui.inspector.room*</c> and
/// <c>def.room_role.*</c> and <c>def.room_level.*</c>.
/// </summary>
public static class RoomTexts
{
    /// <summary>The name of the main function of a room, in lower case, or "room" if it has none.</summary>
    public static string RoleName(TextResolver text, Room room)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(room);
        return room.Roles.Count > 0 ? RoleName(text, room.Roles[0]) : text.Format("ui.rooms.generic");
    }

    /// <summary>The name of a function in lower case, for the current language.</summary>
    public static string RoleName(TextResolver text, RoomRoleDef role) => text.Format(role.NameKey!);

    /// <summary>The grammatical gender of the main function of a room, which its level agrees with.</summary>
    public static string GenderOf(TextResolver text, Room room) =>
        room.Roles.Count > 0 ? text.Format($"def.{room.Roles[0].TextCategory}.{room.Roles[0].Id}.gender") : text.Format("ui.rooms.generic_gender");

    /// <summary>The name of the level of a room, in lower case, agreeing with its main function.</summary>
    public static string LevelName(TextResolver text, Room room) =>
        text.Format(room.Level.NameKey!, ("gender", GenderOf(text, room)));

    /// <summary>"Dormitorio decente": main function and level, starting with a capital.</summary>
    public static string Kind(TextResolver text, Room room) =>
        text.Capitalized(text.Format("ui.rooms.kind", ("role", RoleName(text, room)), ("level", LevelName(text, room))));

    /// <summary>"Dormitorio decente, 22 puntos": the line of the list and of the inspector.</summary>
    public static string Line(TextResolver text, Room room) =>
        text.Capitalized(text.Format("ui.inspector.room", ("role", RoleName(text, room)), ("level", LevelName(text, room)), ("score", room.Score)));

    /// <summary>Every function of the room, like "dormitorio y comedor"; empty if it has none.</summary>
    public static string Roles(TextResolver text, Room room) =>
        Join(text, room.Roles.Select(role => RoleName(text, role)).ToList());

    /// <summary>Names (or any words) with commas and the last one after "and".</summary>
    public static string Join(TextResolver text, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(names);
        if (names.Count == 0)
        {
            return string.Empty;
        }

        var head = names[0];
        for (var i = 1; i < names.Count; i++)
        {
            // In Spanish "y" becomes "e" before an "i" sound, which names and words can start with.
            var last = names[i];
            var soundsLikeI = last.Length > 0 && (char.ToLowerInvariant(last[0]) == 'i' || (last.Length > 1 && char.ToLowerInvariant(last[0]) == 'h' && char.ToLowerInvariant(last[1]) == 'i' && !last.StartsWith("hie", StringComparison.OrdinalIgnoreCase)));
            head = i == names.Count - 1
                ? text.Format("ui.rooms.list_and", ("head", head), ("last", last), ("i_sound", soundsLikeI))
                : text.Format("ui.rooms.list_comma", ("head", head), ("next", last));
        }

        return head;
    }

    /// <summary>The names of the colonists who own beds in the room, joined; empty if nobody does.</summary>
    public static string OwnerNames(TextResolver text, Simulation simulation, Room room)
    {
        var owners = RoomRules.Owners(simulation, room);
        var names = new List<string>(owners.Count);
        foreach (var owner in owners)
        {
            names.Add(ColonyText.FullName(simulation.World.Get<Colonist>(owner)));
        }

        return Join(text, names);
    }
}
