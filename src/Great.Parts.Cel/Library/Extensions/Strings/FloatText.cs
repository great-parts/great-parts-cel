using System;
using System.Buffers;
using System.Globalization;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions.Strings;

// The text forms of a double that the clauses of format write: the shortest digits laid out without an exponent, a fixed number of
// digits after the point, and the scientific form with a fixed number of digits. None uses the culture of the process. A text goes
// to the builder in one piece; a text that does not fit the stack buffer of 128 units is built in an array rented from the shared
// pool, which is returned before the method ends.
internal static class FloatText
{
    // 256 bytes of stack: the most that a hot path takes.
    private const int StackLength = 128;

    // A double below this magnitude has at most 16 digits before the point.
    private const double SmallMagnitude = 1e15;

    // The most digits before the point of a double.
    private const int MaxIntegerDigits = 309;

    // The shortest digits that read back to the same double, without an exponent: 1e21 is 1000000000000000000000, 1e-7 is
    // 0.0000001, -0.0 is -0. NaN, Infinity and -Infinity as written.
    internal static void AppendShortest(ref ResultBuilder builder, double value)
    {
        if (AppendNonFinite(ref builder, value))
        {
            return;
        }

        Span<char> digits = stackalloc char[DoubleTextRules.BufferLength];
        int count = DoubleTextRules.ShortestDigits(value, digits, out int point);
        bool negative = double.IsNegative(value);
        int length = (negative ? 1 : 0) + (point <= 0 ? 2 - point + count : point >= count ? point : count + 1);
        char[]? rented = null;
        try
        {
            Span<char> text = length <= StackLength ? stackalloc char[StackLength] : (rented = ArrayPool<char>.Shared.Rent(length));
            builder.Append(text[..Layout(digits[..count], point, negative, text)]);
        }
        finally
        {
            Return(rented);
        }
    }

    // Exactly the precision's number of digits after the point, correctly rounded from the exact value of the double, an exact tie to
    // the even digit; the sign of a negative zero and of a small negative number that rounds to zero stays.
    internal static void AppendFixed(ref ResultBuilder builder, double value, int precision)
    {
        if (AppendNonFinite(ref builder, value))
        {
            return;
        }

        int length = 2 + (Math.Abs(value) < SmallMagnitude ? 16 : MaxIntegerDigits) + precision;
        char[]? rented = null;
        try
        {
            Span<char> text = length <= StackLength ? stackalloc char[StackLength] : (rented = ArrayPool<char>.Shared.Rent(length));
            Span<char> format = stackalloc char[5];
            _ = value.TryFormat(text, out int written, Format('F', precision, format), CultureInfo.InvariantCulture);
            builder.Append(text[..written]);
        }
        finally
        {
            Return(rented);
        }
    }

    // One digit, a point, the precision's number of digits, e, the sign and the exponent with at least two digits: 1.235e+04,
    // 0.000000e+00, 1.00e+100. The rounding is that of the fixed form.
    internal static void AppendScientific(ref ResultBuilder builder, double value, int precision)
    {
        if (AppendNonFinite(ref builder, value))
        {
            return;
        }

        Span<char> text = stackalloc char[StackLength];
        Span<char> format = stackalloc char[5];
        _ = value.TryFormat(text, out int written, Format('e', precision, format), CultureInfo.InvariantCulture);
        builder.Append(text[..TrimExponent(text, written)]);
    }

    // NaN, Infinity and -Infinity are the same text in every form and for every precision.
    private static bool AppendNonFinite(ref ResultBuilder builder, double value)
    {
        if (double.IsFinite(value))
        {
            return false;
        }

        builder.Append(double.IsNaN(value) ? "NaN" : value > 0 ? "Infinity" : "-Infinity");
        return true;
    }

    // The digits with the point at its place, and zeros where the digits do not reach it: 123 with the point at 5 is 12300, with the
    // point at 1 is 1.23, with the point at -2 is 0.00123.
    private static int Layout(ReadOnlySpan<char> digits, int point, bool negative, Span<char> text)
    {
        int written = 0;
        if (negative)
        {
            text[written++] = '-';
        }

        if (point <= 0)
        {
            text[written++] = '0';
            text[written++] = '.';
            text.Slice(written, -point).Fill('0');
            written -= point;
            digits.CopyTo(text[written..]);
            return written + digits.Length;
        }

        if (point >= digits.Length)
        {
            digits.CopyTo(text[written..]);
            written += digits.Length;
            text.Slice(written, point - digits.Length).Fill('0');
            return written + point - digits.Length;
        }

        digits[..point].CopyTo(text[written..]);
        written += point;
        text[written++] = '.';
        digits[point..].CopyTo(text[written..]);
        return written + digits.Length - point;
    }

    // .NET writes the exponent with three digits at least; Go with two. The leading zero of a three-digit exponent is cut.
    private static int TrimExponent(Span<char> text, int length)
    {
        int exponent = text[..length].LastIndexOf('e') + 2;
        if (length - exponent != 3 || text[exponent] != '0')
        {
            return length;
        }

        text[(exponent + 1)..length].CopyTo(text[exponent..]);
        return length - 1;
    }

    // The standard format string of the letter and the precision, such as F6 or e100.
    private static ReadOnlySpan<char> Format(char letter, int precision, Span<char> buffer)
    {
        buffer[0] = letter;
        _ = precision.TryFormat(buffer[1..], out int written, default, CultureInfo.InvariantCulture);
        return buffer[..(1 + written)];
    }

    private static void Return(char[]? rented)
    {
        if (rented is not null)
        {
            ArrayPool<char>.Shared.Return(rented);
        }
    }
}
