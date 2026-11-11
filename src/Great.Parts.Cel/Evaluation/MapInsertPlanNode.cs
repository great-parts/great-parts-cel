using System;
using System.Collections.Generic;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// The step cel.@mapInsert(@result, key, value) or cel.@mapInsert(@result, map) of transformMap and transformMapEntry: puts the
// pair, or the pairs of the map in their order, into the map builder of the accumulator slot, without the copy of the map that
// the generic implementation of the overload makes. It evaluates the accumulator, then the key, then the value or the map, and is
// strict like a call: an accumulator that is an error value stays, and an error value of the key or of the value becomes the
// accumulator. A key of a kind that cannot be a map key is UnsupportedMapKeyType; each pair then costs 2 and ⌈L/10⌉ more for a
// string key of length L, charged before it is put in, and a key equal to one that is in is DuplicateMapKey. All at the call.
// An operand that is no map for the second overload is EvalNoMatchingOverload; type checking has refused it before. The node has
// no cost of its own: the pairs are charged as they are put in, so a cost limit stops the growth of the map.
internal sealed class MapInsertPlanNode : PlanNode
{
    // The function of the call, the name that the macros of two variables expand to.
    internal const string Function = ExtensionMacros.MapInsertFunction;

    private readonly int _accumulator;

    // The key of the overload with a key and a value; null for the overload with a map.
    private readonly PlanNode? _key;

    // The value, or the map whose pairs are put in.
    private readonly PlanNode _operand;

    internal MapInsertPlanNode(int offset, int accumulator, PlanNode? key, PlanNode operand)
        : base(offset, 0)
    {
        _accumulator = accumulator;
        _key = key;
        _operand = operand;
    }

    internal override CelValue Evaluate(Frame frame)
    {
        CelValue accumulator = frame.Local(_accumulator);
        if (accumulator.Kind == CelValueKind.Error)
        {
            return accumulator;
        }

        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        CelValue key = default;
        if (_key is not null)
        {
            key = _key.Evaluate(frame);
            if (key.Kind == CelValueKind.Error)
            {
                return key;
            }
        }

        CelValue operand = _operand.Evaluate(frame);
        if (operand.Kind == CelValueKind.Error)
        {
            return operand;
        }

        Begin(frame);
        _ = accumulator.TryGetMap(out CelMap? map);
        var builder = (MapBuilder)map!;
        return _key is null ? InsertPairsOf(frame, builder, accumulator, operand) : InsertPair(frame, builder, key, operand) ?? accumulator;
    }

    // The units of one pair: 2, and ⌈L/10⌉ for a string key of length L.
    private static ulong PairUnits(CelValue key) => key.TryGetString(out string? text) ? 2 + (((ulong)text.Length + 9) / 10) : 2;

    // The error of putting a pair in, or null when it is in.
    private CelValue? InsertPair(Frame frame, MapBuilder builder, CelValue key, CelValue value)
    {
        if (!KeyHashRules.IsKeyKind(key.Kind))
        {
            return EvaluationErrors.UnsupportedMapKeyType(key, frame.Types, frame.Lines, Offset);
        }

        if (frame.Metered)
        {
            frame.Charge(PairUnits(key));
        }

        return builder.TryAdd(key, value) ? null : EvaluationErrors.DuplicateMapKey(key, frame.Lines, Offset);
    }

    // The overload with a map: its pairs in the order of its enumeration, each charged and put in as one pair is.
    private CelValue InsertPairsOf(Frame frame, MapBuilder builder, CelValue accumulator, CelValue operand)
    {
        if (!operand.TryGetMap(out CelMap? source))
        {
            ReadOnlySpan<CelValue> arguments = [accumulator, operand];
            return new CallSite(Function, Offset, frame.Lines, types: frame.Types).NoMatchingOverload(arguments);
        }

        // As the range of a comprehension: a map with an error among its keys fails at once, and a view that is sorted pays for
        // the sort before its first pair.
        if (source.TryGetKeyError(frame.Meter, out CelValue keyError, leadingUnit: true))
        {
            return EvaluationErrors.Placed(keyError, frame.Lines, Offset);
        }

        if (frame.Metered)
        {
            source.ChargeEnumeration(frame.Meter!);
        }

        using IEnumerator<KeyValuePair<CelValue, CelValue>> entries = source.EnumerateEntries(frame.Meter, readValues: true);
        while (entries.MoveNext())
        {
            KeyValuePair<CelValue, CelValue> entry = entries.Current;
            if (entry.Value.Kind == CelValueKind.Error)
            {
                return EvaluationErrors.Placed(entry.Value, frame.Lines, Offset);
            }

            if (InsertPair(frame, builder, entry.Key, entry.Value) is { } failure)
            {
                return failure;
            }
        }

        return accumulator;
    }
}
