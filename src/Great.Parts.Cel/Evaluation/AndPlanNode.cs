using System.Collections.Immutable;

namespace Great.Parts.Cel.Evaluation;

// A chain of _&&_ calls.
internal sealed class AndPlanNode : LogicalPlanNode
{
    internal const string Function = "_&&_";

    internal AndPlanNode(int offset, ImmutableArray<PlanNode> operands)
        : base(offset, operands, false, Function)
    {
    }
}
