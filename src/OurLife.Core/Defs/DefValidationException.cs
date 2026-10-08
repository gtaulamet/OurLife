namespace OurLife.Core.Defs;

/// <summary>Content with problems. <see cref="Errors"/> lists every problem found, not just the first.</summary>
public sealed class DefValidationException : Exception
{
    public DefValidationException(IReadOnlyList<string> errors)
        : base($"The content has {errors.Count} problem(s):{Environment.NewLine}{string.Join(Environment.NewLine, errors)}")
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
