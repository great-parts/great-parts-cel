#if NET11_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// The result rows of decimal of the type mapping table (the page of the values, "decimal"; the type mapping, the rows of the table of
// the result): a decimal becomes Decimal128 exactly, with its digits, its exponent and the sign of its zero; it becomes System.Decimal
// rounded half to even to 28 places and 96 bits, with the scale of the value when it fits, and is ResultConversion (3015) at the root,
// "result of type 'decimal' cannot be converted to .NET type 'System.Decimal'", when it is out of the range of System.Decimal (the
// value 1E+29, or one above 2^96 - 1); the nullable forms take the value and give null for null; object takes a boxed Decimal128
// and CelValue the value. There is no row for double: Compile<double> of a decimal expression is the compile error
// ExpectedTypeMismatch at the root, and nothing turns a decimal into a double but the call double(x). The static null is assignable to
// decimal, so Compile<Decimal128>("null") compiles and is ResultConversion with the type null_type, and the nullable form takes it;
// a static type that is not assignable is ExpectedTypeMismatch; a dyn value of another kind is ResultConversion at the root. The
// texts of these errors name the types and are the library's.
[Trait("Requirement", "+R-10")]
[Trait("Requirement", "F-5")]
public sealed class DecimalResultRowTests
{
    private readonly CelEnvironment environment = DecimalExample.Builder()
        .AddVariable("d", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void Decimal_BecomesDecimal128Exactly()
    {
        Assert.Equal((false, (UInt128)150, -2), Parts(Evaluate<Decimal128>("1.50m").GetValueOrThrow()));
        Assert.Equal((true, (UInt128)0, 0), Parts(Evaluate<Decimal128>("-0m").GetValueOrThrow()));
        Assert.Equal((false, (UInt128)1, 2), Parts(Evaluate<Decimal128>("1e2m").GetValueOrThrow()));
        Assert.Equal((false, UInt128.Parse("3333333333333333333333333333333333", CultureInfo.InvariantCulture), -34), Parts(Evaluate<Decimal128>("decimal(1) / decimal(3)").GetValueOrThrow()));
        Assert.Equal((true, (UInt128)15, -1), Parts(Evaluate<Decimal128>("-1.5m").GetValueOrThrow()));
        Assert.Equal((false, (UInt128)15, -1), Parts(Run<Decimal128>("d", new CelBindings().Add("d", CelValue.FromDecimal(DecimalExample.Make(15, -1)))).GetValueOrThrow()));
    }

    [Fact]
    public void Decimal_BecomesSystemDecimalWithItsScale()
    {
        Assert.Equal("1.50", Evaluate<decimal>("1.50m").GetValueOrThrow().ToString(CultureInfo.InvariantCulture));
        Assert.Equal("100", Evaluate<decimal>("100m").GetValueOrThrow().ToString(CultureInfo.InvariantCulture));
        Assert.Equal("100", Evaluate<decimal>("1e2m").GetValueOrThrow().ToString(CultureInfo.InvariantCulture));
        Assert.Equal("-12.34", Evaluate<decimal>("-12.34m").GetValueOrThrow().ToString(CultureInfo.InvariantCulture));
        Assert.Equal(decimal.MaxValue, Evaluate<decimal>("79228162514264337593543950335m").GetValueOrThrow());
        Assert.Equal(decimal.MinValue, Evaluate<decimal>("-79228162514264337593543950335m").GetValueOrThrow());
    }

    // 28 places and 96 bits: the digits that do not fit are rounded away, half to even, and a value that vanishes is zero.
    [Fact]
    public void DecimalWithMoreDigitsThanSystemDecimalHas_IsRoundedHalfToEven()
    {
        Assert.Equal("0.0000000000000000000000000002", Evaluate<decimal>("1.5e-28m").GetValueOrThrow().ToString(CultureInfo.InvariantCulture));
        Assert.Equal("0.0000000000000000000000000002", Evaluate<decimal>("2.5e-28m").GetValueOrThrow().ToString(CultureInfo.InvariantCulture));
        Assert.Equal("0.0000000000000000000000000004", Evaluate<decimal>("3.5e-28m").GetValueOrThrow().ToString(CultureInfo.InvariantCulture));
        Assert.Equal("0.0000000000000000000000000000", Evaluate<decimal>("5e-29m").GetValueOrThrow().ToString(CultureInfo.InvariantCulture));
        Assert.Equal("0.0000000000000000000000000000", Evaluate<decimal>("1e-30m").GetValueOrThrow().ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void DecimalOutOfTheRangeOfSystemDecimal_IsResultConversionAtTheRoot()
    {
        foreach (string expression in new[] { "1e29m", "-1e29m", "79228162514264337593543950336m", "-79228162514264337593543950336m", "1e6111m" })
        {
            CelError error = Assert.Single(Evaluate<decimal>(expression).Errors);

            Assert.Equal((CelErrorCode.ResultConversion, CelErrorStage.Evaluate, 0), (error.Code, error.Stage, error.Location.Offset));
            Assert.Equal("result of type 'decimal' cannot be converted to .NET type 'System.Decimal'", error.Message);
        }

        Assert.True(Evaluate<Decimal128>("1e29m").IsSuccess);
        Assert.True(Evaluate<CelValue>("1e29m").IsSuccess);
    }

    [Fact]
    public void NullableForms_TakeTheValueOrNull()
    {
        Assert.Equal((UInt128)15, Parts(Evaluate<Decimal128?>("1.5m").GetValueOrThrow()!.Value).Coefficient);
        Assert.Equal(1.5m, Evaluate<decimal?>("1.5m").GetValueOrThrow());
        Assert.Null(Run<Decimal128?>("d", new CelBindings().Add("d", CelValue.Null)).GetValueOrThrow());
        Assert.Null(Run<decimal?>("d", new CelBindings().Add("d", CelValue.Null)).GetValueOrThrow());
        Assert.Null(Evaluate<Decimal128?>("null").GetValueOrThrow());
        Assert.Null(Evaluate<decimal?>("null").GetValueOrThrow());
    }

    // A static null is assignable to decimal: the program compiles, and a null value is no Decimal128, so it is ResultConversion at the
    // root, and {celType} is null_type. A type that takes no null, double, is the compile error instead.
    [Fact]
    [Trait("Requirement", "F-4")]
    public void LiteralNull_CompilesAndIsResultConversionForTheFormsWithoutNull()
    {
        CelError error = Assert.Single(Evaluate<Decimal128>("null").Errors);

        Assert.Equal((CelErrorCode.ResultConversion, CelErrorStage.Evaluate, 0), (error.Code, error.Stage, error.Location.Offset));
        Assert.Equal("result of type 'null_type' cannot be converted to .NET type 'System.Numerics.Decimal128'", error.Message);
        Assert.Equal(CelErrorCode.ResultConversion, Assert.Single(Evaluate<decimal>("null").Errors).Code);
        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, Assert.Single(environment.Compile<double>("null").Errors).Code);
    }

    [Fact]
    public void CelValueAndObject_TakeTheValueAsItIs()
    {
        CelValue value = Evaluate<CelValue>("1.50m").GetValueOrThrow();
        Assert.Equal(CelValueKind.Decimal, value.Kind);
        Assert.True(value.TryGetDecimal(out Decimal128 number));
        Assert.Equal((false, (UInt128)150, -2), Parts(number));

        Assert.Equal((false, (UInt128)150, -2), Parts(Assert.IsType<Decimal128>(Evaluate<object>("1.50m").GetValueOrThrow())));
        IReadOnlyList<object?> list = Assert.IsAssignableFrom<IReadOnlyList<object?>>(Evaluate<object>("[1.5m, 2.50m]").GetValueOrThrow());
        Assert.Equal((false, (UInt128)15, -1), Parts(Assert.IsType<Decimal128>(list[0])));
        Assert.Equal((false, (UInt128)250, -2), Parts(Assert.IsType<Decimal128>(list[1])));
    }

    // No row turns a decimal into a double, a long or a string: the static type is the error, at the root.
    [Fact]
    [Trait("Requirement", "F-4")]
    public void StaticTypeThatIsNotAssignable_IsExpectedTypeMismatchAtTheRoot()
    {
        foreach (CelError error in new[]
        {
            Assert.Single(environment.Compile<double>("1.5m").Errors),
            Assert.Single(environment.Compile<long>("1.5m").Errors),
            Assert.Single(environment.Compile<string>("1.5m").Errors),
            Assert.Single(environment.Compile<Decimal128>("1.5").Errors),
            Assert.Single(environment.Compile<decimal>("1").Errors),
            Assert.Single(environment.Compile<Decimal128>("'1.5'").Errors),
        })
        {
            Assert.Equal((CelErrorCode.ExpectedTypeMismatch, CelErrorStage.Check), (error.Code, error.Stage));
        }

        Assert.True(environment.Compile<Decimal128>("decimal(1)").IsSuccess);
        Assert.True(environment.Compile<double>("double(1.5m)").IsSuccess);
        Assert.True(environment.Compile<Decimal128>("d").IsSuccess);
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void DynValueOfAnotherKind_IsResultConversionAtTheRoot()
    {
        CelBindings bindings = new CelBindings().Add("d", 5L);

        CelError error = Assert.Single(Run<Decimal128>("  d", bindings).Errors);

        Assert.Equal((CelErrorCode.ResultConversion, CelErrorStage.Evaluate, 2), (error.Code, error.Stage, error.Location.Offset));
        Assert.Equal(CelErrorCode.ResultConversion, Assert.Single(Run<decimal>("  d", bindings).Errors).Code);
        Assert.Equal((false, (UInt128)15, -1), Parts(Run<Decimal128>("  d", new CelBindings().Add("d", CelValue.FromDecimal(DecimalExample.Make(15, -1)))).GetValueOrThrow()));
    }

    private static (bool Negative, UInt128 Coefficient, int Exponent) Parts(Decimal128 value) => DecimalExample.Parts(value);

    private CelEvalResult<T> Evaluate<T>(string expression) => Run<T>(expression, new CelBindings());

    private CelEvalResult<T> Run<T>(string expression, CelBindings bindings) =>
        environment.Compile<T>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);
}
#endif
