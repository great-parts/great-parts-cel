using System.Collections.Immutable;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A list literal <c>[a, b, ?c]</c>: the <c>Expr.CreateList</c> message of <c>syntax.proto</c>.
/// </summary>
/// <remarks>The node is immutable and can be read from any number of threads at once.</remarks>
public sealed class CelListNode : CelExprNode
{
    internal CelListNode(int id, ImmutableArray<CelExprNode> elements, ImmutableArray<int> optionalIndices)
        : base(id, CelExprKind.List)
    {
        Elements = elements;
        OptionalIndices = optionalIndices;
    }

    /// <summary>
    /// The elements in the order of the text.
    /// </summary>
    public ImmutableArray<CelExprNode> Elements { get; }

    /// <summary>
    /// The indices in <see cref="Elements"/> of the optional elements, written <c>?e</c>, in ascending order: such an element
    /// is added only when its value is an optional with a value. Empty when there are none.
    /// </summary>
    public ImmutableArray<int> OptionalIndices { get; }

    private protected override CelExprNode[] CreateChildren() => [.. Elements];
}
