using System.Globalization;

namespace OurLife.Localization.Icu;

/// <summary>Piece of a parsed message pattern.</summary>
public abstract record IcuNode;

/// <summary>Literal text, with quotes already resolved.</summary>
public sealed record TextNode(string Text) : IcuNode;

/// <summary><c>{name}</c>, <c>{name, number}</c> or <c>{name, number, integer}</c>.</summary>
public sealed record ArgumentNode(string Name, ArgumentStyle Style) : IcuNode;

/// <summary>How an argument is written when its value is a number.</summary>
public enum ArgumentStyle
{
    /// <summary><c>{name}</c>: integers without thousands separators, so a year reads "1203".</summary>
    Plain,

    /// <summary><c>{name, number}</c>: with the thousands separator of the language.</summary>
    Number,

    /// <summary><c>{name, number, integer}</c>: like <see cref="Number"/>, rounded half to even.</summary>
    Integer,
}

/// <summary><c>#</c> inside a plural case: the number of the innermost plural.</summary>
public sealed record PoundNode : IcuNode;

/// <summary><c>{name, plural, one {…} other {…}}</c>.</summary>
public sealed record PluralNode(string Name, IReadOnlyList<IcuCase> Cases) : IcuNode;

/// <summary><c>{name, select, female {…} other {…}}</c>.</summary>
public sealed record SelectNode(string Name, IReadOnlyList<IcuCase> Cases) : IcuNode;

/// <summary>
/// One case of a plural or select: its selector and its sub-message. Immutable, so the exact
/// value of a selector like <c>=0</c> is computed once and always matches it.
/// </summary>
public sealed class IcuCase
{
    public IcuCase(string selector, IReadOnlyList<IcuNode> message)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(message);
        Exact = ParseExact(selector);
        Selector = Exact is { } exact ? ExactSelector(exact) : selector;
        Message = message;
    }

    /// <summary>The case name, or <c>=N</c> without leading zeros.</summary>
    public string Selector { get; }

    public IReadOnlyList<IcuNode> Message { get; }

    /// <summary>Value of an exact plural selector such as <c>=0</c>; null for any other selector.</summary>
    public decimal? Exact { get; }

    /// <summary>The value after <c>=</c>, or null when the selector is not <c>=</c> and digits that fit a decimal.</summary>
    internal static decimal? ParseExact(string selector) =>
        selector.StartsWith('=')
        && decimal.TryParse(selector.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    /// <summary>Canonical form of an exact selector, so <c>=1</c> and <c>=01</c> are the same case.</summary>
    internal static string ExactSelector(decimal value) => $"={value.ToString(CultureInfo.InvariantCulture)}";
}
