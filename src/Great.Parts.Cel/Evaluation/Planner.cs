using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Evaluation;

// Turns a checked expression, or a parsed one without type checking, into a plan of immutable nodes.
// Each distinct variable gets one slot that all its identifiers read. A chain of _||_ calls, which the
// parser builds as a balanced tree, becomes one OrPlanNode with all operands in the order of the text
// and the position of the outermost call.
internal sealed class Planner
{
    private readonly ParsedExpression _parsed;

    // The checked expression; null when planning without type checking.
    private readonly CheckedExpression? _checked;

    // The declarations whose overloads are the candidates of a call without type checking; null with it.
    private readonly DeclarationIndex? _declarations;

    private readonly Dictionary<string, int> _slots = new(StringComparer.Ordinal);

    private Planner(ParsedExpression parsed, CheckedExpression? checkedExpression, DeclarationIndex? declarations)
    {
        _parsed = parsed;
        _checked = checkedExpression;
        _declarations = declarations;
    }

    // Plans a checked expression: a call with one candidate of the reference map calls it, a call with
    // several chooses among them by the kinds of the values.
    internal static Plan Plan(CheckedExpression expression) => new Planner(expression.Parsed, expression, null).Build();

    // Plans a parsed expression without type checking: every identifier reads its name from the bindings,
    // and every call other than _||_ chooses among all overloads of its name and argument count.
    internal static Plan PlanWithoutTypeCheck(ParsedExpression expression, DeclarationIndex declarations) =>
        new Planner(expression, null, declarations).Build();

    private Plan Build()
    {
        PlanNode root = Visit(_parsed.Root, out int stackSize);
        return new Plan(root, _slots.Count, stackSize, _parsed.Lines);
    }

    // The node of a syntax node, and the most argument values on the stack at one time while it is
    // evaluated.
    private PlanNode Visit(ExprNode node, out int stackSize)
    {
        int offset = _parsed.GetOffset(node.Id);
        stackSize = 0;
        switch (node)
        {
            case ConstantNode constant:
                return new ConstantPlanNode(offset, constant.Value);
            case IdentNode identifier:
                return new VariablePlanNode(offset, Slot(identifier.Name), identifier.Name);
            case CallNode call when IsOr(call):
                var operands = new List<ExprNode>();
                AddOperands(call, operands);
                return new OrPlanNode(offset, VisitAll([.. operands], true, out stackSize));
            case CallNode call:
                return VisitCall(call, offset, VisitAll(call.Arguments, true, out stackSize));
            case ListNode list:
                return new ListPlanNode(offset, VisitAll(list.Elements, false, out stackSize));
            case StructNode map:
                return VisitMap(map, offset, out stackSize);
            default:
                throw new NotImplementedException();
        }
    }

    private static bool IsOr(CallNode call) =>
        string.Equals(call.Function, OrPlanNode.Function, StringComparison.Ordinal) && call.Arguments.Length == 2;

    // The operands of a chain of _||_ calls in the order of the text.
    private static void AddOperands(CallNode call, List<ExprNode> operands)
    {
        foreach (ExprNode argument in call.Arguments)
        {
            if (argument is CallNode inner && IsOr(inner))
            {
                AddOperands(inner, operands);
            }
            else
            {
                operands.Add(argument);
            }
        }
    }

    private PlanNode VisitCall(CallNode call, int offset, ImmutableArray<PlanNode> arguments)
    {
        if (_checked is null)
        {
            return new DispatchPlanNode(offset, call.Function, _declarations!.GetOverloads(call.Function, arguments.Length), arguments);
        }

        ImmutableArray<OverloadBinding> candidates = _checked.References[call.Id].Candidates;
        return candidates.Length == 1
            ? new CallPlanNode(offset, candidates[0], HasDynArgument(call, _checked), arguments)
            : new DispatchPlanNode(offset, call.Function, candidates, arguments);
    }

    // Whether an argument has the static type dyn, so that the kinds of its values have to be checked.
    private static bool HasDynArgument(CallNode call, CheckedExpression checkedExpression)
    {
        foreach (ExprNode argument in call.Arguments)
        {
            if (checkedExpression.Types[argument.Id].Kind == CelTypeKind.Dyn)
            {
                return true;
            }
        }

        return false;
    }

    private MapPlanNode VisitMap(StructNode map, int offset, out int stackSize)
    {
        ImmutableArray<PlanNode>.Builder keys = ImmutableArray.CreateBuilder<PlanNode>(map.Entries.Length);
        ImmutableArray<PlanNode>.Builder values = ImmutableArray.CreateBuilder<PlanNode>(map.Entries.Length);
        stackSize = 0;
        foreach (StructEntry entry in map.Entries)
        {
            keys.Add(Visit(entry.Key, out int keyStackSize));
            values.Add(Visit(entry.Value, out int valueStackSize));
            stackSize = Math.Max(stackSize, Math.Max(keyStackSize, valueStackSize));
        }

        return new MapPlanNode(offset, keys.MoveToImmutable(), values.MoveToImmutable());
    }

    // The nodes of the arguments of a call or of the elements of a list. Arguments stay on the stack
    // while the next ones are evaluated; elements of a list do not use the stack.
    private ImmutableArray<PlanNode> VisitAll(ImmutableArray<ExprNode> nodes, bool pushed, out int stackSize)
    {
        ImmutableArray<PlanNode>.Builder planned = ImmutableArray.CreateBuilder<PlanNode>(nodes.Length);
        stackSize = pushed ? nodes.Length : 0;
        for (int i = 0; i < nodes.Length; i++)
        {
            planned.Add(Visit(nodes[i], out int nodeStackSize));
            stackSize = Math.Max(stackSize, (pushed ? i : 0) + nodeStackSize);
        }

        return planned.MoveToImmutable();
    }

    private int Slot(string name)
    {
        if (!_slots.TryGetValue(name, out int slot))
        {
            slot = _slots.Count;
            _slots.Add(name, slot);
        }

        return slot;
    }
}
