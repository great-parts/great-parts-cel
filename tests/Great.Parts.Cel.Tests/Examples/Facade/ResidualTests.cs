using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Evaluation.PartialTestSupport;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// The residual expression: the tree of the program with the known parts replaced by their values, compiled in the same environment
// and evaluated on the full data it gives the result of the program. request = {"user": "alice"} and admins = ["bob"] are known,
// resource is unknown; the full data adds resource = {"owner": "alice", …}.
public sealed class ResidualTests
{
    [Theory]
    [Trait("Requirement", "F-22")]
    [InlineData("request.user == resource.owner || request.user in admins", "\"alice\" == resource.owner")]
    [InlineData("resource.owner == request.user && 1 < 2", "resource.owner == \"alice\"")]
    [InlineData("request.user in admins ? resource.a : resource.b", "resource.b")]
    [InlineData("!(1 > 2) && resource.on", "resource.on")]
    [InlineData("resource.t > timestamp(\"2026-01-01T00:00:00Z\") && 1 < 2", "resource.t > timestamp(\"2026-01-01T00:00:00Z\")")]
    [InlineData("resource.d == duration(\"90s\") + duration(\"30s\")", "resource.d == duration(\"120s\")")]
    [InlineData("resource.n == double(\"NaN\")", "resource.n == double(\"NaN\")")]
    [InlineData("resource.v == optional.of(1).value()", "resource.v == 1")]
    [InlineData("resource.l == [1, 2].map(i, i * 2)", "resource.l == [2, 4]")]
    [InlineData("resource.ok || 1 / 0 == 1", "resource.ok || 1 / 0 == 1")]
    public static void Residual_ReplacesTheKnownPartsAndGivesTheSameResultOnTheFullData(string expression, string residual)
    {
        CelPartialEvalResult<CelValue> result = Evaluate(expression, [P("resource")], Known());

        Assert.True(result.IsUnknown);
        Assert.Equal(residual, CelSyntax.Format(result.GetResidual()));
        Assert.Equal(
            Env.Compile<CelValue>(expression).GetProgramOrThrow().Evaluate(Full(), cancellationToken: CancellationToken.None).GetValueOrThrow().ToString(),
            Env.CompileParsed<CelValue>(result.GetResidual()).GetProgramOrThrow().Evaluate(Full(), cancellationToken: CancellationToken.None).GetValueOrThrow().ToString());
    }

    // A date and a time of day of the calendar set are written as the calls that make them, and the residual compiles in the same environment.
    [Theory]
    [Trait("Requirement", "F-22")]
    [InlineData("resource.d == date('2026-11-19').addDays(12)", "resource.d == date(\"2026-12-01\")")]
    [InlineData("resource.t == time_of_day('09:30')", "resource.t == time_of_day(\"09:30:00\")")]
    [InlineData("resource.t == time_of_day('09:30:00.5')", "resource.t == time_of_day(\"09:30:00.5\")")]
    public static void Residual_WritesADateAndATimeOfDay(string expression, string residual)
    {
        CelEnvironment calendar = Env.ToBuilder().AddExtension(CelExtensions.Calendar(1)).Build().GetEnvironmentOrThrow();

        CelPartialEvalResult<CelValue> result = Evaluate(expression, [P("resource")], Known(), environment: calendar);

        Assert.True(result.IsUnknown);
        Assert.Equal(residual, CelSyntax.Format(result.GetResidual()));
        Assert.True(calendar.CompileParsed<CelValue>(result.GetResidual()).IsSuccess);
    }

    [Theory]
    [Trait("Requirement", "F-22")]
    [InlineData("resource.items.all(i, i > size(request.user))")]
    [InlineData("[1, 2].all(i, i < resource.max)")]
    public static void Residual_OfAComprehension_ReplacesOnlyTheRange(string expression)
    {
        CelPartialEvalResult<CelValue> result = Evaluate(expression, [P("resource")], Known());

        Assert.True(result.IsUnknown);
        Assert.Equal(CelSyntax.Format(ParseOf(expression)), CelSyntax.Format(result.GetResidual()));
        Assert.Equal(
            Env.Compile<CelValue>(expression).GetProgramOrThrow().Evaluate(Full(), cancellationToken: CancellationToken.None).GetValueOrThrow().ToString(),
            Env.CompileParsed<CelValue>(result.GetResidual()).GetProgramOrThrow().Evaluate(Full(), cancellationToken: CancellationToken.None).GetValueOrThrow().ToString());
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void Residual_OfInOfAnEmptyList_IsFalseThoughTheResultIsUnknown()
    {
        const string Expression = "resource.x in []";
        CelPartialEvalResult<CelValue> result = Evaluate(Expression, [P("resource")], Known());

        Assert.True(result.IsUnknown);
        Assert.Equal("false", CelSyntax.Format(result.GetResidual()));
        Assert.False(Env.Compile<CelValue>(Expression).GetProgramOrThrow().Evaluate(Full(), cancellationToken: CancellationToken.None).IsSuccess);
        Assert.False(Env.CompileParsed<CelValue>(result.GetResidual()).GetProgramOrThrow().Evaluate(Full(), cancellationToken: CancellationToken.None).GetValueOrThrow().TryGetBoolean(out bool value) && value);
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void Residual_DropsTheErrorOfABranchThatTheKnownPartRemoved()
    {
        const string Expression = "(1 / 0 in []) || b";
        CelPartialEvalResult<CelValue> result = Evaluate(Expression, [P("b")], Known());
        CelBindings full = Known().Add("b", false);

        Assert.True(result.IsUnknown);
        Assert.Equal("b", CelSyntax.Format(result.GetResidual()));
        Assert.Equal(CelErrorCode.DivisionByZero, Assert.Single(Env.Compile<CelValue>(Expression).GetProgramOrThrow().Evaluate(full, cancellationToken: CancellationToken.None).Errors).Code);
        Assert.False(Env.CompileParsed<bool>(result.GetResidual()).GetProgramOrThrow().Evaluate(full, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void Residual_OfAKnownResult_IsItsConstant()
    {
        CelPartialEvalResult<CelValue> result = Evaluate("false && resource.ok", [P("resource")], Known());

        Assert.True(result.IsSuccess);
        Assert.Equal("false", CelSyntax.Format(result.GetResidual()));
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void Residual_OfAResultWithAnError_IsRefused()
    {
        CelPartialEvalResult<CelValue> result = Evaluate("1 / 0 == resource.x", [P("resource")], Known());

        Assert.False(result.IsUnknown);
        CelException exception = Assert.Throws<CelException>(() => result.GetResidual());
        Assert.Equal(CelErrorCode.DivisionByZero, Assert.Single(exception.Errors).Code);
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void Residual_KeepsThePositionsOfTheProgram()
    {
        const string Expression = "request.user == resource.owner";
        CelEnvironment withoutResource = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable("request", CelType.MapOf(CelType.String, CelType.Dyn))
            .Build()
            .GetEnvironmentOrThrow();
        CelPartialEvalResult<CelValue> result = Evaluate(Expression, [P("resource")], Known());

        CelCompileResult<CelValue> compiled = withoutResource.CompileParsed<CelValue>(result.GetResidual());

        CelError error = Assert.Single(compiled.Errors);
        Assert.Equal(CelErrorCode.UndeclaredReference, error.Code);
        Assert.Equal(Expression.IndexOf("resource", System.StringComparison.Ordinal), error.Location.Offset);
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void Residual_IsANewTree()
    {
        CelPartialEvalResult<CelValue> result = Evaluate("resource.a == 1 && 1 < 2", [P("resource")], Known());
        CelParsedExpr first = result.GetResidual();
        CelParsedExpr second = result.GetResidual();

        Assert.NotSame(first, second);
        Assert.NotSame(first.Root, second.Root);
        Assert.Equal(CelSyntax.Format(first), CelSyntax.Format(second));
        Assert.NotSame(Env.Parse("resource.a == 1 && 1 < 2").GetParsedExprOrThrow().Root, first.Root);
    }

#if NET11_0_OR_GREATER
    [Fact]
    [Trait("Requirement", "F-22")]
    public void Residual_OfADecimal_IsTheConversionOfItsText()
    {
        CelEnvironment decimals = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable("resource", CelType.MapOf(CelType.String, CelType.Dyn))
            .AddExtension(CelExtensions.Decimal(1))
            .Build()
            .GetEnvironmentOrThrow();
        const string Expression = "decimal(\"1.5\") < resource.price";
        CelBindings full = new CelBindings().Add("resource", Map(("price", CelValue.FromDecimal(2m))));

        CelPartialEvalResult<CelValue> result = Evaluate(Expression, [P("resource")], new CelBindings(), environment: decimals);

        Assert.Equal(Expression, CelSyntax.Format(result.GetResidual()));
        Assert.True(decimals.CompileParsed<bool>(result.GetResidual()).GetProgramOrThrow().Evaluate(full, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }
#endif

    private static CelParsedExpr ParseOf(string expression) => Env.Parse(expression).GetParsedExprOrThrow();

    private static CelBindings Known() => new CelBindings()
        .Add("request", Map(("user", Str("alice"))))
        .Add("admins", CelValue.FromList(CelList.Create(Str("bob"))));

    // The full data: the known part and the resource.
    private static CelBindings Full() => Known().Add("resource", Map(
        ("owner", Str("alice")),
        ("a", Int(10)),
        ("b", Int(20)),
        ("on", CelValue.FromBoolean(true)),
        ("ok", CelValue.FromBoolean(true)),
        ("v", Int(1)),
        ("max", Int(3)),
        ("t", CelValue.FromTimestamp(new CelTimestamp(1_800_000_000, 0))),
        ("d", CelValue.FromDuration(new CelDuration(120, 0))),
        ("n", CelValue.FromDouble(double.NaN)),
        ("l", IntList(2, 4)),
        ("items", IntList(5, 6))));
}
