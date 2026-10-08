using OurLife.Core.Sim;

namespace OurLife.Core.Defs;

/// <summary>
/// Decides whether content is available to the colony: the current era must be the def's era
/// or a later one, and the colony must know everything the def requires.
/// </summary>
public sealed class UnlockService
{
    private readonly DefDatabase _defs;

    public UnlockService(DefDatabase defs)
    {
        ArgumentNullException.ThrowIfNull(defs);
        _defs = defs;
    }

    public bool IsUnlocked(Def def, ProgressState progress)
    {
        ArgumentNullException.ThrowIfNull(def);
        ArgumentNullException.ThrowIfNull(progress);

        if (def.Era is not null
            && _defs.Get<EraDef>(progress.CurrentEra).Order < _defs.Get<EraDef>(def.Era).Order)
        {
            return false;
        }

        foreach (var knowledge in def.Requires)
        {
            if (!progress.Knowledge.Contains(knowledge))
            {
                return false;
            }
        }

        return true;
    }
}
