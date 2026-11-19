using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Orders;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Diagnostics;

// The parts of an error as separate values: Subject, ExpectedType, ActualType, by the section "Parts of an error" of docs/error-codes.md. One
// example per code that an expression or a builder reaches without an application function, a JSON description or protobuf; a
// part the column does not name is null. Subject is the name without quotes; a .NET type is its full name; an operator is its
// function name, such as _+_.
[Trait("Requirement", "F-11")]
[Trait("Requirement", "F-8")]
public sealed class ErrorPartsTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder()
        .AddTypes(ShopContext.Default)
        .AddVariable<long>("x")
        .AddVariable<string>("s")
        .AddVariable("l", CelType.ListOf(CelType.Int))
        .AddVariable("m", CelType.MapOf(CelType.String, CelType.Int))
        .AddVariable("d", CelType.Dyn)
        .AddVariable<Order>("order")
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly long[] Numbers = [1, 2];

    private static readonly CelBindings Bindings = new CelBindings()
        .Add("x", 7L)
        .Add("s", "abc")
        .Add("l", Numbers)
        .Add("m", new System.Collections.Generic.Dictionary<string, long> { ["a"] = 1 })
        .Add("d", "text")
        .Add("order", new Order());

    [Theory]
    [InlineData("x +", CelErrorCode.SyntaxError, null)]
    [InlineData("x + package", CelErrorCode.ReservedIdentifier, "package")]
    [InlineData("l.all(1, true)", CelErrorCode.InvalidMacroCall, "all")]
    [InlineData("totl > 1", CelErrorCode.UndeclaredReference, "totl")]
    [InlineData("size(1, 2) == 0", CelErrorCode.NoMatchingOverload, "size")]
    [InlineData("x + s", CelErrorCode.NoMatchingOverload, "_+_")]
    public static void CompileError_HasTheSubjectAndNoTypes(string expression, CelErrorCode code, string? subject)
    {
        CelError error = Assert.Single(Environment.Compile<CelValue>(expression).Errors);

        AssertParts(error, code, subject, null, null);
    }

    [Fact]
    public void ExpectedTypeMismatch_HasBothTypes()
    {
        AssertParts(Assert.Single(Environment.Compile<bool>("x").Errors), CelErrorCode.ExpectedTypeMismatch, null, CelType.Bool, CelType.Int);
    }

    [Fact]
    public void NoSuchField_HasTheFieldAndTheTypeOfTheOperand()
    {
        AssertParts(Assert.Single(Environment.Compile<CelValue>("order.totl").Errors), CelErrorCode.NoSuchField, "totl", null, CelType.ObjectNamed("shop.Order"));
    }

    [Fact]
    public void InvalidComprehensionRange_HasTheTypeOfTheRange()
    {
        AssertParts(Assert.Single(Environment.Compile<CelValue>("x.all(i, true)").Errors), CelErrorCode.InvalidComprehensionRange, null, null, CelType.Int);
    }

    [Theory]
    [InlineData("x / 0", CelErrorCode.DivisionByZero, null)]
    [InlineData("l[5]", CelErrorCode.IndexOutOfRange, null)]
    [InlineData("m['z']", CelErrorCode.NoSuchKey, null)]
    [InlineData("9223372036854775807 + x", CelErrorCode.Overflow, null)]
    [InlineData("int('abc')", CelErrorCode.InvalidArgument, "int")]
    [InlineData("d + 1", CelErrorCode.EvalNoMatchingOverload, "_+_")]
    [InlineData("timestamp('2026-01-01T00:00:00Z').getHours('Mars/Base')", CelErrorCode.TimeZoneNotFound, "Mars/Base")]
    [InlineData("timestamp('2026-01-01T00:00:00Z').getHours('+25:00')", CelErrorCode.InvalidTimeZone, "+25:00")]
    public static void EvaluationError_HasTheSubjectAndNoTypes(string expression, CelErrorCode code, string? subject)
    {
        AssertParts(EvaluationError<CelValue>(expression, Bindings), code, subject, null, null);
    }

    [Fact]
    public void ConversionRange_HasTheTargetAndTheSourceType()
    {
        AssertParts(EvaluationError<CelValue>("int(1e19)", Bindings), CelErrorCode.ConversionRange, null, CelType.Int, CelType.Double);
    }

    [Fact]
    public void Unbound_HasTheVariable()
    {
        AssertParts(EvaluationError<CelValue>("x + 1", new CelBindings()), CelErrorCode.Unbound, "x", null, null);
    }

    [Fact]
    public void BindingTypeMismatch_HasTheVariableAndBothTypes()
    {
        CelBindings wrong = new CelBindings().Add("x", "seven");

        AssertParts(EvaluationError<CelValue>("x + 1", wrong), CelErrorCode.BindingTypeMismatch, "x", CelType.Int, CelType.String);
    }

    [Fact]
    public void ResultConversion_HasTheNetTypeAndTheTypeOfTheValue()
    {
        AssertParts(EvaluationError<long>("d", Bindings), CelErrorCode.ResultConversion, "System.Int64", null, CelType.String);
    }

    [Fact]
    public void CostLimitExceeded_HasNoParts()
    {
        CelEvalResult<long> result = Environment.Compile<long>("x + 1 + 1").GetProgramOrThrow()
            .Evaluate(Bindings, new CelEvalOptions { CostLimit = 1 }, CancellationToken.None);

        AssertParts(Assert.Single(result.Errors), CelErrorCode.CostLimitExceeded, null, null, null);
    }

    [Fact]
    public void BuildErrors_HaveTheSubject()
    {
        AssertParts(
            Assert.Single(new CelEnvironmentBuilder().AddVariable<long>("x").AddVariable<string>("x").Build().Errors),
            CelErrorCode.DuplicateVariable, "x", null, null);
        AssertParts(
            Assert.Single(new CelEnvironmentBuilder().AddVariable<Order>("order").Build().Errors),
            CelErrorCode.TypeNotDeclared, typeof(Order).FullName, null, null);
        AssertParts(
            Assert.Single(new CelEnvironmentBuilder().AddExtension(CelExtensions.Strings(99)).Build().Errors),
            CelErrorCode.UnknownExtensionVersion, "strings", null, null);
    }

    // The first of the two types by ordinal comparison of their full names: Invoice before Order, whatever the order of calls.
    [Fact]
    public void InputConflict_HasTheFirstTypeByOrdinal()
    {
        CelError forward = Assert.Single(new CelEnvironmentBuilder().AddTypes(ShopContext.Default).AddTypes(BillingContext.Default)
            .UseInput<Order>().UseInput<Invoice>().Build().Errors);
        CelError backward = Assert.Single(new CelEnvironmentBuilder().AddTypes(ShopContext.Default).AddTypes(BillingContext.Default)
            .UseInput<Invoice>().UseInput<Order>().Build().Errors);

        AssertParts(forward, CelErrorCode.InputConflict, typeof(Invoice).FullName, null, null);
        Assert.Equal(forward.Message, backward.Message);
        Assert.Equal(
            "the environment input is declared as both '" + typeof(Invoice).FullName + "' and '" + typeof(Order).FullName + "'",
            forward.Message);
    }

    private static CelError EvaluationError<T>(string expression, CelBindings bindings)
    {
        CelEvalResult<T> result = Environment.Compile<T>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        return error;
    }

    private static void AssertParts(CelError error, CelErrorCode code, string? subject, CelType? expected, CelType? actual)
    {
        Assert.Equal(code, error.Code);
        Assert.Equal(subject, error.Subject);
        Assert.Equal(expected, error.ExpectedType);
        Assert.Equal(actual, error.ActualType);
    }
}
