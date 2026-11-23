using System.Diagnostics.CodeAnalysis;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// The kind of a node of a parsed expression: the case of the <c>expr_kind</c> field of the <c>Expr</c> message of
/// <c>syntax.proto</c>, with a message literal and a map literal told apart, as cel-go does.
/// </summary>
/// <remarks>
/// <para>
/// Each kind has its own sealed class: <see cref="CelConstantNode"/>, <see cref="CelIdentNode"/>,
/// <see cref="CelSelectNode"/>, <see cref="CelCallNode"/>, <see cref="CelListNode"/>, <see cref="CelMapNode"/>,
/// <see cref="CelStructNode"/> and <see cref="CelComprehensionNode"/>. A <c>switch</c> over the kind or over the class
/// covers every node.
/// </para>
/// <para>
/// The set of kinds follows <c>syntax.proto</c>. A new kind is a breaking change and comes only in a major version; the
/// numbers of the members never change.
/// </para>
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1008:Enums should have zero value",
    Justification = "Every node has a kind; a zero value would name a node that does not exist, like the unset expr_kind of syntax.proto.")]
public enum CelExprKind
{
    /// <summary>
    /// A literal: <c>Constant</c> of <c>syntax.proto</c>, a <see cref="CelConstantNode"/>.
    /// </summary>
    Constant = 1,

    /// <summary>
    /// An identifier: <c>Expr.Ident</c>, a <see cref="CelIdentNode"/>.
    /// </summary>
    Ident = 2,

    /// <summary>
    /// A field selection or a presence test: <c>Expr.Select</c>, a <see cref="CelSelectNode"/>.
    /// </summary>
    Select = 3,

    /// <summary>
    /// A call of a function or an operator: <c>Expr.Call</c>, a <see cref="CelCallNode"/>.
    /// </summary>
    Call = 4,

    /// <summary>
    /// A list literal: <c>Expr.CreateList</c>, a <see cref="CelListNode"/>.
    /// </summary>
    List = 5,

    /// <summary>
    /// A map literal: <c>Expr.CreateStruct</c> without a message name, a <see cref="CelMapNode"/>.
    /// </summary>
    Map = 6,

    /// <summary>
    /// A message literal: <c>Expr.CreateStruct</c> with a message name, a <see cref="CelStructNode"/>.
    /// </summary>
    Struct = 7,

    /// <summary>
    /// A comprehension, the expansion of a macro such as <c>all</c> or <c>map</c>: <c>Expr.Comprehension</c>, a
    /// <see cref="CelComprehensionNode"/>.
    /// </summary>
    Comprehension = 8,
}
