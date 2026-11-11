using System.Collections.Generic;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A comprehension. The range first: an error value is the result; a list gives its elements in order and
// a map its keys in the order of the map; another value is EvalInvalidComprehensionRange at the range.
// Then the accumulator initializer goes into the accumulator slot, and for each element the iteration
// variable gets it, the loop condition is evaluated and the loop stops when it is false; otherwise the
// step counts an event, charges 1 and evaluates into the accumulator. The result is evaluated after the
// loop. There is no early exit on an error value. The accumulator of map and filter is a list builder,
// created for as many elements as the range has, and the result turns it into a list. A comprehension of two
// variables gives the first the index of a list element or the key of a map entry and the second the element or
// the value; the accumulator of transformMap and transformMapEntry is a map builder, which the result turns into
// a map.
internal sealed class ComprehensionPlanNode : PlanNode
{
    private readonly PlanNode _range;

    // The initializer; null for map, filter and the transformations, whose initializer, [] or {}, is the builder and costs 1.
    private readonly PlanNode? _initializer;
    private readonly PlanNode _condition;
    private readonly PlanNode _step;
    private readonly PlanNode _result;
    private readonly int _variable;

    // The slot of the second iteration variable; -1 for a comprehension of one variable.
    private readonly int _secondVariable;
    private readonly int _accumulator;

    // Whether the builder of an accumulator without an initializer is a map builder rather than a list builder.
    private readonly bool _buildsMap;

    internal ComprehensionPlanNode(
        int offset,
        PlanNode range,
        PlanNode? initializer,
        PlanNode condition,
        PlanNode step,
        PlanNode result,
        int variable,
        int accumulator,
        int secondVariable = -1,
        bool buildsMap = false)
        : base(offset, 1)
    {
        _range = range;
        _initializer = initializer;
        _condition = condition;
        _step = step;
        _result = result;
        _variable = variable;
        _accumulator = accumulator;
        _secondVariable = secondVariable;
        _buildsMap = buildsMap;
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
            RunList(frame, list);
        }
        else if (range.TryGetMap(out CelMap? map))
        {
            if (!TryRunMap(frame, map, out CelValue failure))
            {
                return failure;
            }
        }
        else
        {
            return EvaluationErrors.InvalidComprehensionRange(range, frame.Types, frame.Lines, _range.Offset);
        }

        return Finish(_result.Evaluate(frame));
    }

    // The elements in order; with two variables the first one gets the index.
    private void RunList(Frame frame, CelList list)
    {
        bool twoVariables = _secondVariable >= 0;
        for (int i = 0; i < list.Count; i++)
        {
            CelValue element = list.Read(i, frame.Meter);
            if (!Step(frame, twoVariables ? CelValue.FromInt64(i) : element, element))
            {
                break;
            }
        }
    }

    // The entries in the order of the map; false with the error that stops the loop before its first step.
    private bool TryRunMap(Frame frame, CelMap map, out CelValue failure)
    {
        failure = default;

        // A map that looks at all its keys for an error — a JSON object with a repeated name — fails at the range.
        // The check charges a JSON object by its properties, before each is processed.
        if (map.TryGetKeyError(frame.Meter, out CelValue keyError, leadingUnit: true))
        {
            failure = EvaluationErrors.Placed(keyError, frame.Lines, _range.Offset);
            return false;
        }

        // A view ordered by ascending key pays its sort before the first entry.
        if (frame.Metered)
        {
            map.ChargeEnumeration(frame.Meter!);
        }

        Initialize(frame, map.Count);

        // Only the keys are read for one variable: a map over a JSON object creates no value then.
        using IEnumerator<KeyValuePair<CelValue, CelValue>> entries = map.EnumerateEntries(frame.Meter, readValues: _secondVariable >= 0);
        while (entries.MoveNext())
        {
            if (!Step(frame, entries.Current.Key, entries.Current.Value))
            {
                break;
            }
        }

        return true;
    }

    // The result of the loop; the builder of an accumulator without an initializer becomes the list or the map.
    private CelValue Finish(CelValue result)
    {
        if (_initializer is not null)
        {
            return result;
        }

        if (_buildsMap)
        {
            return result.TryGetMap(out CelMap? built) && built is MapBuilder mapBuilder ? CelValue.FromMap(mapBuilder.ToMap()) : result;
        }

        return result.TryGetList(out CelList? builtList) && builtList is ListBuilder builder ? CelValue.FromList(builder.ToList()) : result;
    }

    private void Initialize(Frame frame, int count)
    {
        if (_initializer is not null)
        {
            frame.Local(_accumulator) = _initializer.Evaluate(frame);
            return;
        }

        // The literal [] or {} that the expansion writes: 1 unit.
        frame.Offset = Offset;
        if (frame.Metered)
        {
            frame.Charge(1);
        }

        frame.Local(_accumulator) = _buildsMap ? CelValue.FromMap(new MapBuilder()) : CelValue.FromList(new ListBuilder(count));
    }

    // One element: false when the loop condition stops the loop. The second variable, when there is one, gets the second value.
    private bool Step(Frame frame, CelValue first, CelValue second)
    {
        frame.Local(_variable) = first;
        if (_secondVariable >= 0)
        {
            frame.Local(_secondVariable) = second;
        }

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
