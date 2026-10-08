namespace OurLife.Localization;

/// <summary>Catalog files with problems. <see cref="Errors"/> lists every problem found, not just the first.</summary>
public sealed class LocalizationException : Exception
{
    public LocalizationException(string message, IReadOnlyList<string> errors)
        : base(message + Environment.NewLine + string.Join(Environment.NewLine, errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
