using System;
using System.Collections.Generic;
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
/// a node is in UTF-16 code units from the start of the text. A factory that serves <see cref="CelExprFactory"/> also keeps
/// the nodes and entries it created, to tell its own from foreign ones; the factory of the parser does not.
/// </remarks>
internal sealed class NodeFactory
{
    // The offset of the node or entry with the id i + 1 at the index i.
    private readonly ImmutableArray<int>.Builder _offsets = ImmutableArray.CreateBuilder<int>();

    // The offset just after the last token of the node or entry with the id i + 1, at the index i, in the same position table.
    private readonly ImmutableArray<int>.Builder _ends = ImmutableArray.CreateBuilder<int>();

    // The nodes and entries created, the one with the id i + 1 at the index i; null for the factory of the parser.
    private readonly List<object>? _created;

    // The ids of the expansions of macro calls and their calls, in the order of recording; created at the first macro call.
    private ImmutableArray<int>.Builder? _macroIds;
    private ImmutableArray<CelMacroCall>.Builder? _macroCalls;

    internal NodeFactory(bool keepCreated = false)
    {
        if (keepCreated)
        {
            _created = [];
        }
    }

    /// <summary>
    /// The offset just after the last token the parser has read: the end of every node created now, since a node is created when
    /// its last token has been read. A factory used without a parser leaves it at 0, and a node then ends where it starts.
    /// </summary>
    internal int PreviousEnd { get; set; }

    /// <summary>
    /// The number of nodes and entries created so far: the last id.
    /// </summary>
    internal int Count => _offsets.Count;

    /// <summary>
    /// The offset of a node this factory created.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The offset in UTF-16 code units from the start of the text, or -1 for a node without a position.</returns>
    internal int OffsetOf(CelExprNode node) => _offsets[node.Id - 1];

    /// <summary>
    /// The end of a node this factory created.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The offset just after its last token, in UTF-16 code units from the start of the text.</returns>
    internal int EndOf(CelExprNode node) => _ends[node.Id - 1];

    /// <summary>
    /// The offset of a node or an entry that this factory created.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>The offset, or -1 for a node without a position.</returns>
    internal int OffsetOf(int id) => _offsets[id - 1];

    /// <summary>
    /// The end of a node or an entry that this factory created.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>The end, or -1 for a node without a position.</returns>
    internal int EndOf(int id) => _ends[id - 1];

    /// <summary>
    /// Tells whether this factory created a node or an entry: its id is of this factory and the object at that id is this one.
    /// Only a factory that keeps what it created can tell.
    /// </summary>
    /// <param name="id">The id of the node or the entry.</param>
    /// <param name="item">The node or the entry.</param>
    /// <returns><see langword="true"/> for an object of this factory.</returns>
    internal bool Owns(int id, object item) =>
        _created is not null && (uint)(id - 1) < (uint)_created.Count && ReferenceEquals(_created[id - 1], item);

    internal CelConstantNode Constant(CelValue value, int offset) => Keep(new CelConstantNode(NextId(offset), value));

    internal CelIdentNode Ident(string name, int offset) => Keep(new CelIdentNode(NextId(offset), name));

    internal CelSelectNode Select(CelExprNode operand, string field, bool isPresenceTest, int offset) =>
        Keep(new CelSelectNode(NextId(offset), operand, field, isPresenceTest));

    internal CelCallNode Call(string function, CelExprNode? target, ImmutableArray<CelExprNode> arguments, int offset) =>
        Keep(new CelCallNode(NextId(offset), function, target, arguments));

    internal CelListNode List(ImmutableArray<CelExprNode> elements, ImmutableArray<int> optionalIndices, int offset) =>
        Keep(new CelListNode(NextId(offset), elements, optionalIndices));

    internal CelMapNode Map(ImmutableArray<CelMapEntryNode> entries, int offset) =>
        Keep(new CelMapNode(NextId(offset), entries));

    internal CelStructNode Struct(string typeName, ImmutableArray<CelStructFieldNode> fields, int offset) =>
        Keep(new CelStructNode(NextId(offset), typeName, fields));

    /// <summary>
    /// Creates an entry of a map literal.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <param name="isOptional">Whether the entry is optional, <c>?k: v</c>.</param>
    /// <param name="offset">The offset of the colon.</param>
    /// <returns>The entry.</returns>
    internal CelMapEntryNode Entry(CelExprNode key, CelExprNode value, bool isOptional, int offset) =>
        Keep(new CelMapEntryNode(NextId(offset), key, value, isOptional));

    /// <summary>
    /// Creates a field of a message literal.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <param name="value">The value.</param>
    /// <param name="isOptional">Whether the field is optional, <c>?f: v</c>.</param>
    /// <param name="offset">The offset of the colon.</param>
    /// <returns>The field.</returns>
    internal CelStructFieldNode Field(string name, CelExprNode value, bool isOptional, int offset) =>
        Keep(new CelStructFieldNode(NextId(offset), name, value, isOptional));

    internal CelComprehensionNode Comprehension(
        string iterVar,
        CelExprNode iterRange,
        string accuVar,
        CelExprNode accuInit,
        CelExprNode loopCondition,
        CelExprNode loopStep,
        CelExprNode result,
        int offset) =>
        Keep(new CelComprehensionNode(NextId(offset), iterVar, null, iterRange, accuVar, accuInit, loopCondition, loopStep, result));

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
    internal CelComprehensionNode Comprehension(
        string iterVar,
        string iterVar2,
        CelExprNode iterRange,
        string accuVar,
        CelExprNode accuInit,
        CelExprNode loopCondition,
        CelExprNode loopStep,
        CelExprNode result,
        int offset) =>
        Keep(new CelComprehensionNode(NextId(offset), iterVar, iterVar2, iterRange, accuVar, accuInit, loopCondition, loopStep, result));

    /// <summary>
    /// Records a macro call under the id of its expansion, the last node created.
    /// </summary>
    /// <param name="expansion">The expansion of the call.</param>
    /// <param name="call">The call.</param>
    internal void RecordMacroCall(CelExprNode expansion, CelMacroCall call)
    {
        _macroIds ??= ImmutableArray.CreateBuilder<int>();
        _macroCalls ??= ImmutableArray.CreateBuilder<CelMacroCall>();
        _macroIds.Add(expansion.Id);
        _macroCalls.Add(call);
    }

    /// <summary>
    /// Creates the parsed expression with the positions of all nodes and entries created so far and the macro calls
    /// recorded.
    /// </summary>
    /// <param name="root">The root node.</param>
    /// <param name="lines">The line table of the text.</param>
    /// <param name="reachable">
    /// By id, the nodes of the tree whose macro calls the expression keeps; <see langword="null"/> to keep every call.
    /// </param>
    /// <param name="sourceName">The name of the source document of the expression, or <see langword="null"/>.</param>
    /// <returns>The parsed expression.</returns>
    internal CelParsedExpr Build(CelExprNode root, LineTable lines, bool[]? reachable = null, string? sourceName = null)
    {
        ImmutableArray<int> offsets = _offsets.ToImmutable();
        ImmutableArray<int> ends = _ends.ToImmutable();
        ImmutableArray<int> macroIds = [];
        ImmutableArray<CelMacroCall> macroCalls = [];
        if (_macroIds is not null)
        {
            int[] ids = [.. _macroIds];
            CelMacroCall[] calls = [.. _macroCalls!];
            Array.Sort(ids, calls);
            if (reachable is not null)
            {
                (ids, calls) = Reachable(ids, calls, reachable);
            }

            macroIds = [.. ids];
            macroCalls = [.. calls];
        }

        return new CelParsedExpr(root, offsets, ends, macroIds, macroCalls, lines.WithNodes(offsets, ends), sourceName);
    }

    // The calls recorded under the ids of reachable nodes, in the order of the ids.
    private static (int[] Ids, CelMacroCall[] Calls) Reachable(int[] ids, CelMacroCall[] calls, bool[] reachable)
    {
        List<int> keptIds = [];
        List<CelMacroCall> keptCalls = [];
        for (int i = 0; i < ids.Length; i++)
        {
            if ((uint)ids[i] < (uint)reachable.Length && reachable[ids[i]])
            {
                keptIds.Add(ids[i]);
                keptCalls.Add(calls[i]);
            }
        }

        return ([.. keptIds], [.. keptCalls]);
    }

    private T Keep<T>(T item)
        where T : class
    {
        _created?.Add(item);
        return item;
    }

    private int NextId(int offset)
    {
        _offsets.Add(offset);
        _ends.Add(Math.Max(PreviousEnd, offset));
        return _offsets.Count;
    }
}
