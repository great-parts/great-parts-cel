using System.Collections.Immutable;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A list literal, the <c>Expr.CreateList</c> message of <c>syntax.proto</c>.
/// </summary>
internal sealed class ListNode : ExprNode
{
    internal ListNode(int id, ImmutableArray<ExprNode> elements)
        : base(id)
    {
        Elements = elements;
    }

    /// <summary>
    /// The elements in the order of the text.
    /// </summary>
    internal ImmutableArray<ExprNode> Elements { get; }
}
