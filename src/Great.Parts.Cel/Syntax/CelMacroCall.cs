using System.Collections.Immutable;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A macro call as written in the text, such as <c>items.all(x, x &gt; 0)</c> or <c>has(order.total)</c>, kept by its tree
/// under the id of its expansion: an entry of <c>SourceInfo.macro_calls</c> of <c>syntax.proto</c>.
/// </summary>
/// <remarks>
/// <para>
/// The target and the arguments are nodes of the tree, except an argument that the expansion does not contain, such as
/// <c>m.f</c> of <c>has(m.f)</c>, which is a node of its own that <c>FindNode</c> does not find; a macro call among them is
/// its expansion, which has a record of its own. The same holds for a tree loaded from protobuf. <see cref="CelSyntax.Format(CelParsedExpr)"/> prints an expansion as its call. The expansion is what type checking
/// and evaluation see; the call is kept for printing, for <c>CelValidationResult.ReferencedFunctions</c> and for tools.
/// </para>
/// <para>The record is immutable and can be read from any number of threads at once.</para>
/// </remarks>
public sealed class CelMacroCall
{
    internal CelMacroCall(string function, CelExprNode? target, ImmutableArray<CelExprNode> arguments, bool isWritten = true)
    {
        Function = function;
        Target = target;
        Arguments = arguments;
        IsWritten = isWritten;
    }

    /// <summary>
    /// Whether the text writes the call. A call that the expansion of another macro records for its own part, such as the
    /// <c>map</c> inside <c>sortBy</c>, is not written; <c>CelParsedExpr.GetMacroCall</c> does not return it.
    /// </summary>
    internal bool IsWritten { get; }

    /// <summary>
    /// The name by which the text calls the macro: <c>all</c>, <c>has</c>, and for a macro of a namespace the namespace and
    /// the name joined by a dot, such as <c>cel.bind</c> or <c>math.greatest</c>.
    /// </summary>
    public string Function { get; }

    /// <summary>
    /// The receiver of a macro in the member form <c>target.all(v, p)</c>, or <see langword="null"/> for a global macro such as
    /// <c>has</c> or <c>cel.bind</c>.
    /// </summary>
    public CelExprNode? Target { get; }

    /// <summary>
    /// The arguments in the order of the text.
    /// </summary>
    public ImmutableArray<CelExprNode> Arguments { get; }
}
