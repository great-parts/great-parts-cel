using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A chain of _&&_ or of _||_ calls as one node, with the operands in the order of the text and the
// position of the outermost call. The operands are evaluated from left to right up to the first that
// decides the result — false for &&, true for || — which is the value. Otherwise the first operand that
// is an error value, or that is not a bool and so gives EvalNoMatchingOverload with the types of all
// operands, is the value; when none decides and none fails, true for && and false for ||. Costs nothing
// beyond the operands.
internal abstract class LogicalPlanNode : PlanNode
{
    private readonly bool _decisive;
    private readonly string _function;

    private protected LogicalPlanNode(int offset, ImmutableArray<PlanNode> operands, bool decisive, string function)
        : base(offset, 0)
    {
        Operands = operands;
        _decisive = decisive;
        _function = function;
    }

    internal ImmutableArray<PlanNode> Operands { get; }

    internal override CelValue Evaluate(Frame frame)
    {
        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        int failed = -1;
        for (int i = 0; i < Operands.Length; i++)
        {
            CelValue value = Operands[i].Evaluate(frame);
            if (value.TryGetBoolean(out bool operand))
            {
                if (operand == _decisive)
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

        Begin(frame);
        ReadOnlySpan<CelValue> values = frame.Peek(Operands.Length);
        CelValue result = failed < 0
            ? CelValue.FromBoolean(!_decisive)
            : values[failed].Kind == CelValueKind.Error
                ? values[failed]
                : new CallSite(_function, Offset, frame.Lines, types: frame.Types).NoMatchingOverload(values);
        frame.Pop(Operands.Length);
        return result;
    }
}
