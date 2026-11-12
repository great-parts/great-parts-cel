#if NET11_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Decimals;

// What the properties of decimal share. The environment is the standard library with the extension set decimal and the literal
// option, and variables of the types the properties bind: a and b are decimal, i an int, u a uint, d a double, s a string and m a
// decimal that the property binds as System.Decimal. A property compares the text of what the library gave with the text of what
// the reference gives, so a failure shows both: the sign, the coefficient and the exponent of a value, or the error code.
internal static class DecimalHarness
{
    internal static readonly CelEnvironment Env = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Decimal(1))
        .EnableDecimalLiterals()
        .AddVariable("a", CelType.Decimal)
        .AddVariable("b", CelType.Decimal)
        .AddVariable("i", CelType.Int)
        .AddVariable("u", CelType.UInt)
        .AddVariable("d", CelType.Double)
        .AddVariable("s", CelType.String)
        .AddVariable("m", CelType.Decimal)
        .Build()
        .GetEnvironmentOrThrow();

    internal static CelProgram<T> Program<T>(string expression) => Env.Compile<T>(expression).GetProgramOrThrow();

    // A decimal value of the library with the sign, the coefficient and the exponent of the reference value, made by the bits of
    // its binary integer encoding.
    internal static CelValue Value(RefDecimal value) =>
        CelValue.FromDecimal(Decimal128.DecodeBinary(ReferenceDecimal.ToBits(value)));

    internal static CelBindings Bind(string name, RefDecimal value) => new CelBindings().Add(name, Value(value));

    internal static CelBindings Bind(RefPair pair) =>
        new CelBindings().Add("a", Value(pair.First)).Add("b", Value(pair.Second));

    // The text of the outcome of an evaluation: what the describing function says of the value, or the code of the first error.
    internal static string Observe<T>(CelEvalResult<T> result, Func<T, string> describe) =>
        result.IsSuccess
            ? describe(result.GetValueOrThrow())
            : string.Create(CultureInfo.InvariantCulture, $"error {result.Errors[0].Code}");

    // The parts of a value of the library, read from its bits by the reference.
    internal static string Parts(Decimal128 value) =>
        ReferenceDecimal.FromBits(Decimal128.EncodeBinary(value)) is { } parts ? parts.ToString() : "a value that is not finite";

    internal static string Truth(bool value) => value ? "true" : "false";

    internal static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    internal static string Number(ulong value) => value.ToString(CultureInfo.InvariantCulture);

    internal static string DoubleBits(double value) =>
        BitConverter.DoubleToInt64Bits(value).ToString("X16", CultureInfo.InvariantCulture);

    internal static string ExpectedError(CelErrorCode code) => string.Create(CultureInfo.InvariantCulture, $"error {code}");

    // What the reference expects of an operation that may overflow: the value, or the error code when there is none.
    internal static string Expected(RefDecimal? value, CelErrorCode otherwise) =>
        value is { } known ? known.ToString() : ExpectedError(otherwise);

    // Null when the library gave what the reference expects, the difference otherwise.
    internal static string? Differs(string what, string expected, string actual) =>
        string.Equals(expected, actual, StringComparison.Ordinal) ? null : $"{what}: expected {expected}, got {actual}";

    // Checks every case of an explicit list: a boundary is a case, and a failing one fails the test with its text.
    internal static void CheckAll<T>(string property, IEnumerable<T> cases, Func<T, string?> violation, Func<T, string> show)
    {
        foreach (T value in cases)
        {
            if (violation(value) is { } reason)
            {
                Assert.Fail($"{property}: {reason}; case: {show(value)}");
            }
        }
    }
}
#endif
