using System;
using System.Collections.Generic;
using Cel.Expr;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using Duration = Google.Protobuf.WellKnownTypes.Duration;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;
using Type = Cel.Expr.Type;
using Value = Cel.Expr.Value;

namespace Great.Parts.Cel.Conformance.Synthetic;

// The values and the types of time in the runner (docs/design/tests.md, "The suite runner and time"): a binding of the message
// google.protobuf.Duration or Timestamp is a value of the CEL type duration or timestamp, the names of the two types are read and
// written, and every case has the time zone database of the tests.
public sealed class TimeConversionTests
{
    private const string DurationMessage = "google.protobuf.Duration";

    private const string TimestampMessage = "google.protobuf.Timestamp";

    [Fact]
    public static void DurationMessage_IsTheValueOfTheTypeDuration()
    {
        Value message = Pack(new Duration { Seconds = 123, Nanos = 321_456_789 });

        CelValue value = ValueConversion.ToCelValue(message);

        Assert.True(value.TryGetDuration(out CelDuration duration));
        Assert.Equal(123_321_456_789L, duration.TotalNanoseconds);
    }

    [Fact]
    public static void TimestampMessage_IsTheValueOfTheTypeTimestamp()
    {
        Value message = Pack(new Timestamp { Seconds = 1_234_567_890, Nanos = 5 });

        CelValue value = ValueConversion.ToCelValue(message);

        Assert.True(value.TryGetTimestamp(out CelTimestamp timestamp));
        Assert.Equal(1_234_567_890L, timestamp.Seconds);
        Assert.Equal(5, timestamp.Nanoseconds);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(-1, -1)]
    [InlineData(-123, -321_456_789)]
    [InlineData(9_223_372_036, 854_775_807)]
    [InlineData(-9_223_372_036, -854_775_808)]
    public static void Duration_IsWrittenWithTheSecondsAndTheNanosecondsOfTheSameSign(long seconds, int nanos)
    {
        CelValue value = CelValue.FromDuration(new CelDuration((seconds * 1_000_000_000L) + nanos));

        Value written = ValueConversion.ToExprValue(value);

        Duration message = written.ObjectValue.Unpack<Duration>();
        Assert.Equal(seconds, message.Seconds);
        Assert.Equal(nanos, message.Nanos);
        Assert.True(ValueConversion.ToCelValue(written).TryGetDuration(out CelDuration read));
        Assert.Equal((seconds * 1_000_000_000L) + nanos, read.TotalNanoseconds);
    }

    [Theory]
    [InlineData(-62_135_596_800L, 0)]
    [InlineData(0L, 1)]
    [InlineData(1_234_567_890L, 999_999_999)]
    [InlineData(253_402_300_799L, 999_999_999)]
    public static void Timestamp_IsWrittenWithItsSecondsAndNanoseconds(long seconds, int nanos)
    {
        CelValue value = CelValue.FromTimestamp(new CelTimestamp(seconds, nanos));

        Value written = ValueConversion.ToExprValue(value);

        Timestamp message = written.ObjectValue.Unpack<Timestamp>();
        Assert.Equal(seconds, message.Seconds);
        Assert.Equal(nanos, message.Nanos);
        Assert.True(ValueConversion.ToCelValue(written).TryGetTimestamp(out CelTimestamp read));
        Assert.Equal(seconds, read.Seconds);
        Assert.Equal(nanos, read.Nanoseconds);
    }

    [Theory]
    [InlineData(TimestampMessage, CelTypeKind.Timestamp)]
    [InlineData(DurationMessage, CelTypeKind.Duration)]
    public static void TypeValueOfATimeType_IsReadByItsFullName(string name, CelTypeKind kind)
    {
        CelValue value = ValueConversion.ToCelValue(new Value { TypeValue = name });

        Assert.True(value.TryGetType(out CelType? type));
        Assert.NotNull(type);
        Assert.Equal(kind, type.Kind);
        Assert.Equal(name, ValueConversion.ToExprValue(value).TypeValue);
    }

    [Theory]
    [InlineData(Type.Types.WellKnownType.Timestamp, CelTypeKind.Timestamp)]
    [InlineData(Type.Types.WellKnownType.Duration, CelTypeKind.Duration)]
    public static void WellKnownTypeOfTime_IsReadAndWritten(Type.Types.WellKnownType wellKnown, CelTypeKind kind)
    {
        CelType type = TypeConversion.ToCelType(new Type { WellKnown = wellKnown });

        Assert.Equal(kind, type.Kind);
        Assert.Equal(wellKnown, TypeConversion.ToExprType(type).WellKnown);
    }

    [Theory]
    [InlineData(TimestampMessage, CelTypeKind.Timestamp)]
    [InlineData(DurationMessage, CelTypeKind.Duration)]
    public static void MessageTypeOfTime_IsTheTypeOfTime(string name, CelTypeKind kind) =>
        Assert.Equal(kind, TypeConversion.ToCelType(new Type { MessageType = name }).Kind);

    // The case of cel-spec timestamps/duration_converters/get_milliseconds: x is a Duration of 123 s and 321 456 789 ns.
    [Fact]
    public static void BindingOfADurationMessage_IsReadByTheReceiversOfDuration()
    {
        Dictionary<string, ExprValue> bindings = new()
        {
            ["x"] = new ExprValue { Value = Pack(new Duration { Seconds = 123, Nanos = 321_456_789 }) },
        };

        CaseOutcome outcome = Evaluate("x.getMilliseconds()", [Variable("x", DurationMessage)], bindings);

        Assert.Null(outcome.Exception);
        Assert.Equal(CaseStage.Evaluate, outcome.Stage);
        Assert.Empty(outcome.Errors);
        Assert.Equal(321, outcome.Value?.Int64Value);
        Assert.Equal(Type.Types.PrimitiveType.Int64, outcome.ResultType?.Primitive);
    }

    [Fact]
    public static void ResultOfTime_IsWrittenAsAMessageOfItsType()
    {
        CaseOutcome timestamp = Evaluate("timestamp('2009-02-13T23:31:30Z')", [], new Dictionary<string, ExprValue>());
        CaseOutcome duration = Evaluate("duration('1.5s')", [], new Dictionary<string, ExprValue>());

        Assert.Equal(Type.Types.WellKnownType.Timestamp, timestamp.ResultType?.WellKnown);
        Assert.Equal(1_234_567_890L, timestamp.Value?.ObjectValue.Unpack<Timestamp>().Seconds);
        Assert.Equal(Type.Types.WellKnownType.Duration, duration.ResultType?.WellKnown);
        Assert.Equal(1, duration.Value?.ObjectValue.Unpack<Duration>().Seconds);
        Assert.Equal(500_000_000, duration.Value?.ObjectValue.Unpack<Duration>().Nanos);
    }

    [Theory]
    [InlineData("timestamp('2023-01-01T00:00:00Z').getHours('Asia/Kathmandu')", 5)]
    [InlineData("timestamp('2023-01-01T00:00:00Z').getMinutes('Asia/Kathmandu')", 45)]
    [InlineData("timestamp('2023-03-12T07:00:00Z').getHours('America/New_York')", 3)]
    [InlineData("timestamp('2023-03-12T06:59:59Z').getHours('America/New_York')", 1)]
    public static void EveryCase_HasTheTimeZoneDatabaseOfTheTests(string expression, long expected)
    {
        CaseOutcome outcome = Evaluate(expression, [], new Dictionary<string, ExprValue>());

        Assert.Null(outcome.Exception);
        Assert.Empty(outcome.Errors);
        Assert.Equal(expected, outcome.Value?.Int64Value);
    }

    [Fact]
    public static void ZoneOutsideTheDatabase_IsAnEvaluationError()
    {
        CaseOutcome outcome = Evaluate("timestamp(0).getHours('Mars/Base')", [], new Dictionary<string, ExprValue>());

        Assert.Equal(CaseStage.Evaluate, outcome.Stage);
        Assert.Null(outcome.Exception);
        Assert.Single(outcome.Errors);
        Assert.Contains("Mars/Base", outcome.Errors[0], StringComparison.Ordinal);
    }

    private static Value Pack(IMessage message) => new() { ObjectValue = Any.Pack(message) };

    private static Decl Variable(string name, string messageType) => new()
    {
        Name = name,
        Ident = new Decl.Types.IdentDecl { Type = new Type { MessageType = messageType } },
    };

    private static CaseOutcome Evaluate(string expression, IReadOnlyList<Decl> declarations, IReadOnlyDictionary<string, ExprValue> bindings) =>
        new LibraryEvaluator().Evaluate(new ConformanceCase(
            "f/s/t",
            "f.textproto",
            1,
            1,
            expression,
            new CaseEnvironment("", declarations, false),
            bindings,
            CheckMode.Check,
            new Expectation(ExpectationKind.Value, "none", new Value { BoolValue = true }, null)));
}
