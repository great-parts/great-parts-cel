#if NET11_0_OR_GREATER
using System.Numerics;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Rows;
using Great.Parts.Cel.Tests.Examples.Library;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Generated.GeneratedExample;

namespace Great.Parts.Cel.Tests.Examples.Generated;

// The members of the .NET types Decimal128 and System.Decimal of a type that the generator described (the sample class DecimalValues of
// the samples of the generator, members Exact, Money and OptMoney, whose names are the camel-case names of the default policy). Such
// a member is a field of the CEL type decimal. With the set decimal in the environment the field is read exactly — the digits, the exponent
// and the sign of a zero of a Decimal128, the scale of a System.Decimal — and a nullable member is the type of the value, null being
// UnexpectedNull at the selection and has() false. Without the set the field is not available to the expression: FieldNotAccessible
// (2007) at the check, at the "." of the selection and at the "(" of has(), with the text "the CEL type decimal needs the extension set
// 'decimal'", although the type and the variable of it build. A Decimal128 member that is a NaN or an infinity is the error ConversionRange at the
// selection, "NaN is out of the range of decimal", "+Inf ...", "-Inf ...". The members of the .NET type System.Decimal that the
// generator excludes in net10.0 (a warning of the generator) are in the examples of the generator.
[Trait("Requirement", "F-2")]
[Trait("Requirement", "F-5")]
public sealed class GeneratedDecimalTests
{
    private const string Reason = "is not available to expressions: the CEL type decimal needs the extension set 'decimal'";

    private static readonly CelEnvironment WithTheSet = Builder(DecimalContext.Default)
        .AddExtension(CelExtensions.Decimal(1))
        .EnableDecimalLiterals()
        .AddVariable<DecimalValues>("v")
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelEnvironment WithoutTheSet = Builder(DecimalContext.Default)
        .AddVariable<DecimalValues>("v")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void MembersOfDecimal128AndSystemDecimal_AreReadAsDecimalFields()
    {
        var values = new DecimalValues { Exact = DecimalExample.Make(150, -2), Money = 2.50m, OptMoney = 3.5m };
        CelBindings bindings = new CelBindings().Add("v", values);

        Assert.Equal("1.50", Evaluate<string>(WithTheSet, "string(v.exact)", bindings));
        Assert.Equal("2.50", Evaluate<string>(WithTheSet, "string(v.money)", bindings));
        Assert.Equal("3.5", Evaluate<string>(WithTheSet, "string(v.optMoney)", bindings));
        Assert.True(Evaluate<bool>(WithTheSet, "v.exact == 1.5m && v.money == 2.5m && v.optMoney == 3.5m && v.exact < v.money", bindings));
        Assert.True(Evaluate<bool>(WithTheSet, "type(v.exact) == decimal && type(v.money) == decimal && type(v.optMoney) == decimal", bindings));
        Assert.True(Evaluate<bool>(WithTheSet, "has(v.exact) && has(v.money) && has(v.optMoney)", bindings));
        Assert.Equal("-0", Evaluate<string>(WithTheSet, "string(v.exact)", new CelBindings().Add("v", new DecimalValues { Exact = DecimalExample.Make(0, 0, negative: true) })));
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void NullOfANullableMember_IsUnexpectedNullAtTheSelection_AndHasIsFalse()
    {
        CelBindings bindings = new CelBindings().Add("v", new DecimalValues { OptMoney = null });

        AssertError(EvaluationError(WithTheSet, "v.optMoney", bindings), CelErrorCode.UnexpectedNull, 1);
        Assert.True(Evaluate<bool>(WithTheSet, "!has(v.optMoney) && has(v.money)", bindings));
    }

    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("v.exact", "exact", 1)]
    [InlineData("v.money", "money", 1)]
    [InlineData("v.optMoney", "optMoney", 1)]
    [InlineData("has(v.exact)", "exact", 3)]
    [InlineData("has(v.money)", "money", 3)]
    public void SelectionWithoutTheSet_IsFieldNotAccessibleAtTheSelection(string text, string field, int offset)
    {
        CelError error = CompileError(WithoutTheSet, text);

        AssertError(error, CelErrorCode.FieldNotAccessible, offset, "field '" + field + "' of type 'samples.DecimalValues' " + Reason);
        Assert.Equal(CelErrorStage.Check, error.Stage);
    }

    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("NaN")]
    [InlineData("+Inf")]
    [InlineData("-Inf")]
    public void NaNOrInfinityMember_IsConversionRangeAtTheSelection(string text)
    {
        Decimal128 value = text switch
        {
            "NaN" => Decimal128.NaN,
            "+Inf" => Decimal128.PositiveInfinity,
            _ => Decimal128.NegativeInfinity,
        };

        CelError error = EvaluationError(WithTheSet, "v.exact", new CelBindings().Add("v", new DecimalValues { Exact = value }));

        AssertError(error, CelErrorCode.ConversionRange, 1, text + " is out of the range of decimal");
    }
}
#endif
