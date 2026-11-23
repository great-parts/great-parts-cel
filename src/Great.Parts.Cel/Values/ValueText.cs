using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

// The text of a value in CEL syntax (CelValue.ToString), for logs and messages: one writer for every kind, with a counter of the depth.
// A list, a map, an optional or an object nested deeper than MaxDepth levels is written as the ellipsis. The text does not depend on
// the culture, and it is not a contract: it is not meant to be parsed. The string and bytes forms are those of CelSyntax.
internal static class ValueText
{
    // The levels written in full; a nested value at this depth is the ellipsis.
    internal const int MaxDepth = 32;

    private const char Ellipsis = '…';

    // The builder of the last text of the thread, kept when it did not grow large, so that a log line does not allocate one.
    private const int KeptCapacity = 4096;

    [ThreadStatic]
    private static StringBuilder? t_kept;

    internal static string Of(CelValue value)
    {
        StringBuilder text = t_kept ?? new StringBuilder();
        t_kept = null;
        text.Clear();
        Write(text, value, 0, null);
        string result = text.ToString();
        if (text.Capacity <= KeptCapacity)
        {
            t_kept = text;
        }

        return result;
    }

    // A string in double quotes: the backslash and the quote escaped, \a \b \f \n \r \t \v for those controls, any other code point
    // below U+0020 and U+007F as \x and two upper-case hexadecimal digits, the rest as it is, a surrogate pair as one character. A
    // lone surrogate is \u and four digits: the text of a value shows it, while CelSyntax.QuoteString refuses it.
    internal static void AppendString(StringBuilder text, string value)
    {
        text.Append('"');
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '"':
                    text.Append("\\\"");
                    break;
                case '\\':
                    text.Append("\\\\");
                    break;
                default:
                    if (AppendControl(text, c))
                    {
                        break;
                    }

                    if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                    {
                        text.Append(c).Append(value[++i]);
                    }
                    else if (char.IsSurrogate(c))
                    {
                        text.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        text.Append(c);
                    }

                    break;
            }
        }

        text.Append('"');
    }

    // The escape of a control character; false for any other.
    private static bool AppendControl(StringBuilder text, char c)
    {
        switch (c)
        {
            case '\a':
                text.Append("\\a");
                return true;
            case '\b':
                text.Append("\\b");
                return true;
            case '\f':
                text.Append("\\f");
                return true;
            case '\n':
                text.Append("\\n");
                return true;
            case '\r':
                text.Append("\\r");
                return true;
            case '\t':
                text.Append("\\t");
                return true;
            case '\v':
                text.Append("\\v");
                return true;
            case < ' ' or '\u007F':
                AppendHex(text, c);
                return true;
            default:
                return false;
        }
    }

    private static void AppendHex(StringBuilder text, int code) =>
        text.Append("\\x").Append(code.ToString("X2", CultureInfo.InvariantCulture));

    // Bytes as b"…": printable ASCII other than the quote and the backslash as it is, the controls as in a string, other bytes as \x.
    private static void AppendBytes(StringBuilder text, ReadOnlySpan<byte> value)
    {
        text.Append("b\"");
        foreach (byte b in value)
        {
            if (b == '"')
            {
                text.Append("\\\"");
            }
            else if (b == '\\')
            {
                text.Append("\\\\");
            }
            else if (b >= 0x20 && b < 0x7F)
            {
                text.Append((char)b);
            }
            else if (b >= 0x7F)
            {
                AppendHex(text, b);
            }
            else
            {
                // Below U+0020: a control character, which has an escape of its own or \x.
                _ = AppendControl(text, (char)b);
            }
        }

        text.Append('"');
    }

    // The value at a depth; the declared type of its place, when the place has one, names an object that no description reaches.
    private static void Write(StringBuilder text, CelValue value, int depth, CelType? declared)
    {
        switch (value.Kind)
        {
            case CelValueKind.Bool:
                _ = value.TryGetBoolean(out bool flag);
                text.Append(flag ? "true" : "false");
                break;
            case CelValueKind.Int:
                _ = value.TryGetInt64(out long number);
                text.Append(number.ToString(CultureInfo.InvariantCulture));
                break;
            case CelValueKind.UInt:
                _ = value.TryGetUInt64(out ulong unsigned);
                text.Append(unsigned.ToString(CultureInfo.InvariantCulture)).Append('u');
                break;
            case CelValueKind.Double:
                _ = value.TryGetDouble(out double real);
                AppendDouble(text, real);
                break;
            case CelValueKind.String:
                _ = value.TryGetString(out string? s);
                AppendString(text, s!);
                break;
            case CelValueKind.Bytes:
                _ = value.TryGetBytes(out ReadOnlyMemory<byte> bytes);
                AppendBytes(text, bytes.Span);
                break;
            default:
                WriteOther(text, value, depth, declared);
                break;
        }
    }

    private static void WriteOther(StringBuilder text, CelValue value, int depth, CelType? declared)
    {
        switch (value.Kind)
        {
            case CelValueKind.List:
                _ = value.TryGetList(out CelList? list);
                WriteList(text, list!, depth, declared);
                break;
            case CelValueKind.Map:
                _ = value.TryGetMap(out CelMap? map);
                WriteMap(text, map!, depth, declared);
                break;
            case CelValueKind.Object:
                WriteObject(text, value, depth, declared);
                break;
            case CelValueKind.Optional:
                WriteOptional(text, value, depth);
                break;
            case CelValueKind.Timestamp:
                _ = value.TryGetTimestamp(out CelTimestamp timestamp);
                text.Append("timestamp(\"").Append(timestamp.ToString()).Append("\")");
                break;
            case CelValueKind.Duration:
                _ = value.TryGetDuration(out CelDuration duration);
                text.Append("duration(\"").Append(duration.ToString()).Append("\")");
                break;
            case CelValueKind.Date:
                _ = value.TryGetDate(out DateOnly date);
                Span<char> dateText = stackalloc char[CalendarTextRules.DateLength];
                text.Append("date(\"").Append(dateText[..CalendarTextRules.WriteDate(date, dateText)]).Append("\")");
                break;
            case CelValueKind.TimeOfDay:
                _ = value.TryGetTimeOfDay(out TimeOnly time);
                Span<char> timeText = stackalloc char[CalendarTextRules.MaxTimeOfDayLength];
                text.Append("time_of_day(\"").Append(timeText[..CalendarTextRules.WriteTimeOfDay(time, timeText)]).Append("\")");
                break;
            case CelValueKind.Type:
                _ = value.TryGetType(out CelType? type);
                text.Append(type!.ToString());
                break;
            case CelValueKind.Error:
                _ = value.TryGetError(out CelError? error);
                text.Append("error(").Append(error!.Code.ToString()).Append(": ").Append(error.Message).Append(')');
                break;
            default:
                WriteRest(text, value);
                break;
        }
    }

    // The null, an abstract value and a decimal. An abstract value is written as its type name: its content is the business of its set.
    private static void WriteRest(StringBuilder text, CelValue value)
    {
        if (value.TryGetAbstract(out AbstractValue? abstractValue))
        {
            text.Append(abstractValue.Type.Name).Append("(…)");
            return;
        }

#if NET11_0_OR_GREATER
        if (value.Kind == CelValueKind.Decimal)
        {
            _ = value.TryGetDecimal128(out System.Numerics.Decimal128 number);
            Span<char> digits = stackalloc char[DecimalTextRules.BufferLength];
            int count = DecimalTextRules.Format(DecimalParts.From(number), digits);
            text.Append(digits[..count]).Append('m');
            return;
        }
#endif

        text.Append("null");
    }

    // A double as string(double) writes it, with .0 added to an integral value; a non-finite one as the conversion that gives it.
    private static void AppendDouble(StringBuilder text, double value)
    {
        if (double.IsNaN(value))
        {
            text.Append("double(\"NaN\")");
            return;
        }

        if (double.IsInfinity(value))
        {
            text.Append(value > 0 ? "double(\"Infinity\")" : "double(\"-Infinity\")");
            return;
        }

        Span<char> digits = stackalloc char[DoubleTextRules.BufferLength];
        ReadOnlySpan<char> written = digits[..DoubleTextRules.Format(value, digits)];
        text.Append(written);
        if (written.IndexOfAny('.', 'e') < 0)
        {
            text.Append(".0");
        }
    }

    private static void WriteList(StringBuilder text, CelList list, int depth, CelType? declared)
    {
        if (depth >= MaxDepth)
        {
            text.Append(Ellipsis);
            return;
        }

        CelType? element = declared is { Kind: CelTypeKind.List } ? declared.Parameters[0] : null;
        text.Append('[');
        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0)
            {
                text.Append(", ");
            }

            Write(text, list[i], depth + 1, element);
        }

        text.Append(']');
    }

    private static void WriteMap(StringBuilder text, CelMap map, int depth, CelType? declared)
    {
        if (depth >= MaxDepth)
        {
            text.Append(Ellipsis);
            return;
        }

        CelType? entry = declared is { Kind: CelTypeKind.Map } ? declared.Parameters[1] : null;
        text.Append('{');
        bool first = true;
        foreach (KeyValuePair<CelValue, CelValue> pair in map)
        {
            if (!first)
            {
                text.Append(", ");
            }

            first = false;
            Write(text, pair.Key, depth + 1, null);
            text.Append(": ");
            Write(text, pair.Value, depth + 1, entry);
        }

        text.Append('}');
    }

    private static void WriteOptional(StringBuilder text, CelValue value, int depth)
    {
        _ = value.TryGetOptional(out CelOptional<CelValue> optional);
        if (!optional.HasValue)
        {
            text.Append("optional.none()");
        }
        else if (depth >= MaxDepth)
        {
            text.Append(Ellipsis);
        }
        else
        {
            text.Append("optional.of(");
            Write(text, optional.Value, depth + 1, null);
            text.Append(')');
        }
    }

    // TypeName{field: value} with the fields that are present, in the order of the description, read through it. An instance that no
    // description reaches is written by the name of its declared type, or else of its .NET type, with the ellipsis for its fields. An
    // exception of a getter passes: this call is not an evaluation.
    private static void WriteObject(StringBuilder text, CelValue value, int depth, CelType? declared)
    {
        _ = value.TryGetObject(out object? instance);
        CelObjectType? description = ObjectRules.Describe(value, null);
        if (description is null)
        {
            text.Append(declared is { Kind: CelTypeKind.Object } ? declared.Name : instance!.GetType().ToString()).Append("{…}");
            return;
        }

        text.Append(description.Name);
        if (depth >= MaxDepth)
        {
            text.Append("{…}");
            return;
        }

        text.Append('{');
        bool first = true;
        foreach (CelObjectField field in description.Fields)
        {
            if (!description.TryReadField(instance!, field.Number, out CelValue fieldValue))
            {
                continue;
            }

            if (!first)
            {
                text.Append(", ");
            }

            first = false;
            text.Append(field.Name).Append(": ");
            Write(text, fieldValue, depth + 1, field.Type);
        }

        text.Append('}');
    }
}
