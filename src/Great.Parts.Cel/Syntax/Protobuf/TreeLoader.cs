using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Syntax;

// Turns the data of a message into a parsed expression: checks the rules of syntax.proto and of the library, creates the nodes through
// NodeFactory (so the ids start from 1, children before their parents), gives each node the position of its message id and builds the
// macro calls. The first fault stops the load with InvalidSyntaxTree, the id of the node in the message, and the place of that node
// when the message gives its position.
internal sealed class TreeLoader
{
    private readonly SourceData _source;
    private readonly LineTable _lines;
    private readonly bool _hasText;
    private readonly NodeFactory _nodes = new();

    // The nodes of the tree by the id of the message, and the ids that nodes and entries of the tree have, which are each used once.
    private readonly Dictionary<long, CelExprNode> _nodesById = [];
    private readonly HashSet<long> _ids = [];

    // Whether the nodes being made are the copies of a macro call, which are not nodes of the tree and are not registered.
    private bool _copy;

    private TreeLoader(SourceData source, LineTable lines, bool hasText)
    {
        _source = source;
        _lines = lines;
        _hasText = hasText;
    }

    /// <summary>
    /// Loads the parsed expression of the data read from a message.
    /// </summary>
    /// <param name="root">The expression of the message, or <see langword="null"/> when the message has none.</param>
    /// <param name="source">The source info of the message.</param>
    /// <param name="sourceText">The text that the positions refer to, or <see langword="null"/>.</param>
    /// <returns>The parsed expression.</returns>
    /// <exception cref="CelException">The message breaks a rule; the exception carries the error.</exception>
    internal static CelParsedExpr Load(ExprData? root, SourceData source, string? sourceText)
    {
        LineTable lines = sourceText is not null ? new LineTable(sourceText) : FromLineOffsets(source.LineOffsets);
        var loader = new TreeLoader(source, lines, sourceText is not null);
        if (root is null)
        {
            throw loader.Fault("the message has no expression", 0);
        }

        CelExprNode tree = loader.Node(root);
        loader.RecordMacroCalls();
        return loader._nodes.Build(tree, lines, null, string.IsNullOrEmpty(source.Location) ? null : source.Location);
    }

    // The table of a message read without a text: the offsets of the lines are the starts of the lines after the first, and the last
    // value is the length of the text plus 1.
    private static LineTable FromLineOffsets(List<int> offsets)
    {
        if (offsets.Count == 0)
        {
            return LineTable.FromLineStarts([], 0);
        }

        ImmutableArray<int>.Builder starts = ImmutableArray.CreateBuilder<int>(offsets.Count - 1);
        for (int i = 0; i < offsets.Count - 1; i++)
        {
            starts.Add(offsets[i]);
        }

        return LineTable.FromLineStarts(starts.MoveToImmutable(), Math.Max(offsets[^1] - 1, 0));
    }

    // The fault at a node or an entry of the message: InvalidSyntaxTree with its place, when the message gives a position inside the text.
    private CelException Fault(string message, long id)
    {
        CelSourceLocation location = default;
        if (id != 0 && _source.Positions.TryGetValue(id, out int position) && position >= 0 && (!_hasText || position <= _lines.CodePointLength))
        {
            location = _lines.GetLocation(_hasText ? _lines.ToUtf16(position) : position);
        }

        return ProtoErrors.Stop(CelErrorCode.InvalidSyntaxTree, message, location);
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    // The offset in UTF-16 code units of the position that the message gives an id, or -1 for no position.
    private int OffsetOf(long id)
    {
        if (!_source.Positions.TryGetValue(id, out int position))
        {
            return -1;
        }

        if (position < 0 || (_hasText && position > _lines.CodePointLength))
        {
            throw ProtoErrors.Stop(
                CelErrorCode.InvalidSyntaxTree,
                "the position " + Number(position) + " of the node " + Number(id) + " is outside the source text");
        }

        return _hasText ? _lines.ToUtf16(position) : position;
    }

    // The offset for the next node or entry; a loaded node ends where it starts.
    private int At(long id)
    {
        int offset = OffsetOf(id);
        _nodes.PreviousEnd = offset;
        return offset;
    }

    // A node of the tree: made, then registered under its id, which no other node or entry has.
    private CelExprNode Node(ExprData data)
    {
        CelExprNode node = Create(data);
        Register(data.Id);
        _nodesById[data.Id] = node;
        return node;
    }

    private void Register(long id)
    {
        if (!_ids.Add(id))
        {
            throw Fault("the id " + Number(id) + " is used by more than one node or entry", id);
        }
    }

    // A part of a node: a node of the tree, or, in a macro call, a copy.
    private CelExprNode Child(ExprData? part, long parentId)
    {
        if (part is null)
        {
            throw Fault("the expression " + Number(parentId) + " lacks a part", parentId);
        }

        return _copy ? Copy(part) : Node(part);
    }

    // A copy of a macro call: the node of the tree that has the id, or the expansion that a copy without a kind names, or a new node
    // made from the copy by the same rule for its parts.
    private CelExprNode Copy(ExprData data)
    {
        if (data.Case == ExprCase.None)
        {
            if (_source.MacroCalls.ContainsKey(data.Id) && _nodesById.TryGetValue(data.Id, out CelExprNode? expansion))
            {
                return expansion;
            }

            throw Fault(
                "the macro call names the expansion " + Number(data.Id) + ", which is not a node of the tree with a macro call of its own",
                data.Id);
        }

        return _nodesById.TryGetValue(data.Id, out CelExprNode? known) ? known : Create(data);
    }

    private CelExprNode Create(ExprData data)
    {
        switch (data.Case)
        {
            case ExprCase.Constant:
                return CreateConstant(data);
            case ExprCase.Ident:
                return _nodes.Ident(Named(data.Name, data, "identifier"), At(data.Id));
            case ExprCase.Select:
                return CreateSelect(data);
            case ExprCase.Call:
                return CreateCall(data);
            case ExprCase.List:
                return CreateList(data);
            case ExprCase.Struct:
                return CreateStruct(data);
            case ExprCase.Comprehension:
                return CreateComprehension(data);
            default:
                throw Fault("the expression " + Number(data.Id) + " has no kind", data.Id);
        }
    }

    private string Named(string name, ExprData data, string what)
    {
        if (name.Length == 0)
        {
            throw Fault("the " + what + " of the expression " + Number(data.Id) + " has an empty name", data.Id);
        }

        return name;
    }

    private CelConstantNode CreateConstant(ExprData data)
    {
        if (data.Value is not { } value)
        {
            throw Fault("the constant of the expression " + Number(data.Id) + " has no value", data.Id);
        }

        return _nodes.Constant(value, At(data.Id));
    }

    private CelSelectNode CreateSelect(ExprData data)
    {
        CelExprNode operand = Child(data.First, data.Id);
        string field = Named(data.Name, data, "field");
        return _nodes.Select(operand, field, data.TestOnly, At(data.Id));
    }

    private CelCallNode CreateCall(ExprData data)
    {
        CelExprNode? target = data.First is null ? null : Child(data.First, data.Id);
        string function = Named(data.Name, data, "function");
        return _nodes.Call(function, target, Items(data), At(data.Id));
    }

    private ImmutableArray<CelExprNode> Items(ExprData data)
    {
        ImmutableArray<CelExprNode>.Builder items = ImmutableArray.CreateBuilder<CelExprNode>(data.Items.Count);
        foreach (ExprData item in data.Items)
        {
            items.Add(Child(item, data.Id));
        }

        return items.MoveToImmutable();
    }

    private CelListNode CreateList(ExprData data)
    {
        ImmutableArray<CelExprNode> elements = Items(data);
        int[] indices = [.. data.OptionalIndices];
        Array.Sort(indices);
        for (int i = 0; i < indices.Length; i++)
        {
            if (indices[i] < 0 || indices[i] >= elements.Length || (i > 0 && indices[i] == indices[i - 1]))
            {
                throw Fault("the optional index " + Number(indices[i]) + " of the list " + Number(data.Id) + " is outside the list or repeated", data.Id);
            }
        }

        return _nodes.List(elements, [.. indices], At(data.Id));
    }

    // A map literal has no message name; a message literal has one. The entries of each have the key of their kind.
    private CelExprNode CreateStruct(ExprData data)
    {
        if (data.Name.Length == 0)
        {
            ImmutableArray<CelMapEntryNode>.Builder entries = ImmutableArray.CreateBuilder<CelMapEntryNode>(data.Entries.Count);
            foreach (EntryData entry in data.Entries)
            {
                entries.Add(CreateMapEntry(entry, data));
            }

            return _nodes.Map(entries.MoveToImmutable(), At(data.Id));
        }

        ImmutableArray<CelStructFieldNode>.Builder fields = ImmutableArray.CreateBuilder<CelStructFieldNode>(data.Entries.Count);
        foreach (EntryData entry in data.Entries)
        {
            fields.Add(CreateField(entry, data));
        }

        return _nodes.Struct(data.Name, fields.MoveToImmutable(), At(data.Id));
    }

    private CelMapEntryNode CreateMapEntry(EntryData entry, ExprData owner)
    {
        if (entry.FieldKey is not null || entry.MapKey is null)
        {
            throw Fault("the entry " + Number(entry.Id) + " of the map " + Number(owner.Id) + " has no key expression or has a field name", entry.Id);
        }

        CelExprNode key = Child(entry.MapKey, owner.Id);
        CelExprNode value = Child(entry.Value, owner.Id);
        RegisterEntry(entry.Id);
        return _nodes.Entry(key, value, entry.IsOptional, At(entry.Id));
    }

    private CelStructFieldNode CreateField(EntryData entry, ExprData owner)
    {
        if (entry.MapKey is not null || string.IsNullOrEmpty(entry.FieldKey))
        {
            throw Fault("the entry " + Number(entry.Id) + " of the message " + Number(owner.Id) + " has no field name or has a key expression", entry.Id);
        }

        CelExprNode value = Child(entry.Value, owner.Id);
        RegisterEntry(entry.Id);
        return _nodes.Field(entry.FieldKey, value, entry.IsOptional, At(entry.Id));
    }

    // An entry of a literal in the tree has an id of its own; an entry of a copy is not registered.
    private void RegisterEntry(long id)
    {
        if (!_copy)
        {
            Register(id);
        }
    }

    private CelComprehensionNode CreateComprehension(ExprData data)
    {
        string iterVar = Named(data.Name, data, "iteration variable");
        string accuVar = Named(data.AccuName, data, "accumulator");
        CelExprNode range = Child(data.First, data.Id);
        CelExprNode init = Child(data.AccuInit, data.Id);
        CelExprNode condition = Child(data.Condition, data.Id);
        CelExprNode step = Child(data.Step, data.Id);
        CelExprNode result = Child(data.Result, data.Id);
        int offset = At(data.Id);
        return data.SecondName.Length == 0
            ? _nodes.Comprehension(iterVar, range, accuVar, init, condition, step, result, offset)
            : _nodes.Comprehension(iterVar, data.SecondName, range, accuVar, init, condition, step, result, offset);
    }

    // The macro calls of the message, in the order of their ids: each is recorded under the node of the tree it names, with a target
    // and arguments that are copies.
    private void RecordMacroCalls()
    {
        long[] ids = [.. _source.MacroCalls.Keys];
        Array.Sort(ids);
        _copy = true;
        foreach (long id in ids)
        {
            ExprData call = _source.MacroCalls[id];
            if (!_nodesById.TryGetValue(id, out CelExprNode? expansion))
            {
                throw Fault("a macro call is recorded under the id " + Number(id) + ", which is not a node of the tree", id);
            }

            if (call.Case != ExprCase.Call || call.Name.Length == 0)
            {
                throw Fault("the macro call recorded under the id " + Number(id) + " is not a call of a function", id);
            }

            // A macro of a namespace is recorded as cel-go writes it, a call with the identifier of the namespace as target; the library
            // keeps the namespace in the name and no target, so the record is brought to that form and the identifier is not copied.
            if (call.First is { Case: ExprCase.Ident } identifier
                && MacroTable.All.FindQualified(identifier.Name + "." + call.Name, call.Items.Count, hasTarget: false) is { Namespace: not null } macro)
            {
                _nodes.RecordMacroCall(expansion, new CelMacroCall(macro.QualifiedName, null, Items(call)));
                continue;
            }

            CelExprNode? target = call.First is null ? null : Copy(call.First);
            ImmutableArray<CelExprNode> arguments = Items(call);
            _nodes.RecordMacroCall(expansion, new CelMacroCall(call.Name, target, arguments));
        }
    }
}
