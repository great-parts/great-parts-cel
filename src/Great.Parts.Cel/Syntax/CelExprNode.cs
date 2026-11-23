using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Threading;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A node of a parsed expression: the <c>Expr</c> message of <c>syntax.proto</c>. The derived sealed class and
/// <see cref="Kind"/> tell which kind of expression it is.
/// </summary>
/// <remarks>
/// <para>
/// Nodes are immutable. They are created by parsing (<see cref="CelEnvironment.Parse(string, CelCompileOptions)"/>), by
/// loading (<see cref="CelParsedExpr.FromProtobuf(ReadOnlySpan{byte}, string)"/>) or by a <see cref="CelExprFactory"/>;
/// there is no public constructor. A node belongs to the tree it was created for: its position and the macro call it
/// expands are kept by that tree, <see cref="CelParsedExpr"/>, under <see cref="Id"/>.
/// </para>
/// <para>
/// The shape of the tree that parsing gives for a text — the kinds, the order of the children and the expansion of each
/// macro — is the shape that the parser of cel-go gives, and it is part of the public API: it changes only in a major
/// version, or with a fix of conformance to cel-spec described in the release notes. The values of ids are not part of it.
/// </para>
/// <para>A node can be read from any number of threads at once.</para>
/// </remarks>
public abstract class CelExprNode
{
    // The children, made on the first read and kept; two threads can make them at the same time, and both arrays are equal.
    private CelExprNode[]? _children;

    private protected CelExprNode(int id, CelExprKind kind)
    {
        Id = id;
        Kind = kind;
    }

    /// <summary>
    /// The id of the node: a positive number unique among the nodes and entries of its tree. The positions, the macro calls,
    /// the types and the references of a tree are kept by id.
    /// </summary>
    /// <remarks>
    /// Ids are assigned by the source of the tree, in the order in which it created the nodes; an id is not reused within one
    /// tree, but the ids of a tree can have gaps. Loading renumbers the ids of the loaded message.
    /// </remarks>
    public int Id { get; }

    /// <summary>
    /// The kind of the node, which names its sealed class.
    /// </summary>
    public CelExprKind Kind { get; }

    /// <summary>
    /// The child expressions of the node, in the order of the text: the operand of a selection; the target and then the
    /// arguments of a call; the elements of a list; the key and the value of each map entry; the value of each message field;
    /// the range, the initial accumulator, the condition, the step and the result of a comprehension. Empty for a constant and
    /// an identifier.
    /// </summary>
    /// <remarks>The array is created on the first read and kept.</remarks>
    public ImmutableArray<CelExprNode> Children
    {
        get
        {
            CelExprNode[]? children = Volatile.Read(ref _children);
            if (children is null)
            {
                children = CreateChildren();
                Volatile.Write(ref _children, children);
            }

            return ImmutableCollectionsMarshal.AsImmutableArray(children);
        }
    }

    /// <summary>
    /// Enumerates the node and all its descendants in pre-order: the node, then the subtree of each child in the order of
    /// <see cref="Children"/>.
    /// </summary>
    /// <remarks>
    /// The enumeration does not recurse, so a tree of any depth is enumerated without running out of stack. The entries of maps
    /// and messages are not enumerated, their keys and values are.
    /// </remarks>
    /// <returns>The nodes, this node first.</returns>
    public IEnumerable<CelExprNode> DescendantsAndSelf() => Enumerate(this);

    /// <summary>
    /// Makes the children of the node: the constant and the identifier have none.
    /// </summary>
    /// <returns>The children in the order of <see cref="Children"/>.</returns>
    private protected virtual CelExprNode[] CreateChildren() => [];

    // Pre-order with an explicit stack: the children are pushed in reverse, so that the first one is taken first.
    private static IEnumerable<CelExprNode> Enumerate(CelExprNode root)
    {
        Stack<CelExprNode> pending = new();
        pending.Push(root);
        while (pending.Count > 0)
        {
            CelExprNode node = pending.Pop();
            yield return node;
            ImmutableArray<CelExprNode> children = node.Children;
            for (int i = children.Length - 1; i >= 0; i--)
            {
                pending.Push(children[i]);
            }
        }
    }
}
