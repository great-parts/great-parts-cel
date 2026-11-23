using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Syntax;

// Writes the tree of a parsed expression as Expr messages and its source info as a SourceInfo message, in a form: the parsed form,
// or the checked form of the layer of type checking. The write runs three times (ProtoWriter.Encode); what a later part of the
// message needs from an earlier one — the positions of the nodes that the macro calls hold, the ids of the arguments of decimal
// constants — is collected on the first run and kept for the others.
internal sealed class ExprWriter
{
    private const string DecimalFunction = "decimal";

    private readonly CelParsedExpr _parsed;
    private readonly ExprForm _form;

    // The ids of the nodes of the tree that are the expansion of a macro call that the text writes: such a node in the part of a macro
    // call is written as its id alone.
    private readonly HashSet<int> _expansions = [];

    // The position of each written id, in code points, in ascending order of the ids.
    private readonly SortedDictionary<int, int> _positions = [];

    // The id of the argument of the call that stands for a decimal constant, by the id of the constant.
    private readonly Dictionary<int, int> _decimalArguments = [];

    private ProtoWriter _writer = null!;

    // Whether the nodes being written are in a macro call: the parsed form, and an expansion is only its id.
    private bool _inMacroCall;

    internal ExprWriter(CelParsedExpr parsed, ExprForm form)
    {
        _parsed = parsed;
        _form = form;
        for (int i = 0; i < parsed.MacroIds.Length; i++)
        {
            if (parsed.MacroCalls[i].IsWritten && parsed.FindNode(parsed.MacroIds[i]) is not null)
            {
                _ = _expansions.Add(parsed.MacroIds[i]);
            }
        }
    }

    /// <summary>
    /// The ids of the arguments of the calls that stand for decimal constants, by the id of the constant, after a run of the write.
    /// </summary>
    internal IReadOnlyDictionary<int, int> DecimalArguments => _decimalArguments;

    /// <summary>
    /// The position of a decimal constant, which its call and its argument share, in code points; -1 for none.
    /// </summary>
    /// <param name="constantId">The id of the constant.</param>
    /// <returns>The position.</returns>
    internal int DecimalPosition(int constantId)
    {
        int offset = _parsed.GetOffset(constantId);
        return offset < 0 ? -1 : _parsed.Lines.ToCodePoints(offset);
    }

    internal void Use(ProtoWriter writer)
    {
        _writer = writer;
        _inMacroCall = false;
    }

    /// <summary>
    /// Writes the expression as the field of a message.
    /// </summary>
    /// <param name="field">The number of the field.</param>
    /// <param name="root">The root of the tree.</param>
    internal void WriteExpr(int field, CelExprNode root) => WriteNode(field, root);

    private void WriteNode(int field, CelExprNode node)
    {
        int token = _writer.Begin(field, out int start);
        WriteNodeBody(node);
        _writer.End(token, start);
    }

    private void WriteNodeBody(CelExprNode node)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            throw new InvalidOperationException("The tree is too deep to be written for the stack that is left.");
        }

        _writer.Int64(2, node.Id);
        if (_inMacroCall && _expansions.Contains(node.Id))
        {
            return;
        }

        Position(node.Id);
        if (!_inMacroCall && _form.TryIdentifier(node, out string? name))
        {
            WriteIdent(name);
            return;
        }

        switch (node)
        {
            case CelConstantNode constant:
                WriteConstant(constant);
                break;
            case CelIdentNode identifier:
                WriteIdent(identifier.Name);
                break;
            case CelSelectNode select:
                WriteSelect(select);
                break;
            case CelCallNode call:
                WriteCall(call);
                break;
            case CelListNode list:
                WriteList(list);
                break;
            case CelMapNode map:
                WriteMap(map);
                break;
            case CelStructNode message:
                WriteStruct(message);
                break;
            default:
                WriteComprehension((CelComprehensionNode)node);
                break;
        }
    }

    // The position of a node or an entry in code points, remembered for the source info.
    private void Position(int id)
    {
        int offset = _parsed.GetOffset(id);
        if (offset >= 0)
        {
            _positions[id] = _parsed.Lines.ToCodePoints(offset);
        }
    }

    private void WriteIdent(string name)
    {
        int token = _writer.Begin(4, out int start);
        _writer.String(1, name);
        _writer.End(token, start);
    }

    private void WriteSelect(CelSelectNode select)
    {
        int token = _writer.Begin(5, out int start);
        WriteNode(1, select.Operand);
        _writer.String(2, select.Field);
        _writer.Bool(3, select.IsPresenceTest);
        _writer.End(token, start);
    }

    private void WriteCall(CelCallNode call)
    {
        string function = call.Function;
        CelExprNode? target = call.Target;
        if (!_inMacroCall && _form.TryCall(call, out string? written, out bool dropTarget))
        {
            function = written;
            target = dropTarget ? null : target;
        }

        int token = _writer.Begin(6, out int start);
        if (target is not null)
        {
            WriteNode(1, target);
        }

        _writer.String(2, function);
        foreach (CelExprNode argument in call.Arguments)
        {
            WriteNode(3, argument);
        }

        _writer.End(token, start);
    }

    private void WriteList(CelListNode list)
    {
        int token = _writer.Begin(7, out int start);
        foreach (CelExprNode element in list.Elements)
        {
            WriteNode(1, element);
        }

        _writer.PackedInt32(2, list.OptionalIndices.AsSpan());
        _writer.End(token, start);
    }

    private void WriteMap(CelMapNode map)
    {
        int token = _writer.Begin(8, out int start);
        foreach (CelMapEntryNode entry in map.Entries)
        {
            int entryToken = _writer.Begin(2, out int entryStart);
            _writer.Int64(1, entry.Id);
            Position(entry.Id);
            WriteNode(3, entry.Key);
            WriteNode(4, entry.Value);
            _writer.Bool(5, entry.IsOptional);
            _writer.End(entryToken, entryStart);
        }

        _writer.End(token, start);
    }

    private void WriteStruct(CelStructNode message)
    {
        int token = _writer.Begin(8, out int start);
        _writer.String(1, _inMacroCall ? message.TypeName : _form.MessageName(message));
        foreach (CelStructFieldNode field in message.Fields)
        {
            int fieldToken = _writer.Begin(2, out int fieldStart);
            _writer.Int64(1, field.Id);
            Position(field.Id);
            _writer.StringAlways(2, field.Name);
            WriteNode(4, field.Value);
            _writer.Bool(5, field.IsOptional);
            _writer.End(fieldToken, fieldStart);
        }

        _writer.End(token, start);
    }

    private void WriteComprehension(CelComprehensionNode comprehension)
    {
        int token = _writer.Begin(9, out int start);
        _writer.String(1, comprehension.IterVar);
        WriteNode(2, comprehension.IterRange);
        _writer.String(3, comprehension.AccuVar);
        WriteNode(4, comprehension.AccuInit);
        WriteNode(5, comprehension.LoopCondition);
        WriteNode(6, comprehension.LoopStep);
        WriteNode(7, comprehension.Result);
        _writer.String(8, comprehension.IterVar2);
        _writer.End(token, start);
    }

    private void WriteConstant(CelConstantNode constant)
    {
        CelValue value = constant.Value;
        if (value.Kind == CelValueKind.Decimal)
        {
            WriteDecimal(constant);
            return;
        }

        int token = _writer.Begin(3, out int start);
        WriteConstantValue(value);
        _writer.End(token, start);
    }

    private void WriteConstantValue(CelValue value)
    {
        switch (value.Kind)
        {
            case CelValueKind.Bool:
                _ = value.TryGetBoolean(out bool flag);
                _writer.BoolAlways(2, flag);
                break;
            case CelValueKind.Int:
                _ = value.TryGetInt64(out long integer);
                _writer.Int64Always(3, integer);
                break;
            case CelValueKind.UInt:
                _ = value.TryGetUInt64(out ulong unsigned);
                _writer.UInt64Always(4, unsigned);
                break;
            case CelValueKind.Double:
                _ = value.TryGetDouble(out double number);
                _writer.DoubleAlways(5, number);
                break;
            case CelValueKind.String:
                _ = value.TryGetString(out string? text);
                _writer.StringAlways(6, text!);
                break;
            case CelValueKind.Bytes:
                _ = value.TryGetBytes(out ReadOnlyMemory<byte> bytes);
                _writer.BytesAlways(7, bytes.Span);
                break;
            case CelValueKind.Duration:
                _ = value.TryGetDuration(out CelDuration duration);
                WriteSecondsAndNanos(8, duration.Seconds, duration.Nanoseconds);
                break;
            case CelValueKind.Timestamp:
                _ = value.TryGetTimestamp(out CelTimestamp timestamp);
                WriteSecondsAndNanos(9, timestamp.Seconds, timestamp.Nanoseconds);
                break;
            default:
                _writer.Int64Always(1, 0);
                break;
        }
    }

    // google.protobuf.Duration and Timestamp: the seconds and the nanoseconds, as the deprecated fields of the constant hold them.
    private void WriteSecondsAndNanos(int field, long seconds, int nanoseconds)
    {
        int token = _writer.Begin(field, out int start);
        _writer.Int64(1, seconds);
        _writer.Int64(2, nanoseconds);
        _writer.End(token, start);
    }

    // A decimal constant has no constant message: it is the call decimal("text") whose id is the id of the constant and whose argument
    // has an id above all ids of the tree. Both start where the constant does.
    private void WriteDecimal(CelConstantNode constant)
    {
        int argument = DecimalArgument(constant.Id);
        int call = _writer.Begin(6, out int callStart);
        _writer.StringAlways(2, DecimalFunction);
        int node = _writer.Begin(3, out int nodeStart);
        _writer.Int64(2, argument);
        int position = DecimalPosition(constant.Id);
        if (position >= 0)
        {
            _positions[argument] = position;
        }

        int message = _writer.Begin(3, out int messageStart);
        _writer.StringAlways(6, DecimalText(constant.Value));
        _writer.End(message, messageStart);
        _writer.End(node, nodeStart);
        _writer.End(call, callStart);
    }

    private int DecimalArgument(int constantId)
    {
        ref int argument = ref CollectionsMarshal.GetValueRefOrAddDefault(_decimalArguments, constantId, out bool exists);
        if (!exists)
        {
            argument = _parsed.IdCount + _decimalArguments.Count;
        }

        return argument;
    }

    // The text of the value as string() writes it.
    private static string DecimalText(CelValue value)
    {
        _ = value.TryGetPlatformDecimal(out PlatformDecimal number);
        Span<char> digits = stackalloc char[DecimalTextRules.BufferLength];
        int count = DecimalTextRules.Format(DecimalPlatform.ToParts(number), digits);
        return new string(digits[..count]);
    }

    /// <summary>
    /// Writes the source info as the field of a message: the location, the lines, the positions and the macro calls.
    /// </summary>
    /// <param name="field">The number of the field.</param>
    internal void WriteSourceInfo(int field)
    {
        int token = _writer.Begin(field, out int start);
        _writer.String(2, _parsed.SourceName);
        WriteLineOffsets();
        foreach (KeyValuePair<int, int> position in _positions)
        {
            int entry = _writer.Begin(4, out int entryStart);
            _writer.Int64Always(1, position.Key);
            _writer.Int64(2, position.Value);
            _writer.End(entry, entryStart);
        }

        WriteMacroCalls();
        _writer.End(token, start);
    }

    // The start of each line after the first, then the length of the text plus 1, in code points, as the parser of cel-go counts them.
    private void WriteLineOffsets()
    {
        LineTable lines = _parsed.Lines;
        if (lines.Length == 0)
        {
            return;
        }

        ImmutableArray<int> starts = lines.LineStarts;
        int[] offsets = new int[starts.Length + 1];
        for (int i = 0; i < starts.Length; i++)
        {
            offsets[i] = lines.ToCodePoints(starts[i]);
        }

        offsets[^1] = lines.CodePointLength + 1;
        _writer.PackedInt32(3, offsets);
    }

    // map<int64, Expr> macro_calls: for the expansion of each macro call that the text writes, the call with its target and
    // arguments as copies in the form of parsing; an argument that is an expansion itself is only its id.
    private void WriteMacroCalls()
    {
        _inMacroCall = true;
        for (int i = 0; i < _parsed.MacroIds.Length; i++)
        {
            int id = _parsed.MacroIds[i];
            if (!_expansions.Contains(id))
            {
                continue;
            }

            CelMacroCall macroCall = _parsed.MacroCalls[i];
            int entry = _writer.Begin(5, out int entryStart);
            _writer.Int64Always(1, id);
            int value = _writer.Begin(2, out int valueStart);
            int call = _writer.Begin(6, out int callStart);
            if (macroCall.Target is { } target)
            {
                WriteNode(1, target);
            }

            _writer.String(2, macroCall.Function);
            foreach (CelExprNode argument in macroCall.Arguments)
            {
                WriteNode(3, argument);
            }

            _writer.End(call, callStart);
            _writer.End(value, valueStart);
            _writer.End(entry, entryStart);
        }

        _inMacroCall = false;
    }
}
