using OurLife.Core.Defs;
using OurLife.Core.Ecs;

namespace OurLife.Core.Entities;

/// <summary>
/// What a colonist thinks of each other colonist it knows, sorted by the other's id (see
/// "Relaciones y familia" in <c>docs/gdd/vida-y-generaciones.md</c>). It need not be what the other
/// thinks back. Friendship and rivalry come from the opinions; they are not stored.
/// </summary>
public sealed class Relations
{
    public List<Relation> Others { get; set; } = [];

    /// <summary>The relation with another colonist, or null if it does not know it.</summary>
    public Relation? Find(EntityId other)
    {
        foreach (var relation in Others)
        {
            if (relation.Other == other)
            {
                return relation;
            }
        }

        return null;
    }
}

/// <summary>A colonist's view of another: opinion from -100 to 100, romance from 0 to 100, and when they last interacted.</summary>
public sealed class Relation
{
    public EntityId Other { get; set; }

    public int Opinion { get; set; }

    public int Romance { get; set; }

    /// <summary>The tick of their last interaction, or null if they never had one.</summary>
    public long? LastInteractionTick { get; set; }

    /// <summary>The tie between the two last told in the log, so a tie that comes and goes is told once.</summary>
    public Tie Told { get; set; }
}

/// <summary>A tie between two colonists that the log tells when it starts.</summary>
public enum Tie
{
    None,
    Friends,
    Rivals,
}

/// <summary>
/// A colonist's family: father, mother, partner and children, with their names kept for when they
/// die. Brothers and sisters are those who share a father or a mother.
/// </summary>
public sealed class Family
{
    public Relative? Father { get; set; }

    public Relative? Mother { get; set; }

    /// <summary>The partner, alive or dead; single without one, or with a dead one.</summary>
    public Relative? Partner { get; set; }

    /// <summary>Partners who died before a new one came, oldest first.</summary>
    public List<Relative> LatePartners { get; set; } = [];

    public List<Relative> Children { get; set; } = [];

    /// <summary>Whether the couple may have children, the player's choice; on by default.</summary>
    public bool ChildrenAllowed { get; set; } = true;

    /// <summary>The colonist's days of life when its partner died, or null if it is not a widow or widower.</summary>
    public int? WidowedAtAgeDays { get; set; }
}

/// <summary>A member of the family, named as it was in case it is gone.</summary>
public sealed class Relative
{
    public EntityId Entity { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Surname { get; set; } = string.Empty;

    public Gender Gender { get; set; }

    public bool Dead { get; set; }
}

/// <summary>A colonist's last interactions, newest last, for its sheet.</summary>
public sealed class RecentInteractions
{
    public List<InteractionRecord> Entries { get; set; } = [];
}

/// <summary>An interaction a colonist took part in.</summary>
public sealed class InteractionRecord
{
    public long Tick { get; set; }

    [DefRef(typeof(InteractionDef))]
    public string Interaction { get; set; } = string.Empty;

    public EntityId Other { get; set; }

    /// <summary>Whether this colonist started it.</summary>
    public bool Started { get; set; }
}
