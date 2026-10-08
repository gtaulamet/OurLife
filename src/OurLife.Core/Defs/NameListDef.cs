namespace OurLife.Core.Defs;

/// <summary>
/// Proper names for individuals. Names are not translated. Lists can be tied to an era
/// through <see cref="Def.Era"/>.
/// </summary>
public sealed class NameListDef : Def
{
    public required IReadOnlyList<string> Male { get; init; }

    public required IReadOnlyList<string> Female { get; init; }

    internal override IEnumerable<string> Validate()
    {
        if (Male.Count == 0 || Female.Count == 0)
        {
            yield return "male and female lists cannot be empty.";
        }

        if (Male.Concat(Female).Any(string.IsNullOrWhiteSpace))
        {
            yield return "names cannot be blank.";
        }
    }
}
