using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A call with several candidates, or with none: evaluates the arguments and calls the first candidate,
// in the order of the reference map, whose parameter types fit the kinds of the values. When none fits,
// and so always without candidates, the call is EvalNoMatchingOverload at its position. The cost is
// that of the chosen overload, so planning knows none of it.
internal sealed class DispatchPlanNode : PlanNode
{
    private readonly string _function;

    internal DispatchPlanNode(int offset, string function, ImmutableArray<OverloadBinding> candidates, ImmutableArray<PlanNode> arguments)
        : base(offset, 0)
    {
        _function = function;
        Candidates = candidates;
        Arguments = arguments;
    }

    internal ImmutableArray<OverloadBinding> Candidates { get; }

    internal ImmutableArray<PlanNode> Arguments { get; }

    internal override CelValue Evaluate(Frame frame)
    {
        if (!TryPushArguments(frame, Arguments, out CelValue error))
        {
            return error;
        }

        ReadOnlySpan<CelValue> values = frame.Peek(Arguments.Length);
        var site = new CallSite(_function, Offset, frame.Lines);
        CelValue result = Call(values, site);
        frame.Pop(Arguments.Length);
        return result;
    }

    private CelValue Call(ReadOnlySpan<CelValue> values, in CallSite site)
    {
        foreach (OverloadBinding candidate in Candidates)
        {
            if (KindMatch.Fits(values, candidate.Declaration.ParameterTypes))
            {
                // Only the special form _||_ has no implementation, and the planner plans it as an OrPlanNode.
                return candidate.Implementation!(values, in site);
            }
        }

        return site.NoMatchingOverload(values);
    }
}
