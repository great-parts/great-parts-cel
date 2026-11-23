using System.Collections.Immutable;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A call of a function or an operator: the <c>Expr.Call</c> message of <c>syntax.proto</c>, such as <c>size(x)</c>,
/// <c>s.startsWith("a")</c> or <c>a + b</c>.
/// </summary>
/// <remarks>
/// <para>
/// Operators are calls of functions with the names of the CEL language definition: <c>_+_</c>, <c>_&amp;&amp;_</c>,
/// <c>!_</c>, <c>-_</c>, <c>_[_]</c>, <c>_?_:_</c>, <c>@in</c>, and with the optional syntax <c>_?._</c> and
/// <c>_[?_]</c>. A call in the member form <c>target.f(args)</c> has a <see cref="Target"/>; for a call of a qualified
/// function name such as <c>math.greatest(a, b)</c> that is not a macro, the parser gives the member form with the target
/// <c>math</c>, and type checking resolves the name.
/// </para>
/// <para>The node is immutable and can be read from any number of threads at once.</para>
/// </remarks>
public sealed class CelCallNode : CelExprNode
{
    internal CelCallNode(int id, string function, CelExprNode? target, ImmutableArray<CelExprNode> arguments)
        : base(id, CelExprKind.Call)
    {
        Function = function;
        Target = target;
        Arguments = arguments;
    }

    /// <summary>
    /// The name of the function or the operator.
    /// </summary>
    public string Function { get; }

    /// <summary>
    /// The receiver of a call in the member form <c>target.f(args)</c>, or <see langword="null"/> for a global call.
    /// </summary>
    public CelExprNode? Target { get; }

    /// <summary>
    /// The arguments in the order of the text, without the target.
    /// </summary>
    public ImmutableArray<CelExprNode> Arguments { get; }

    private protected override CelExprNode[] CreateChildren()
    {
        if (Target is null)
        {
            return [.. Arguments];
        }

        var children = new CelExprNode[Arguments.Length + 1];
        children[0] = Target;
        Arguments.CopyTo(children, 1);
        return children;
    }
}
