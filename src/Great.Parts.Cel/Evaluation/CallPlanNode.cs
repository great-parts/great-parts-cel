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

    // The object that type checking prepared from the constant argument of the call, and the cache of patterns of the environment;
    // both are given to the call site of each evaluation.
    private readonly object? _prepared;
    private readonly object? _cache;

    internal CallPlanNode(
        int offset,
        OverloadBinding overload,
        ImmutableArray<bool> marks,
        bool charges,
        ImmutableArray<PlanNode> arguments,
        object? prepared = null,
        object? cache = null)
        : base(offset, charges ? overload.FixedCost : 0)
    {
        _prepared = prepared;
        _cache = cache;
        _marks = marks;
        _checks = marks.Contains(true) || overload.ShallowArguments;
        _charges = charges;
        Overload = overload;
        Arguments = arguments;
    }

    internal OverloadBinding Overload { get; }

    internal ImmutableArray<PlanNode> Arguments { get; }

    // Whether the call checks its arguments against the parameter types: the planner marked an argument for the check at depth, or
    // the overload reads the elements itself and has only the check of the top level.
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
            ? Invocation.Call(frame, Overload, values, Offset, _charges, _prepared, _cache)
            : EvaluationErrors.NotMatched(fit, Overload.Function, values, frame, Offset);
        frame.Pop(Arguments.Length);
        return result;
    }
}
