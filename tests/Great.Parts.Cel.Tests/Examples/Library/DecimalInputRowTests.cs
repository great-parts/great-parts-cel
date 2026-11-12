#if NET11_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The input rows of decimal of the type mapping (the page of the values, "decimal", "Conversion of decimal values"; the page of the extension
// set decimal): a binding of the .NET kind becomes the CEL value of the row. Decimal128 and System.Decimal are the type decimal and
// are exact: the digits, the exponent and the sign of a zero are kept, so a Decimal128 of 150 with the exponent -2 is "1.50" and a
// System.Decimal of scale 2 is "1.50" too, and the 29 digits and the scale of at most 28 that System.Decimal has fit the 34 digits.
// The nullable forms have the type of the value, and a null binding of such a variable is BindingTypeMismatch at the identifier. A
// NaN and an infinity of Decimal128 are no CEL value: CelBindings.Add refuses them with ArgumentException; a boxed one in a place
// of the type dyn is the error ConversionRange at the node that reads it. A decimal of the type dyn — a boxed System.Decimal or
// Decimal128, an element of a CelList or a value of a CelMap, an element of a collection of object — is a decimal wherever the
// environment has the set, and works as one, in a comprehension too; in an environment without the set the same value is
// TypeNotAccessible (3012) at the node that reads it (DecimalWithoutSetTests has the System.Decimal in both targets). A typed
// collection of decimals is outside the closed list (DecimalSetBuildTests). Rows and conventions are those of the examples of
// time (TimeInputRowTests).
[Trait("Requirement", "+R-10")]
[Trait("Requirement", "F-2")]
[Trait("Requirement", "F-5")]
public sealed class DecimalInputRowTests
{
    private static readonly string[] Doubled = ["3.0", "5.0"];

    private static readonly UInt128 MaxCoefficient = UInt128.Parse(DecimalExample.Max34, CultureInfo.InvariantCulture);

    private readonly CelEnvironment environment = DecimalExample.Builder()
        .AddVariable<Decimal128>("a")
        .AddVariable<decimal>("b")
        .AddVariable<Decimal128?>("na")
        .AddVariable<decimal?>("nb")
        .AddVariable("d", CelType.Dyn)
        .AddVariable("l", CelType.ListOf(CelType.Dyn))
        .AddVariable("m", CelType.MapOf(CelType.String, CelType.Dyn))
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelEnvironment withoutTheSet = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("d", CelType.Dyn)
        .AddVariable("l", CelType.ListOf(CelType.Dyn))
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void Decimal128_IsExact_WithItsDigitsAndItsExponent()
    {
        (Decimal128 Value, string Text)[] cases =
        [
            (DecimalExample.Make(150, -2), "1.50"),
            (DecimalExample.Make(15, -1), "1.5"),
            (DecimalExample.Make(0, 0), "0"),
            (DecimalExample.Make(0, 0, negative: true), "-0"),
            (DecimalExample.Make(0, -1), "0.0"),
            (DecimalExample.Make(1, 2), "1E+2"),
            (DecimalExample.Make(1, -6176), "1E-6176"),
            (DecimalExample.Make(MaxCoefficient, 6111), "9.999999999999999999999999999999999E+6144"),
            (DecimalExample.Make(MaxCoefficient, 0, negative: true), "-9999999999999999999999999999999999"),
        ];

        foreach ((Decimal128 value, string text) in cases)
        {
            Assert.Equal(text, Evaluate<string>("string(a)", new CelBindings().Add("a", value)));
        }

        Assert.True(Evaluate<bool>("a == 1.5m && a > 1m && type(a) == decimal", new CelBindings().Add("a", DecimalExample.Make(150, -2))));
    }

    [Fact]
    public void SystemDecimal_IsExact_WithItsScale()
    {
        (decimal Value, string Text)[] cases =
        [
            (1.50m, "1.50"),
            (1.5m, "1.5"),
            (0m, "0"),
            (100m, "100"),
            (new decimal(0, 0, 0, true, 2), "-0.00"),
            (decimal.MaxValue, "79228162514264337593543950335"),
            (decimal.MinValue, "-79228162514264337593543950335"),
            (0.0000000000000000000000000001m, "1E-28"),
            (7.9228162514264337593543950335m, "7.9228162514264337593543950335"),
        ];

        foreach ((decimal value, string text) in cases)
        {
            Assert.Equal(text, Evaluate<string>("string(b)", new CelBindings().Add("b", value)));
        }

        Assert.True(Evaluate<bool>("b == 1.5m && b + 1m == 2.5m", new CelBindings().Add("b", 1.50m)));
    }

    [Fact]
    public void NullableOfDecimal_HasTheTypeOfTheValue_AndANullBindingIsBindingTypeMismatch()
    {
        Assert.True(Evaluate<bool>("na == 1.5m", new CelBindings().Add<Decimal128?>("na", DecimalExample.Make(15, -1))));
        Assert.True(Evaluate<bool>("nb == 1.5m", new CelBindings().Add<decimal?>("nb", 1.5m)));

        foreach ((string name, CelBindings bindings) in new[]
        {
            ("na", new CelBindings().Add<Decimal128?>("na", null)),
            ("nb", new CelBindings().Add<decimal?>("nb", null)),
        })
        {
            CelError error = Assert.Single(Run("[" + name + "]", bindings).Errors);

            Assert.Equal((CelErrorCode.BindingTypeMismatch, CelErrorStage.Evaluate, 1), (error.Code, error.Stage, error.Location.Offset));
        }
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void NaNAndInfinityOfDecimal128_AreRefusedByAdd()
    {
        Assert.Throws<ArgumentException>(() => new CelBindings().Add("a", Decimal128.NaN));
        Assert.Throws<ArgumentException>(() => new CelBindings().Add("a", Decimal128.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => new CelBindings().Add("a", Decimal128.NegativeInfinity));
    }

    // In a place of the type dyn the boxed value is read lazily: the error is that of the node that reads it.
    [Fact]
    [Trait("Requirement", "F-4")]
    public void BoxedNaNOrInfinityInAPlaceOfTheTypeDyn_IsConversionRangeAtTheNodeThatReadsIt()
    {
        foreach ((Decimal128 value, string text) in new[] { (Decimal128.NaN, "NaN"), (Decimal128.PositiveInfinity, "+Inf"), (Decimal128.NegativeInfinity, "-Inf") })
        {
            CelError error = Assert.Single(Run("d", new CelBindings().Add<object>("d", value)).Errors);

            Assert.Equal((CelErrorCode.ConversionRange, CelErrorStage.Evaluate, 0), (error.Code, error.Stage, error.Location.Offset));
            Assert.Equal(text + " is out of the range of decimal", error.Message);
        }
    }

    // A decimal of the type dyn, whatever put it there: a boxed System.Decimal, a boxed Decimal128, a CelValue, an element of a CelList
    // and a value of a CelMap, an element of a collection of object. It is a decimal with its digits, it is compared and computed as
    // one, and a comprehension over such a list binds it.
    [Fact]
    public void DecimalInAPlaceOfTheTypeDyn_IsADecimalWhenTheEnvironmentHasTheSet()
    {
        Assert.Equal("1.50", Evaluate<string>("string(d)", new CelBindings().Add<object>("d", 1.50m)));
        Assert.Equal("1.50", Evaluate<string>("string(d)", new CelBindings().Add<object>("d", DecimalExample.Make(150, -2))));
        Assert.Equal("1.50", Evaluate<string>("string(d)", new CelBindings().Add("d", CelValue.FromDecimal(DecimalExample.Make(150, -2)))));
        Assert.True(Evaluate<bool>("d == 1.5m && d + 1m == 2.5m && type(d) == decimal && d > 1m", new CelBindings().Add<object>("d", 1.50m)));
        Assert.True(Evaluate<bool>("d == 1.5m && d != 1", new CelBindings().Add<object>("d", DecimalExample.Make(15, -1))));

        CelBindings list = new CelBindings().Add<IReadOnlyList<object?>>("l", new object?[] { DecimalExample.Make(15, -1), 2.5m });
        Assert.True(Evaluate<bool>("l[0] < l[1] && l[0] + l[1] == 4m", list));
        Assert.Equal("4.0", Evaluate<string>("string(l[0] + l[1])", list));
        Assert.Equal(Doubled, Evaluate<IReadOnlyList<string>>("l.map(x, string(x * 2m))", list));

        CelBindings values = new CelBindings()
            .Add("l", CelList.Create(CelValue.FromDecimal(DecimalExample.Make(15, -1)), CelValue.FromDecimal(DecimalExample.Make(25, -1))))
            .Add("m", CelMap.Create([new KeyValuePair<CelValue, CelValue>(CelValue.FromString("a"), CelValue.FromDecimal(DecimalExample.Make(150, -2)))]));
        Assert.True(Evaluate<bool>("l[0] + l[1] == 4m && m.a == 1.5m && m['a'] > 1m", values));
        Assert.Equal("1.50", Evaluate<string>("string(m.a)", values));
    }

    // Without the set the same values are an error where they are read, and the node that reads is the place: the identifier for the first
    // read of the variable, the "[" for an element. A Decimal128 is named by its own type name.
    [Fact]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "R-5")]
    public void DecimalInAPlaceOfTheTypeDynWithoutTheSet_IsTypeNotAccessibleAtTheNodeThatReadsIt()
    {
        const string Tail = "is not available to expressions: no type source of the environment describes it";
        CelBindings boxed = new CelBindings().Add<object>("d", DecimalExample.Make(15, -1));
        CelBindings value = new CelBindings().Add("d", CelValue.FromDecimal(DecimalExample.Make(15, -1)));
        CelBindings list = new CelBindings().Add("l", CelList.Create(CelValue.FromDecimal(DecimalExample.Make(15, -1))));

        Assert.Empty(ExtensionRows.CheckErrors(withoutTheSet, "Evaluate | ^d | TypeNotAccessible | .NET type 'System.Numerics.Decimal128' " + Tail, boxed));
        Assert.Empty(ExtensionRows.CheckErrors(withoutTheSet, "Evaluate | ^d | TypeNotAccessible | .NET type 'System.Numerics.Decimal128' " + Tail, value));
        Assert.Empty(ExtensionRows.CheckErrors(withoutTheSet, "Evaluate | l^[0] | TypeNotAccessible | .NET type 'System.Numerics.Decimal128' " + Tail, list));
    }

    private CelEvalResult<CelValue> Run(string expression, CelBindings bindings) =>
        environment.Compile<CelValue>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);

    private T Evaluate<T>(string expression, CelBindings bindings) =>
        environment.Compile<T>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
}
#endif
