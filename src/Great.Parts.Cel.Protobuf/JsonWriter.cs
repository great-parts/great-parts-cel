using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// Writes CEL values as the JSON-like messages <c>Value</c>, <c>Struct</c> and <c>ListValue</c>.
/// </summary>
internal static class JsonWriter
{
    // The integers that a double holds exactly, the number of a Value: from -(2^53 - 1) to 2^53 - 1.
    private const long MaxExactInteger = 9_007_199_254_740_991;

    /// <summary>
    /// Writes a value as a <c>Value</c>. An integer outside the exact range of a <c>double</c> travels as its decimal text, a
    /// <c>bytes</c> value as standard Base64, a time as the text of <c>string()</c>, and an object as the canonical JSON of its message.
    /// </summary>
    /// <param name="value">The value, not an error value.</param>
    /// <param name="owner">The description that errors name.</param>
    /// <param name="number">The number of the field that errors name.</param>
    /// <param name="result">The message when the method returns <see langword="true"/>.</param>
    /// <param name="error">The error value when the method returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the value can be written.</returns>
    internal static bool TryValue(CelValue value, MessageType owner, int number, [NotNullWhen(true)] out Value? result, out CelValue error)
    {
        error = default;
        result = null;
        switch (value.Kind)
        {
            case CelValueKind.Error:
                error = value;
                return false;
            case CelValueKind.Null:
                result = Value.ForNull();
                return true;
            case CelValueKind.List:
                _ = value.TryGetList(out CelList? list);
                if (!TryList(list!, owner, number, out ListValue? listValue, out error))
                {
                    return false;
                }

                result = new Value { ListValue = listValue };
                return true;
            case CelValueKind.Map:
                _ = value.TryGetMap(out CelMap? map);
                if (!TryStruct(map!, owner, number, out Struct? structure, out error))
                {
                    return false;
                }

                result = new Value { StructValue = structure };
                return true;
            case CelValueKind.Object:
                _ = value.TryGetObject(out object? instance);
                if (instance is IMessage message)
                {
                    return TryMessage(message, owner, number, out result, out error);
                }

                error = FieldErrors.Mismatch(owner, number, value);
                return false;
            default:
                result = Scalar(value);
                if (result is not null)
                {
                    return true;
                }

                error = FieldErrors.Mismatch(owner, number, value);
                return false;
        }
    }

    /// <summary>
    /// Writes a map as a <c>Struct</c>, in the order of the map.
    /// </summary>
    /// <param name="map">The map.</param>
    /// <param name="owner">The description that errors name.</param>
    /// <param name="number">The number of the field that errors name.</param>
    /// <param name="result">The message when the method returns <see langword="true"/>.</param>
    /// <param name="error">The error value when the method returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the map can be written; a key that is not a string and a value nested too deeply cannot.</returns>
    internal static bool TryStruct(CelMap map, MessageType owner, int number, [NotNullWhen(true)] out Struct? result, out CelValue error)
    {
        result = null;
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            error = TooDeep(owner, number);
            return false;
        }

        var written = new Struct();
        foreach (KeyValuePair<CelValue, CelValue> entry in map)
        {
            if (!entry.Key.TryGetString(out string? key))
            {
                error = FieldErrors.InvalidArgument(owner, number, "a key of the type " + ValueNames.Of(entry.Key) + " is not a string");
                return false;
            }

            if (!TryValue(entry.Value, owner, number, out Value? element, out error))
            {
                return false;
            }

            written.Fields[key] = element;
        }

        result = written;
        error = default;
        return true;
    }

    /// <summary>
    /// Writes a list as a <c>ListValue</c>.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="owner">The description that errors name.</param>
    /// <param name="number">The number of the field that errors name.</param>
    /// <param name="result">The message when the method returns <see langword="true"/>.</param>
    /// <param name="error">The error value when the method returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the list can be written.</returns>
    internal static bool TryList(CelList list, MessageType owner, int number, [NotNullWhen(true)] out ListValue? result, out CelValue error)
    {
        result = null;
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            error = TooDeep(owner, number);
            return false;
        }

        var written = new ListValue();
        for (int index = 0; index < list.Count; index++)
        {
            if (!TryValue(list[index], owner, number, out Value? element, out error))
            {
                return false;
            }

            written.Values.Add(element);
        }

        result = written;
        error = default;
        return true;
    }

    // The canonical JSON of a message as a Value. The formatter throws when the message holds an Any whose type the registry does
    // not describe or whose content does not parse; the outcome depends only on the data, so it is an error value, with a fixed
    // text that does not carry the message of the exception, which changes with the version of Google.Protobuf.
    private static bool TryMessage(IMessage message, MessageType owner, int number, [NotNullWhen(true)] out Value? result, out CelValue error)
    {
        try
        {
            result = Value.Parser.ParseJson(owner.Registry.Json.Format(message));
            error = default;
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidProtocolBufferException)
        {
            result = null;
            error = FieldErrors.InvalidArgument(
                owner,
                number,
                "the message cannot be written as JSON: it holds a google.protobuf.Any of a type that the type source does not describe, or with content that does not parse");
            return false;
        }
    }

    private static CelValue TooDeep(MessageType owner, int number) =>
        FieldErrors.InvalidArgument(owner, number, "the value is nested too deeply");

    // A value of the kinds that are not collections or objects; null for a kind that a Value cannot hold.
    private static Value? Scalar(CelValue value)
    {
        switch (value.Kind)
        {
            case CelValueKind.Bool:
                _ = value.TryGetBoolean(out bool boolean);
                return Value.ForBool(boolean);
            case CelValueKind.Int:
                _ = value.TryGetInt64(out long integer);
                return integer is >= -MaxExactInteger and <= MaxExactInteger
                    ? Value.ForNumber(integer)
                    : Value.ForString(integer.ToString(CultureInfo.InvariantCulture));
            case CelValueKind.UInt:
                _ = value.TryGetUInt64(out ulong unsigned);
                return unsigned <= MaxExactInteger
                    ? Value.ForNumber(unsigned)
                    : Value.ForString(unsigned.ToString(CultureInfo.InvariantCulture));
            case CelValueKind.Double:
                _ = value.TryGetDouble(out double number);
                return Value.ForNumber(number);
            case CelValueKind.String:
                _ = value.TryGetString(out string? text);
                return Value.ForString(text!);
            case CelValueKind.Bytes:
                _ = value.TryGetBytes(out ReadOnlyMemory<byte> bytes);
                return Value.ForString(Convert.ToBase64String(bytes.Span));
            case CelValueKind.Timestamp:
                _ = value.TryGetTimestamp(out CelTimestamp timestamp);
                return Value.ForString(timestamp.ToString());
            case CelValueKind.Duration:
                _ = value.TryGetDuration(out CelDuration duration);
                return Value.ForString(duration.ToString());
            default:
                return null;
        }
    }
}
