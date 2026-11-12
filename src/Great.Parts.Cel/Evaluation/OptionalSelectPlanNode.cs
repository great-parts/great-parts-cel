using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// An optional selection e.?f, the call _?._(e, 'f'). The operand comes first, and an error value is the result. The operand
// need not be an optional: a map, a JSON object or an object value is read as the held value of an optional would be, and an
// optional is read through its held value, the empty one giving the empty optional. The result is always an optional:
// the value of the key or of the field when it is there, else the empty optional. A held value that is neither a map nor an
// object — null and an optional included — is FieldSelectionNotSupported, a name that the description of an object does not have
// is EvalNoSuchField. Costs 1, and on a map ⌈L/10⌉ more for a key of length L; on a JSON object n · (1 + ⌈L/10⌉) more for n
// properties; an object field charges what FieldPlanNode charges for it.
internal sealed class OptionalSelectPlanNode : PlanNode
{
    private readonly PlanNode _operand;
    private readonly string _field;
    private readonly ulong _keyCost;

    internal OptionalSelectPlanNode(int offset, PlanNode operand, string field)
        : base(offset, 1)
    {
        _operand = operand;
        _field = field;
        _keyCost = KeyCost(field);
    }

    // ⌈L/10⌉ for a key of length L.
    internal static ulong KeyCost(string field) => ((ulong)field.Length + 9) / 10;

    internal override CelValue Evaluate(Frame frame)
    {
        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        CelValue operand = _operand.Evaluate(frame);
        if (operand.Kind == CelValueKind.Error)
        {
            return operand;
        }

        Begin(frame);
        CelValue held = operand;
        if (operand.TryGetOptional(out CelOptional<CelValue> optional))
        {
            if (!optional.HasValue)
            {
                return CelValue.FromOptional(default);
            }

            held = optional.Value;
        }

        return Read(frame, held, _field, _keyCost, isPresenceTest: false, Offset);
    }

    // The selection of a field on a held value, which is neither an error value nor the empty case that the caller handles, after the
    // unit of the node. A map or an object gives the optional of the value of the field, or the empty optional when it is not there;
    // a presence test gives whether it is. A value of another kind has no fields.
    internal static CelValue Read(Frame frame, CelValue held, string field, ulong keyCost, bool isPresenceTest, int offset)
    {
        if (held.Kind == CelValueKind.Object)
        {
            return ReadObject(frame, held, field, isPresenceTest, offset);
        }

        if (!held.TryGetMap(out CelMap? map))
        {
            return EvaluationErrors.FieldSelectionNotSupported(held, field, frame.Lines, offset);
        }

        if (frame.Metered)
        {
            frame.Charge(map.ProbeUnits(keyCost));
        }

        bool found = map.TryRead(CelValue.FromString(field), frame.Meter, readValue: !isPresenceTest, out CelValue value);
        if (isPresenceTest)
        {
            // The lookup of a key that a JSON object repeats is an error; any other value is not read by the test.
            return found && value.TryGetError(out CelError? repeated) && repeated.Code == CelErrorCode.DuplicateMapKey
                ? EvaluationErrors.Placed(value, frame.Lines, offset)
                : CelValue.FromBoolean(found);
        }

        return found ? OptionalOf(EvaluationErrors.Placed(value, frame.Lines, offset)) : CelValue.FromOptional(default);
    }

    // The field of an object value by its name in the description of the value. Not set is the empty optional; a presence test is
    // HasField and costs what the test of FieldPlanNode costs.
    private static CelValue ReadObject(Frame frame, CelValue held, string field, bool isPresenceTest, int offset)
    {
        if (!ObjectRules.TryDescribe(held, frame.Types, out CelObjectType? description, out CelValue notAccessible))
        {
            return EvaluationErrors.Placed(notAccessible, frame.Lines, offset);
        }

        if (!description!.TryGetField(field, out CelObjectField? declared))
        {
            return EvaluationErrors.NoSuchField(field, description, frame.Lines, offset);
        }

        _ = held.TryGetObject(out object? instance);
        if (isPresenceTest)
        {
            return FieldPlanNode.Read(frame, description, instance!, declared.Number, isPresenceTest: true, offset);
        }

        // The units of the read are charged before the field is looked at, once; the test and the read are one event.
        CelValue value;
        try
        {
            if (frame.Metered)
            {
                long units = description.FieldReadCost(instance!, declared.Number);
                if (units > 0)
                {
                    frame.Charge((ulong)units);
                }
            }

            frame.Event();
            if (!description.HasField(instance!, declared.Number))
            {
                return CelValue.FromOptional(default);
            }

            value = ObjectRules.ReadField(description, instance!, declared.Number, frame.Types);
        }
        catch (Exception exception) when (frame.IsApplicationException(exception))
        {
            return EvaluationErrors.FieldException(exception, description, declared.Number, false, frame.Lines, offset);
        }

        return OptionalOf(EvaluationErrors.Placed(value, frame.Lines, offset));
    }

    // optional.of(value) for a value, and the error value itself for an error value, which no optional holds.
    private static CelValue OptionalOf(CelValue value) =>
        value.Kind == CelValueKind.Error ? value : CelValue.FromOptional(new CelOptional<CelValue>(value));
}
