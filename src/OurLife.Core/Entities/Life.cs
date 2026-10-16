using OurLife.Core.Defs;

namespace OurLife.Core.Entities;

/// <summary>
/// How long a colonist has lived and its stage of life (see <c>docs/gdd/vida-y-generaciones.md</c>).
/// A colonist without it lives as an adult and never ages.
/// </summary>
public sealed class Life
{
    /// <summary>Days of life: a year of life has as many days as a year of the calendar.</summary>
    public int AgeDays { get; set; }

    /// <summary>Id of its <see cref="LifeStageDef"/>, the one its age falls in.</summary>
    [DefRef(typeof(LifeStageDef))]
    public string Stage { get; set; } = string.Empty;
}
