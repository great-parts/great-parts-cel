using System;
using System.Buffers;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions.Strings;

// The functions of the set strings that look for a substring: indexOf and lastIndexOf, replace and split, all on SubstringSearch
// over UTF-16 code units. A match counts only at a code point boundary. Each function has an internal form that takes the counter of
// steps and the meter, and the property test of the search calls those forms; an overload only reads its arguments and calls the form.
internal static partial class StringFunctions
{
    // The result of IndexOf and LastIndexOf for an offset outside the string; the results otherwise are indexes from 0 and -1.
    internal const long OffsetOutOfRange = -2;

    // string_index_of_string, string.indexOf(string) -> int.
    internal static CelValue IndexOfString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? pattern))
        {
            return site.NoMatchingOverload(arguments);
        }

        long steps = 0;
        return CelValue.FromInt64(IndexOf(text, pattern, 0, ref steps));
    }

    // string_index_of_string_int, string.indexOf(string, int) -> int: from the offset, in code points.
    internal static CelValue IndexOfStringInt(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? pattern) || !arguments[2].TryGetInt64(out long offset))
        {
            return site.NoMatchingOverload(arguments);
        }

        long steps = 0;
        long index = IndexOf(text, pattern, offset, ref steps);
        return index == OffsetOutOfRange ? IndexError(in site, offset) : CelValue.FromInt64(index);
    }

    // string_last_index_of_string, string.lastIndexOf(string) -> int.
    internal static CelValue LastIndexOfString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? pattern))
        {
            return site.NoMatchingOverload(arguments);
        }

        long steps = 0;
        return CelValue.FromInt64(LastIndexOf(text, pattern, null, ref steps));
    }

    // string_last_index_of_string_int, string.lastIndexOf(string, int) -> int: the last occurrence that starts at or before the offset.
    internal static CelValue LastIndexOfStringInt(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? pattern) || !arguments[2].TryGetInt64(out long offset))
        {
            return site.NoMatchingOverload(arguments);
        }

        long steps = 0;
        long index = LastIndexOf(text, pattern, offset, ref steps);
        return index == OffsetOutOfRange ? IndexError(in site, offset) : CelValue.FromInt64(index);
    }

    // string_replace_string_string, string.replace(string, string) -> string: every occurrence.
    internal static CelValue ReplaceAll(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? oldText) || !arguments[2].TryGetString(out string? newText))
        {
            return site.NoMatchingOverload(arguments);
        }

        long steps = 0;
        string result = Replace(text, oldText, newText, -1, site.Meter, ref steps);
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

        long steps = 0;
        string result = Replace(text, oldText, newText, limit, site.Meter, ref steps);
        return ReferenceEquals(result, text) ? arguments[0] : CelValue.FromString(result);
    }

    // string_split_string, string.split(string) -> list(string): every part.
    internal static CelValue SplitAll(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? separator))
        {
            return site.NoMatchingOverload(arguments);
        }

        long steps = 0;
        return Split(text, separator, -1, site.Meter, ref steps);
    }

    // string_split_string_int, string.split(string, int) -> list(string): at most n parts, the last holding the rest.
    internal static CelValue SplitLimited(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? separator) || !arguments[2].TryGetInt64(out long limit))
        {
            return site.NoMatchingOverload(arguments);
        }

        long steps = 0;
        return Split(text, separator, limit, site.Meter, ref steps);
    }

    // The index in code points of the first occurrence of the pattern at or after the offset in code points, -1 for none, or
    // OffsetOutOfRange when the offset is below 0 or above the number of code points. An empty pattern is found at the offset without
    // a search; a non-empty one is not found at the offset equal to the length. The steps of the search are added to the counter.
    internal static long IndexOf(string text, string pattern, long offset, ref long steps)
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

        int found = FindFirst(text, pattern, start, ref steps);
        return found < 0 ? -1 : offset + CodePoints.Count(text.AsSpan(start, found - start));
    }

    // The index in code points of the last occurrence of the pattern, -1 for none. With an offset, the last occurrence that starts at or
    // before it, and OffsetOutOfRange for an offset below 0 or above the number of code points; the empty pattern then gives the offset,
    // a non-empty one is not found at the offset equal to the length. Without an offset the empty pattern gives the number of code
    // points. The steps of the search are added to the counter.
    internal static long LastIndexOf(string text, string pattern, long? offset, ref long steps)
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

        int found = FindLast(text, pattern, end, ref steps);
        return found < 0 ? -1 : CodePoints.Count(text.AsSpan(0, found));
    }

    // The receiver with at most limit occurrences of the old string replaced, all of them for a negative limit; with an empty old string
    // the new one is put before each code point and at the end, up to the limit. The result is the receiver itself when nothing
    // changes, which is charged as a copy. The result is charged to the meter as it is built; the steps of the search are added to the counter.
    internal static string Replace(string text, string oldText, string newText, long limit, IEvaluationMeter? meter, ref long steps)
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

        Span<byte> buffer = stackalloc byte[SubstringSearch.StackPatternLength];
        SubstringSearch.PreparedPattern prepared = SubstringSearch.PreparedPattern.Create(oldText, false, buffer, ref steps);
        int index = FindFrom(text, oldText.Length, in prepared, 0, ref steps);
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

                index = FindFrom(text, oldText.Length, in prepared, from, ref steps);
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
    // has none. Each part is charged, 1 and its length, before it is created; the steps of the search are added to the counter.
    internal static CelValue Split(string text, string separator, long limit, IEvaluationMeter? meter, ref long steps)
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
                Span<byte> buffer = stackalloc byte[SubstringSearch.StackPatternLength];
                SubstringSearch.PreparedPattern prepared = SubstringSearch.PreparedPattern.Create(separator, false, buffer, ref steps);
                while (parts.Count < limit - 1)
                {
                    int index = FindFrom(text, separator.Length, in prepared, from, ref steps);
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

    // The offset in code units of the first occurrence at or after from, or -1; the pattern is not empty.
    private static int FindFirst(string text, string pattern, int from, ref long steps)
    {
        if (pattern.Length > text.Length - from)
        {
            return -1;
        }

        Span<byte> buffer = stackalloc byte[SubstringSearch.StackPatternLength];
        SubstringSearch.PreparedPattern prepared = SubstringSearch.PreparedPattern.Create(pattern, false, buffer, ref steps);
        return FindFrom(text, pattern.Length, in prepared, from, ref steps);
    }

    // The offset in code units of the last occurrence that ends at or before end, or -1; the pattern is not empty.
    private static int FindLast(string text, string pattern, int end, ref long steps)
    {
        if (pattern.Length > end)
        {
            return -1;
        }

        Span<byte> buffer = stackalloc byte[SubstringSearch.StackPatternLength];
        SubstringSearch.PreparedPattern prepared = SubstringSearch.PreparedPattern.Create(pattern, true, buffer, ref steps);
        while (true)
        {
            int index = prepared.LastIndexOf(text, end, ref steps);
            if (index < 0)
            {
                return -1;
            }

            // An occurrence that cuts a pair does not count; the next one ends before this one does.
            if (!CodePoints.SplitsPair(text, index) && !CodePoints.SplitsPair(text, index + pattern.Length))
            {
                return index;
            }

            end = index + pattern.Length - 1;
            if (end < pattern.Length)
            {
                return -1;
            }
        }
    }

    // The first occurrence at or after from that starts and ends at a code point boundary. The search goes on one unit further past an
    // occurrence that cuts a pair, which only a pattern with a lone surrogate at an end can make.
    private static int FindFrom(string text, int patternLength, in SubstringSearch.PreparedPattern prepared, int from, ref long steps)
    {
        while (true)
        {
            int index = prepared.IndexOf(text, from, ref steps);
            if (index < 0)
            {
                return -1;
            }

            if (!CodePoints.SplitsPair(text, index) && !CodePoints.SplitsPair(text, index + patternLength))
            {
                return index;
            }

            from = index + 1;
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
