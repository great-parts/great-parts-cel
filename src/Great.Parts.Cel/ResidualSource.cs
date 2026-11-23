using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel;

// What the residual expression of a partial evaluation is made from: the parsed expression of the program and the values that its
// nodes had. Build follows PruneAst of cel-go (interpreter/prune.go): a node with a known value that has a written form is replaced
// by it; calls shrink when an operand is known; of a comprehension only the range is replaced. The tree is copied with
// CelExprFactory.Copy, whose callback decides for each node, from the top, whether it is replaced.
internal sealed class ResidualSource(CelParsedExpr tree, ValueRecord values)
{
    private const string LogicalOr = "_||_";
    private const string LogicalAnd = "_&&_";
    private const string Conditional = "_?_:_";
    private const string LogicalNot = "!_";
    private const string InOperator = "@in";

    // The residual expression: a new tree with new ids, whose nodes keep the positions of the program.
    internal CelParsedExpr Build()
    {
        var factory = new CelExprFactory(tree.Lines);
        var pruner = new Pruner(factory, tree, values);
        CelExprNode root = factory.Copy(tree, tree.Root, pruner.Replace);
        return factory.Build(root);
    }

    // The decisions of one copy of the tree.
    private sealed class Pruner(CelExprFactory factory, CelParsedExpr tree, ValueRecord known)
    {
        // The ids of the nodes of the loop of a comprehension and of its result: not replaced, because the record keeps the value of
        // the last evaluation of a node, and of a loop that is not the value of every step.
        private readonly HashSet<int> _frozen = [];

        // The replacement of a node of the tree, or null to copy it and look at its children.
        internal CelExprNode? Replace(CelExprNode node)
        {
            if (_frozen.Contains(node.Id))
            {
                return null;
            }

            if (known.TryGet(node.Id, out CelValue value) && Written(node, value) is { } written)
            {
                return written;
            }

            switch (node)
            {
                case CelComprehensionNode comprehension:
                    Freeze(comprehension);
                    return null;
                case CelCallNode { Target: null } call:
                    return Shrink(call);
                default:
                    return null;
            }
        }

        // The call after an operand became known: the result of the operator or the operand that stays.
        private CelExprNode? Shrink(CelCallNode call)
        {
            if (ShrunkValue(call) is { } literal)
            {
                return Written(call, literal);
            }

            int count = call.Arguments.Length;
            if (call.Function is LogicalOr or LogicalAnd && count == 2)
            {
                // The deciding operand was handled above: an operand that is known and does not decide drops out, and the other stays.
                for (int i = 0; i < 2; i++)
                {
                    if (ValueOf(call.Arguments[i]) is not null)
                    {
                        return Copy(call.Arguments[1 - i]);
                    }
                }

                return null;
            }

            return call.Function == Conditional && count == 3 && ValueOf(call.Arguments[0]) is { } condition && condition.TryGetBoolean(out bool chosen)
                ? Copy(call.Arguments[chosen ? 1 : 2])
                : null;
        }

        // The literal that a call shrinks to, as the pruning of cel-go gives a node the value of the literal it makes: the negation of a
        // known bool, in of a known empty collection, and && or || with an operand that decides; null for any other call.
        private CelValue? ShrunkValue(CelCallNode call)
        {
            int count = call.Arguments.Length;
            switch (call.Function)
            {
                case LogicalNot when count == 1 && ValueOf(call.Arguments[0]) is { } operand && operand.TryGetBoolean(out bool negated):
                    return CelValue.FromBoolean(!negated);
                case InOperator when count == 2 && IsEmpty(call.Arguments[1]):
                    return CelValue.FromBoolean(false);
                case LogicalOr when count == 2:
                    return Decided(call, true);
                case LogicalAnd when count == 2:
                    return Decided(call, false);
                default:
                    return null;
            }
        }

        // The deciding value, true for || and false for &&, when an operand of the call has it.
        private CelValue? Decided(CelCallNode call, bool decisive)
        {
            foreach (CelExprNode argument in call.Arguments)
            {
                if (ValueOf(argument) is { } value && value.TryGetBoolean(out bool flag) && flag == decisive)
                {
                    return value;
                }
            }

            return null;
        }

        // The known value of an operand, as it was evaluated or as its call shrinks to a literal; null when it was not evaluated, was
        // an error or an unknown, and does not shrink.
        private CelValue? ValueOf(CelExprNode node)
        {
            if (known.TryGet(node.Id, out CelValue value))
            {
                return value;
            }

            return node is CelCallNode { Target: null } call ? ShrunkValue(call) : null;
        }

        // Whether the operand is an empty list or map: a literal without elements, which a failed sibling operand can leave unevaluated,
        // or a known value.
        private bool IsEmpty(CelExprNode operand) =>
            operand is CelListNode { Elements.Length: 0 } or CelMapNode { Count: 0 }
            || (ValueOf(operand) is { } collection
                && ((collection.TryGetList(out CelList? list) && list.Count == 0) || (collection.TryGetMap(out CelMap? map) && map.Count == 0)));

        private CelExprNode Copy(CelExprNode node) => factory.Copy(tree, node, Replace);

        // Marks the nodes of the loop condition, the loop step, the initializer and the result of a comprehension: only its range is
        // replaced.
        private void Freeze(CelComprehensionNode comprehension)
        {
            var pending = new Stack<CelExprNode>();
            pending.Push(comprehension.AccuInit);
            pending.Push(comprehension.LoopCondition);
            pending.Push(comprehension.LoopStep);
            pending.Push(comprehension.Result);
            while (pending.Count > 0)
            {
                CelExprNode node = pending.Pop();
                _ = _frozen.Add(node.Id);
                foreach (CelExprNode child in node.Children)
                {
                    pending.Push(child);
                }
            }
        }

        // The node that writes the value at the position of the node, or null when the value has no written form.
        private CelExprNode? Written(CelExprNode node, CelValue value)
        {
            int offset = tree.GetOffset(node.Id);
            factory.Position = offset < 0 ? null : offset..tree.GetEnd(node.Id);
            return Write(value);
        }

        private CelExprNode? Write(CelValue value)
        {
            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            {
                return null;
            }

            switch (value.Kind)
            {
                case CelValueKind.Null or CelValueKind.Bool or CelValueKind.Int or CelValueKind.UInt or CelValueKind.String or CelValueKind.Bytes:
                    return factory.Constant(value);
                case CelValueKind.Double:
                    return WriteDouble(value);
                case CelValueKind.Timestamp:
                    _ = value.TryGetTimestamp(out CelTimestamp timestamp);
                    return factory.Call("timestamp", factory.Constant(CelValue.FromString(timestamp.ToString())));
                case CelValueKind.Duration:
                    _ = value.TryGetDuration(out CelDuration duration);
                    return factory.Call("duration", factory.Constant(CelValue.FromString(duration.ToString())));
                case CelValueKind.Date:
                    return WriteDate(value);
                case CelValueKind.TimeOfDay:
                    return WriteTimeOfDay(value);
                case CelValueKind.Optional:
                    return WriteOptional(value);
                case CelValueKind.List:
                    return WriteList(value);
                case CelValueKind.Map:
                    return WriteMap(value);
#if NET11_0_OR_GREATER
                case CelValueKind.Decimal:
                    return WriteDecimal(value);
#endif
                default:
                    return null;
            }
        }

        // A date and a time of day are the calls of the calendar set on their text, which the library writes without a culture.
        private CelCallNode WriteDate(CelValue value)
        {
            _ = value.TryGetDate(out DateOnly date);
            Span<char> text = stackalloc char[CalendarTextRules.DateLength];
            return factory.Call("date", factory.Constant(CelValue.FromString(new string(text[..CalendarTextRules.WriteDate(date, text)]))));
        }

        private CelCallNode WriteTimeOfDay(CelValue value)
        {
            _ = value.TryGetTimeOfDay(out TimeOnly time);
            Span<char> text = stackalloc char[CalendarTextRules.MaxTimeOfDayLength];
            return factory.Call("time_of_day", factory.Constant(CelValue.FromString(new string(text[..CalendarTextRules.WriteTimeOfDay(time, text)]))));
        }

        // A finite double is a literal; a NaN and an infinity are the conversion that gives them.
        private CelExprNode? WriteDouble(CelValue value)
        {
            _ = value.TryGetDouble(out double number);
            if (double.IsFinite(number))
            {
                return factory.Constant(value);
            }

            string text = double.IsNaN(number) ? "NaN" : number > 0 ? "Infinity" : "-Infinity";
            return factory.Call("double", factory.Constant(CelValue.FromString(text)));
        }

#if NET11_0_OR_GREATER
        private CelCallNode WriteDecimal(CelValue value)
        {
            // The text of the value ends with the suffix m of a decimal literal; the conversion takes the digits.
            string literal = value.ToString();
            return factory.Call("decimal", factory.Constant(CelValue.FromString(literal[..^1])));
        }
#endif

        private CelCallNode? WriteOptional(CelValue value)
        {
            _ = value.TryGetOptional(out CelOptional<CelValue> optional);
            if (!optional.HasValue)
            {
                return factory.Call("optional.none");
            }

            return Write(optional.Value) is { } held ? factory.Call("optional.of", held) : null;
        }

        private CelListNode? WriteList(CelValue value)
        {
            _ = value.TryGetList(out CelList? list);
            ImmutableArray<CelExprNode>.Builder elements = ImmutableArray.CreateBuilder<CelExprNode>();
            foreach (CelValue item in list!)
            {
                if (item.Kind == CelValueKind.Error || Write(item) is not { } element)
                {
                    return null;
                }

                elements.Add(element);
            }

            return factory.List(elements.ToImmutable().AsSpan());
        }

        private CelMapNode? WriteMap(CelValue value)
        {
            _ = value.TryGetMap(out CelMap? map);
            ImmutableArray<CelMapEntryNode>.Builder entries = ImmutableArray.CreateBuilder<CelMapEntryNode>();
            foreach (KeyValuePair<CelValue, CelValue> entry in map!)
            {
                if (entry.Key.Kind == CelValueKind.Error || entry.Value.Kind == CelValueKind.Error
                    || Write(entry.Key) is not { } key || Write(entry.Value) is not { } item)
                {
                    return null;
                }

                entries.Add(factory.MapEntry(key, item));
            }

            return factory.Map(entries.ToImmutable().AsSpan());
        }
    }
}
