namespace OurLife.Localization.Icu;

/// <summary>Piece of a parsed message pattern.</summary>
public abstract record IcuNode;

/// <summary>Literal text, with quotes already resolved.</summary>
public sealed record TextNode(string Text) : IcuNode;

/// <summary><c>{name}</c> or <c>{name, number}</c>.</summary>
public sealed record ArgumentNode(string Name, bool AsNumber) : IcuNode;

/// <summary><c>#</c> inside a plural case: the number of the innermost plural.</summary>
public sealed record PoundNode : IcuNode;

/// <summary><c>{name, plural, one {…} other {…}}</c>.</summary>
public sealed record PluralNode(string Name, IReadOnlyList<IcuCase> Cases) : IcuNode;

/// <summary><c>{name, select, female {…} other {…}}</c>.</summary>
public sealed record SelectNode(string Name, IReadOnlyList<IcuCase> Cases) : IcuNode;

/// <summary>One case of a plural or select: its selector and its sub-message.</summary>
public sealed record IcuCase(string Selector, IReadOnlyList<IcuNode> Message);
