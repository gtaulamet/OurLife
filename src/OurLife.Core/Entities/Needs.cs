using OurLife.Core.Defs;

namespace OurLife.Core.Entities;

/// <summary>The needs of a creature, in the order its <see cref="Defs.CreatureDef"/> lists them.</summary>
public sealed class Needs
{
    public List<NeedState> Values { get; set; } = [];

    /// <summary>The state of a need, or null if the creature does not have it.</summary>
    public NeedState? Find(string need)
    {
        foreach (var value in Values)
        {
            if (value.Need == need)
            {
                return value;
            }
        }

        return null;
    }
}

/// <summary>How satisfied a need is, from 0 to the need's maximum.</summary>
public sealed class NeedState
{
    /// <summary>Id of its <see cref="Defs.NeedDef"/>.</summary>
    [DefRef(typeof(NeedDef))]
    public string Need { get; set; } = string.Empty;

    public int Value { get; set; }

    /// <summary>Change not yet applied, in hundredths of a point × minutes: integer rates, and a bed's percent, never lose a fraction.</summary>
    public int Remainder { get; set; }

    /// <summary>Game minutes the need has spent at zero in a row.</summary>
    public int MinutesAtZero { get; set; }
}
