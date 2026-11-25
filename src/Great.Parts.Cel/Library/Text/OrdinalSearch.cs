using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace Great.Parts.Cel.Library.Text;

// The one place of the library that calls the search for a substring of .NET. The functions of strings search a pattern that comes
// from the data by the ordinal search, of any length, and a pattern that is a literal of the expression by the SearchValues<string>
// that was prepared once when the expression was checked. Neither search is linear in the worst case, so the cost of a call pays
// for the worst case instead: up to m code units of the pattern are compared at each place of the text, and the cost multiplies the
// length of the call by ceil(m / 64). A match counts only at a code point boundary: a pattern that starts with a lone low surrogate
// or ends with a lone high one, which only a string of the application can hold, is rejected where it would cut a pair, and the
// search goes on one unit further.
[SuppressMessage(
    "ApiDesign",
    "RS0030:Do not use banned APIs",
    Justification = "The only place that searches a substring: the cost of a call pays for the worst case of the search, and the search is vectorized.")]
internal static class OrdinalSearch
{
    // The values for a literal pattern of at least one code unit, made once when the expression is checked and shared by every
    // evaluation: a SearchValues is immutable and safe to use from many threads.
    internal static SearchValues<string> Prepare(string pattern) => SearchValues.Create([pattern], StringComparison.Ordinal);

    // The index of the first occurrence of the pattern in the text by the ordinal search, or -1; no boundary is looked at. An empty
    // pattern is found at 0.
    internal static int IndexOf(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern) => text.IndexOf(pattern, StringComparison.Ordinal);

    // The offset in code units of the first occurrence at or after from that starts and ends at a code point boundary, or -1. The
    // values are those of the pattern when it is a literal, and null for a pattern from the data, which is searched by the ordinal search.
    internal static int FindFirst(string text, string pattern, SearchValues<string>? values, int from)
    {
        while (true)
        {
            ReadOnlySpan<char> rest = text.AsSpan(from);
            int found = values is null ? rest.IndexOf(pattern.AsSpan(), StringComparison.Ordinal) : rest.IndexOfAny(values);
            if (found < 0)
            {
                return -1;
            }

            int index = from + found;
            if (!SplitsPair(text, index) && !SplitsPair(text, index + pattern.Length))
            {
                return index;
            }

            from = index + 1;
        }
    }

    // The offset in code units of the last occurrence that ends at or before end and starts and ends at a code point boundary, or -1;
    // the pattern is not empty.
    internal static int FindLast(string text, string pattern, int end)
    {
        while (true)
        {
            int index = text.AsSpan(0, end).LastIndexOf(pattern.AsSpan(), StringComparison.Ordinal);
            if (index < 0)
            {
                return -1;
            }

            if (!SplitsPair(text, index) && !SplitsPair(text, index + pattern.Length))
            {
                return index;
            }

            // The occurrence cuts a pair: the next one ends before this one does.
            end = index + pattern.Length - 1;
            if (end < pattern.Length)
            {
                return -1;
            }
        }
    }

    // Whether a cut before the offset would split a pair: a low surrogate after a high one.
    private static bool SplitsPair(string text, int offset) =>
        offset > 0 && offset < text.Length && char.IsHighSurrogate(text[offset - 1]) && char.IsLowSurrogate(text[offset]);
}
