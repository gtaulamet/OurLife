using System.Text;

namespace OurLife.Localization.Icu;

/// <summary>
/// Recursive-descent parser for the subset of ICU MessageFormat used by the game:
/// plain arguments, <c>number</c>, <c>plural</c> (categories and <c>=N</c>), <c>select</c>,
/// <c>#</c> and apostrophe quoting. Anything else is a syntax error.
/// </summary>
internal sealed class IcuParser
{
    private static readonly HashSet<string> PluralCategories =
        new(StringComparer.Ordinal) { "zero", "one", "two", "few", "many", "other" };

    private readonly string _source;
    private int _position;

    private IcuParser(string source)
    {
        _source = source;
    }

    public static IReadOnlyList<IcuNode> Parse(string source)
    {
        var parser = new IcuParser(source);
        var nodes = parser.ParseMessage(poundAllowed: false, nested: false);
        return nodes;
    }

    private char Current => _position < _source.Length ? _source[_position] : '\0';

    private bool AtEnd => _position >= _source.Length;

    private List<IcuNode> ParseMessage(bool poundAllowed, bool nested)
    {
        var nodes = new List<IcuNode>();
        var text = new StringBuilder();

        while (!AtEnd)
        {
            var c = Current;
            if (c == '{')
            {
                FlushText(nodes, text);
                nodes.Add(ParseArgument(poundAllowed));
            }
            else if (c == '}')
            {
                if (!nested)
                {
                    throw Error("Unexpected '}'. Use '}' in quotes to write it as text.");
                }

                break;
            }
            else if (c == '#' && poundAllowed)
            {
                FlushText(nodes, text);
                nodes.Add(new PoundNode());
                _position++;
            }
            else if (c == '\'')
            {
                ReadApostrophe(text, poundAllowed);
            }
            else
            {
                text.Append(c);
                _position++;
            }
        }

        FlushText(nodes, text);
        return nodes;
    }

    private static void FlushText(List<IcuNode> nodes, StringBuilder text)
    {
        if (text.Length > 0)
        {
            nodes.Add(new TextNode(text.ToString()));
            text.Clear();
        }
    }

    /// <summary>
    /// <c>''</c> is a literal apostrophe. An apostrophe before a syntax character starts a quoted
    /// literal that ends at the next single apostrophe. Any other apostrophe is plain text.
    /// </summary>
    private void ReadApostrophe(StringBuilder text, bool poundAllowed)
    {
        var next = Peek(1);
        if (next == '\'')
        {
            text.Append('\'');
            _position += 2;
            return;
        }

        if (next is not ('{' or '}') && !(poundAllowed && next == '#'))
        {
            text.Append('\'');
            _position++;
            return;
        }

        _position++;
        while (!AtEnd)
        {
            if (Current == '\'')
            {
                if (Peek(1) == '\'')
                {
                    text.Append('\'');
                    _position += 2;
                    continue;
                }

                _position++;
                return;
            }

            text.Append(Current);
            _position++;
        }
    }

    private IcuNode ParseArgument(bool poundAllowed)
    {
        Expect('{');
        SkipWhitespace();
        var name = ReadIdentifier("an argument name");
        SkipWhitespace();
        if (TryConsume('}'))
        {
            return new ArgumentNode(name, AsNumber: false);
        }

        Expect(',');
        SkipWhitespace();
        var type = ReadIdentifier("an argument type");
        SkipWhitespace();

        switch (type)
        {
            case "number":
                if (TryConsume(','))
                {
                    SkipWhitespace();
                    var style = ReadIdentifier("a number style");
                    if (style != "integer")
                    {
                        throw Error($"Unsupported number style '{style}'. Only 'integer' is supported.");
                    }

                    SkipWhitespace();
                }

                Expect('}');
                return new ArgumentNode(name, AsNumber: true);

            case "plural":
            case "select":
                Expect(',');
                var isPlural = type == "plural";
                var cases = ParseCases(isPlural, poundAllowed: isPlural || poundAllowed);
                Expect('}');
                return isPlural ? new PluralNode(name, cases) : new SelectNode(name, cases);

            default:
                throw Error($"Unsupported argument type '{type}'. Use plural, select or number.");
        }
    }

    private List<IcuCase> ParseCases(bool isPlural, bool poundAllowed)
    {
        var cases = new List<IcuCase>();
        var selectors = new HashSet<string>(StringComparer.Ordinal);

        while (true)
        {
            SkipWhitespace();
            if (AtEnd || Current == '}')
            {
                break;
            }

            var selector = ReadSelector(isPlural);
            if (!selectors.Add(selector))
            {
                throw Error($"Duplicate case '{selector}'.");
            }

            SkipWhitespace();
            Expect('{');
            var message = ParseMessage(poundAllowed, nested: true);
            Expect('}');
            cases.Add(new IcuCase(selector, message));
        }

        if (!selectors.Contains("other"))
        {
            throw Error("A plural or select needs an 'other' case.");
        }

        return cases;
    }

    private string ReadSelector(bool isPlural)
    {
        if (isPlural && Current == '=')
        {
            var start = _position;
            _position++;
            while (char.IsAsciiDigit(Current))
            {
                _position++;
            }

            if (_position == start + 1)
            {
                throw Error("Expected a number after '='.");
            }

            return _source[start.._position];
        }

        var word = ReadIdentifier("a case name");
        if (isPlural && !PluralCategories.Contains(word))
        {
            throw Error($"Unknown plural category '{word}'. Use zero, one, two, few, many, other or =N.");
        }

        return word;
    }

    private string ReadIdentifier(string what)
    {
        var start = _position;
        while (!AtEnd && (char.IsAsciiLetterOrDigit(Current) || Current == '_'))
        {
            _position++;
        }

        if (_position == start)
        {
            throw Error($"Expected {what}.");
        }

        return _source[start.._position];
    }

    private void SkipWhitespace()
    {
        while (!AtEnd && char.IsWhiteSpace(Current))
        {
            _position++;
        }
    }

    private bool TryConsume(char expected)
    {
        if (Current != expected || AtEnd)
        {
            return false;
        }

        _position++;
        return true;
    }

    private void Expect(char expected)
    {
        if (!TryConsume(expected))
        {
            throw Error(AtEnd ? $"Missing '{expected}'." : $"Expected '{expected}' but found '{Current}'.");
        }
    }

    private char Peek(int offset) =>
        _position + offset < _source.Length ? _source[_position + offset] : '\0';

    private IcuSyntaxException Error(string message) => new(message, _source, _position);
}
