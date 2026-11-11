using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A call with several candidates, or with none: evaluates the arguments and calls the first candidate,
// in the order of the reference map, that Matches the values — the top level of every argument and the
// arguments the planner marked for that candidate at any depth. When none matches, and so always without
// candidates, the call is EvalNoMatchingOverload at its position. An error value that an element of a marked
// argument is ends the search: it is the result, with the place of the call. The cost is that of the chosen
// overload, so planning knows none of it.
internal sealed class DispatchPlanNode : PlanNode
{
    private readonly string _function;

    // The marks of the arguments for each candidate, at the same index.
    private readonly ImmutableArray<ImmutableArray<bool>> _marks;
    private readonly bool _charges;

    // The object that type checking prepared from the constant argument of the call, and the cache of patterns of the environment;
    // both are given to the call site of each evaluation.
    private readonly object? _prepared;
    private readonly object? _cache;

    internal DispatchPlanNode(
        int offset,
        string function,
        ImmutableArray<OverloadBinding> candidates,
        ImmutableArray<ImmutableArray<bool>> marks,
        bool charges,
        ImmutableArray<PlanNode> arguments,
        object? prepared = null,
        object? cache = null)
        : base(offset, 0)
    {
        _prepared = prepared;
        _cache = cache;
        _function = function;
        _marks = marks;
        _charges = charges;
        Candidates = candidates;
        Arguments = arguments;
    }

    internal ImmutableArray<OverloadBinding> Candidates { get; }

    internal ImmutableArray<PlanNode> Arguments { get; }

    internal override CelValue Evaluate(Frame frame)
    {
        if (!HasStack(frame, out CelValue error) || !TryPushArguments(frame, Arguments, out error))
        {
            return error;
        }

        frame.Offset = Offset;
        ReadOnlySpan<CelValue> values = frame.Peek(Arguments.Length);
        CelValue result = Call(frame, values);
        frame.Pop(Arguments.Length);
        return result;
    }

    private CelValue Call(Frame frame, ReadOnlySpan<CelValue> values)
    {
        for (int i = 0; i < Candidates.Length; i++)
        {
            OverloadBinding candidate = Candidates[i];
            TypeFit fit = candidate.Matches(values, _marks[i].AsSpan(), frame.Meter, frame.Types);
            if (fit.IsFit)
            {
                return Invocation.Call(frame, candidate, values, Offset, _charges, _prepared, _cache);
            }

            // An error value of an element is the result of the call: no later candidate is tried.
            if (fit.ElementError is not null)
            {
                return EvaluationErrors.NotMatched(fit, _function, values, frame, Offset);
            }
        }

        return EvaluationErrors.NotMatched(TypeFit.No, _function, values, frame, Offset);
    }
}
