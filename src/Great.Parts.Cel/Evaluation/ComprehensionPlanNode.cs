using System.Collections.Generic;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A comprehension. The range first: an error value is the result; a list gives its elements in order and
// a map its keys in the order of the map; another value is EvalInvalidComprehensionRange at the range.
// Then the accumulator initializer goes into the accumulator slot, and for each element the iteration
// variable gets it, the loop condition is evaluated and the loop stops when it is false; otherwise the
// step counts an event, charges 1 and evaluates into the accumulator. The result is evaluated after the
// loop. There is no early exit on an error value. The accumulator of map and filter is a list builder,
// created for as many elements as the range has, and the result turns it into a list.
internal sealed class ComprehensionPlanNode : PlanNode
{
    private readonly PlanNode _range;

    // The initializer; null for map and filter, whose initializer [] is the list builder and costs 1.
    private readonly PlanNode? _initializer;
    private readonly PlanNode _condition;
    private readonly PlanNode _step;
    private readonly PlanNode _result;
    private readonly int _variable;
    private readonly int _accumulator;

    internal ComprehensionPlanNode(
        int offset,
        PlanNode range,
        PlanNode? initializer,
        PlanNode condition,
        PlanNode step,
        PlanNode result,
        int variable,
        int accumulator)
        : base(offset, 1)
    {
        _range = range;
        _initializer = initializer;
        _condition = condition;
        _step = step;
        _result = result;
        _variable = variable;
        _accumulator = accumulator;
    }

    internal override CelValue Evaluate(Frame frame)
    {
        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        CelValue range = _range.Evaluate(frame);
        if (range.Kind == CelValueKind.Error)
        {
            return range;
        }

        if (range.TryGetList(out CelList? list))
        {
            Initialize(frame, list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                if (!Step(frame, list.Read(i, frame.Meter)))
                {
                    break;
                }
            }
        }
        else if (range.TryGetMap(out CelMap? map))
        {
            // A map that looks at all its keys for an error — a JSON object with a repeated name — fails at the range.
            // The check charges a JSON object by its properties, before each is processed.
            if (map.TryGetKeyError(frame.Meter, out CelValue keyError, leadingUnit: true))
            {
                return EvaluationErrors.Placed(keyError, frame.Lines, _range.Offset);
            }

            // A view ordered by ascending key pays its sort before the first entry.
            if (frame.Metered)
            {
                map.ChargeEnumeration(frame.Meter!);
            }

            Initialize(frame, map.Count);
            // Only the keys are read: a map over a JSON object creates no value.
            using IEnumerator<KeyValuePair<CelValue, CelValue>> entries = map.EnumerateEntries(frame.Meter, readValues: false);
            while (entries.MoveNext())
            {
                if (!Step(frame, entries.Current.Key))
                {
                    break;
                }
            }
        }
        else
        {
            return EvaluationErrors.InvalidComprehensionRange(range, frame.Types, frame.Lines, _range.Offset);
        }

        CelValue result = _result.Evaluate(frame);
        return _initializer is null && result.TryGetList(out CelList? built) && built is ListBuilder builder
            ? CelValue.FromList(builder.ToList())
            : result;
    }

    private void Initialize(Frame frame, int count)
    {
        if (_initializer is not null)
        {
            frame.Local(_accumulator) = _initializer.Evaluate(frame);
            return;
        }

        // The literal [] that the expansion writes: 1 unit.
        frame.Offset = Offset;
        if (frame.Metered)
        {
            frame.Charge(1);
        }

        frame.Local(_accumulator) = CelValue.FromList(new ListBuilder(count));
    }

    // One element: false when the loop condition stops the loop.
    private bool Step(Frame frame, CelValue element)
    {
        frame.Local(_variable) = element;
        CelValue condition = _condition.Evaluate(frame);
        if (condition.TryGetBoolean(out bool proceed) && !proceed)
        {
            return false;
        }

        frame.Event();
        Begin(frame);
        frame.Local(_accumulator) = _step.Evaluate(frame);
        return true;
    }
}
