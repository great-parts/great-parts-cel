using System;
using System.Globalization;

namespace Great.Parts.Cel.Eng.Difftest.Numbers;

/// <summary>
/// The shortest decimal text of a finite <c>double</c> that reads back to the same bits, for the literals of the printer, the
/// bindings sent to the oracle and the values in finding messages. It does not rely on <c>"R"</c>: on .NET 10 that format gives
/// for some values, such as 2^-25, the text of the neighbouring <c>double</c>. Instead it takes the smallest precision from 1 to
/// 17 whose <c>"G"</c> text reads back to the same bits; 17 significant digits always do.
/// </summary>
internal static class DoubleRoundTrip
{
    /// <summary>The precision whose text always reads back to the same bits.</summary>
    internal const int MaxPrecision = 17;

    private static readonly string[] Formats = CreateFormats();

    /// <summary>
    /// The shortest text of a finite value in the <c>"G"</c> format of the invariant culture that reads back to the same bits:
    /// <c>"1.5E-05"</c>, <c>"123.456"</c>, <c>"1E+20"</c>, <c>"0"</c>, <c>"-0"</c>, <c>"2.9802322387695312E-08"</c>.
    /// </summary>
    /// <param name="value">A finite value.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is not finite.</exception>
    internal static string Format(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The value is not finite.");
        }

        long bits = BitConverter.DoubleToInt64Bits(value);
        for (int precision = 1; precision < MaxPrecision; precision++)
        {
            string text = value.ToString(Formats[precision], CultureInfo.InvariantCulture);
            if (BitConverter.DoubleToInt64Bits(Read(text)) == bits)
            {
                return text;
            }
        }

        return value.ToString(Formats[MaxPrecision], CultureInfo.InvariantCulture);
    }

    private static double Read(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static string[] CreateFormats()
    {
        string[] formats = new string[MaxPrecision + 1];
        for (int precision = 1; precision <= MaxPrecision; precision++)
        {
            formats[precision] = "G" + precision.ToString(CultureInfo.InvariantCulture);
        }

        return formats;
    }
}
