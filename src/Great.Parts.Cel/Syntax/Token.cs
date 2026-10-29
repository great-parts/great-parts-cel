namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A token of the expression text.
/// </summary>
/// <param name="Kind">The kind of the token.</param>
/// <param name="Offset">The offset of its first character in UTF-16 code units.</param>
/// <param name="Length">Its length in UTF-16 code units; 0 for <see cref="TokenKind.End"/>.</param>
internal readonly record struct Token(TokenKind Kind, int Offset, int Length);
