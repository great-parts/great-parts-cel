using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A chain of _||_ calls as one node, with the operands in the order of the text and the position of the
// outermost call. The operands are evaluated from left to right up to the first true, which is the
// value. Otherwise the first operand that is an error value, or that is not a bool and so gives
// EvalNoMatchingOverload with the types of all operands, is the value; when all are false, false.
// Costs nothing beyond the operands.
internal sealed class OrPlanNode : PlanNode
{
    internal const string Function = "_||_";

    internal OrPlanNode(int offset, ImmutableArray<PlanNode> operands)
        : base(offset, 0)
    {
        Operands = operands;
    }

    internal ImmutableArray<PlanNode> Operands { get; }

    internal override CelValue Evaluate(Frame frame)
    {
        int failed = -1;
        for (int i = 0; i < Operands.Length; i++)
        {
            CelValue value = Operands[i].Evaluate(frame);
            if (value.TryGetBoolean(out bool operand))
            {
                if (operand)
                {
                    frame.Pop(i);
                    return value;
                }
            }
            else if (failed < 0)
            {
                failed = i;
            }

            frame.Push(value);
        }

        ReadOnlySpan<CelValue> values = frame.Peek(Operands.Length);
        CelValue result = failed < 0
            ? CelValue.FromBoolean(false)
            : values[failed].Kind == CelValueKind.Error
                ? values[failed]
                : new CallSite(Function, Offset, frame.Lines).NoMatchingOverload(values);
        frame.Pop(Operands.Length);
        return result;
    }
}
