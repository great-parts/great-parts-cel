using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// @not_strictly_false, the loop condition of all and exists: a bool operand is the result, and any other
// value, an error included, is true. Costs nothing.
internal sealed class NotStrictlyFalsePlanNode : PlanNode
{
    internal const string Function = "@not_strictly_false";

    private static readonly CelValue True = CelValue.FromBoolean(true);

    private readonly PlanNode _operand;

    internal NotStrictlyFalsePlanNode(int offset, PlanNode operand)
        : base(offset, 0)
    {
        _operand = operand;
    }

    internal override CelValue Evaluate(Frame frame)
    {
        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        CelValue value = _operand.Evaluate(frame);
        return value.Kind == CelValueKind.Bool ? value : True;
    }
}
