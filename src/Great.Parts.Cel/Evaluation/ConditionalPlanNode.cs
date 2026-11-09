using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// _?_:_: the condition first; true evaluates only the second operand and false only the third. An error
// value of the condition is the result, and another kind is EvalNoMatchingOverload with the type of the
// condition. Costs nothing beyond the operands.
internal sealed class ConditionalPlanNode : PlanNode
{
    internal const string Function = "_?_:_";

    private readonly PlanNode _condition;
    private readonly PlanNode _then;
    private readonly PlanNode _else;

    internal ConditionalPlanNode(int offset, PlanNode condition, PlanNode then, PlanNode otherwise)
        : base(offset, 0)
    {
        _condition = condition;
        _then = then;
        _else = otherwise;
    }

    internal override CelValue Evaluate(Frame frame)
    {
        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        CelValue condition = _condition.Evaluate(frame);
        if (condition.TryGetBoolean(out bool value))
        {
            return value ? _then.Evaluate(frame) : _else.Evaluate(frame);
        }

        Begin(frame);
        return condition.Kind == CelValueKind.Error
            ? condition
            : new CallSite(Function, Offset, frame.Lines, types: frame.Types).NoMatchingOverload([condition]);
    }
}
