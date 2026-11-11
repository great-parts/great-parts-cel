using System;
using System.Buffers;
using System.Globalization;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions.Strings;

// The functions of the set strings that cut, change and join strings, with indexes in code points. The search and the functions
// built on it are in StringFunctions.Search.cs. An index or an offset outside the string is IndexOutOfRange at the call, an invalid
// range InvalidArgument. The part of the cost that the call node charges before the function runs — 1 + ⌈Lₐ/10⌉ — is the argument
// cost of the table; the function charges the length of its result, ⌈Lᵣ/10⌉, before it creates the result.
internal static partial class StringFunctions
{
    // The character that quote writes for a lone surrogate: U+FFFD.
    private const char ReplacementCharacter = (char)0xFFFD;

    // The text of an index error: the index is the number of the error.
    private static readonly Func<CelError, string> IndexOutOfRangeText = static error =>
        "index out of range: " + error.Number.ToString(CultureInfo.InvariantCulture);

    // The function name is the first part and the start, the number: a detail names one number of the arguments.
    private static readonly Func<CelError, string> SubstringRangeText = static error =>
        "invalid argument to '" + (string)error.First! + "': invalid substring range: start index " + error.Number.ToString(CultureInfo.InvariantCulture)
        + " must be less than or equal to the end index";

    // string_char_at_int, string.charAt(int) -> string: the code point at the index; the index equal to the length gives "".
    internal static CelValue CharAt(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetInt64(out long index))
        {
            return site.NoMatchingOverload(arguments);
        }

        int offset = CodePoints.OffsetOf(text, index);
        if (offset < 0)
        {
            return IndexError(in site, index);
        }

        if (offset == text.Length)
        {
            return CelValue.FromString(string.Empty);
        }

        int units = CodePoints.UnitsAt(text, offset);
        site.Charge(Costs.Tenths((ulong)units));
        return CelValue.FromString(text.Substring(offset, units));
    }

    // string_substring_int, string.substring(int) -> string: from the index to the end.
    internal static CelValue SubstringFrom(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetInt64(out long start))
        {
            return site.NoMatchingOverload(arguments);
        }

        int offset = CodePoints.OffsetOf(text, start);
        if (offset < 0)
        {
            return IndexError(in site, start);
        }

        site.Charge(Costs.Tenths((ulong)(text.Length - offset)));
        return offset == 0 ? arguments[0] : CelValue.FromString(text.Substring(offset));
    }

    // string_substring_int_int, string.substring(int, int) -> string: from the first index to the second, the end excluded. A start
    // above the end is checked first, then the start, then the end.
    internal static CelValue SubstringRange(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetInt64(out long start) || !arguments[2].TryGetInt64(out long end))
        {
            return site.NoMatchingOverload(arguments);
        }

        if (start > end)
        {
            return site.Error(CelErrorCode.InvalidArgument, SubstringRangeText, site.Function, number: start);
        }

        int startOffset = CodePoints.OffsetOf(text, start);
        if (startOffset < 0)
        {
            return IndexError(in site, start);
        }

        int endOffset = CodePoints.OffsetOf(text.AsSpan(startOffset), end - start);
        if (endOffset < 0)
        {
            return IndexError(in site, end);
        }

        endOffset += startOffset;
        site.Charge(Costs.Tenths((ulong)(endOffset - startOffset)));
        return startOffset == 0 && endOffset == text.Length ? arguments[0] : CelValue.FromString(text.Substring(startOffset, endOffset - startOffset));
    }

    // string_trim, string.trim() -> string: the White_Space code points of the table below removed from both ends.
    internal static CelValue Trim(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        int start = 0;
        int end = text.Length;
        while (start < end && IsSpace(text[start]))
        {
            start++;
        }

        while (end > start && IsSpace(text[end - 1]))
        {
            end--;
        }

        site.Charge(Costs.Tenths((ulong)(end - start)));
        return start == 0 && end == text.Length ? arguments[0] : CelValue.FromString(text.Substring(start, end - start));
    }

    // string_lower_ascii, string.lowerAscii() -> string: only A–Z change; every other code point stays.
    internal static CelValue LowerAscii(ReadOnlySpan<CelValue> arguments, in CallSite site) => ChangeCase(arguments, in site, upper: false);

    // string_upper_ascii, string.upperAscii() -> string: only a–z change.
    internal static CelValue UpperAscii(ReadOnlySpan<CelValue> arguments, in CallSite site) => ChangeCase(arguments, in site, upper: true);

    // string_reverse, string.reverse() -> string: the code points in reverse order; a pair stays a pair and a lone surrogate stays itself.
    internal static CelValue Reverse(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        site.Charge(Costs.Tenths((ulong)text.Length));
        if (text.Length < 2)
        {
            return arguments[0];
        }

        return CelValue.FromString(string.Create(text.Length, text, static (result, source) =>
        {
            int read = source.Length;
            int write = 0;
            while (read > 0)
            {
                int units = read >= 2 && char.IsLowSurrogate(source[read - 1]) && char.IsHighSurrogate(source[read - 2]) ? 2 : 1;
                source.AsSpan(read - units, units).CopyTo(result[write..]);
                write += units;
                read -= units;
            }
        }));
    }

    // strings_quote, strings.quote(string) -> string: the string in double quotes with \a, \b, \f, \n, \r, \t, \v, \\ and \" escaped;
    // every other code point stays, and a lone surrogate becomes U+FFFD.
    internal static CelValue Quote(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        long length = (long)text.Length + 2;
        foreach (char unit in text)
        {
            if (Escape(unit) != 0)
            {
                length++;
            }
        }

        site.Charge(Costs.Tenths((ulong)length));
        return CelValue.FromString(string.Create((int)length, text, static (result, source) =>
        {
            int write = 0;
            result[write++] = '"';
            for (int i = 0; i < source.Length; i++)
            {
                char unit = source[i];
                if (char.IsHighSurrogate(unit))
                {
                    if (i + 1 < source.Length && char.IsLowSurrogate(source[i + 1]))
                    {
                        result[write++] = unit;
                        result[write++] = source[++i];
                    }
                    else
                    {
                        result[write++] = ReplacementCharacter;
                    }

                    continue;
                }

                if (char.IsLowSurrogate(unit))
                {
                    result[write++] = ReplacementCharacter;
                    continue;
                }

                char escape = Escape(unit);
                if (escape != 0)
                {
                    result[write++] = '\\';
                    result[write++] = escape;
                }
                else
                {
                    result[write++] = unit;
                }
            }

            result[write] = '"';
        }));
    }

    // list_join and list_join_string, list(string).join([string]) -> string: the elements joined by the separator, by none for the
    // first form. The elements are read once into a pooled array; their lengths give the length of the result, which is charged
    // before the string is created once with it.
    internal static CelValue Join(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetList(out CelList? list))
        {
            return site.NoMatchingOverload(arguments);
        }

        string separator = string.Empty;
        if (arguments.Length > 1)
        {
            if (!arguments[1].TryGetString(out string? given))
            {
                return site.NoMatchingOverload(arguments);
            }

            separator = given;
        }

        int count = list.Count;
        if (count == 0)
        {
            return CelValue.FromString(string.Empty);
        }

        string[] elements = ArrayPool<string>.Shared.Rent(count);
        try
        {
            long total = (long)separator.Length * (count - 1);
            for (int i = 0; i < count; i++)
            {
                CelValue element = list.Read(i, site.Meter);
                if (element.Kind == CelValueKind.Error)
                {
                    return site.Placed(element);
                }

                if (!element.TryGetString(out string? text))
                {
                    return site.NoMatchingOverload(arguments);
                }

                elements[i] = text;
                total += text.Length;
            }

            site.Charge(Costs.Tenths((ulong)total));
            return CelValue.FromString(string.Create((int)Math.Min(total, int.MaxValue), (elements, count, separator), static (result, state) =>
            {
                int write = 0;
                for (int i = 0; i < state.count; i++)
                {
                    if (i > 0)
                    {
                        state.separator.AsSpan().CopyTo(result[write..]);
                        write += state.separator.Length;
                    }

                    state.elements[i].AsSpan().CopyTo(result[write..]);
                    write += state.elements[i].Length;
                }
            }));
        }
        finally
        {
            ArrayPool<string>.Shared.Return(elements, clearArray: true);
        }
    }

    // The argument cost of join: n for the size of the list, and ⌈Lₐ/10⌉ for the lengths of its string elements and of the separator,
    // in one pass over the list.
    internal static ulong JoinArgumentCost(ReadOnlySpan<CelValue> arguments)
    {
        ulong units = 0;
        ulong length = 0;
        foreach (CelValue argument in arguments)
        {
            if (argument.TryGetList(out CelList? list))
            {
                units += (ulong)list.Count;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].TryGetString(out string? text))
                    {
                        length += (ulong)text.Length;
                    }
                }
            }
            else if (argument.TryGetString(out string? separator))
            {
                length += (ulong)separator.Length;
            }
        }

        return units + Costs.Tenths(length);
    }

    // The White_Space code points of Go's unicode.IsSpace, all in the basic plane; the table is the code's own.
    internal static bool IsSpace(char unit) => (int)unit switch
    {
        >= 0x0009 and <= 0x000D => true,
        0x0020 or 0x0085 or 0x00A0 or 0x1680 => true,
        >= 0x2000 and <= 0x200A => true,
        0x2028 or 0x2029 or 0x202F or 0x205F or 0x3000 => true,
        _ => false,
    };

    // The letter after the backslash for a code unit that quote escapes, or 0 for one it keeps.
    private static char Escape(char unit) => unit switch
    {
        '\u0007' => 'a',
        '\b' => 'b',
        '\f' => 'f',
        '\n' => 'n',
        '\r' => 'r',
        '\t' => 't',
        '\v' => 'v',
        '\\' => '\\',
        '"' => '"',
        _ => '\0',
    };

    private static CelValue ChangeCase(ReadOnlySpan<CelValue> arguments, in CallSite site, bool upper)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        site.Charge(Costs.Tenths((ulong)text.Length));
        char low = upper ? 'a' : 'A';
        int first = 0;
        while (first < text.Length && !(text[first] >= low && text[first] <= low + 25))
        {
            first++;
        }

        // A string without a letter to change is its own result.
        if (first == text.Length)
        {
            return arguments[0];
        }

        return CelValue.FromString(string.Create(text.Length, (text, first, upper), static (result, state) =>
        {
            state.text.AsSpan().CopyTo(result);
            char low = state.upper ? 'a' : 'A';
            for (int i = state.first; i < result.Length; i++)
            {
                char unit = result[i];
                if (unit >= low && unit <= low + 25)
                {
                    result[i] = (char)(unit ^ 0x20);
                }
            }
        }));
    }

    private static CelValue IndexError(in CallSite site, long index) =>
        site.Error(CelErrorCode.IndexOutOfRange, IndexOutOfRangeText, null, null, index);
}
