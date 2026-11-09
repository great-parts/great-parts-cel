using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// The functions of time through the whole path: parsing, checking, planning and evaluation, with and without a time zone
// database, with and without type checking. The folding of constants at planning, the places of the errors, the cost of the
// calls, and the receivers on values of static type dyn.
public sealed class TimeEvaluationTests
{
    private static readonly CelEnvironment WithZones = Environment(CelTimeZoneDatabase.Create(
        [
            TzifFile.Pair("Test/Moved", TzifFile.Build([1_000_000_000], [1], [3_600, 7_200], string.Empty)),
            TzifFile.Pair("Test/ZeroBased", TzifFile.Fixed(0, string.Empty)),
        ],
        "2026a"));

    private static readonly CelEnvironment WithoutZones = Environment(null);

    private static CelBindings Bindings(string zone = "+03:00") => new CelBindings()
        .Add("ts", new CelTimestamp(1_234_567_890, 0))
        .Add("du", new CelDuration(5_400_000_000_000L))
        .Add("tsstr", "2009-02-13T23:31:30Z")
        .Add("z", zone)
        .Add("d", new CelTimestamp(1_234_567_890, 0));

    [Theory]
    [InlineData("timestamp('2009-02-13T23:31:30Z') + duration('1h') == timestamp('2009-02-14T00:31:30Z')", "true")]
    [InlineData("duration('1h') + timestamp('2009-02-13T23:31:30Z') == timestamp('2009-02-14T00:31:30Z')", "true")]
    [InlineData("timestamp('2009-02-13T23:31:30Z') - timestamp('2009-02-13T22:31:30Z') == duration('1h')", "true")]
    [InlineData("timestamp('2009-02-13T23:31:30Z') - duration('1h') < timestamp('2009-02-13T23:31:30Z')", "true")]
    [InlineData("duration('1h') + duration('30m') > duration('89m')", "true")]
    [InlineData("duration('1h') - duration('30m') <= duration('30m')", "true")]
    [InlineData("duration('1h') >= duration('3601s')", "false")]
    [InlineData("timestamp('2009-02-13T23:31:30Z').getFullYear()", "2009")]
    [InlineData("int(timestamp(1234567890)) == 1234567890", "true")]
    [InlineData("int(duration('1s'))", "1000000000")]
    [InlineData("string(timestamp(1234567890))", "2009-02-13T23:31:30Z")]
    [InlineData("string(duration('90m'))", "5400s")]
    [InlineData("timestamp(timestamp(5)) == timestamp(5)", "true")]
    [InlineData("duration(duration('5s')) == duration('5000ms')", "true")]
    [InlineData("timestamp('2009-02-13T23:31:30Z').getHours('+03:00')", "2")]
    [InlineData("timestamp('2009-02-13T23:31:30Z').getHours('Test/Moved')", "1")]
    [InlineData("timestamp(1).getHours('Test/Moved')", "1")]
    [InlineData("timestamp(1).getHours('Test/ZeroBased')", "0")]
    [InlineData("duration('36h').getHours()", "36")]
    [InlineData("duration('-1.5s').getMilliseconds()", "-500")]
    [InlineData("type(timestamp(0)) == google.protobuf.Timestamp", "true")]
    [InlineData("type(duration('1s')) == google.protobuf.Duration", "true")]
    [InlineData("google.protobuf.Timestamp == google.protobuf.Duration", "false")]
    public static void TimeExpressions_GiveTheirValues(string expression, string expected)
    {
        Assert.Equal(expected, Show(Run(WithZones, expression).GetValueOrThrow()));
        Assert.Equal(expected, Show(RunWithoutTypeCheck(WithZones, expression).GetValueOrThrow()));
    }

    [Theory]
    [InlineData("timestamp('x')", CelErrorCode.InvalidArgument, 9, "invalid argument to 'timestamp': cannot parse x as timestamp")]
    [InlineData("duration('1H')", CelErrorCode.InvalidArgument, 8, "invalid argument to 'duration': cannot parse 1H as duration")]
    [InlineData("1 + timestamp('x').getFullYear()", CelErrorCode.InvalidArgument, 13, "invalid argument to 'timestamp': cannot parse x as timestamp")]
    [InlineData("timestamp(253402300800)", CelErrorCode.ConversionRange, 9, "253402300800 is out of the range of timestamp")]
    [InlineData("timestamp('0000-01-01T00:00:00Z')", CelErrorCode.ConversionRange, 9, "0000-01-01T00:00:00Z is out of the range of timestamp")]
    [InlineData("duration('9223372036854775808ns')", CelErrorCode.ConversionRange, 8, "9223372036854775808ns is out of the range of duration")]
    [InlineData("timestamp('9999-12-31T23:59:59Z') + duration('1s')", CelErrorCode.Overflow, 34, "timestamp overflow")]
    [InlineData("duration('9223372036854775807ns') + duration('1ns')", CelErrorCode.Overflow, 34, "duration overflow")]
    [InlineData("timestamp(0).getHours('Mars')", CelErrorCode.TimeZoneNotFound, 21, "time zone 'Mars' is not found in the time zone database of the environment (version 2026a)")]
    [InlineData("timestamp(0).getHours('+1:5')", CelErrorCode.InvalidTimeZone, 21, "invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM")]
    [InlineData("timestamp(0).getHours('')", CelErrorCode.TimeZoneNotFound, 21, "time zone '' is not found in the time zone database of the environment (version 2026a)")]
    public static void FailedCall_IsTheErrorOfTheCallAtItsPlace(string expression, CelErrorCode code, int offset, string message)
    {
        CelError checkedError = Assert.Single(Run(WithZones, expression).Errors);
        CelError uncheckedError = Assert.Single(RunWithoutTypeCheck(WithZones, expression).Errors);

        Assert.Equal((code, offset, message), (checkedError.Code, checkedError.Location.Offset, checkedError.Message));
        Assert.Equal((code, offset, message), (uncheckedError.Code, uncheckedError.Location.Offset, uncheckedError.Message));
    }

    // An error of the call is an error of the call that is evaluated: a branch that is not taken has none, a constant that cannot
    // be read compiles, and a constant zone that is wrong is no compile error either.
    [Theory]
    [InlineData("true ? 1 : timestamp('x').getHours()", "1")]
    [InlineData("false && timestamp('x') == timestamp(0)", "false")]
    [InlineData("true || timestamp('x') == timestamp(0)", "true")]
    [InlineData("true ? 1 : timestamp(0).getHours('Mars')", "1")]
    [InlineData("false ? timestamp(0).getHours('+1:5') : 2", "2")]
    public static void ErrorOfAConstant_IsFoundOnlyWhenTheCallIsEvaluated(string expression, string expected)
    {
        Assert.Equal(expected, Show(Run(WithZones, expression).GetValueOrThrow()));
    }

    [Fact]
    public static void ConstantsOfTimeFromAStringLiteral_AreFoldedAtPlanning()
    {
        Assert.IsType<ConstantPlanNode>(PlanOf("timestamp('2009-02-13T23:31:30Z')").Root);
        Assert.IsType<ConstantPlanNode>(PlanOf("duration('72h')").Root);
        Assert.IsType<ConstantPlanNode>(PlanOf("timestamp('2009-02-13T23:31:30.5+03:00')").Root);
    }

    [Fact]
    public static void CallThatCannotBeFolded_StaysACall()
    {
        Assert.IsType<CallPlanNode>(PlanOf("timestamp('x')").Root);
        Assert.IsType<CallPlanNode>(PlanOf("duration('')").Root);
        Assert.IsType<CallPlanNode>(PlanOf("timestamp(tsstr)").Root);
        Assert.IsType<CallPlanNode>(PlanOf("timestamp(5)").Root);
        Assert.IsType<CallPlanNode>(PlanOf("string(timestamp(5))").Root);
    }

    // Planning without type checking folds nothing: its calls are dispatches, and the value and the cost are the same.
    [Fact]
    public static void WithoutTypeCheck_NothingIsFolded()
    {
        Assert.IsType<DispatchPlanNode>(PlanWithoutTypeCheck("timestamp('2009-02-13T23:31:30Z')").Root);
        Assert.IsType<DispatchPlanNode>(PlanWithoutTypeCheck("duration('72h')").Root);
    }

    // Folding changes no value, no error and no cost: the units are those of the call that was replaced.
    [Theory]
    [InlineData("timestamp(0)", 1UL)]
    [InlineData("int(timestamp(0))", 2UL)]
    [InlineData("timestamp(ts)", 2UL)]
    [InlineData("duration(du)", 2UL)]
    [InlineData("int(du)", 2UL)]
    [InlineData("string(ts)", 4UL)]
    [InlineData("string(du)", 3UL)]
    [InlineData("timestamp(tsstr)", 4UL)]
    [InlineData("timestamp('2009-02-13T23:31:30Z')", 3UL)]
    [InlineData("duration('72h')", 2UL)]
    [InlineData("duration('1h30m45s500ms')", 3UL)]
    [InlineData("ts.getHours()", 2UL)]
    [InlineData("du.getSeconds()", 2UL)]
    [InlineData("ts.getHours(z)", 4UL)]
    [InlineData("ts.getHours('UTC')", 3UL)]
    [InlineData("ts.getHours('+03:00')", 3UL)]
    [InlineData("ts.getHours('Test/ZeroBased')", 4UL)]
    [InlineData("ts + du", 3UL)]
    [InlineData("du - du", 3UL)]
    [InlineData("ts - ts", 3UL)]
    [InlineData("ts < ts", 3UL)]
    [InlineData("du >= du", 3UL)]
    [InlineData("ts == ts", 3UL)]
    public static void Units_AreThoseOfTheCostTable(string expression, ulong units)
    {
        Assert.Equal(units, Charged(PlanOf(expression), expression));
        Assert.Equal(units, Charged(PlanWithoutTypeCheck(expression), expression));
    }

    // The limit that is exactly the cost passes, and one unit less stops the call before it runs.
    [Fact]
    public static void CostLimit_StopsAFoldedConstantAndACallAlike()
    {
        foreach ((string expression, ulong units) in new[] { ("timestamp('2009-02-13T23:31:30Z')", 3UL), ("ts.getHours('Test/ZeroBased')", 4UL), ("string(ts)", 4UL) })
        {
            Assert.True(Run(WithZones, expression, limit: units).IsSuccess, expression);
            CelError error = Assert.Single(Run(WithZones, expression, limit: units - 1).Errors);
            Assert.Equal(CelErrorCode.CostLimitExceeded, error.Code);
        }
    }

    // A receiver on a value of static type dyn is chosen by the kind of the value.
    [Fact]
    public static void DynReceiver_IsChosenByTheKindOfTheValue()
    {
        Assert.Equal("2009", Show(Run(WithZones, "d.getFullYear()", Bindings()).GetValueOrThrow()));
        Assert.Equal("2", Show(Run(WithZones, "d.getHours('+03:00')", Bindings()).GetValueOrThrow()));
        CelBindings duration = new CelBindings().Add("d", new CelDuration(7_200_000_000_000L)).Add("z", "UTC");
        Assert.Equal("2", Show(Run(WithZones, "d.getHours()", duration).GetValueOrThrow()));
        Assert.Equal("1", Show(Run(WithZones, "d.getMilliseconds() + 1", duration).GetValueOrThrow()));
    }

    // With a zone no candidate takes a duration, and the kinds are checked before the name of the zone.
    [Fact]
    public static void DurationWithAZone_AndAnIntegerZone_AreNoMatchingOverload()
    {
        CelBindings duration = new CelBindings().Add("d", new CelDuration(1)).Add("z", "UTC");
        CelError first = Assert.Single(Run(WithZones, "d.getHours(z)", duration).Errors);
        CelError second = Assert.Single(RunWithoutTypeCheck(WithZones, "timestamp(0).getHours(5)").Errors);
        CelError third = Assert.Single(RunWithoutTypeCheck(WithZones, "timestamp(0) + 1").Errors);

        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, first.Code);
        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, second.Code);
        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, third.Code);
        Assert.Equal(CelErrorCode.NoMatchingOverload, Assert.Single(WithZones.Compile<CelValue>("timestamp(0).getHours(5)").Errors).Code);
        Assert.Equal(CelErrorCode.NoMatchingOverload, Assert.Single(WithZones.Compile<CelValue>("timestamp(0) + 1").Errors).Code);
    }

    // A zone read from a variable takes the same path as a constant one.
    [Theory]
    [InlineData("+03:00", "2")]
    [InlineData("UTC", "23")]
    [InlineData("Test/Moved", "1")]
    public static void ZoneFromAVariable_ResolvesAsAConstantDoes(string zone, string expected)
    {
        CelBindings bindings = Bindings(zone);

        Assert.Equal(expected, Show(Run(WithZones, "ts.getHours(z)", bindings).GetValueOrThrow()));
        Assert.Equal(expected, Show(Run(WithZones, "ts.getHours('" + zone + "')", bindings).GetValueOrThrow()));
    }

    [Fact]
    public static void ZoneNameWithoutADatabase_IsNotFoundAndSaysThereIsNone()
    {
        CelError constant = Assert.Single(Run(WithoutZones, "timestamp(0).getHours('Test/Moved')").Errors);
        CelError variable = Assert.Single(Run(WithoutZones, "ts.getHours('Test/Moved')", Bindings()).Errors);

        Assert.Equal((CelErrorCode.TimeZoneNotFound, "time zone 'Test/Moved' is not found: the environment has no time zone database"), (constant.Code, constant.Message));
        Assert.Equal(constant.Message, variable.Message);
        Assert.Equal("2", Show(Run(WithoutZones, "ts.getHours('+03:00')", Bindings()).GetValueOrThrow()));
        Assert.Equal("23", Show(Run(WithoutZones, "ts.getHours('UTC')", Bindings()).GetValueOrThrow()));
    }

    [Fact]
    public static void EmptyDatabase_HasNoZonesAndNoVersionInTheMessage()
    {
        CelEnvironment environment = Environment(CelTimeZoneDatabase.Empty);

        CelError error = Assert.Single(Run(environment, "timestamp(0).getHours('Test/Moved')").Errors);

        Assert.Equal("time zone 'Test/Moved' is not found in the time zone database of the environment", error.Message);
        Assert.Equal("0", Show(Run(environment, "timestamp(0).getHours('UTC')").GetValueOrThrow()));
    }

    private static CelEnvironment Environment(CelTimeZoneDatabase? database)
    {
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<CelTimestamp>("ts")
            .AddVariable<CelDuration>("du")
            .AddVariable<string>("tsstr")
            .AddVariable<string>("z")
            .AddVariable("d", CelType.Dyn);
        if (database is not null)
        {
            builder.UseTimeZones(database);
        }

        return builder.Build().GetEnvironmentOrThrow();
    }

    private static CelEvalResult<CelValue> Run(CelEnvironment environment, string expression, CelBindings? bindings = null, ulong? limit = null)
    {
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(expression);
        Assert.True(compiled.IsSuccess, expression + ": " + string.Join("; ", compiled.Errors));
        return compiled.GetProgramOrThrow().Evaluate(bindings ?? Bindings(), limit is { } cost ? new CelEvalOptions { CostLimit = cost } : null);
    }

    private static CelEvalResult<CelValue> RunWithoutTypeCheck(CelEnvironment environment, string expression) =>
        environment.EvaluateWithoutTypeCheck(Parse(environment, expression), Bindings());

    private static Plan PlanOf(string expression) => WithZones.Compile<CelValue>(expression).GetProgramOrThrow().Plan;

    private static Plan PlanWithoutTypeCheck(string expression) =>
        Planner.PlanWithoutTypeCheck(Parse(WithZones, expression), WithZones.Declarations, WithZones.TimeZones);

    private static ParsedExpression Parse(CelEnvironment environment, string expression)
    {
        ParseResult parsed = environment.Parse(expression);
        Assert.True(parsed.IsSuccess, expression);
        return parsed.Expression;
    }

    // The units charged by one evaluation of the plan on the bindings.
    private static ulong Charged(Plan plan, string expression)
    {
        CelBindings bindings = Bindings();
        Frame frame = plan.CreateFrame(bindings.Values, 1_000_000, CancellationToken.None);
        CelValue value = plan.Run(frame);
        Assert.False(value.Kind == CelValueKind.Error, expression);
        return frame.ChargedUnits;
    }

    private static string Show(CelValue value)
    {
        if (value.TryGetBoolean(out bool flag))
        {
            return flag ? "true" : "false";
        }

        if (value.TryGetInt64(out long number))
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        return value.TryGetString(out string? text) ? text : value.Kind.ToString();
    }
}
