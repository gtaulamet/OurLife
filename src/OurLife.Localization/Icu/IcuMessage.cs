using System.Globalization;
using System.Text;

namespace OurLife.Localization.Icu;

/// <summary>
/// A parsed message pattern. Formatting never throws: a missing argument is shown as
/// <c>⟦name⟧</c> so the problem is visible on screen and caught by tests.
/// </summary>
public sealed class IcuMessage
{
    private static readonly IReadOnlyDictionary<string, object?> NoArguments = new Dictionary<string, object?>();

    private IcuMessage(string pattern, IReadOnlyList<IcuNode> nodes)
    {
        Pattern = pattern;
        Nodes = nodes;
        var names = new SortedSet<string>(StringComparer.Ordinal);
        CollectArguments(nodes, names);
        ArgumentNames = names;
    }

    public string Pattern { get; }

    public IReadOnlyList<IcuNode> Nodes { get; }

    /// <summary>Every argument the pattern uses, at any depth, sorted by name.</summary>
    public IReadOnlySet<string> ArgumentNames { get; }

    /// <exception cref="IcuSyntaxException">The pattern is not valid.</exception>
    public static IcuMessage Parse(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return new IcuMessage(pattern, IcuParser.Parse(pattern));
    }

    /// <summary>
    /// Formats the message. Integers are written without thousands separators (years read
    /// "1203"), unless the pattern asks for <c>{n, number}</c>, which uses the culture.
    /// </summary>
    public string Format(IReadOnlyDictionary<string, object?>? arguments, PluralRule pluralRule, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(pluralRule);
        ArgumentNullException.ThrowIfNull(culture);
        var builder = new StringBuilder(Pattern.Length);
        Append(builder, Nodes, arguments ?? NoArguments, pluralRule, culture, poundValue: null);
        return builder.ToString();
    }

    private static void Append(
        StringBuilder builder,
        IReadOnlyList<IcuNode> nodes,
        IReadOnlyDictionary<string, object?> arguments,
        PluralRule pluralRule,
        CultureInfo culture,
        decimal? poundValue)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case TextNode text:
                    builder.Append(text.Text);
                    break;

                case PoundNode:
                    builder.Append(poundValue is { } pound ? FormatNumber(pound, grouping: false, culture) : "#");
                    break;

                case ArgumentNode argument:
                    if (arguments.TryGetValue(argument.Name, out var value) && value is not null)
                    {
                        builder.Append(FormatValue(value, argument.AsNumber, culture));
                    }
                    else
                    {
                        AppendMissing(builder, argument.Name);
                    }

                    break;

                case PluralNode plural:
                    if (arguments.TryGetValue(plural.Name, out var countValue) && TryGetNumber(countValue, out var count))
                    {
                        var chosen = ChoosePluralCase(plural.Cases, count, pluralRule);
                        Append(builder, chosen.Message, arguments, pluralRule, culture, count);
                    }
                    else
                    {
                        AppendMissing(builder, plural.Name);
                    }

                    break;

                case SelectNode select:
                    var key = arguments.TryGetValue(select.Name, out var selectValue) && selectValue is not null
                        ? Convert.ToString(selectValue, CultureInfo.InvariantCulture)
                        : null;
                    Append(builder, ChooseSelectCase(select.Cases, key).Message, arguments, pluralRule, culture, poundValue);
                    break;
            }
        }
    }

    private static IcuCase ChoosePluralCase(IReadOnlyList<IcuCase> cases, decimal count, PluralRule pluralRule)
    {
        foreach (var candidate in cases)
        {
            if (candidate.Selector[0] == '='
                && decimal.Parse(candidate.Selector.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture) == count)
            {
                return candidate;
            }
        }

        var category = pluralRule(count);
        foreach (var candidate in cases)
        {
            if (candidate.Selector == category)
            {
                return candidate;
            }
        }

        return ChooseSelectCase(cases, "other");
    }

    private static IcuCase ChooseSelectCase(IReadOnlyList<IcuCase> cases, string? key)
    {
        IcuCase? other = null;
        foreach (var candidate in cases)
        {
            if (candidate.Selector == key)
            {
                return candidate;
            }

            if (candidate.Selector == "other")
            {
                other = candidate;
            }
        }

        return other ?? throw new InvalidOperationException("Parsed plural or select without an 'other' case.");
    }

    private static string FormatValue(object value, bool asNumber, CultureInfo culture)
    {
        if (TryGetNumber(value, out var number) && value is not string)
        {
            return FormatNumber(number, asNumber, culture);
        }

        return value switch
        {
            string text => text,
            IFormattable formattable => formattable.ToString(null, culture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string FormatNumber(decimal number, bool grouping, CultureInfo culture)
    {
        if (grouping)
        {
            return number.ToString(number == decimal.Truncate(number) ? "#,0" : "#,0.##", culture);
        }

        return number == decimal.Truncate(number)
            ? number.ToString("0", CultureInfo.InvariantCulture)
            : number.ToString(culture);
    }

    private static bool TryGetNumber(object? value, out decimal number)
    {
        switch (value)
        {
            case byte or sbyte or short or ushort or int or uint or long or ulong or decimal:
                number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            case float or double:
                var asDouble = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsFinite(asDouble) && Math.Abs(asDouble) < 7.9e27)
                {
                    number = (decimal)asDouble;
                    return true;
                }

                break;
        }

        number = 0;
        return false;
    }

    private static void AppendMissing(StringBuilder builder, string name) =>
        builder.Append('⟦').Append(name).Append('⟧');

    private static void CollectArguments(IReadOnlyList<IcuNode> nodes, SortedSet<string> names)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case ArgumentNode argument:
                    names.Add(argument.Name);
                    break;
                case PluralNode plural:
                    names.Add(plural.Name);
                    foreach (var pluralCase in plural.Cases)
                    {
                        CollectArguments(pluralCase.Message, names);
                    }

                    break;
                case SelectNode select:
                    names.Add(select.Name);
                    foreach (var selectCase in select.Cases)
                    {
                        CollectArguments(selectCase.Message, names);
                    }

                    break;
            }
        }
    }
}
