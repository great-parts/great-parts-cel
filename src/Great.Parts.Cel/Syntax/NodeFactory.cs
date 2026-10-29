using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// Creates the nodes of one tree: assigns ids from 1 in the order of creation and records the position of each node
/// and entry. Every source of trees in the assembly creates nodes only through it.
/// </summary>
internal sealed class NodeFactory
{
    // The offset of the node or entry with the id i + 1 at the index i.
    private readonly ImmutableArray<int>.Builder _offsets = ImmutableArray.CreateBuilder<int>();

    internal ConstantNode Constant(CelValue value, int offset) => new(NextId(offset), value);

    internal IdentNode Ident(string name, int offset) => new(NextId(offset), name);

    internal CallNode Call(string function, ExprNode? target, ImmutableArray<ExprNode> arguments, int offset) =>
        new(NextId(offset), function, target, arguments);

    internal ListNode List(ImmutableArray<ExprNode> elements, int offset) => new(NextId(offset), elements);

    internal StructNode Struct(string messageName, ImmutableArray<StructEntry> entries, int offset) =>
        new(NextId(offset), messageName, entries);

    internal StructEntry Entry(ExprNode key, ExprNode value, int offset) => new(NextId(offset), key, value);

    /// <summary>
    /// Creates the parsed expression with the positions of all nodes and entries created so far.
    /// </summary>
    /// <param name="root">The root node.</param>
    /// <param name="lines">The line table of the text.</param>
    /// <returns>The parsed expression.</returns>
    internal ParsedExpression Build(ExprNode root, LineTable lines) => new(root, _offsets.ToImmutable(), lines);

    private int NextId(int offset)
    {
        _offsets.Add(offset);
        return _offsets.Count;
    }
}
