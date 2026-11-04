using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A node of a plan. A node never changes after planning, so one plan serves any number of threads at
// once; the state of an evaluation lives in its frame. A node charges its cost after its children and
// before its own operation, and records its offset in the frame before it.
internal abstract class PlanNode
{
    private protected PlanNode(int offset, ulong cost)
    {
        Offset = offset;
        Cost = cost;
    }

    // The offset of the syntax node, the position of the errors of this node.
    internal int Offset { get; }

    // The cost that the node charges before its operation, as far as planning knows it: a call adds the
    // argument cost of its overload, a dispatch the cost of the overload it chooses, and a map literal the
    // cost of its string keys.
    internal ulong Cost { get; }

    // The value of the node, or an error value.
    internal abstract CelValue Evaluate(Frame frame);

    // Records the node as the one whose operation runs and charges its cost, with a limit.
    private protected void Begin(Frame frame)
    {
        frame.Offset = Offset;
        if (frame.Metered && Cost != 0)
        {
            frame.Charge(Cost);
        }
    }

    // Whether the stack has room for the children of the node; otherwise the error InsufficientStack at
    // the node, which does not evaluate them.
    private protected bool HasStack(Frame frame, out CelValue error)
    {
        if (RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            error = default;
            return true;
        }

        error = EvaluationErrors.InsufficientStack(frame.Lines, Offset);
        return false;
    }

    // Evaluates the arguments of a call from left to right onto the argument stack. Calls are strict: at
    // the first error value the arguments pushed so far are popped and the error is returned.
    private protected static bool TryPushArguments(Frame frame, ImmutableArray<PlanNode> arguments, out CelValue error)
    {
        for (int i = 0; i < arguments.Length; i++)
        {
            CelValue value = arguments[i].Evaluate(frame);
            if (value.Kind == CelValueKind.Error)
            {
                frame.Pop(i);
                error = value;
                return false;
            }

            frame.Push(value);
        }

        error = default;
        return true;
    }
}
