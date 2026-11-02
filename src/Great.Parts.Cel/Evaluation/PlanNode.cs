using System.Collections.Immutable;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A node of a plan. A node never changes after planning, so one plan serves any number of threads at
// once; the state of an evaluation lives in its frame.
internal abstract class PlanNode
{
    private protected PlanNode(int offset, ulong cost)
    {
        Offset = offset;
        Cost = cost;
    }

    // The offset of the syntax node, the position of the errors of this node.
    internal int Offset { get; }

    // The cost that the node charges before its operation, as far as planning knows it: a dispatch adds
    // the cost of the overload it chooses, a call the argument cost of its overload, and a map literal
    // the cost of its string keys.
    internal ulong Cost { get; }

    // The value of the node, or an error value.
    internal abstract CelValue Evaluate(Frame frame);

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
