using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The identifiers google.protobuf.Timestamp and google.protobuf.Duration of the standard library page: a
// type value, the type of the values of time, with the static type type(timestamp) or type(duration).
// timestamp and duration are functions, not type identifiers: written alone they are an undeclared
// reference. The static types of the functions and operators of time, the receivers and the arithmetic.
[Trait("Requirement", "F-1")]
[Trait("Requirement", "R-1")]
public sealed class TimeTypeIdentifierTests
{
    private const string Rows = """
        google.protobuf.Timestamp == type(timestamp(0)) | bool true
        type(timestamp(0)) == google.protobuf.Timestamp | bool true
        google.protobuf.Duration == type(duration('1s')) | bool true
        type(duration('1s')) == google.protobuf.Duration | bool true
        google.protobuf.Timestamp == google.protobuf.Duration | bool false
        google.protobuf.Timestamp != google.protobuf.Duration | bool true
        type(timestamp(0)) == type(duration('1s')) | bool false
        google.protobuf.Timestamp == google.protobuf.Timestamp | bool true
        type(dyn(timestamp(0))) == google.protobuf.Timestamp | bool true
        type(timestamp(0) + duration('1s')) == google.protobuf.Timestamp | bool true
        type(timestamp(1) - timestamp(0)) == google.protobuf.Duration | bool true
        type(google.protobuf.Timestamp) == type | bool true
        type(google.protobuf.Duration) == type | bool true
        type(timestamp(0)) == int | bool false
        dyn(google.protobuf.Timestamp) == google.protobuf.Timestamp | bool true
        timestamp | compile UndeclaredReference
        duration | compile UndeclaredReference
        google.protobuf.timestamp | compile UndeclaredReference
        google.protobuf.Time | compile UndeclaredReference
        """;

    private readonly CelEnvironment environment =
        new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    [Fact]
    public void EveryRow_IsItsOutcome()
    {
        Assert.Empty(TimeRows.Check(environment, Rows));
    }

    [Fact]
    public void TypeIdentifier_IsATypeValueOfTheTypeOfTime()
    {
        Assert.Equal(CelType.Timestamp, Evaluate<CelType>("google.protobuf.Timestamp"));
        Assert.Equal(CelType.Duration, Evaluate<CelType>("google.protobuf.Duration"));
        Assert.Equal(CelType.Timestamp, Evaluate<CelType>("type(timestamp(0))"));
        Assert.Equal(CelType.Duration, Evaluate<CelType>("type(duration('1s'))"));
        Assert.Equal(CelTypeKind.Timestamp, Evaluate<CelType>("type(timestamp(0))").Kind);
        Assert.Equal(CelTypeKind.Duration, Evaluate<CelType>("type(duration('1s'))").Kind);
    }

    [Theory]
    [InlineData("timestamp(0)", CelTypeKind.Timestamp)]
    [InlineData("timestamp('2009-02-13T23:31:30Z')", CelTypeKind.Timestamp)]
    [InlineData("duration('1s')", CelTypeKind.Duration)]
    [InlineData("timestamp(0) + duration('1s')", CelTypeKind.Timestamp)]
    [InlineData("duration('1s') + timestamp(0)", CelTypeKind.Timestamp)]
    [InlineData("timestamp(0) - duration('1s')", CelTypeKind.Timestamp)]
    [InlineData("timestamp(1) - timestamp(0)", CelTypeKind.Duration)]
    [InlineData("duration('1s') - duration('1s')", CelTypeKind.Duration)]
    [InlineData("timestamp(timestamp(0))", CelTypeKind.Timestamp)]
    [InlineData("duration(duration('1s'))", CelTypeKind.Duration)]
    [InlineData("int(timestamp(0))", CelTypeKind.Int)]
    [InlineData("int(duration('1s'))", CelTypeKind.Int)]
    [InlineData("string(timestamp(0))", CelTypeKind.String)]
    [InlineData("string(duration('1s'))", CelTypeKind.String)]
    [InlineData("timestamp(0).getHours()", CelTypeKind.Int)]
    [InlineData("timestamp(0).getHours('UTC')", CelTypeKind.Int)]
    [InlineData("duration('1s').getMilliseconds()", CelTypeKind.Int)]
    [InlineData("timestamp(0) < timestamp(1)", CelTypeKind.Bool)]
    [InlineData("duration('1s') == duration('2s')", CelTypeKind.Bool)]
    [InlineData("dyn(timestamp(0))", CelTypeKind.Dyn)]
    public void StaticTypeOfAnExpressionOfTime(string expression, CelTypeKind kind)
    {
        Assert.Equal(kind, environment.Compile<CelValue>(expression).GetProgramOrThrow().ResultType.Kind);
    }

    // The result type of the identifier is the type value of the type of time.
    [Fact]
    public void StaticTypeOfTheIdentifier_IsTypeOfTheTypeOfTime()
    {
        Assert.Equal(CelType.TypeOf(CelType.Timestamp), environment.Compile<CelType>("google.protobuf.Timestamp").GetProgramOrThrow().ResultType);
        Assert.Equal(CelType.TypeOf(CelType.Duration), environment.Compile<CelType>("google.protobuf.Duration").GetProgramOrThrow().ResultType);
    }

    // timestamp() and duration() take one argument of the kinds of the table: the other kinds have no overload.
    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("timestamp(1.5)", 9)]
    [InlineData("timestamp(true)", 9)]
    [InlineData("timestamp(1u)", 9)]
    [InlineData("timestamp(duration('1s'))", 9)]
    [InlineData("timestamp()", 9)]
    [InlineData("timestamp(1, 2)", 9)]
    [InlineData("duration(1)", 8)]
    [InlineData("duration(1.5)", 8)]
    [InlineData("duration(timestamp(0))", 8)]
    [InlineData("duration()", 8)]
    [InlineData("int(timestamp(0), 1)", 3)]
    [InlineData("string(timestamp(0), 1)", 6)]
    public void ArgumentOfAnotherKind_IsNoMatchingOverloadAtTheCall(string expression, int offset)
    {
        CelError error = Assert.Single(environment.Compile<CelValue>(expression).Errors);

        Assert.Equal((CelErrorCode.NoMatchingOverload, CelErrorStage.Check, offset), (error.Code, error.Stage, error.Location.Offset));
    }

    // The same with dyn: no overload fits the value, at evaluation.
    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("timestamp(dyn(1.5))", 9)]
    [InlineData("timestamp(dyn(true))", 9)]
    [InlineData("timestamp(dyn(duration('1s')))", 9)]
    [InlineData("duration(dyn(1))", 8)]
    [InlineData("duration(dyn(timestamp(0)))", 8)]
    public void DynArgumentOfAnotherKind_IsEvalNoMatchingOverloadAtTheCall(string expression, int offset)
    {
        CelEvalResult<CelValue> result = environment.Compile<CelValue>(expression).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.EvalNoMatchingOverload, CelErrorStage.Evaluate, offset), (error.Code, error.Stage, error.Location.Offset));
    }

    private T Evaluate<T>(string expression) =>
        environment.Compile<T>(expression).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();
}
