#if NET11_0_OR_GREATER
using System;
using System.Numerics;

namespace Great.Parts.Cel.Tests.Examples.Library;

// What the examples of the extension set decimal share (net11.0 only: the set, the literal option and the type Decimal128
// exist there alone).
//
// Create() is the environment of the examples: the standard library, the set decimal version 1 and the literal option, so that
// 12.34m and 100m are literals. Rows are those of ExtensionRows. A value of a row that has to show its digits is written as
// string(x), since the equality of two decimals ignores the cohort: string(1.50m) is "1.50" and string(1.5m) is "1.5".
//
// Make and Parts give a Decimal128 with a chosen coefficient, exponent and sign and read them back, by the bits of the binary
// integer encoding that Decimal128.EncodeBinary and DecodeBinary give and take (IEEE 754-2019 BID: the sign in bit 127, the
// exponent plus 6176 in bits 113 to 126, the coefficient in bits 0 to 112). The examples use no parsing and no formatting of
// Decimal128, which the library does not use either.
internal static class DecimalExample
{
    internal const string Max34 = "9999999999999999999999999999999999";

    internal static CelEnvironmentBuilder Builder() =>
        new CelEnvironmentBuilder(CelStandardLibrary.V1).AddExtension(CelExtensions.Decimal(1)).EnableDecimalLiterals();

    internal static CelEnvironment Create() => Builder().Build().GetEnvironmentOrThrow();

    internal static Decimal128 Make(ulong coefficient, int exponent, bool negative = false) => Make((UInt128)coefficient, exponent, negative);

    internal static Decimal128 Make(UInt128 coefficient, int exponent, bool negative = false) =>
        Decimal128.DecodeBinary(
            (negative ? UInt128.One << 127 : UInt128.Zero) | ((UInt128)(uint)(exponent + 6176) << 113) | coefficient);

    internal static (bool Negative, UInt128 Coefficient, int Exponent) Parts(Decimal128 value)
    {
        UInt128 bits = Decimal128.EncodeBinary(value);
        UInt128 mask = (UInt128.One << 113) - UInt128.One;
        return ((bits >> 127) != UInt128.Zero, bits & mask, (int)((bits >> 113) & (UInt128)0x3FFF) - 6176);
    }
}
#endif
