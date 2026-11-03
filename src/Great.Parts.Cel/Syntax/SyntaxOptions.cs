using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// The syntax that the parser accepts beyond the grammar of the language definition. Immutable.
/// </summary>
/// <param name="OptionalSyntax">
/// Whether optional selection <c>a.?f</c>, the optional index <c>a[?i]</c> and <c>?</c> before a list element, a map key
/// or a message field are accepted; otherwise each is <see cref="CelErrorCode.SyntaxNotEnabled"/> at its <c>?</c>.
/// </param>
/// <param name="Macros">The macros of the parser.</param>
internal sealed record SyntaxOptions(bool OptionalSyntax, MacroTable Macros)
{
    /// <summary>
    /// The syntax of an environment without extension sets: <see cref="OptionalSyntax"/> off and
    /// <see cref="MacroTable.Standard"/>.
    /// </summary>
    internal static SyntaxOptions Standard { get; } = new(OptionalSyntax: false, MacroTable.Standard);
}
