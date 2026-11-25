using System;
using System.Buffers;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Library.Text;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions.Strings;

// The functions of the set strings that look for a substring: indexOf and lastIndexOf, replace and split, all on OrdinalSearch over
// UTF-16 code units. A match counts only at a code point boundary. A pattern that is a literal of the expression arrives prepared in
// the call site, as the SearchValues<string> that the expression made once, and is searched through it; any other pattern is searched
// by the ordinal search. lastIndexOf has no preparation: .NET does not search from the end by SearchValues<string>. Each function has
// a form that takes the values of the pattern, null for a pattern from the data, and the meter; an overload only reads its arguments
// and calls the form.
internal static partial class StringFunctions
{
    // The result of IndexOf and LastIndexOf for an offset outside the string; the results otherwise are indexes from 0 and -1.
    private const long OffsetOutOfRange = -2;

    // string_index_of_string, string.indexOf(string) -> int.
    internal static CelValue IndexOfString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? pattern))
        {
            return site.NoMatchingOverload(arguments);
        }

        return CelValue.FromInt64(IndexOf(text, pattern, SearchPattern.ValuesOf(in site), 0));
    }

    // string_index_of_string_int, string.indexOf(string, int) -> int: from the offset, in code points.
    internal static CelValue IndexOfStringInt(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? pattern) || !arguments[2].TryGetInt64(out long offset))
        {
            return site.NoMatchingOverload(arguments);
        }

        long index = IndexOf(text, pattern, SearchPattern.ValuesOf(in site), offset);
        return index == OffsetOutOfRange ? IndexError(in site, offset) : CelValue.FromInt64(index);
    }

    // string_last_index_of_string, string.lastIndexOf(string) -> int.
    internal static CelValue LastIndexOfString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? pattern))
        {
            return site.NoMatchingOverload(arguments);
        }

        return CelValue.FromInt64(LastIndexOf(text, pattern, null));
    }

    // string_last_index_of_string_int, string.lastIndexOf(string, int) -> int: the last occurrence that starts at or before the offset.
    internal static CelValue LastIndexOfStringInt(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? pattern) || !arguments[2].TryGetInt64(out long offset))
        {
            return site.NoMatchingOverload(arguments);
        }

        long index = LastIndexOf(text, pattern, offset);
        return index == OffsetOutOfRange ? IndexError(in site, offset) : CelValue.FromInt64(index);
    }

    // string_replace_string_string, string.replace(string, string) -> string: every occurrence.
    internal static CelValue ReplaceAll(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? oldText) || !arguments[2].TryGetString(out string? newText))
        {
            return site.NoMatchingOverload(arguments);
        }

        string result = Replace(text, oldText, SearchPattern.ValuesOf(in site), newText, -1, site.Meter);
        return ReferenceEquals(result, text) ? arguments[0] : CelValue.FromString(result);
    }

    // string_replace_string_string_int, string.replace(string, string, int) -> string: at most n occurrences, all below 0.
    internal static CelValue ReplaceLimited(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? oldText)
            || !arguments[2].TryGetString(out string? newText) || !arguments[3].TryGetInt64(out long limit))
        {
            return site.NoMatchingOverload(arguments);
        }

        string result = Replace(text, oldText, SearchPattern.ValuesOf(in site), newText, limit, site.Meter);
        return ReferenceEquals(result, text) ? arguments[0] : CelValue.FromString(result);
    }

    // string_split_string, string.split(string) -> list(string): every part.
    internal static CelValue SplitAll(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? separator))
        {
            return site.NoMatchingOverload(arguments);
        }

        return Split(text, separator, SearchPattern.ValuesOf(in site), -1, site.Meter);
    }

    // string_split_string_int, string.split(string, int) -> list(string): at most n parts, the last holding the rest.
    internal static CelValue SplitLimited(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? separator) || !arguments[2].TryGetInt64(out long limit))
        {
            return site.NoMatchingOverload(arguments);
        }

        return Split(text, separator, SearchPattern.ValuesOf(in site), limit, site.Meter);
    }

    // The index in code points of the first occurrence of the pattern at or after the offset in code points, -1 for none, or
    // OffsetOutOfRange when the offset is below 0 or above the number of code points. An empty pattern is found at the offset without
    // a search; a non-empty one is not found at the offset equal to the length. The values are those of a pattern that is a literal,
    // and null for a pattern from the data.
    private static long IndexOf(string text, string pattern, SearchValues<string>? values, long offset)
    {
        int start = CodePoints.OffsetOf(text, offset);
        if (start < 0)
        {
            return OffsetOutOfRange;
        }

        if (pattern.Length == 0)
        {
            return offset;
        }

        int found = OrdinalSearch.FindFirst(text, pattern, values, start);
        return found < 0 ? -1 : offset + CodePoints.Count(text.AsSpan(start, found - start));
    }

    // The index in code points of the last occurrence of the pattern, -1 for none. With an offset, the last occurrence that starts at or
    // before it, and OffsetOutOfRange for an offset below 0 or above the number of code points; the empty pattern then gives the offset,
    // a non-empty one is not found at the offset equal to the length. Without an offset the empty pattern gives the number of code
    // points. The search is the ordinal search from the end, for a literal pattern too.
    private static long LastIndexOf(string text, string pattern, long? offset)
    {
        int end = text.Length;
        if (offset is long given)
        {
            int start = CodePoints.OffsetOf(text, given);
            if (start < 0)
            {
                return OffsetOutOfRange;
            }

            if (pattern.Length == 0)
            {
                return given;
            }

            if (start == text.Length)
            {
                return -1;
            }

            end = (int)Math.Min(text.Length, (long)start + pattern.Length);
        }
        else if (pattern.Length == 0)
        {
            return CodePoints.Count(text);
        }

        int found = OrdinalSearch.FindLast(text, pattern, end);
        return found < 0 ? -1 : CodePoints.Count(text.AsSpan(0, found));
    }

    // The receiver with at most limit occurrences of the old string replaced, all of them for a negative limit; with an empty old string
    // the new one is put before each code point and at the end, up to the limit. The result is the receiver itself when nothing
    // changes, which is charged as a copy. The result is charged to the meter as it is built. The values are those of an old string that
    // is a literal, and null for one from the data.
    private static string Replace(string text, string oldText, SearchValues<string>? values, string newText, long limit, IEvaluationMeter? meter)
    {
        if (limit < 0)
        {
            limit = long.MaxValue;
        }

        // The receiver is the result: nothing to replace, nothing to put, or an old string that cannot occur.
        if (limit == 0 || oldText.Length > text.Length || (oldText.Length == 0 && newText.Length == 0))
        {
            meter?.Charge(Costs.Tenths((ulong)text.Length));
            return text;
        }

        if (oldText.Length == 0)
        {
            return InsertAtBoundaries(text, newText, limit, meter);
        }

        int index = OrdinalSearch.FindFirst(text, oldText, values, 0);
        if (index < 0)
        {
            meter?.Charge(Costs.Tenths((ulong)text.Length));
            return text;
        }

        ResultBuilder builder = new(meter);
        try
        {
            int from = 0;
            long replaced = 0;
            while (true)
            {
                builder.Append(text.AsSpan(from, index - from));
                builder.Append(newText);
                from = index + oldText.Length;
                replaced++;
                if (replaced == limit)
                {
                    break;
                }

                index = OrdinalSearch.FindFirst(text, oldText, values, from);
                if (index < 0)
                {
                    break;
                }
            }

            builder.Append(text.AsSpan(from));
            return builder.ToResultString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    // The list of the parts of the receiver between the occurrences of the separator: at most limit parts, the last holding the rest
    // unsplit, all of them for a negative limit and none for 0. With an empty separator each code point is a part, and an empty string
    // has none. Each part is charged, 1 and its length, before it is created. The values are those of a separator that is a literal, and
    // null for one from the data.
    private static CelValue Split(string text, string separator, SearchValues<string>? values, long limit, IEvaluationMeter? meter)
    {
        if (limit < 0)
        {
            limit = long.MaxValue;
        }

        Parts parts = new(meter);
        try
        {
            if (limit == 0 || (separator.Length == 0 && text.Length == 0))
            {
                return parts.ToList();
            }

            if (separator.Length == 0)
            {
                int position = 0;
                while (position < text.Length)
                {
                    if (parts.Count == limit - 1)
                    {
                        parts.Add(text, position, text.Length - position);
                        break;
                    }

                    int units = CodePoints.UnitsAt(text, position);
                    parts.Add(text, position, units);
                    position += units;
                }

                return parts.ToList();
            }

            int from = 0;
            if (separator.Length <= text.Length && limit > 1)
            {
                while (parts.Count < limit - 1)
                {
                    int index = OrdinalSearch.FindFirst(text, separator, values, from);
                    if (index < 0)
                    {
                        break;
                    }

                    parts.Add(text, from, index - from);
                    from = index + separator.Length;
                }
            }

            parts.Add(text, from, text.Length - from);
            return parts.ToList();
        }
        finally
        {
            parts.Dispose();
        }
    }

    // The replacement of an empty old string: the new string before each code point and at the end, for the first limit places.
    private static string InsertAtBoundaries(string text, string newText, long limit, IEvaluationMeter? meter)
    {
        ResultBuilder builder = new(meter);
        try
        {
            int position = 0;
            long inserted = 0;
            while (true)
            {
                if (inserted == limit)
                {
                    builder.Append(text.AsSpan(position));
                    break;
                }

                builder.Append(newText);
                inserted++;
                if (position == text.Length)
                {
                    break;
                }

                int units = CodePoints.UnitsAt(text, position);
                builder.Append(text.AsSpan(position, units));
                position += units;
            }

            return builder.ToResultString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    // The parts of a split, in a pooled array of values. Each part is charged — 1 and the units of its length by the running length of
    // the parts, ⌈(T + l)/10⌉ − ⌈T/10⌉ — before it is created.
    private ref struct Parts
    {
        private readonly IEvaluationMeter? _meter;
        private CelValue[]? _items;
        private int _count;
        private ulong _length;

        internal Parts(IEvaluationMeter? meter)
        {
            _meter = meter;
            _items = null;
            _count = 0;
            _length = 0;
        }

        internal readonly int Count => _count;

        internal void Add(string text, int start, int length)
        {
            _meter?.Charge(1 + Costs.Tenths(_length + (ulong)length) - Costs.Tenths(_length));
            if (_items is null)
            {
                _items = ArrayPool<CelValue>.Shared.Rent(8);
            }
            else if (_count == _items.Length)
            {
                CelValue[] larger = ArrayPool<CelValue>.Shared.Rent(_count * 2);
                _items.AsSpan(0, _count).CopyTo(larger);
                ArrayPool<CelValue>.Shared.Return(_items, clearArray: true);
                _items = larger;
            }

            _items[_count++] = CelValue.FromString(start == 0 && length == text.Length ? text : text.Substring(start, length));
            _length += (ulong)length;
        }

        // The list of the parts, in an array of exactly their number.
        internal readonly CelValue ToList()
        {
            if (_count == 0)
            {
                return CelValue.FromList(new ValueArrayList([]));
            }

            CelValue[] exact = new CelValue[_count];
            _items.AsSpan(0, _count).CopyTo(exact);
            return CelValue.FromList(new ValueArrayList(exact));
        }

        internal void Dispose()
        {
            if (_items is not null)
            {
                ArrayPool<CelValue>.Shared.Return(_items, clearArray: true);
                _items = null;
            }
        }
    }
}
