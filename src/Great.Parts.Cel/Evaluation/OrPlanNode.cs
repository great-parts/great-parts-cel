using System.Collections.Immutable;

namespace Great.Parts.Cel.Evaluation;

// A chain of _||_ calls.
internal sealed class OrPlanNode : LogicalPlanNode
{
    internal const string Function = "_||_";

    internal OrPlanNode(int offset, ImmutableArray<PlanNode> operands)
        : base(offset, operands, true, Function)
    {
    }
}
