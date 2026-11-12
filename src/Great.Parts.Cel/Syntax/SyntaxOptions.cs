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
/// <param name="DecimalLiterals">
/// Whether a <c>decimal</c> literal such as <c>12.34m</c> or <c>100m</c> is accepted; otherwise it is
/// <see cref="CelErrorCode.SyntaxNotEnabled"/>. The member exists on every target and is never set on .NET 10, where a
/// <c>decimal</c> literal is not accepted whatever the options say.
/// </param>
internal sealed record SyntaxOptions(bool OptionalSyntax, MacroTable Macros, bool DecimalLiterals = false)
{
    /// <summary>
    /// The syntax of an environment without extension sets: <see cref="OptionalSyntax"/> and <see cref="DecimalLiterals"/>
    /// off and <see cref="MacroTable.Standard"/>.
    /// </summary>
    internal static SyntaxOptions Standard { get; } = new(OptionalSyntax: false, MacroTable.Standard);
}
