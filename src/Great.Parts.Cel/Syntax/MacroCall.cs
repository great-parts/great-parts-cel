using System.Collections.Immutable;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A macro call as written in the text, recorded under the id of its expansion, as <c>SourceInfo.macro_calls</c> of
/// <c>syntax.proto</c>. The nodes of the target and the arguments are those of the tree; a nested macro call among
/// them is its expansion, which has a record of its own.
/// </summary>
internal sealed class MacroCall
{
    internal MacroCall(string function, ExprNode? target, ImmutableArray<ExprNode> arguments)
    {
        Function = function;
        Target = target;
        Arguments = arguments;
    }

    /// <summary>
    /// The name of the macro.
    /// </summary>
    internal string Function { get; }

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
