namespace OurLife.Core.Defs;

/// <summary>A period of history. The era with the lowest order is where every game starts.</summary>
public sealed class EraDef : Def
{
    /// <summary>Position in history: 0 for the first era, higher for later ones.</summary>
    public required int Order { get; init; }

    internal override IEnumerable<string> Validate()
    {
        if (Order < 0)
        {
            yield return "order must be zero or positive.";
        }

        if (Era is not null || Requires.Count > 0)
        {
            yield return "an era cannot declare 'era' or 'requires'.";
        }
    }
}
