namespace OurLife.Localization.Icu;

/// <summary>A message pattern that does not follow the supported ICU MessageFormat syntax.</summary>
public sealed class IcuSyntaxException : FormatException
{
    public IcuSyntaxException(string message, string pattern, int position)
        : base($"{message} At position {position} of \"{pattern}\".")
    {
        Pattern = pattern;
        Position = position;
    }

    public string Pattern { get; }

    public int Position { get; }
}
