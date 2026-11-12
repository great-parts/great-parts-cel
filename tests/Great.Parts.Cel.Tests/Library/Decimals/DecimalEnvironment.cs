#if NET11_0_OR_GREATER
using System;
using System.Numerics;
using System.Threading;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Library.Decimals;

// The environment of the tests of the set decimal: the standard library, the set, the sets strings and encoders, and two variables of
// the type dyn, whose values reach the overloads without the check of static types. No literal option is on, so a decimal is made by
// decimal('…') or bound from code.
internal static class DecimalEnvironment
{
    internal static readonly CelEnvironment Instance = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Decimal(1))
        .AddExtension(CelExtensions.Strings(5))
        .AddExtension(CelExtensions.Encoders(1))
        .AddVariable("x", CelType.Dyn)
        .AddVariable("y", CelType.Dyn)
        .AddVariable("text", CelType.String)
        .Build()
        .GetEnvironmentOrThrow();

    // Evaluates an expression on the bindings, with a cost limit or without one.
    internal static CelEvalResult<CelValue> Evaluate(string expression, CelBindings? bindings = null, ulong? costLimit = null) =>
        Instance.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings ?? new CelBindings(), new CelEvalOptions { CostLimit = costLimit }, CancellationToken.None);

    // The value of an expression that has to succeed.
    internal static CelValue ValueOf(string expression, CelBindings? bindings = null) => Evaluate(expression, bindings).GetValueOrThrow();

    // The value that decimal('<text>') gives, to bind to a variable.
    internal static CelValue Of(string text) => ValueOf("decimal('" + text + "')");

    // A decimal with the given sign, coefficient and exponent, made from the bits of the binary encoding of IEEE 754-2019 (the sign in
    // bit 127, the exponent plus 6176 in bits 113 to 126, the coefficient in bits 0 to 112). A coefficient that does not fit 34 digits
    // is a pattern that Decimal128 reads as a zero.
    internal static CelValue Make(UInt128 coefficient, int exponent, bool negative = false) =>
        CelValue.FromDecimal(
            Decimal128.DecodeBinary((negative ? UInt128.One << 127 : UInt128.Zero) | ((UInt128)(uint)(exponent + 6176) << 113) | coefficient));
}
#endif
