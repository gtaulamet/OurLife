namespace OurLife.Core.Defs;

/// <summary>
/// Marks a string property of a component that holds the id of a def of
/// <see cref="DefType"/>. Loading a saved game checks that each of these ids exists in the
/// current content (see ADR-010).
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class DefRefAttribute(Type defType) : Attribute
{
    public Type DefType { get; } = defType;
}
