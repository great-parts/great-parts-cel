using System;
#if NET11_0_OR_GREATER
using System.Numerics;
#endif
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Declarations;

// A function of the application whose parameter or result is a decimal, in an environment without the set that declares the type: the
// overload is TypeNotDeclared of the kind overload, and the hint names the set to add, on both targets. The examples of the set have the variable; the delegate form and the declaration of an overload are
// another path of the same check. A type of the kind decimal at a depth is named whole, as it is written.
public sealed class DecimalFunctionDeclarationTests
{
    private const string Hint = "; add the extension set 'decimal'";
    private const string SystemDecimalType = "decimal";

    private static CelEnvironmentBuilder Builder() => new(CelStandardLibrary.V1);

    [Fact]
    public static void DelegateWithASystemDecimalParameterOrResult_IsTypeNotDeclared()
    {
        AssertOverloadError(Builder().AddFunction<decimal, long>("cents", _ => 0), SystemDecimalType);
        AssertOverloadError(Builder().AddFunction<long, decimal>("price", _ => 0m), SystemDecimalType);
        AssertOverloadError(Builder().AddFunction<decimal, decimal, bool>("same", (left, right) => left == right), SystemDecimalType);
    }

    // The type with a question mark is the CEL type decimal too.
    [Fact]
    public static void DelegateWithANullableSystemDecimal_HasTheHintToo()
    {
        CelBuildResult result = Builder().AddFunction<decimal?, bool>("known", value => value.HasValue).Build();

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.TypeNotDeclared, CelErrorStage.Build), (error.Code, error.Stage));
        Assert.EndsWith(Hint, error.Message, StringComparison.Ordinal);
    }

    private static void AssertOverloadError(CelEnvironmentBuilder builder, string type)
    {
        CelBuildResult result = builder.Build();

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.TypeNotDeclared, CelErrorStage.Build), (error.Code, error.Stage));
        Assert.StartsWith("overload '", error.Message, StringComparison.Ordinal);
        Assert.EndsWith("' has type '" + type + "', which no type source or extension of the environment declares" + Hint, error.Message, StringComparison.Ordinal);
        Assert.Null(error.SourceLocation);
    }

    private static readonly CelFunctionImplementation Nothing = _ => CelValue.Null;

#if NET11_0_OR_GREATER
    [Fact]
    public static void DelegateWithADecimal128ParameterOrResult_IsTypeNotDeclared()
    {
        AssertOverloadError(Builder().AddFunction<Decimal128, long>("cents", _ => 0), "decimal");
        AssertOverloadError(Builder().AddFunction<long, Decimal128>("price", _ => default), "decimal");
    }
#endif

    [Fact]
    public static void OverloadWithAListOrAMapOfDecimals_IsNamedByTheWholeType()
    {
        AssertOverloadError(
            Builder()
                .AddFunction(new CelFunctionDecl("sum", CelOverloadDecl.Global("sum_list", [CelType.ListOf(CelType.Decimal)], CelType.Int)))
                .AddFunctionImplementation("sum_list", Nothing),
            "list(decimal)");
        AssertOverloadError(
            Builder()
                .AddFunction(new CelFunctionDecl("rates", CelOverloadDecl.Global("rates_none", [], CelType.MapOf(CelType.String, CelType.Decimal))))
                .AddFunctionImplementation("rates_none", Nothing),
            "map(string, decimal)");
    }
}
