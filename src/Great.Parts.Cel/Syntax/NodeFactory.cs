using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// Creates the nodes of one tree: assigns ids from 1 in the order of creation and records the position of each node
/// and entry. Every source of trees in the assembly creates nodes only through it.
/// </summary>
/// <remarks>
/// Each method takes the children of the node it creates, so children are created before their parent. The offset of
/// a node is in UTF-16 code units from the start of the text.
/// </remarks>
internal sealed class NodeFactory
{
    // The offset of the node or entry with the id i + 1 at the index i.
    private readonly ImmutableArray<int>.Builder _offsets = ImmutableArray.CreateBuilder<int>();

    // The ids of the expansions of macro calls in ascending order and their calls; created at the first macro call.
    private ImmutableArray<int>.Builder? _macroIds;
    private ImmutableArray<MacroCall>.Builder? _macroCalls;

    /// <summary>
    /// The offset of a node this factory created.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The offset in UTF-16 code units from the start of the text.</returns>
    internal int OffsetOf(ExprNode node) => _offsets[node.Id - 1];

    internal ConstantNode Constant(CelValue value, int offset) => new(NextId(offset), value);

    internal IdentNode Ident(string name, int offset) => new(NextId(offset), name);

    internal SelectNode Select(ExprNode operand, string field, bool isPresenceTest, int offset) =>
        new(NextId(offset), operand, field, isPresenceTest);

    internal CallNode Call(string function, ExprNode? target, ImmutableArray<ExprNode> arguments, int offset) =>
        new(NextId(offset), function, target, arguments);

    internal ListNode List(ImmutableArray<ExprNode> elements, ImmutableArray<int> optionalIndices, int offset) =>
        new(NextId(offset), elements, optionalIndices);

    internal StructNode Struct(string messageName, ImmutableArray<StructEntry> entries, int offset) =>
        new(NextId(offset), messageName, entries);

    /// <summary>
    /// Creates an entry of a map literal.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <param name="isOptional">Whether the entry is optional, <c>?k: v</c>.</param>
    /// <param name="offset">The offset of the colon.</param>
    /// <returns>The entry.</returns>
    internal StructEntry Entry(ExprNode key, ExprNode value, bool isOptional, int offset) =>
        new(NextId(offset), key, value, isOptional);

    /// <summary>
    /// Creates an entry of a message literal.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The value.</param>
    /// <param name="isOptional">Whether the entry is optional, <c>?f: v</c>.</param>
    /// <param name="offset">The offset of the colon.</param>
    /// <returns>The entry.</returns>
    internal StructEntry Entry(string fieldName, ExprNode value, bool isOptional, int offset) =>
        new(NextId(offset), fieldName, value, isOptional);

    internal ComprehensionNode Comprehension(
        string iterVar,
        ExprNode iterRange,
        string accuVar,
        ExprNode accuInit,
        ExprNode loopCondition,
        ExprNode loopStep,
        ExprNode result,
        int offset) =>
        new(NextId(offset), iterVar, null, iterRange, accuVar, accuInit, loopCondition, loopStep, result);

    /// <summary>
    /// Creates a comprehension of two iteration variables: the index and the element of a list, the key and the value of a map.
    /// </summary>
    /// <param name="iterVar">The name of the first iteration variable.</param>
    /// <param name="iterVar2">The name of the second iteration variable.</param>
    /// <param name="iterRange">The range.</param>
    /// <param name="accuVar">The name of the accumulator.</param>
    /// <param name="accuInit">The initial value of the accumulator.</param>
    /// <param name="loopCondition">The condition checked before each step.</param>
    /// <param name="loopStep">The step.</param>
    /// <param name="result">The result.</param>
    /// <param name="offset">The offset of the node.</param>
    /// <returns>The comprehension.</returns>
    internal ComprehensionNode Comprehension(
        string iterVar,
        string iterVar2,
        ExprNode iterRange,
        string accuVar,
        ExprNode accuInit,
        ExprNode loopCondition,
        ExprNode loopStep,
        ExprNode result,
        int offset) =>
        new(NextId(offset), iterVar, iterVar2, iterRange, accuVar, accuInit, loopCondition, loopStep, result);

    /// <summary>
    /// Records a macro call as written under the id of its expansion, the last node created.
    /// </summary>
    /// <param name="expansion">The expansion of the call.</param>
    /// <param name="call">The call as written.</param>
    internal void RecordMacroCall(ExprNode expansion, MacroCall call)
    {
        _macroIds ??= ImmutableArray.CreateBuilder<int>();
        _macroCalls ??= ImmutableArray.CreateBuilder<MacroCall>();
        _macroIds.Add(expansion.Id);
        _macroCalls.Add(call);
    }

    /// <summary>
    /// Creates the parsed expression with the positions of all nodes and entries created so far and the macro calls
    /// recorded.
    /// </summary>
    /// <param name="root">The root node.</param>
    /// <param name="lines">The line table of the text.</param>
    /// <returns>The parsed expression.</returns>
    internal ParsedExpression Build(ExprNode root, LineTable lines) =>
        new(root, _offsets.ToImmutable(), _macroIds?.ToImmutable() ?? [], _macroCalls?.ToImmutable() ?? [], lines);

    private int NextId(int offset)
    {
        _offsets.Add(offset);
        return _offsets.Count;
    }
}
