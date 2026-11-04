using System;
using System.Runtime.InteropServices;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library;

// The functions of strings and bytes: size, contains, startsWith, endsWith and concatenation. Strings compare
// by UTF-16 code units, which for equality of parts is comparison by code points.
internal static class Strings
{
    private const ushort HighSurrogateStart = 0xD800;
    private const ushort HighSurrogateEnd = 0xDBFF;

    // size_string and string_size, (string) -> int: the number of code points, a surrogate pair counted once
    // and a lone surrogate once.
    internal static CelValue StringSize(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetString(out string? text) ? CelValue.FromInt64(CodePoints(text)) : site.NoMatchingOverload(arguments);

    // size_bytes and bytes_size, (bytes) -> int.
    internal static CelValue BytesSize(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetBytes(out ReadOnlyMemory<byte> bytes) ? CelValue.FromInt64(bytes.Length) : site.NoMatchingOverload(arguments);

    // contains_string, string.contains(string) -> bool: by the linear search for a substring.
    internal static CelValue Contains(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? pattern))
        {
            return site.NoMatchingOverload(arguments);
        }

        long steps = 0;
        return CelValue.FromBoolean(SubstringSearch.IndexOf(text, pattern, ref steps) >= 0);
    }

    // starts_with_string, string.startsWith(string) -> bool.
    internal static CelValue StartsWith(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? prefix))
        {
            return site.NoMatchingOverload(arguments);
        }

        return CelValue.FromBoolean(text.AsSpan().StartsWith(prefix.AsSpan()));
    }

    // ends_with_string, string.endsWith(string) -> bool.
    internal static CelValue EndsWith(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text) || !arguments[1].TryGetString(out string? suffix))
        {
            return site.NoMatchingOverload(arguments);
        }

        return CelValue.FromBoolean(text.AsSpan().EndsWith(suffix.AsSpan()));
    }

    // add_string, (string, string) -> string.
    internal static CelValue AddString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? left) || !arguments[1].TryGetString(out string? right))
        {
            return site.NoMatchingOverload(arguments);
        }

        return CelValue.FromString(string.Concat(left, right));
    }

    // add_bytes, (bytes, bytes) -> bytes.
    internal static CelValue AddBytes(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetBytes(out ReadOnlyMemory<byte> left) || !arguments[1].TryGetBytes(out ReadOnlyMemory<byte> right))
        {
            return site.NoMatchingOverload(arguments);
        }

        // A value does not change, so an empty operand leaves the other one as the result.
        if (left.IsEmpty)
        {
            return arguments[1];
        }

        if (right.IsEmpty)
        {
            return arguments[0];
        }

        byte[] result = new byte[left.Length + right.Length];
        left.Span.CopyTo(result);
        right.Span.CopyTo(result.AsSpan(left.Length));
        return CelValue.FromBytes(result);
    }

    // The number of code points: the code units less one for each high surrogate followed by a low one. The search
    // runs over the code units as ushort: the char overload of IndexOfAnyInRange allocates 96 bytes a call on .NET 10
    // and 11, the ushort overload nothing.
    private static int CodePoints(ReadOnlySpan<char> text)
    {
        ReadOnlySpan<ushort> units = MemoryMarshal.Cast<char, ushort>(text);
        int count = text.Length;
        int position = 0;
        while (true)
        {
            int high = units[position..].IndexOfAnyInRange(HighSurrogateStart, HighSurrogateEnd);
            if (high < 0)
            {
                return count;
            }

            position += high + 1;
            if (position < text.Length && char.IsLowSurrogate(text[position]))
            {
                count--;
                position++;
            }
        }
    }
}
