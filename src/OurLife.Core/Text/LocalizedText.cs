using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace OurLife.Core.Text;

/// <summary>
/// Text for the player as a translation key plus named arguments. The simulation never builds
/// final strings: the presentation resolves the key in the active language (architecture
/// rule 3). Argument values can be numbers, strings, entity ids or other localized texts.
/// Instances are immutable; <see cref="With"/> returns a new one.
/// </summary>
public sealed partial class LocalizedText
{
    private static readonly ImmutableSortedDictionary<string, object> NoArgs =
        ImmutableSortedDictionary.Create<string, object>(StringComparer.Ordinal);

    private LocalizedText(string key, ImmutableSortedDictionary<string, object> args)
    {
        Key = key;
        Args = args;
    }

    /// <summary>Translation key, for example <c>event.colonist.starved</c>.</summary>
    public string Key { get; }

    /// <summary>Arguments sorted by name.</summary>
    public ImmutableSortedDictionary<string, object> Args { get; }

    /// <exception cref="ArgumentException">The key does not follow the convention in docs/i18n.md.</exception>
    public static LocalizedText Of(string key)
    {
        if (!IsValidKey(key))
        {
            throw new ArgumentException(
                $"Invalid localization key '{key}'. Use lowercase segments separated by dots, e.g. 'event.colonist.starved'.",
                nameof(key));
        }

        return new LocalizedText(key, NoArgs);
    }

    public static bool IsValidKey(string? key) => key is not null && KeyPattern().IsMatch(key);

    /// <summary>Returns a copy with the argument added or replaced.</summary>
    public LocalizedText With(string name, object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (name is null || !ArgumentNamePattern().IsMatch(name))
        {
            throw new ArgumentException(
                $"Invalid argument name '{name}'. Use lowercase letters, digits and underscores, starting with a letter.",
                nameof(name));
        }

        return new LocalizedText(Key, Args.SetItem(name, value));
    }

    public override string ToString() =>
        Args.IsEmpty ? Key : $"{Key}({string.Join(", ", Args.Select(arg => $"{arg.Key}={arg.Value}"))})";

    [GeneratedRegex(@"^[a-z0-9_]+(\.[a-z0-9_]+)+$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex ArgumentNamePattern();
}
