namespace OurLife.Core.Defs;

/// <summary>
/// The numbers of the rotting of food (see "Deterioro" in <c>docs/gdd/agricultura.md</c>). The
/// content defines at most one; without it nothing rots.
/// </summary>
public sealed class SpoilageConfigDef : Def
{
    /// <summary>Speed, in percent, at which a pile rots when something solid is above its cell (a roof, a built floor, rock).</summary>
    public required int CoveredPercent { get; init; }

    /// <summary>Fewest units of an item that rot in a day for the player to be told.</summary>
    public required int RotNoticeMinUnits { get; init; }

    internal override IEnumerable<string> Validate()
    {
        foreach (var error in base.Validate())
        {
            yield return error;
        }

        if (CoveredPercent is < 1 or > 100)
        {
            yield return "coveredPercent must be between 1 and 100.";
        }

        if (RotNoticeMinUnits < 1)
        {
            yield return "rotNoticeMinUnits must be at least 1.";
        }
    }
}
