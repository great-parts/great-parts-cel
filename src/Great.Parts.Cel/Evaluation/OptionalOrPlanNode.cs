using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// r.or(a) and r.orValue(a) of the set optional: the receiver first. An error value of the receiver is the result; a value that is not an
// optional is EvalNoMatchingOverload with the type of the receiver, and the argument is not evaluated. With a limit the node then charges
// its 1 unit. A receiver that holds a value is the result of or and its held value the result of orValue, and the argument is not
// evaluated. An empty receiver makes the argument evaluated: its error value is the result; for or the argument has to be an optional,
// else EvalNoMatchingOverload with the types of both, and it is the result; for orValue the value of any kind is the result. The node
// counts no event, since it calls no code of the application.
internal sealed class OptionalOrPlanNode : PlanNode
{
    private const string OrFunction = "or";
    private const string OrValueFunction = "orValue";

    private readonly PlanNode _receiver;
    private readonly PlanNode _argument;
    private readonly bool _isValue;

    internal OptionalOrPlanNode(int offset, ulong cost, PlanNode receiver, PlanNode argument, bool isValue)
        : base(offset, cost)
    {
        _receiver = receiver;
        _argument = argument;
        _isValue = isValue;
    }

    internal override CelValue Evaluate(Frame frame)
    {
        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        CelValue receiver = _receiver.Evaluate(frame);
        if (receiver.Kind == CelValueKind.Error)
        {
            return receiver;
        }

        if (!receiver.TryGetOptional(out CelOptional<CelValue> optional))
        {
            frame.Offset = Offset;
            return NoMatchingOverload(frame, [receiver]);
        }

        Begin(frame);
        if (optional.HasValue)
        {
            return _isValue ? optional.Value : receiver;
        }

        CelValue argument = _argument.Evaluate(frame);
        if (argument.Kind == CelValueKind.Error || _isValue || argument.Kind == CelValueKind.Optional)
        {
            return argument;
        }

        frame.Offset = Offset;
        return NoMatchingOverload(frame, [receiver, argument]);
    }

    private CelValue NoMatchingOverload(Frame frame, ReadOnlySpan<CelValue> values) =>
        new CallSite(_isValue ? OrValueFunction : OrFunction, Offset, frame.Lines, types: frame.Types).NoMatchingOverload(values);
}
