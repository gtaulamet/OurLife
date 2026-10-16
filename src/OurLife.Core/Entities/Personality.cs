using OurLife.Core.Defs;

namespace OurLife.Core.Entities;

/// <summary>Who a colonist is attracted to (see <c>docs/gdd/vida-y-generaciones.md</c>). It never changes.</summary>
public enum Orientation
{
    /// <summary>People of the other sex.</summary>
    Opposite,

    /// <summary>People of the same sex.</summary>
    Same,

    /// <summary>People of either sex.</summary>
    Both,
}

/// <summary>
/// A colonist's personality: its traits, sorted by id, and its orientation. A colonist without it,
/// or without a trait, counts as average in it (50).
/// </summary>
public sealed class Personality
{
    public List<TraitValue> Traits { get; set; } = [];

    public Orientation Orientation { get; set; }

    /// <summary>The game day the <see cref="TraitValue.MovedToday"/> counts belong to; on another day they start at 0.</summary>
    public long DriftDay { get; set; }

    /// <summary>The value of a trait, or null if the colonist does not have it.</summary>
    public TraitValue? Find(string trait)
    {
        foreach (var value in Traits)
        {
            if (value.Trait == trait)
            {
                return value;
            }
        }

        return null;
    }
}

/// <summary>A trait of a colonist, from 0 to 100, and how far experiences moved it on <see cref="Personality.DriftDay"/>.</summary>
public sealed class TraitValue
{
    [DefRef(typeof(TraitDef))]
    public string Trait { get; set; } = string.Empty;

    public int Value { get; set; }

    /// <summary>Points moved that day, up as positive and down as negative: the daily cap holds for the sum.</summary>
    public int MovedToday { get; set; }
}
