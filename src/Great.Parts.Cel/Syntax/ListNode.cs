using System.Collections.Immutable;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A list literal, the <c>Expr.CreateList</c> message of <c>syntax.proto</c>.
/// </summary>
internal sealed class ListNode : ExprNode
{
    internal ListNode(int id, ImmutableArray<ExprNode> elements, ImmutableArray<int> optionalIndices)
        : base(id)
    {
        Elements = elements;
        OptionalIndices = optionalIndices;
    }

    /// <summary>
    /// The elements in the order of the text.
    /// </summary>
    internal ImmutableArray<ExprNode> Elements { get; }

    /// <summary>
    /// The indices of the optional elements, written <c>?e</c>, in ascending order; empty when there are none.
    /// </summary>
    internal ImmutableArray<int> OptionalIndices { get; }
}
