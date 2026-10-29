using System.Collections.Immutable;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A call of a function or an operator, the <c>Expr.Call</c> message of <c>syntax.proto</c>. Operators are calls of
/// functions with names such as <c>_+_</c> and <c>-_</c>.
/// </summary>
internal sealed class CallNode : ExprNode
{
    internal CallNode(int id, string function, ExprNode? target, ImmutableArray<ExprNode> arguments)
        : base(id)
    {
        Function = function;
        Target = target;
        Arguments = arguments;
    }

    /// <summary>
    /// The name of the function.
    /// </summary>
    internal string Function { get; }

    /// <summary>
    /// The receiver of a call in the member form <c>target.f(args)</c>, or <see langword="null"/> for a global call.
    /// </summary>
    internal ExprNode? Target { get; }

    /// <summary>
    /// The arguments in the order of the text.
    /// </summary>
    internal ImmutableArray<ExprNode> Arguments { get; }
}
