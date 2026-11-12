using System;
using System.Collections;
using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// The equality of two messages of one description by CEL equality: the fields in the order of their numbers, the registered
/// extensions after the others. A field present in one message and absent from the other makes them unequal; values of present
/// fields are compared by what reading gives, without making the values. A NaN is equal to nothing. Unknown fields are not compared.
/// Nested messages wait on a stack of the comparison itself, so the depth of the messages is bounded by memory.
/// </summary>
internal static class MessageEquality
{
    /// <summary>
    /// The cost of comparing two messages: 1 and one for every ten of the sizes of both in bytes of the wire format.
    /// </summary>
    /// <param name="left">The first message.</param>
    /// <param name="right">The second message.</param>
    /// <returns>The units.</returns>
    internal static long Cost(IMessage left, IMessage right) => 1 + (((long)left.CalculateSize()) + right.CalculateSize() + 9) / 10;

    /// <summary>
    /// Compares two messages.
    /// </summary>
    /// <param name="type">The description of both messages.</param>
    /// <param name="left">The first message.</param>
    /// <param name="right">The second message.</param>
    /// <returns><see langword="true"/> when the messages are equal.</returns>
    internal static bool Equal(MessageType type, IMessage left, IMessage right)
    {
        var pending = new Stack<Pair>();
        pending.Push(new Pair(type, left, right));
        while (pending.Count > 0)
        {
            Pair pair = pending.Pop();
            if (!EqualFields(pair.Type, pair.Left, pair.Right, pending))
            {
                return false;
            }
        }

        return true;
    }

    // The fields of one pair of messages; a pair of nested messages goes on the stack.
    private static bool EqualFields(MessageType type, IMessage left, IMessage right, Stack<Pair> pending)
    {
        FieldPlan[] plans = type.Plans;
        for (int number = 0; number < plans.Length; number++)
        {
            FieldPlan plan = plans[number];
            bool present = plan.Has(left);
            if (present != plan.Has(right))
            {
                return false;
            }

            if (present && !EqualField(type, number, plan, left, right, pending))
            {
                return false;
            }
        }

        return true;
    }

    // A field that is present in both messages: a single value, the elements of a list pairwise, the entries of a map by key.
    private static bool EqualField(MessageType type, int number, FieldPlan plan, IMessage left, IMessage right, Stack<Pair> pending)
    {
        object? leftRaw = plan.ReadRaw(left);
        object? rightRaw = plan.ReadRaw(right);
        switch (plan.Container)
        {
            case FieldContainer.Repeated:
                return EqualLists((IList)leftRaw!, (IList)rightRaw!, type, number, plan, pending);
            case FieldContainer.Map:
                return EqualMaps((IDictionary)leftRaw!, (IDictionary)rightRaw!, type, number, plan, pending);
            default:
                return EqualValues(plan, leftRaw, rightRaw, type, number, pending);
        }
    }

    private static bool EqualLists(IList left, IList right, MessageType type, int number, FieldPlan plan, Stack<Pair> pending)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            if (!EqualValues(plan, left[index], right[index], type, number, pending))
            {
                return false;
            }
        }

        return true;
    }

    private static bool EqualMaps(IDictionary left, IDictionary right, MessageType type, int number, FieldPlan plan, Stack<Pair> pending)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        // The non-generic indexer of a map field gives the default of the value type for an absent key, so presence is asked first.
        IDictionaryEnumerator entries = left.GetEnumerator();
        while (entries.MoveNext())
        {
            if (!right.Contains(entries.Key) || !EqualValues(plan, entries.Value, right[entries.Key], type, number, pending))
            {
                return false;
            }
        }

        return true;
    }

    // Two values of the kind of the field.
    private static bool EqualValues(FieldPlan plan, object? left, object? right, MessageType type, int number, Stack<Pair> pending)
    {
        ValueShape shape = plan.Value;
        switch (shape.Kind)
        {
            case FieldKind.Message:
                pending.Push(new Pair(plan.Nested!, (IMessage)left!, (IMessage)right!));
                return true;
            case FieldKind.Wrapper:
                return EqualScalars(shape.Inner, left, right);
            case FieldKind.Timestamp:
                var leftTimestamp = (Timestamp)left!;
                var rightTimestamp = (Timestamp)right!;
                return leftTimestamp.Seconds == rightTimestamp.Seconds && leftTimestamp.Nanos == rightTimestamp.Nanos;
            case FieldKind.Duration:
                var leftDuration = (Duration)left!;
                var rightDuration = (Duration)right!;
                return leftDuration.Seconds == rightDuration.Seconds && leftDuration.Nanos == rightDuration.Nanos;
            case FieldKind.Value:
                return JsonEqual(JsonViews.FromValue((Value)left!), JsonViews.FromValue((Value)right!));
            case FieldKind.Struct:
                return JsonEqual(CelValue.FromMap(JsonViews.StructView((Struct)left!)), CelValue.FromMap(JsonViews.StructView((Struct)right!)));
            case FieldKind.ListValue:
                return JsonEqual(CelValue.FromList(JsonViews.ListView((ListValue)left!)), CelValue.FromList(JsonViews.ListView((ListValue)right!)));
            case FieldKind.Any:
                return EqualAnys((Any)left!, (Any)right!, type, number, pending);
            default:
                return EqualScalars(shape.Kind, left, right);
        }
    }

    // The primitive kinds: numbers with ==, so a NaN is equal to nothing, strings ordinal, bytes by content.
    private static bool EqualScalars(FieldKind kind, object? left, object? right)
    {
        return kind switch
        {
            FieldKind.Int32 or FieldKind.Enum => (int)left! == (int)right!,
            FieldKind.Int64 => (long)left! == (long)right!,
            FieldKind.UInt32 => (uint)left! == (uint)right!,
            FieldKind.UInt64 => (ulong)left! == (ulong)right!,
            FieldKind.Float => (float)left! == (float)right!,
            FieldKind.Double => (double)left! == (double)right!,
            FieldKind.Bool => (bool)left! == (bool)right!,
            FieldKind.String => string.Equals((string?)left, (string?)right, StringComparison.Ordinal),
            _ => ((ByteString)left!).Equals((ByteString)right!),
        };
    }

    // The JSON-like values by CEL equality: null equals null, numbers by ==, maps with the same keys and equal values, lists pairwise.
    private static bool JsonEqual(CelValue left, CelValue right) =>
        CelValue.CelEquals(left, right).TryGetBoolean(out bool equal) && equal;

    // Both Any messages are unpacked, without a charge, and the two results are compared by CEL equality; when either does not unpack,
    // they are equal when their addresses are equal and their contents are equal byte for byte. The equality never fails.
    private static bool EqualAnys(Any left, Any right, MessageType type, int number, Stack<Pair> pending)
    {
        ProviderTypes registry = type.Registry;
        if (AnyUnpacker.TryUnpack(registry, left, out IMessage? leftContent, out _, out _, out _)
            && AnyUnpacker.TryUnpack(registry, right, out IMessage? rightContent, out _, out _, out _))
        {
            CelValue leftValue = KnownValues.FromMessage(leftContent, type, number);
            CelValue rightValue = KnownValues.FromMessage(rightContent, type, number);
            if (leftValue.Kind != CelValueKind.Error && rightValue.Kind != CelValueKind.Error)
            {
                return EqualUnpacked(leftContent, rightContent, leftValue, rightValue, registry, pending);
            }
        }

        return string.Equals(left.TypeUrl, right.TypeUrl, StringComparison.Ordinal) && left.Value.Equals(right.Value);
    }

    // Two unpacked contents: two messages of the provider by this algorithm, anything else by CEL equality of the values they read as.
    private static bool EqualUnpacked(IMessage leftContent, IMessage rightContent, CelValue leftValue, CelValue rightValue, ProviderTypes registry, Stack<Pair> pending)
    {
        if (leftValue.Kind == CelValueKind.Object || rightValue.Kind == CelValueKind.Object)
        {
            if (leftValue.Kind != rightValue.Kind
                || !ReferenceEquals(leftContent.Descriptor, rightContent.Descriptor)
                || !registry.TryGetType(leftContent.Descriptor, out MessageType? described))
            {
                return false;
            }

            pending.Push(new Pair(described, leftContent, rightContent));
            return true;
        }

        return JsonEqual(leftValue, rightValue);
    }

    private readonly record struct Pair(MessageType Type, IMessage Left, IMessage Right);
}
