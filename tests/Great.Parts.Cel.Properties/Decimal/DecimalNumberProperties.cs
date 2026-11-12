#if NET11_0_OR_GREATER
using System.Globalization;
using System.Numerics;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Properties.Decimals;

// The conversions of decimal against the reference: int() and uint() truncate toward zero and are ConversionRange exactly when the
// truncated value is out of the range; decimal(i) and decimal(u) are exact over the whole range of both; double() is the nearest
// double, half to even, and ConversionRange exactly when it is infinite; decimal(d) is the shortest digits of the double in the
// layout of the %g verb of Go, equals decimal(string(d)), and is ConversionRange for a NaN and an infinity; and System.Decimal
// goes in exactly and comes out rounded half to even to 28 places and 96 bits, ResultConversion exactly when it does not fit.
public sealed class DecimalNumberProperties
{
    private const ulong IntegerSeed = 20261221;
    private const ulong FromIntegerSeed = 20261222;
    private const ulong DoubleSeed = 20261223;
    private const ulong FromDoubleSeed = 20261224;
    private const ulong SystemSeed = 20261225;
    private const int Cases = 3000;

    private static readonly CelProgram<long> ToInt = DecimalHarness.Program<long>("int(a)");
    private static readonly CelProgram<ulong> ToUint = DecimalHarness.Program<ulong>("uint(a)");
    private static readonly CelProgram<Decimal128> FromInt = DecimalHarness.Program<Decimal128>("decimal(i)");
    private static readonly CelProgram<Decimal128> FromUint = DecimalHarness.Program<Decimal128>("decimal(u)");
    private static readonly CelProgram<double> ToDouble = DecimalHarness.Program<double>("double(a)");
    private static readonly CelProgram<Decimal128> FromDouble = DecimalHarness.Program<Decimal128>("decimal(d)");
    private static readonly CelProgram<Decimal128> FromDoubleText = DecimalHarness.Program<Decimal128>("decimal(string(d))");
    private static readonly CelProgram<Decimal128> FromSystem = DecimalHarness.Program<Decimal128>("m");
    private static readonly CelProgram<decimal> ToSystem = DecimalHarness.Program<decimal>("a");

    [Fact]
    [Trait("Requirement", "F-5")]
    public void Integers_AreTheTruncationOfTheReference() =>
        PropertyLoop.Check(nameof(Integers_AreTheTruncationOfTheReference), DecimalGenerators.IntegerValues, IntegerSeed, Cases, TruncationViolation, value => value.ToString());

    [Fact]
    [Trait("Requirement", "F-5")]
    public void Integers_AreTheTruncationOfTheReferenceOnTheBoundaries() =>
        DecimalHarness.CheckAll(nameof(Integers_AreTheTruncationOfTheReferenceOnTheBoundaries), DecimalGenerators.IntegerEdges, TruncationViolation, value => value.ToString());

    [Fact]
    [Trait("Requirement", "F-5")]
    public void DecimalOfAnInt_IsExact() =>
        PropertyLoop.Check(nameof(DecimalOfAnInt_IsExact), DecimalGenerators.Longs, FromIntegerSeed, Cases, FromIntViolation, value => DecimalHarness.Number(value));

    [Fact]
    [Trait("Requirement", "F-5")]
    public void DecimalOfAnIntOnTheBoundaries_IsExact() =>
        DecimalHarness.CheckAll(nameof(DecimalOfAnIntOnTheBoundaries_IsExact), DecimalGenerators.LongEdges, FromIntViolation, value => DecimalHarness.Number(value));

    [Fact]
    [Trait("Requirement", "F-5")]
    public void DecimalOfAUint_IsExact() =>
        PropertyLoop.Check(nameof(DecimalOfAUint_IsExact), DecimalGenerators.ULongs, FromIntegerSeed, Cases, FromUintViolation, value => DecimalHarness.Number(value));

    [Fact]
    [Trait("Requirement", "F-5")]
    public void DecimalOfAUintOnTheBoundaries_IsExact() =>
        DecimalHarness.CheckAll(nameof(DecimalOfAUintOnTheBoundaries_IsExact), DecimalGenerators.ULongEdges, FromUintViolation, value => DecimalHarness.Number(value));

    [Fact]
    [Trait("Requirement", "F-5")]
    public void Double_IsTheNearestDoubleOfTheReference() =>
        PropertyLoop.Check(nameof(Double_IsTheNearestDoubleOfTheReference), DecimalGenerators.DoubleValues, DoubleSeed, Cases, ToDoubleViolation, value => value.ToString());

    [Fact]
    [Trait("Requirement", "F-5")]
    public void Double_IsTheNearestDoubleOfTheReferenceOnTheBoundaries() =>
        DecimalHarness.CheckAll(nameof(Double_IsTheNearestDoubleOfTheReferenceOnTheBoundaries), DecimalGenerators.DoubleDecimalEdges, ToDoubleViolation, value => value.ToString());

    [Fact]
    [Trait("Requirement", "F-5")]
    public void DecimalOfADouble_IsTheLayoutOfTheShortestDigits() =>
        PropertyLoop.Check(nameof(DecimalOfADouble_IsTheLayoutOfTheShortestDigits), DecimalGenerators.Doubles, FromDoubleSeed, Cases, FromDoubleViolation, ShowDouble);

    [Fact]
    [Trait("Requirement", "F-5")]
    public void DecimalOfADoubleOnTheBoundaries_IsTheLayoutOfTheShortestDigits() =>
        DecimalHarness.CheckAll(nameof(DecimalOfADoubleOnTheBoundaries_IsTheLayoutOfTheShortestDigits), DecimalGenerators.DoubleEdges, FromDoubleViolation, ShowDouble);

    [Fact]
    [Trait("Requirement", "F-5")]
    public void SystemDecimalGoingIn_IsExact() =>
        PropertyLoop.Check(nameof(SystemDecimalGoingIn_IsExact), DecimalGenerators.SystemDecimals, SystemSeed, Cases, FromSystemViolation, ShowSystem);

    [Fact]
    [Trait("Requirement", "F-5")]
    public void SystemDecimalGoingInOnTheBoundaries_IsExact() =>
        DecimalHarness.CheckAll(nameof(SystemDecimalGoingInOnTheBoundaries_IsExact), DecimalGenerators.SystemDecimalEdges, FromSystemViolation, ShowSystem);

    [Fact]
    [Trait("Requirement", "F-5")]
    public void SystemDecimalComingOut_IsTheRoundingOfTheReference() =>
        PropertyLoop.Check(nameof(SystemDecimalComingOut_IsTheRoundingOfTheReference), DecimalGenerators.SystemRangeValues, SystemSeed, Cases, ToSystemViolation, value => value.ToString());

    [Fact]
    [Trait("Requirement", "F-5")]
    public void SystemDecimalComingOutOnTheBoundaries_IsTheRoundingOfTheReference() =>
        DecimalHarness.CheckAll(nameof(SystemDecimalComingOutOnTheBoundaries_IsTheRoundingOfTheReference), DecimalGenerators.SystemRangeEdges, ToSystemViolation, value => value.ToString());

    private static string? TruncationViolation(RefDecimal value)
    {
        CelBindings bindings = DecimalHarness.Bind("a", value);
        string integer = ReferenceDecimalNumbers.ToInt64(value) is { } signed
            ? DecimalHarness.Number(signed)
            : DecimalHarness.ExpectedError(CelErrorCode.ConversionRange);
        string unsigned = ReferenceDecimalNumbers.ToUInt64(value) is { } natural
            ? DecimalHarness.Number(natural)
            : DecimalHarness.ExpectedError(CelErrorCode.ConversionRange);
        return DecimalHarness.Differs("int(a)", integer, DecimalHarness.Observe(ToInt.Evaluate(bindings), DecimalHarness.Number))
            ?? DecimalHarness.Differs("uint(a)", unsigned, DecimalHarness.Observe(ToUint.Evaluate(bindings), DecimalHarness.Number));
    }

    private static string? FromIntViolation(long value) =>
        DecimalHarness.Differs(
            "decimal(i)",
            ReferenceDecimalNumbers.FromInt64(value).ToString(),
            DecimalHarness.Observe(FromInt.Evaluate(new CelBindings().Add("i", value)), DecimalHarness.Parts));

    private static string? FromUintViolation(ulong value) =>
        DecimalHarness.Differs(
            "decimal(u)",
            ReferenceDecimalNumbers.FromUInt64(value).ToString(),
            DecimalHarness.Observe(FromUint.Evaluate(new CelBindings().Add("u", value)), DecimalHarness.Parts));

    private static string? ToDoubleViolation(RefDecimal value)
    {
        string expected = ReferenceDecimalNumbers.ToDoubleBits(value) is { } bits
            ? bits.ToString("X16", CultureInfo.InvariantCulture)
            : DecimalHarness.ExpectedError(CelErrorCode.ConversionRange);
        return DecimalHarness.Differs("double(a)", expected, DecimalHarness.Observe(ToDouble.Evaluate(DecimalHarness.Bind("a", value)), DecimalHarness.DoubleBits));
    }

    private static string? FromDoubleViolation(double value)
    {
        bool finite = double.IsFinite(value);
        string expected = finite
            ? ReferenceDecimalText.FromDouble(value).ToString()
            : DecimalHarness.ExpectedError(CelErrorCode.ConversionRange);
        CelBindings bindings = new CelBindings().Add("d", value);
        string? direct = DecimalHarness.Differs("decimal(d)", expected, DecimalHarness.Observe(FromDouble.Evaluate(bindings), DecimalHarness.Parts));

        // decimal(d) and decimal(string(d)) are the same value with the same digits, for a double that has a text with digits.
        return direct ?? (finite
            ? DecimalHarness.Differs("decimal(string(d))", expected, DecimalHarness.Observe(FromDoubleText.Evaluate(bindings), DecimalHarness.Parts))
            : null);
    }

    private static string? FromSystemViolation(decimal value) =>
        DecimalHarness.Differs(
            "m as Decimal128",
            ReferenceDecimalNumbers.FromSystemDecimal(value).ToString(),
            DecimalHarness.Observe(FromSystem.Evaluate(new CelBindings().Add("m", value)), DecimalHarness.Parts));

    private static string? ToSystemViolation(RefDecimal value)
    {
        string expected = ReferenceDecimalNumbers.ToSystemDecimal(value) is { } parts
            ? parts.ToString()
            : DecimalHarness.ExpectedError(CelErrorCode.ResultConversion);
        string actual = DecimalHarness.Observe(ToSystem.Evaluate(DecimalHarness.Bind("a", value)), static number => ReferenceDecimalNumbers.FromSystemDecimal(number).ToString());
        return DecimalHarness.Differs("a as System.Decimal", expected, actual);
    }

    private static string ShowDouble(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture) + " (bits " + DecimalHarness.DoubleBits(value) + ")";

    private static string ShowSystem(decimal value) => ReferenceDecimalNumbers.FromSystemDecimal(value).ToString();
}
#endif
