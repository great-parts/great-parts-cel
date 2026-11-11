using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library.Extensions.Strings;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions;

// json.encode of the extension set encoders: the JSON text of a value as cel-go writes it, that is, as protobuf converts the value to
// a JSON value and the compact encoder of Go writes it, with its escaping of HTML characters. The text goes to a ResultBuilder, which
// charges as it grows, so the walk of the value is inside the charge: every node gives at least one character. A failure is an error
// value of at most 64 bytes whose detail is formed when it is read.
internal static class JsonEncoder
{
    // The largest integer that a double holds exactly, 2^53 - 1: an int or uint beyond it is written as a string.
    private const long SafeInteger = 9_007_199_254_740_991;

    // 256 bytes of stack at most for the text of a number.
    private const int NumberLength = 40;

    // The line and the paragraph separator, which Go escapes so that the text is valid in JavaScript.
    private const char LineSeparator = (char)0x2028;
    private const char ParagraphSeparator = (char)0x2029;

    private const string StackMessage = "insufficient execution stack to continue encoding";

    // invalid argument to 'json.encode': detail. The function name is the first part, the kind of a key that is not a string the number.
    private static readonly Func<CelError, string> ErrorText = static error =>
        "invalid argument to '" + (string)error.First! + "': " + Detail(error);

    private enum JsonFailure : byte
    {
        NotFinite,
        KeyNotString,
        Unsupported,
    }

    // json_encode_dyn, json.encode(dyn) -> string.
    internal static CelValue Encode(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        var builder = new ResultBuilder(site.Meter);
        try
        {
            return TryAppend(ref builder, arguments[0], in site, out CelValue error) ? CelValue.FromString(builder.ToResultString()) : error;
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static bool TryAppend(ref ResultBuilder builder, CelValue value, in CallSite site, out CelValue error)
    {
        error = default;
        switch (value.Kind)
        {
            case CelValueKind.Null:
                builder.Append("null");
                return true;
            case CelValueKind.Bool:
                _ = value.TryGetBoolean(out bool flag);
                builder.Append(flag ? "true" : "false");
                return true;
            case CelValueKind.Int:
            case CelValueKind.UInt:
            case CelValueKind.Double:
                return TryAppendNumber(ref builder, value, in site, out error);
            case CelValueKind.String:
                _ = value.TryGetString(out string? text);
                AppendQuoted(ref builder, text);
                return true;
            case CelValueKind.Bytes:
                _ = value.TryGetBytes(out ReadOnlyMemory<byte> bytes);
                builder.Append("\"");
                Base64Codec.Append(ref builder, bytes.Span);
                builder.Append("\"");
                return true;
            case CelValueKind.Timestamp:
            case CelValueKind.Duration:
                AppendTime(ref builder, value);
                return true;
            case CelValueKind.List:
                _ = value.TryGetList(out CelList? list);
                return TryAppendList(ref builder, list!, in site, out error);
            case CelValueKind.Map:
                _ = value.TryGetMap(out CelMap? map);
                return TryAppendMap(ref builder, map!, in site, out error);
            case CelValueKind.Error:
                error = site.Placed(value);
                return false;
            default:
                error = Failure(JsonFailure.Unsupported, 0, in site);
                return false;
        }
    }

    // An int or a uint, a double that has a text, or the error of a NaN and an infinity.
    private static bool TryAppendNumber(ref ResultBuilder builder, CelValue value, in CallSite site, out CelValue error)
    {
        error = default;
        if (value.TryGetInt64(out long integer))
        {
            AppendInteger(ref builder, integer, integer is >= -SafeInteger and <= SafeInteger);
        }
        else if (value.TryGetUInt64(out ulong unsigned))
        {
            AppendUnsigned(ref builder, unsigned);
        }
        else
        {
            _ = value.TryGetDouble(out double real);
            if (!double.IsFinite(real))
            {
                error = Failure(JsonFailure.NotFinite, 0, in site);
                return false;
            }

            AppendDouble(ref builder, real);
        }

        return true;
    }

    // A timestamp or a duration: the text of string() in double quotes.
    private static void AppendTime(ref ResultBuilder builder, CelValue value)
    {
        if (value.TryGetTimestamp(out CelTimestamp timestamp))
        {
            AppendTimestamp(ref builder, timestamp);
        }
        else if (value.TryGetDuration(out CelDuration duration))
        {
            AppendDuration(ref builder, duration);
        }
    }

    private static string Detail(CelError error) => (JsonFailure)error.Small switch
    {
        JsonFailure.NotFinite => "a double that is NaN or infinite cannot be encoded",
        JsonFailure.KeyNotString => "unsupported type conversion from '" + ValueTypeNames.Of((CelValueKind)error.Number) + "' to string",
        _ => "unsupported type conversion",
    };

    private static CelValue Failure(JsonFailure failure, long number, in CallSite site) =>
        site.Error(CelErrorCode.InvalidArgument, ErrorText, site.Function, null, number, (byte)failure);

    private static void AppendInteger(ref ResultBuilder builder, long value, bool plain)
    {
        Span<char> text = stackalloc char[NumberLength];
        _ = value.TryFormat(text[1..], out int written, default, CultureInfo.InvariantCulture);
        Frame(ref builder, text, written, plain);
    }

    private static void AppendUnsigned(ref ResultBuilder builder, ulong value)
    {
        Span<char> text = stackalloc char[NumberLength];
        _ = value.TryFormat(text[1..], out int written, default, CultureInfo.InvariantCulture);
        Frame(ref builder, text, written, value <= SafeInteger);
    }

    // The digits are at the second place of the buffer; the first and the one after them are for the quotes of a number written as a
    // string.
    private static void Frame(ref ResultBuilder builder, scoped Span<char> text, int written, bool plain)
    {
        if (plain)
        {
            builder.Append(text.Slice(1, written));
            return;
        }

        text[0] = '"';
        text[written + 1] = '"';
        builder.Append(text[..(written + 2)]);
    }

    // As encoding/json writes a float64: the shortest digits that read back to the same number, in plain digits when the magnitude is
    // from 1e-6 up to but not including 1e21 and otherwise as d.ddde±XX, where the exponent e-07 becomes e-7. The zero is 0, or -0.
    private static void AppendDouble(ref ResultBuilder builder, double value)
    {
        Span<char> digits = stackalloc char[DoubleTextRules.BufferLength];
        int count = DoubleTextRules.ShortestDigits(value, digits, out int point);
        Span<char> text = stackalloc char[NumberLength];
        int written = 0;
        if (double.IsNegative(value))
        {
            text[written++] = '-';
        }

        double magnitude = Math.Abs(value);
        if (magnitude == 0)
        {
            text[written++] = '0';
        }
        else if (magnitude < 1e-6 || magnitude >= 1e21)
        {
            text[written++] = digits[0];
            if (count > 1)
            {
                text[written++] = '.';
                digits[1..count].CopyTo(text[written..]);
                written += count - 1;
            }

            int exponent = point - 1;
            text[written++] = 'e';
            text[written++] = exponent < 0 ? '-' : '+';
            _ = Math.Abs(exponent).TryFormat(text[written..], out int exponentLength, exponent < 0 ? "0" : "00", CultureInfo.InvariantCulture);
            written += exponentLength;
        }
        else if (point <= 0)
        {
            text[written++] = '0';
            text[written++] = '.';
            text.Slice(written, -point).Fill('0');
            written += -point;
            digits[..count].CopyTo(text[written..]);
            written += count;
        }
        else if (point >= count)
        {
            digits[..count].CopyTo(text[written..]);
            written += count;
            text.Slice(written, point - count).Fill('0');
            written += point - count;
        }
        else
        {
            digits[..point].CopyTo(text[written..]);
            written += point;
            text[written++] = '.';
            digits[point..count].CopyTo(text[written..]);
            written += count - point;
        }

        builder.Append(text[..written]);
    }

    // In double quotes: " and \ escaped; \n, \r, \t, \b and \f as these two characters; every other code unit below U+0020 as \u00xx;
    // <, > and & as their \u escapes; U+2028 and U+2029 as theirs; a lone surrogate as the escape of U+FFFD; U+007F and the rest
    // as they are. The runs that need no escape are appended whole.
    private static void AppendQuoted(ref ResultBuilder builder, ReadOnlySpan<char> text)
    {
        builder.Append("\"");
        int run = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char unit = text[i];
            if (char.IsHighSurrogate(unit) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
                continue;
            }

            if (unit >= ' ' && unit is not ('"' or '\\' or '<' or '>' or '&' or LineSeparator or ParagraphSeparator) && !char.IsSurrogate(unit))
            {
                continue;
            }

            builder.Append(text[run..i]);
            run = i + 1;
            AppendEscape(ref builder, unit);
        }

        builder.Append(text[run..]);
        builder.Append("\"");
    }

    private static void AppendEscape(ref ResultBuilder builder, char unit)
    {
        switch (unit)
        {
            case '"':
                builder.Append("\\\"");
                return;
            case '\\':
                builder.Append("\\\\");
                return;
            case '\n':
                builder.Append("\\n");
                return;
            case '\r':
                builder.Append("\\r");
                return;
            case '\t':
                builder.Append("\\t");
                return;
            case '\b':
                builder.Append("\\b");
                return;
            case '\f':
                builder.Append("\\f");
                return;
        }

        int code = char.IsSurrogate(unit) ? 0xFFFD : unit;
        Span<char> escape = stackalloc char[6];
        escape[0] = '\\';
        escape[1] = 'u';
        escape[2] = HexDigit(code >> 12);
        escape[3] = HexDigit(code >> 8);
        escape[4] = HexDigit(code >> 4);
        escape[5] = HexDigit(code);
        builder.Append(escape);
    }

    private static char HexDigit(int value) => "0123456789abcdef"[value & 0xF];

    private static void AppendTimestamp(ref ResultBuilder builder, CelTimestamp timestamp)
    {
        Span<char> text = stackalloc char[TimeTextRules.MaxTimestampLength + 2];
        text[0] = '"';
        int written = TimeTextRules.WriteTimestamp(timestamp, text[1..]);
        text[written + 1] = '"';
        builder.Append(text[..(written + 2)]);
    }

    private static void AppendDuration(ref ResultBuilder builder, CelDuration duration)
    {
        Span<char> text = stackalloc char[TimeTextRules.MaxDurationLength + 2];
        text[0] = '"';
        int written = TimeTextRules.WriteDuration(duration.TotalNanoseconds, text[1..]);
        text[written + 1] = '"';
        builder.Append(text[..(written + 2)]);
    }

    // [ the elements separated by commas ]
    private static bool TryAppendList(ref ResultBuilder builder, CelList list, in CallSite site, out CelValue error)
    {
        error = default;
        if (!System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            error = site.Error(CelErrorCode.InsufficientStack, StackMessage);
            return false;
        }

        builder.Append("[");
        int count = list.Count;
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                builder.Append(",");
            }

            if (!TryAppend(ref builder, list.Read(i, site.Meter), in site, out error))
            {
                return false;
            }
        }

        builder.Append("]");
        return true;
    }

    // { "key":value pairs separated by commas }, ordered by the key by code points. The pairs are written, and charged, in the order of
    // the map; the commas are charged ahead; the pairs are then written in order after them without a second charge and the first
    // texts are removed, so the units are those of the length of the result. A key that is not a string is an error.
    private static bool TryAppendMap(ref ResultBuilder builder, CelMap map, in CallSite site, out CelValue error)
    {
        error = default;
        if (!System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            error = site.Error(CelErrorCode.InsufficientStack, StackMessage);
            return false;
        }

        if (map.TryGetKeyError(site.Meter, out CelValue keyError))
        {
            error = site.Placed(keyError);
            return false;
        }

        int count = map.Count;
        if (count == 0)
        {
            builder.Append("{}");
            return true;
        }

        if (site.Meter is { } meter)
        {
            map.ChargeEnumeration(meter);
        }

        string[] keys = ArrayPool<string>.Shared.Rent(count);

        // The start and the length of each pair, then the order and a second array for the merges.
        int[] work = ArrayPool<int>.Shared.Rent(4 * count);
        try
        {
            return TryAppendPairs(ref builder, map, count, keys, work, in site, out error);
        }
        finally
        {
            ArrayPool<string>.Shared.Return(keys, clearArray: true);
            ArrayPool<int>.Shared.Return(work);
        }
    }

    private static bool TryAppendPairs(ref ResultBuilder builder, CelMap map, int count, string[] keys, int[] work, in CallSite site, out CelValue error)
    {
        error = default;
        builder.Append("{");
        int start = builder.Length;
        int index = 0;
        using (IEnumerator<KeyValuePair<CelValue, CelValue>> entries = map.EnumerateEntries(site.Meter, readValues: true))
        {
            while (index < count && entries.MoveNext())
            {
                if (!entries.Current.Key.TryGetString(out string? key))
                {
                    error = Failure(JsonFailure.KeyNotString, (long)entries.Current.Key.Kind, in site);
                    return false;
                }

                int pairStart = builder.Length;
                AppendQuoted(ref builder, key);
                builder.Append(":");
                if (!TryAppend(ref builder, entries.Current.Value, in site, out error))
                {
                    return false;
                }

                keys[index] = key;
                work[2 * index] = pairStart;
                work[(2 * index) + 1] = builder.Length - pairStart;
                index++;
            }
        }

        int textLength = builder.Length - start;
        builder.Charge(count - 1);
        Span<int> order = work.AsSpan(2 * count, count);
        for (int i = 0; i < count; i++)
        {
            order[i] = i;
        }

        IndexSort.Sort(order, work.AsSpan(3 * count, count), new KeyOrder(keys));
        Span<char> room = builder.WriteCharged(textLength + count - 1);
        WritePairs(builder.Written, room, work, order);
        builder.Remove(start, textLength);
        builder.Append("}");
        return true;
    }

    // The pairs in the order, separated by commas, into the room made after the pairs of the order of the map.
    private static void WritePairs(ReadOnlySpan<char> written, Span<char> room, int[] work, ReadOnlySpan<int> order)
    {
        int place = 0;
        for (int i = 0; i < order.Length; i++)
        {
            if (i > 0)
            {
                room[place++] = ',';
            }

            int pair = order[i];
            written.Slice(work[2 * pair], work[(2 * pair) + 1]).CopyTo(room[place..]);
            place += work[(2 * pair) + 1];
        }
    }

    // The order of the keys of a map by code points.
    private readonly struct KeyOrder(string[] keys) : IIndexOrder
    {
        public bool Less(int left, int right) => OrderingRules.CompareStrings(keys[left], keys[right]) == ValueOrdering.Less;
    }
}
