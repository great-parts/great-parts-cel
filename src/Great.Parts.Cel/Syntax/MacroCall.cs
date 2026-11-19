using System.Collections.Immutable;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A macro call as written in the text, recorded under the id of its expansion, as <c>SourceInfo.macro_calls</c> of
/// <c>syntax.proto</c>. The nodes of the target and the arguments are those of the tree; a nested macro call among
/// them is its expansion, which has a record of its own.
/// </summary>
internal sealed class MacroCall
{
    internal MacroCall(string function, ExprNode? target, ImmutableArray<ExprNode> arguments, string? @namespace = null, bool isWritten = true)
    {
        Function = function;
        Target = target;
        Arguments = arguments;
        QualifiedName = @namespace is null ? function : @namespace + "." + function;
        IsWritten = isWritten;
    }

    /// <summary>
    /// Whether the text writes the call. A call that the expansion of another macro records for its own part, such as the
    /// <c>map</c> inside <c>sortBy</c>, is not written.
    /// </summary>
    internal bool IsWritten { get; }

    /// <summary>
    /// The name of the macro.
    /// </summary>
    internal string Function { get; }

    /// <summary>
    /// The name that the text calls the macro by: <see cref="Function"/>, and for a macro of a namespace, such as
    /// <c>math.greatest</c>, the namespace and the name joined by a dot.
    /// </summary>
    internal string QualifiedName { get; }

    /// <summary>
    /// The receiver of a macro in the member form <c>target.all(v, p)</c>, or <see langword="null"/> for a global one
    /// such as <c>has</c>.
    /// </summary>
    internal ExprNode? Target { get; }

    /// <summary>
    /// The arguments in the order of the text.
    /// </summary>
    internal ImmutableArray<ExprNode> Arguments { get; }
}
