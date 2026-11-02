using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A call with one candidate: evaluates the arguments and calls the implementation of the candidate. When
// an argument has the static type dyn, the kinds of the values are checked against the parameter types
// first; a mismatch is EvalNoMatchingOverload at the call. Costs the fixed cost of the overload.
internal sealed class CallPlanNode : PlanNode
{
    private readonly OverloadImplementation _implementation;
    private readonly bool _checksKinds;

    internal CallPlanNode(int offset, OverloadBinding overload, bool checksKinds, ImmutableArray<PlanNode> arguments)
        : base(offset, overload.FixedCost)
    {
        // Only the special form _||_ has no implementation, and the planner plans it as an OrPlanNode.
        _implementation = overload.Implementation!;
        _checksKinds = checksKinds;
        Overload = overload;
        Arguments = arguments;
    }

    internal OverloadBinding Overload { get; }

    internal ImmutableArray<PlanNode> Arguments { get; }

    internal override CelValue Evaluate(Frame frame)
    {
        if (!TryPushArguments(frame, Arguments, out CelValue error))
        {
            return error;
        }

        ReadOnlySpan<CelValue> values = frame.Peek(Arguments.Length);
        var site = new CallSite(Overload.Function, Offset, frame.Lines);
        CelValue result = _checksKinds && !KindMatch.Fits(values, Overload.Declaration.ParameterTypes)
            ? site.NoMatchingOverload(values)
            : _implementation(values, in site);
        frame.Pop(Arguments.Length);
        return result;
    }
}
