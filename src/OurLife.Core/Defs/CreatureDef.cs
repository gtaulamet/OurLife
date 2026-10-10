namespace OurLife.Core.Defs;

/// <summary>A kind of living being: humans now, animals later.</summary>
public sealed class CreatureDef : Def, IHasVisual
{
    public required string Visual { get; init; }

    /// <summary>Ticks needed to walk to a neighbouring tile.</summary>
    public required int MoveTicksPerTile { get; init; }

    public IReadOnlyList<string> Needs { get; init; } = [];

    /// <summary>Names for individuals of this creature, if they get one.</summary>
    public string? NameList { get; init; }

    internal override IEnumerable<DefReference> References()
    {
        foreach (var need in Needs)
        {
            yield return new DefReference(typeof(NeedDef), need, "needs");
        }

        yield return new DefReference(typeof(NameListDef), NameList, "nameList");
    }

    internal override IEnumerable<string> Validate()
    {
        if (MoveTicksPerTile <= 0)
        {
            yield return "moveTicksPerTile must be positive.";
        }

        if (Needs.Distinct(StringComparer.Ordinal).Count() != Needs.Count)
        {
            yield return "needs cannot repeat.";
        }
    }
}
