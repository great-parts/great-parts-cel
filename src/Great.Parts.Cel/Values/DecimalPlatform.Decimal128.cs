#if NET11_0_OR_GREATER
using System;
using System.Numerics;

namespace Great.Parts.Cel.Values;

// The platform of the set decimal on net11.0: the value is a Decimal128, 34 digits and the exponents −6176 to 6111. The arithmetic and
// the comparison are those of Decimal128, which follows IEEE 754-2019 (the quantum of a result, rounding to 34 digits half to even)
// and never throws; an infinity is the only result to refuse, as Overflow.
internal static partial class DecimalPlatform
{
    // The .NET type of the value, which the error of a value that the environment cannot have names.
    internal static readonly Type ClrType = typeof(Decimal128);

    internal static DecimalParts ToParts(PlatformDecimal value) => DecimalParts.From(value);

    internal static PlatformDecimal FromParts(DecimalParts parts) => parts.ToDecimal128();

    // The value of parts that a reading or a double gave: always in the range of decimal128.
    internal static bool TryRound(DecimalParts parts, out PlatformDecimal result)
    {
        result = parts.ToDecimal128();
        return true;
    }

    // false for an infinity: Overflow.
    internal static bool TryAdd(PlatformDecimal left, PlatformDecimal right, out PlatformDecimal result)
    {
        result = left + right;
        return Decimal128.IsFinite(result);
    }

    internal static bool TrySubtract(PlatformDecimal left, PlatformDecimal right, out PlatformDecimal result)
    {
        result = left - right;
        return Decimal128.IsFinite(result);
    }

    internal static bool TryMultiply(PlatformDecimal left, PlatformDecimal right, out PlatformDecimal result)
    {
        result = left * right;
        return Decimal128.IsFinite(result);
    }

    // The divisor is not a zero: the caller has refused it.
    internal static bool TryDivide(PlatformDecimal left, PlatformDecimal right, out PlatformDecimal result)
    {
        result = left / right;
        return Decimal128.IsFinite(result);
    }

    internal static PlatformDecimal Negate(PlatformDecimal value) => -value;

    // The 128 bits of the value, the low 64 in the first word: the payload of CelValue.
    internal static void ToWords(PlatformDecimal value, out ulong low, out ulong high)
    {
        UInt128 bits = Decimal128.EncodeBinary(value);
        low = (ulong)bits;
        high = (ulong)(bits >> 64);
    }

    internal static PlatformDecimal FromWords(ulong low, ulong high) => Decimal128.DecodeBinary(new UInt128(high, low));

    // A System.Decimal of the application: exactly, with its scale as the exponent and the sign of its zero.
    internal static PlatformDecimal FromSystemDecimal(decimal value) => DecimalNumberRules.FromSystemDecimal(value).ToDecimal128();

    // The System.Decimal of the value, rounded half to even to a scale of at most 28 and 96 bits; false when the integer part does not fit.
    internal static bool TryToSystemDecimal(PlatformDecimal value, out decimal result) =>
        DecimalNumberRules.ToSystemDecimal(ToParts(value), out result);

    // The System.Decimal that equals the value exactly; false when there is none: more digits, a smaller exponent or a larger value.
    internal static bool TryToSystemDecimalExact(PlatformDecimal value, out decimal result)
    {
        if (TryToSystemDecimal(value, out result) && FromSystemDecimal(result) == value)
        {
            return true;
        }

        result = 0m;
        return false;
    }

    // The detail of InvalidLiteral for a literal that decimal128 does not hold.
    internal static string LiteralDetail(DecimalParseResult result) =>
        result == DecimalParseResult.TooManyDigits ? "more than 34 significant digits" : "exponent out of the range of decimal128";
}
#endif
