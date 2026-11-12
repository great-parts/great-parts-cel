using System.Collections.Immutable;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A list literal with elements that have a ?, [1, ?e]. The elements are evaluated from left to right onto the argument stack, and
// the first error value is the result. The value of an element with ? must be an optional: a value that is not one, which only an
// expression of the static type dyn can give, is EvalNoMatchingOverload with the function ? at the element; an empty optional
// drops the element, and one that holds a value stores that value. The node charges 1 + the number of elements stored, after its
// children and before the list is created.
internal sealed class OptionalListPlanNode : PlanNode
{
    private readonly ImmutableArray<PlanNode> _elements;

    // Whether each element has a ?.
    private readonly ImmutableArray<bool> _optional;

    internal OptionalListPlanNode(int offset, ImmutableArray<PlanNode> elements, ImmutableArray<bool> optional)
        : base(offset, 1)
    {
        _elements = elements;
        _optional = optional;
    }

    internal override CelValue Evaluate(Frame frame)
    {
        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        int stored = 0;
        for (int i = 0; i < _elements.Length; i++)
        {
            CelValue value = _elements[i].Evaluate(frame);
            if (value.Kind == CelValueKind.Error)
            {
                frame.Pop(stored);
                return value;
            }

            if (_optional[i])
            {
                if (!value.TryGetOptional(out CelOptional<CelValue> optional))
                {
                    frame.Pop(stored);
                    return EvaluationErrors.NotAnOptional(value, frame, _elements[i].Offset);
                }

                if (!optional.HasValue)
                {
                    continue;
                }

                value = optional.Value;
            }

            frame.Push(value);
            stored++;
        }

        Begin(frame);
        if (frame.Metered && stored != 0)
        {
            frame.Charge((ulong)stored);
        }

        CelValue[] items = stored == 0 ? [] : frame.Peek(stored).ToArray();
        frame.Pop(stored);
        return CelValue.FromList(new ValueArrayList(items));
    }
}
