#if NET11_0_OR_GREATER
using System.Globalization;
using System.Numerics;

namespace Great.Parts.Cel.Properties.Decimals;

// A finite decimal128 value as the reference sees it: a sign, an exact coefficient and an exponent, so that the value is
// the coefficient times ten to the exponent, and the digits of the coefficient (its cohort) are part of the value. A zero has
// a sign and an exponent too.
internal readonly record struct RefDecimal(bool Negative, BigInteger Coefficient, int Exponent)
{
    internal bool IsZero => Coefficient.IsZero;

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{(Negative ? '-' : '+')}{Coefficient}E{Exponent}");
}

// The two operands of a binary operation.
internal readonly record struct RefPair(RefDecimal First, RefDecimal Second)
{
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{First} and {Second}");
}
#endif
