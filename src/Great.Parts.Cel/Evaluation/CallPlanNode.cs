using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A call with one candidate: evaluates the arguments and calls the implementation of the candidate. When
// the planner marked arguments, the values are checked with Matches first — the top level of each and
// the marked ones at any depth, charging the descent; a mismatch is EvalNoMatchingOverload at the call, and an error
// value that an element of a marked argument is, is the result, with the place of the call.
// A call whose arguments are all unmarked calls the implementation directly. Costs the cost of the
// overload, after the checks.
internal sealed class CallPlanNode : PlanNode
{
    private readonly ImmutableArray<bool> _marks;
    private readonly bool _checks;
    private readonly bool _charges;

    internal CallPlanNode(int offset, OverloadBinding overload, ImmutableArray<bool> marks, bool charges, ImmutableArray<PlanNode> arguments)
        : base(offset, charges ? overload.FixedCost : 0)
    {
        _marks = marks;
        _checks = marks.Contains(true);
        _charges = charges;
        Overload = overload;
        Arguments = arguments;
    }

    internal OverloadBinding Overload { get; }

    internal ImmutableArray<PlanNode> Arguments { get; }

    // Whether the planner marked any argument for the check.
    internal bool Checks => _checks;

    internal override CelValue Evaluate(Frame frame)
    {
        if (!HasStack(frame, out CelValue error) || !TryPushArguments(frame, Arguments, out error))
        {
            return error;
        }

        // The cost is charged after the checks, by Invocation.
        frame.Offset = Offset;
        ReadOnlySpan<CelValue> values = frame.Peek(Arguments.Length);
        TypeFit fit = _checks ? Overload.Matches(values, _marks.AsSpan(), frame.Meter, frame.Types) : TypeFit.Yes;
        CelValue result = fit.IsFit
            ? Invocation.Call(frame, Overload, values, Offset, _charges)
            : EvaluationErrors.NotMatched(fit, Overload.Function, values, frame, Offset);
        frame.Pop(Arguments.Length);
        return result;
    }
}
