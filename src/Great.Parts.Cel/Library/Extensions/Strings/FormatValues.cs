using System;
using System.Buffers;
using System.Collections.Generic;

using System.Text.Unicode;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions.Strings;

// The text of the values that the clauses of format write at evaluation. Every piece goes to the ResultBuilder, which charges for it
// before it takes the memory; nothing here is charged otherwise. A failure is an error value whose parts are numbers and references
// that exist already, so it holds at most 64 bytes and no text: the detail is formed when the error is read.
internal static class FormatValues
{
    // 256 bytes of stack: the most that a hot path takes.
    private const int StackLength = 128;

    // The bytes of UTF-8 that a stack buffer of hex digits is filled from: two digits each.
    private const int HexChunk = StackLength / 2;

    // Pairs of a map sorted in runs of this many by insertion before the runs are merged.
    private const int InsertionRun = 12;

    // The longest text of an integer in base 2: the sign and 64 digits.
    private const int IntegerLength = 66;

    private const string StackMessage = "insufficient execution stack to continue formatting";

    // The message of the error of a clause: the function, the detail of the kind of the failure, with its number and, for a kind of
    // value, its type name — all read from the parts of the error.
    internal static readonly Func<CelError, string> ErrorText = static error =>
        "invalid argument to '" + (string)error.First! + "': " + FormatRules.Detail((FormatFailureKind)error.Small, error.Number, (string?)error.Second);

    // Appends the text of a clause for the value, or gives the error value of the call. The letter takes the kind of the value
    // first; a kind that it does not take is the error at once, before anything of the clause is written.
    internal static bool TryAppend(ref ResultBuilder builder, FormatClause clause, CelValue value, in CallSite site, out CelValue error)
    {
        error = default;
        FormatKind kind = FormatRules.KindOf(value.Kind);
        if (!FormatRules.Accepts(clause.Specifier, kind))
        {
            error = KindError(FormatRules.KindFailure(clause.Specifier), value, in site);
            return false;
        }

        switch (clause.Specifier)
        {
            case FormatSpecifier.String:
                return TryAppendText(ref builder, value, in site, out error);
            case FormatSpecifier.Decimal:
                AppendDecimal(ref builder, value, kind);
                break;
            case FormatSpecifier.Fixed:
                FloatText.AppendFixed(ref builder, ToDouble(value, kind), clause.Precision);
                break;
            case FormatSpecifier.Scientific:
                FloatText.AppendScientific(ref builder, ToDouble(value, kind), clause.Precision);
                break;
            case FormatSpecifier.Binary:
                AppendBinary(ref builder, value, kind);
                break;
            case FormatSpecifier.Octal:
                AppendInteger(ref builder, value, kind, 8, upper: false);
                break;
            default:
                AppendHex(ref builder, value, kind, clause.Specifier == FormatSpecifier.HexUpper);
                break;
        }

        return true;
    }

    // The error of a clause that fails before a value is written: the format string, not a value.
    internal static CelValue FormatError(FormatFailure failure, in CallSite site) =>
        site.Error(CelErrorCode.InvalidArgument, ErrorText, site.Function, null, failure.Number, (byte)failure.Kind);

    // The error of a letter that does not take the value; the type name is that of the value at run time.
    private static CelValue KindError(FormatFailureKind kind, CelValue value, in CallSite site) =>
        site.Error(CelErrorCode.InvalidArgument, ErrorText, site.Function, ValueTypeRules.NameOf(value, site.Types), 0, (byte)kind);

    // %d: an integer in decimal digits, a double as the shortest text.
    private static void AppendDecimal(ref ResultBuilder builder, CelValue value, FormatKind kind)
    {
        if (kind == FormatKind.Double)
        {
            FloatText.AppendShortest(ref builder, ToDouble(value, kind));
            return;
        }

        AppendInteger(ref builder, value, kind, 10, upper: false);
    }

    // %b: 1 or 0 for a bool, an integer in base 2.
    private static void AppendBinary(ref ResultBuilder builder, CelValue value, FormatKind kind)
    {
        if (kind == FormatKind.Bool)
        {
            _ = value.TryGetBoolean(out bool flag);
            builder.Append(flag ? "1" : "0");
            return;
        }

        AppendInteger(ref builder, value, kind, 2, upper: false);
    }

    // %x and %X: an integer in base 16, the two digits of each byte of a string as UTF-8 or of bytes.
    private static void AppendHex(ref ResultBuilder builder, CelValue value, FormatKind kind, bool upper)
    {
        switch (kind)
        {
            case FormatKind.String:
                _ = value.TryGetString(out string? text);
                AppendHexOfText(ref builder, text!, upper);
                break;
            case FormatKind.Bytes:
                _ = value.TryGetBytes(out ReadOnlyMemory<byte> bytes);
                AppendHexOfBytes(ref builder, bytes.Span, upper);
                break;
            default:
                AppendInteger(ref builder, value, kind, 16, upper);
                break;
        }
    }

    // The number as a double, an integer rounded to the nearest, ties to even, as a conversion of the language does.
    private static double ToDouble(CelValue value, FormatKind kind)
    {
        switch (kind)
        {
            case FormatKind.Int:
                _ = value.TryGetInt64(out long integer);
                return integer;
            case FormatKind.UInt:
                _ = value.TryGetUInt64(out ulong unsigned);
                return unsigned;
            default:
                _ = value.TryGetDouble(out double number);
                return number;
        }
    }

    // An int or a uint in the base, with a minus for a negative int in front of the digits of its magnitude.
    private static void AppendInteger(ref ResultBuilder builder, CelValue value, FormatKind kind, int radix, bool upper)
    {
        bool negative = false;
        ulong magnitude;
        if (kind == FormatKind.Int)
        {
            _ = value.TryGetInt64(out long integer);
            negative = integer < 0;
            magnitude = negative ? (ulong)(-(integer + 1)) + 1UL : (ulong)integer;
        }
        else
        {
            _ = value.TryGetUInt64(out magnitude);
        }

        Span<char> text = stackalloc char[IntegerLength];
        int position = text.Length;
        char letter = upper ? 'A' : 'a';
        do
        {
            int digit = (int)(magnitude % (uint)radix);
            text[--position] = (char)(digit < 10 ? '0' + digit : letter + digit - 10);
            magnitude /= (uint)radix;
        }
        while (magnitude != 0);

        if (negative)
        {
            text[--position] = '-';
        }

        builder.Append(text[position..]);
    }

    // The two digits of each byte, in pieces that fit the stack buffer.
    private static void AppendHexOfBytes(ref ResultBuilder builder, scoped ReadOnlySpan<byte> bytes, bool upper)
    {
        Span<char> digits = stackalloc char[StackLength];
        string alphabet = upper ? "0123456789ABCDEF" : "0123456789abcdef";
        while (!bytes.IsEmpty)
        {
            ReadOnlySpan<byte> piece = bytes.Length > HexChunk ? bytes[..HexChunk] : bytes;
            for (int i = 0; i < piece.Length; i++)
            {
                digits[2 * i] = alphabet[piece[i] >> 4];
                digits[(2 * i) + 1] = alphabet[piece[i] & 0xF];
            }

            builder.Append(digits[..(2 * piece.Length)]);
            bytes = bytes[piece.Length..];
        }
    }

    // The hex of the UTF-8 text of a string; a lone surrogate, which only a string of the application can hold, is U+FFFD.
    private static void AppendHexOfText(ref ResultBuilder builder, string text, bool upper)
    {
        Span<byte> utf8 = stackalloc byte[HexChunk];
        ReadOnlySpan<char> rest = text;
        while (!rest.IsEmpty)
        {
            _ = Utf8.FromUtf16(rest, utf8, out int read, out int written, replaceInvalidSequences: true, isFinalBlock: true);
            AppendHexOfBytes(ref builder, utf8[..written], upper);
            rest = rest[read..];
        }
    }

    // The text of bytes read as UTF-8; an invalid sequence is U+FFFD.
    private static void AppendUtf8Text(ref ResultBuilder builder, scoped ReadOnlySpan<byte> bytes)
    {
        Span<char> chunk = stackalloc char[StackLength];
        while (!bytes.IsEmpty)
        {
            _ = Utf8.ToUtf16(bytes, chunk, out int read, out int written, replaceInvalidSequences: true, isFinalBlock: true);
            builder.Append(chunk[..written]);
            bytes = bytes[read..];
        }
    }

    // %s, and the elements and entries of a list or a map in it: the value as text.
    private static bool TryAppendText(ref ResultBuilder builder, CelValue value, in CallSite site, out CelValue error)
    {
        error = default;
        if (value.Kind == CelValueKind.List)
        {
            _ = value.TryGetList(out CelList? list);
            return TryAppendList(ref builder, list!, in site, out error);
        }

        if (value.Kind == CelValueKind.Map)
        {
            _ = value.TryGetMap(out CelMap? map);
            return TryAppendMap(ref builder, map!, in site, out error);
        }

        if (AppendScalar(ref builder, value))
        {
            return true;
        }

        error = value.Kind == CelValueKind.Error ? site.Placed(value) : KindError(FormatFailureKind.StringKind, value, in site);
        return false;
    }

    // The text of a value that is not a collection; false for a kind that %s does not take.
    private static bool AppendScalar(ref ResultBuilder builder, CelValue value)
    {
        switch (value.Kind)
        {
            case CelValueKind.Null:
                builder.Append("null");
                return true;
            case CelValueKind.Bool:
                _ = value.TryGetBoolean(out bool flag);
                builder.Append(flag ? "true" : "false");
                return true;
            case CelValueKind.Int or CelValueKind.UInt:
                AppendInteger(ref builder, value, FormatRules.KindOf(value.Kind), 10, upper: false);
                return true;
            case CelValueKind.Double:
                FloatText.AppendShortest(ref builder, ToDouble(value, FormatKind.Double));
                return true;
            case CelValueKind.String:
                _ = value.TryGetString(out string? text);
                builder.Append(text!);
                return true;
            case CelValueKind.Bytes:
                _ = value.TryGetBytes(out ReadOnlyMemory<byte> bytes);
                AppendUtf8Text(ref builder, bytes.Span);
                return true;
            default:
                return AppendNamedOrTime(ref builder, value);
        }
    }

    // The name of a type, and a timestamp or a duration as the text of string() of the value.
    private static bool AppendNamedOrTime(ref ResultBuilder builder, CelValue value)
    {
        switch (value.Kind)
        {
            case CelValueKind.Type:
                _ = value.TryGetType(out CelType? type);
                builder.Append(type!.Name);
                return true;
            case CelValueKind.Timestamp:
                _ = value.TryGetTimestamp(out CelTimestamp timestamp);
                AppendTimestamp(ref builder, timestamp);
                return true;
            case CelValueKind.Duration:
                _ = value.TryGetDuration(out CelDuration duration);
                AppendDuration(ref builder, duration);
                return true;
            default:
                return false;
        }
    }

    private static void AppendTimestamp(ref ResultBuilder builder, CelTimestamp timestamp)
    {
        Span<char> text = stackalloc char[TimeTextRules.MaxTimestampLength];
        builder.Append(text[..TimeTextRules.WriteTimestamp(timestamp, text)]);
    }

    private static void AppendDuration(ref ResultBuilder builder, CelDuration duration)
    {
        Span<char> text = stackalloc char[TimeTextRules.MaxDurationLength];
        builder.Append(text[..TimeTextRules.WriteDuration(duration.TotalNanoseconds, text)]);
    }

    // [ the elements separated by ", " ]
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
                builder.Append(", ");
            }

            if (!TryAppendText(ref builder, list.Read(i, site.Meter), in site, out error))
            {
                return false;
            }
        }

        builder.Append("]");
        return true;
    }

    // { key: value pairs separated by ", " }, ordered by the text of the key. The texts of the keys and values are written, and
    // charged, in the order of the map; the characters between them are charged ahead; the pairs are then written in order after
    // them without a second charge and the first texts are removed, so the units are those of the length of the result.
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

        // Three numbers for each pair — where its key starts, the length of the key, the length of the value — then the order and a
        // second array for the merges.
        int[] work = ArrayPool<int>.Shared.Rent(5 * count);
        try
        {
            return TryAppendPairs(ref builder, map, count, work, in site, out error);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(work);
        }
    }

    private static bool TryAppendPairs(ref ResultBuilder builder, CelMap map, int count, int[] work, in CallSite site, out CelValue error)
    {
        error = default;
        int start = builder.Length;
        int index = 0;
        using (IEnumerator<KeyValuePair<CelValue, CelValue>> entries = map.EnumerateEntries(site.Meter, readValues: true))
        {
            while (index < count && entries.MoveNext())
            {
                int keyStart = builder.Length;
                if (!TryAppendText(ref builder, entries.Current.Key, in site, out error))
                {
                    return false;
                }

                int valueStart = builder.Length;
                if (!TryAppendText(ref builder, entries.Current.Value, in site, out error))
                {
                    return false;
                }

                work[3 * index] = keyStart;
                work[(3 * index) + 1] = valueStart - keyStart;
                work[(3 * index) + 2] = builder.Length - valueStart;
                index++;
            }
        }

        int textLength = builder.Length - start;
        builder.Charge(4L * count);
        Span<int> order = SortPairs(builder.Written, work, count);
        Span<char> room = builder.WriteCharged(textLength + (4 * count));
        WritePairs(builder.Written, room, work, order);
        builder.Remove(start, textLength);
        return true;
    }

    // The pairs ordered by the text of the key, by code points, stably: insertion in runs, then merges of runs of twice the length.
    // The result is the order in one of the two arrays that follow the pairs.
    private static Span<int> SortPairs(ReadOnlySpan<char> text, int[] work, int count)
    {
        Span<int> order = work.AsSpan(3 * count, count);
        Span<int> other = work.AsSpan(4 * count, count);
        for (int i = 0; i < count; i++)
        {
            order[i] = i;
        }

        for (int low = 0; low < count; low += InsertionRun)
        {
            InsertionSort(text, work, order[low..Math.Min(low + InsertionRun, count)]);
        }

        for (int width = InsertionRun; width < count; width *= 2)
        {
            for (int low = 0; low < count; low += 2 * width)
            {
                Merge(text, work, order, other, low, Math.Min(low + width, count), Math.Min(low + (2 * width), count));
            }

            Span<int> swap = order;
            order = other;
            other = swap;
        }

        return order;
    }

    private static void InsertionSort(ReadOnlySpan<char> text, int[] work, Span<int> run)
    {
        for (int i = 1; i < run.Length; i++)
        {
            int pair = run[i];
            int j = i;
            while (j > 0 && Compare(text, work, run[j - 1], pair) == ValueOrdering.Greater)
            {
                run[j] = run[j - 1];
                j--;
            }

            run[j] = pair;
        }
    }

    // Merges the sorted runs [low, middle) and [middle, high) of the order into the same places of the other array; the left run wins
    // a tie, which keeps the order of the map.
    private static void Merge(ReadOnlySpan<char> text, int[] work, ReadOnlySpan<int> order, Span<int> other, int low, int middle, int high)
    {
        int left = low;
        int right = middle;
        for (int to = low; to < high; to++)
        {
            bool takeLeft = right >= high || (left < middle && Compare(text, work, order[left], order[right]) != ValueOrdering.Greater);
            other[to] = takeLeft ? order[left++] : order[right++];
        }
    }

    private static ValueOrdering Compare(ReadOnlySpan<char> text, int[] work, int left, int right) =>
        OrderingRules.CompareStrings(
            text.Slice(work[3 * left], work[(3 * left) + 1]),
            text.Slice(work[3 * right], work[(3 * right) + 1]));

    // The braces, the pairs in the order with ": " between key and value and ", " between pairs, copied from the texts that are
    // written before them.
    private static void WritePairs(ReadOnlySpan<char> text, Span<char> room, int[] work, ReadOnlySpan<int> order)
    {
        int position = 0;
        room[position++] = '{';
        for (int i = 0; i < order.Length; i++)
        {
            if (i > 0)
            {
                room[position++] = ',';
                room[position++] = ' ';
            }

            int keyStart = work[3 * order[i]];
            int keyLength = work[(3 * order[i]) + 1];
            int valueLength = work[(3 * order[i]) + 2];
            text.Slice(keyStart, keyLength).CopyTo(room[position..]);
            position += keyLength;
            room[position++] = ':';
            room[position++] = ' ';
            text.Slice(keyStart + keyLength, valueLength).CopyTo(room[position..]);
            position += valueLength;
        }

        room[position] = '}';
    }
}
