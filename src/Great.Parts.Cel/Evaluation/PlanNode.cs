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

    // The indices 0 to count - 1 of the positions of a literal with ? that were stored before the first one was dropped, in an array
    // with room for every position: from then on the node records the index of each position it stores.
    private protected static int[] FirstIndices(int room, int count)
    {
        int[] indices = new int[room];
        for (int i = 0; i < count; i++)
        {
            indices[i] = i;
        }

        return indices;
    }

    // Evaluates the arguments of a call from left to right onto the argument stack. Calls are strict: at
    // the first error value the arguments pushed so far are popped and the error is returned. An unknown value of a partial
    // evaluation is not the end: the other arguments are evaluated, and a real error among them is the result, as an error is
    // the result of a strict call whatever the other arguments are; otherwise the result is the union of the unknown values.
    private protected bool TryPushArguments(Frame frame, ImmutableArray<PlanNode> arguments, out CelValue error)
    {
        CelValue pending = default;
        int pushed = 0;
        for (int i = 0; i < arguments.Length; i++)
        {
            CelValue value = arguments[i].Evaluate(frame);
            if (value.Kind == CelValueKind.Error)
            {
                if (!value.IsUnknown)
                {
                    frame.Pop(pushed);
                    error = value;
                    return false;
                }

                pending = Defer(frame, pending, value);
                continue;
            }

            frame.Push(value);
            pushed++;
        }

        if (pending.IsUnknown)
        {
            frame.Pop(pushed);
            error = pending;
            return false;
        }

        error = default;
        return true;
    }

    // The unknown value that an unknown operand adds to those of the operands before it: the first one when there is none yet
    // (pending is not an unknown value), otherwise the union. The union is charged before it is made, even when one set already holds
    // the other, since the check reads both: a loop that is unknown in every pass makes a union of growing size in each one.
    private protected CelValue Defer(Frame frame, CelValue pending, CelValue unknown)
    {
        if (!pending.TryGetUnknown(out UnknownSet? earlier) || !unknown.TryGetUnknown(out UnknownSet? later))
        {
            return unknown;
        }

        if (frame.Metered)
        {
            frame.Offset = Offset;
            frame.Charge(UnknownSet.MergeUnits(earlier, later));
        }

        return CelValue.FromUnknown(UnknownSet.Merge(earlier, later));
    }
}
