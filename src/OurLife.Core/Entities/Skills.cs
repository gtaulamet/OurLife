using OurLife.Core.Defs;

namespace OurLife.Core.Entities;

/// <summary>
/// A colonist's skills, one per work type with a skill, sorted by work type id (see "Habilidades"
/// in <c>docs/gdd/vida-y-generaciones.md</c>). A colonist without it works at the speed of an
/// average one and learns nothing.
/// </summary>
public sealed class Skills
{
    public List<SkillValue> Values { get; set; } = [];

    /// <summary>The skill of a work type, or null if the colonist does not have it.</summary>
    public SkillValue? Find(string workType)
    {
        foreach (var value in Values)
        {
            if (value.WorkType == workType)
            {
                return value;
            }
        }

        return null;
    }
}

/// <summary>A skill: its level and the experience gathered towards the next one.</summary>
public sealed class SkillValue
{
    /// <summary>Id of its <see cref="WorkTypeDef"/>: the skill has the work type's name.</summary>
    [DefRef(typeof(WorkTypeDef))]
    public string WorkType { get; set; } = string.Empty;

    public int Level { get; set; }

    /// <summary>Experience towards the next level, in hundredths of a point, so a percent never loses its fraction.</summary>
    public int Experience { get; set; }
}
