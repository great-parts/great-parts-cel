using System;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The set calendar, version 1: the types date and time_of_day with 36 overloads. The rows state the text of the value that the
// expression gives (CelValue.ToString: date("2026-11-19"), "2026-11-19", 12), or the code of the error and the stage. The time zones are
// those of the zoneinfo database that the tests share, so the transitions of Europe/Moscow and America/Sao_Paulo are the real ones: a
// local time that a zone skips is moved forward by the length of the gap, and one that it repeats takes the earlier offset. The costs
// are those of the page of the costs of the set: a call is 1 unit, the strings that are read add ⌈L/10⌉, the text of a result too.
[Trait("Requirement", "F-24")]
[Trait("Requirement", "F-15")]
public sealed class CalendarTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Calendar(1))
        .UseTimeZones(ZoneInfoFile.Database())
        .AddVariable<CelTimestamp>("t")
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelBindings Bindings = new CelBindings()
        .Add("t", CelTimestamp.FromDateTimeOffset(new DateTimeOffset(2026, 11, 19, 22, 30, 0, TimeSpan.Zero)));

    [Theory]
    [InlineData("date('2026-11-19')", "date(\"2026-11-19\")")]
    [InlineData("string(date('2026-11-19'))", "\"2026-11-19\"")]
    [InlineData("date(2024, 2, 29)", "date(\"2024-02-29\")")]
    [InlineData("date(1, 1, 1)", "date(\"0001-01-01\")")]
    [InlineData("date(9999, 12, 31)", "date(\"9999-12-31\")")]
    [InlineData("date(timestamp('2026-11-19T22:30:00Z'))", "date(\"2026-11-19\")")]
    [InlineData("date(timestamp('2026-11-19T22:30:00Z'), 'Europe/Moscow')", "date(\"2026-11-20\")")]
    [InlineData("date(timestamp('2026-11-19T02:00:00Z'), 'America/New_York')", "date(\"2026-11-18\")")]
    [InlineData("date(timestamp('2026-11-19T22:30:00Z'), '+03:00')", "date(\"2026-11-20\")")]
    [InlineData("date(t, 'UTC')", "date(\"2026-11-19\")")]
    [InlineData("date(date('2026-11-19')) == date('2026-11-19')", "true")]
    [InlineData("time_of_day('09:30')", "time_of_day(\"09:30:00\")")]
    [InlineData("string(time_of_day('09:30'))", "\"09:30:00\"")]
    [InlineData("string(time_of_day('09:30:15'))", "\"09:30:15\"")]
    [InlineData("string(time_of_day('09:30:15.1200000'))", "\"09:30:15.12\"")]
    [InlineData("string(time_of_day('09:30:15.1'))", "\"09:30:15.1\"")]
    [InlineData("string(time_of_day('23:59:59.9999999'))", "\"23:59:59.9999999\"")]
    [InlineData("string(time_of_day('00:00:00.0000000'))", "\"00:00:00\"")]
    [InlineData("string(time_of_day(timestamp('2026-11-19T09:30:15.123456789Z')))", "\"09:30:15.1234567\"")]
    [InlineData("time_of_day(timestamp('2026-11-19T22:30:00Z'), 'Europe/Moscow') == time_of_day('01:30')", "true")]
    [InlineData("time_of_day(time_of_day('09:30')) == time_of_day('09:30')", "true")]
    public static void Creation_IsExactAndInTheZoneOfTheCall(string expression, string expected)
    {
        Assert.Equal(expected, Evaluate(expression).ToString());
    }

    [Theory]
    [InlineData("timestamp(date('2026-11-19'), time_of_day('09:30'))", "timestamp(\"2026-11-19T09:30:00Z\")")]
    [InlineData("timestamp(date('2026-11-19'), time_of_day('09:30:15.1234567'))", "timestamp(\"2026-11-19T09:30:15.1234567Z\")")]
    [InlineData("timestamp(date('2026-11-19'), time_of_day('09:30'), 'Europe/Moscow')", "timestamp(\"2026-11-19T06:30:00Z\")")]
    [InlineData("timestamp(date('2026-11-19'), time_of_day('09:30'), '-05:00')", "timestamp(\"2026-11-19T14:30:00Z\")")]
    [InlineData("timestamp(date('2018-11-04'), time_of_day('00:30'), 'America/Sao_Paulo')", "timestamp(\"2018-11-04T03:30:00Z\")")]
    [InlineData("timestamp(date('2018-11-04'), time_of_day('00:00'), 'America/Sao_Paulo')", "timestamp(\"2018-11-04T03:00:00Z\")")]
    [InlineData("timestamp(date('2014-10-26'), time_of_day('01:30'), 'Europe/Moscow')", "timestamp(\"2014-10-25T21:30:00Z\")")]
    [InlineData("timestamp(date('2014-10-26'), time_of_day('00:59'), 'Europe/Moscow')", "timestamp(\"2014-10-25T20:59:00Z\")")]
    [InlineData("timestamp(date('2014-10-26'), time_of_day('02:00'), 'Europe/Moscow')", "timestamp(\"2014-10-25T23:00:00Z\")")]
    [InlineData("timestamp(date('2026-03-08'), time_of_day('02:30'), 'America/New_York')", "timestamp(\"2026-03-08T07:30:00Z\")")]
    [InlineData("timestamp(date('2026-03-08'), time_of_day('03:30'), 'America/New_York')", "timestamp(\"2026-03-08T07:30:00Z\")")]
    [InlineData("timestamp(date('2026-11-01'), time_of_day('01:30'), 'America/New_York')", "timestamp(\"2026-11-01T05:30:00Z\")")]
    [InlineData("timestamp(date('2026-11-01'), time_of_day('02:30'), 'America/New_York')", "timestamp(\"2026-11-01T07:30:00Z\")")]
    public static void Timestamp_OfALocalDateAndTime_FollowsTheRuleOfGapsAndOverlaps(string expression, string expected)
    {
        Assert.Equal(expected, Evaluate(expression).ToString());
    }

    [Theory]
    [InlineData("date('2026-11-19').getFullYear()", "2026")]
    [InlineData("date('2026-11-19').getMonth()", "10")]
    [InlineData("date('2026-11-19').getDate()", "19")]
    [InlineData("date('2026-11-19').getDayOfMonth()", "18")]
    [InlineData("date('2026-11-19').getDayOfWeek()", "4")]
    [InlineData("date('2026-11-19').getDayOfYear()", "322")]
    [InlineData("date('2024-12-31').getDayOfYear()", "365")]
    [InlineData("date('0001-01-01').getDayOfWeek()", "1")]
    [InlineData("time_of_day('09:30:15.250').getHours()", "9")]
    [InlineData("time_of_day('09:30:15.250').getMinutes()", "30")]
    [InlineData("time_of_day('09:30:15.250').getSeconds()", "15")]
    [InlineData("time_of_day('09:30:15.250').getMilliseconds()", "250")]
    [InlineData("time_of_day('23:59:59.9999999').getMilliseconds()", "999")]
    [InlineData("date('2026-01-31').addMonths(1)", "date(\"2026-02-28\")")]
    [InlineData("date('2024-02-29').addYears(1)", "date(\"2025-02-28\")")]
    [InlineData("date('2024-02-29').addYears(4)", "date(\"2028-02-29\")")]
    [InlineData("date('2026-11-19').addDays(-19)", "date(\"2026-10-31\")")]
    [InlineData("date('2026-11-19').addMonths(-11)", "date(\"2025-12-19\")")]
    [InlineData("date('9999-12-31').addDays(0)", "date(\"9999-12-31\")")]
    [InlineData("date('2026-12-01') - date('2026-11-19')", "12")]
    [InlineData("date('2026-11-19') - date('2026-12-01')", "-12")]
    [InlineData("time_of_day('18:00') - time_of_day('09:30')", "duration(\"30600s\")")]
    [InlineData("time_of_day('09:30') - time_of_day('18:00')", "duration(\"-30600s\")")]
    [InlineData("date('2026-11-19') < date('2026-11-20')", "true")]
    [InlineData("date('2026-11-19') >= date('2026-11-20')", "false")]
    [InlineData("date('2026-11-19') <= date('2026-11-19')", "true")]
    [InlineData("date('2026-11-20') > date('2026-11-19')", "true")]
    [InlineData("time_of_day('09:30') >= time_of_day('09:30:00')", "true")]
    [InlineData("time_of_day('09:30') < time_of_day('09:30:00.0000001')", "true")]
    [InlineData("date('2026-11-19') == date(2026, 11, 19)", "true")]
    [InlineData("date('2026-11-19') != date(2026, 11, 20)", "true")]
    [InlineData("dyn(date('2026-11-19')) == '2026-11-19'", "false")]
    [InlineData("dyn(date('2026-11-19')) == dyn(time_of_day('00:00'))", "false")]
    [InlineData("type(date('2026-11-19')) == date", "true")]
    [InlineData("type(time_of_day('09:30')) == time_of_day", "true")]
    [InlineData("type(date('2026-11-19'))", "date")]
    public static void PartsShiftsDifferencesAndOrderings_AreTheirValues(string expression, string expected)
    {
        Assert.Equal(expected, Evaluate(expression).ToString());
    }

    [Theory]
    [InlineData("date('2026-2-1')")]
    [InlineData("date('26-11-19')")]
    [InlineData("date('2026-02-30')")]
    [InlineData("date('2026-11-19T00:00:00Z')")]
    [InlineData("date('0000-01-01')")]
    [InlineData("date('2026-13-01')")]
    [InlineData("date('２０２６-11-19')")]
    [InlineData("date('')")]
    [InlineData("date(2023, 2, 29)")]
    [InlineData("date(0, 1, 1)")]
    [InlineData("date(2026, 13, 1)")]
    [InlineData("date(10000, 1, 1)")]
    [InlineData("date(2026, 1, 0)")]
    [InlineData("time_of_day('24:00')")]
    [InlineData("time_of_day('9:30')")]
    [InlineData("time_of_day('09:30:15.12345678')")]
    [InlineData("time_of_day('09:60')")]
    [InlineData("time_of_day('09:30:60')")]
    [InlineData("time_of_day('09:30:')")]
    [InlineData("time_of_day('09:30:15.')")]
    [InlineData("time_of_day('09:30:1')")]
    [InlineData("time_of_day('')")]
    public static void TextAndPartsThatAreNoDateOrTimeOfDay_AreInvalidArgument(string expression)
    {
        CelError error = EvaluationError(expression);

        Assert.Equal(CelErrorCode.InvalidArgument, error.Code);
        Assert.StartsWith("invalid argument to '", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("timestamp(date('9999-12-31'), time_of_day('23:00'), '-05:00')", CelErrorCode.Overflow)]
    [InlineData("date('9999-12-31').addDays(1)", CelErrorCode.Overflow)]
    [InlineData("date('0001-01-01').addDays(-1)", CelErrorCode.Overflow)]
    [InlineData("date('0001-01-01').addMonths(-1)", CelErrorCode.Overflow)]
    [InlineData("date('9999-12-31').addMonths(1)", CelErrorCode.Overflow)]
    [InlineData("date('9999-12-31').addYears(1)", CelErrorCode.Overflow)]
    [InlineData("date('2026-11-19').addYears(9223372036854775807)", CelErrorCode.Overflow)]
    [InlineData("date('2026-11-19').addMonths(-9223372036854775807)", CelErrorCode.Overflow)]
    [InlineData("date('2026-11-19').addDays(9223372036854775807)", CelErrorCode.Overflow)]
    [InlineData("date(timestamp('0001-01-01T00:00:00Z'), '-05:00')", CelErrorCode.Overflow)]
    [InlineData("date(t, 'Mars/Base')", CelErrorCode.TimeZoneNotFound)]
    [InlineData("date(t, '+25:00')", CelErrorCode.InvalidTimeZone)]
    [InlineData("time_of_day(t, 'Mars/Base')", CelErrorCode.TimeZoneNotFound)]
    [InlineData("timestamp(date('2026-11-19'), time_of_day('09:30'), 'Mars/Base')", CelErrorCode.TimeZoneNotFound)]
    [InlineData("timestamp(date('2026-11-19'), time_of_day('09:30'), '+1:5')", CelErrorCode.InvalidTimeZone)]
    [InlineData("{date('2026-11-19'): 1}", CelErrorCode.UnsupportedMapKeyType)]
    [InlineData("dyn(date('2026-11-19')) < t", CelErrorCode.EvalNoMatchingOverload)]
    [InlineData("dyn(date('2026-11-19')) < dyn(time_of_day('09:30'))", CelErrorCode.EvalNoMatchingOverload)]
    [InlineData("dyn(time_of_day('09:30')).addDays(1)", CelErrorCode.EvalNoMatchingOverload)]
    public static void FailingCall_IsItsErrorAtEvaluation(string expression, CelErrorCode code)
    {
        Assert.Equal(code, EvaluationError(expression).Code);
    }

    [Fact]
    public static void ErrorOfAZone_NamesTheZone()
    {
        CelError error = EvaluationError("date(t, 'Mars/Base')");

        Assert.Equal("Mars/Base", error.Subject);
        Assert.Contains("'Mars/Base'", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("date('2026-11-19') < t")]
    [InlineData("date('2026-11-19') < time_of_day('09:30')")]
    [InlineData("date('2026-11-19') + 1")]
    [InlineData("date('2026-11-19').addDays('1')")]
    [InlineData("date('2026-11-19').getHours()")]
    [InlineData("time_of_day('09:30').getFullYear()")]
    [InlineData("date(1, 2)")]
    [InlineData("date(1.5)")]
    [InlineData("timestamp(date('2026-11-19'))")]
    public static void CallWithTypesThatNoOverloadTakes_IsNoMatchingOverloadAtCheck(string expression)
    {
        CelCompileResult<CelValue> result = Environment.Compile<CelValue>(expression);

        Assert.False(result.IsSuccess);
        Assert.Equal(CelErrorCode.NoMatchingOverload, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public static void WithoutTheSet_TheFunctionsAreUndeclared()
    {
        CelEnvironment without = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

        foreach (string expression in new[] { "date('2026-11-19')", "time_of_day('09:30')", "type(1) == date" })
        {
            CelCompileResult<CelValue> result = without.Compile<CelValue>(expression);

            Assert.False(result.IsSuccess);
            Assert.Equal(CelErrorCode.UndeclaredReference, Assert.Single(result.Errors).Code);
        }
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(2u)]
    public static void VersionThatTheSetDoesNotHave_IsUnknownExtensionVersion(uint version)
    {
        CelExtension extension = CelExtensions.Calendar(version);
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddExtension(extension).Build();

        Assert.Equal(("calendar", version), (extension.Name, extension.Version));
        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.UnknownExtensionVersion, error.Code);
        Assert.Equal("extension 'calendar' has no version " + version + "; available versions: 1", error.Message);
    }

    [Fact]
    public static void SameSetAddedAgain_IsOneSet()
    {
        Assert.Same(CelExtensions.Calendar(1), CelExtensions.Calendar(1));
        Assert.True(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddExtension(CelExtensions.Calendar(1)).AddExtension(CelExtensions.Calendar(1)).Build().IsSuccess);
    }

    [Fact]
    public static void ValueOfAResult_HasTheTextOfItsKind()
    {
        Assert.Equal(CelValueKind.Date, Evaluate("date('2026-11-19')").Kind);
        Assert.Equal(CelValueKind.TimeOfDay, Evaluate("time_of_day('09:30')").Kind);
        Assert.Equal("date(\"2026-11-19\")", Evaluate("date('2026-11-19')").ToString());
        Assert.Equal("time_of_day(\"09:30:00\")", Evaluate("time_of_day('09:30')").ToString());
    }

    // The 36 overloads, each with the cost that the page gives: a call is 1 unit and the strings that it reads and writes add ⌈L/10⌉; a
    // variable read costs 1; a constant, 0. The database of the environment is the application's, so a zone adds no charge of its own.
    private static readonly (string Id, string Expression, ulong Cost)[] Costs =
    [
        ("string_to_date", "date('2026-11-19')", 2),
        ("int64_int64_int64_to_date", "date(2026, 11, 19)", 1),
        ("timestamp_to_date", "date(t)", 2),
        ("timestamp_to_date_with_tz", "date(t, 'Europe/Moscow')", 4),
        ("date_to_date", "date(date(2026, 11, 19))", 2),
        ("string_to_time_of_day", "time_of_day('09:30')", 2),
        ("timestamp_to_time_of_day", "time_of_day(t)", 2),
        ("timestamp_to_time_of_day_with_tz", "time_of_day(t, 'Europe/Moscow')", 4),
        ("time_of_day_to_time_of_day", "time_of_day(time_of_day('09:30'))", 3),
        ("date_to_string", "string(date(2026, 11, 19))", 3),
        ("time_of_day_to_string", "string(time_of_day('09:30'))", 4),
        ("date_time_of_day_to_timestamp", "timestamp(date(2026, 11, 19), time_of_day('09:30'))", 4),
        ("date_time_of_day_to_timestamp_with_tz", "timestamp(date(2026, 11, 19), time_of_day('09:30'), 'Europe/Moscow')", 6),
        ("date_to_year", "date(2026, 11, 19).getFullYear()", 2),
        ("date_to_month", "date(2026, 11, 19).getMonth()", 2),
        ("date_to_day_of_month_1_based", "date(2026, 11, 19).getDate()", 2),
        ("date_to_day_of_month", "date(2026, 11, 19).getDayOfMonth()", 2),
        ("date_to_day_of_week", "date(2026, 11, 19).getDayOfWeek()", 2),
        ("date_to_day_of_year", "date(2026, 11, 19).getDayOfYear()", 2),
        ("time_of_day_to_hours", "time_of_day('09:30').getHours()", 3),
        ("time_of_day_to_minutes", "time_of_day('09:30').getMinutes()", 3),
        ("time_of_day_to_seconds", "time_of_day('09:30').getSeconds()", 3),
        ("time_of_day_to_milliseconds", "time_of_day('09:30').getMilliseconds()", 3),
        ("date_add_days", "date(2026, 11, 19).addDays(1)", 2),
        ("date_add_months", "date(2026, 11, 19).addMonths(1)", 2),
        ("date_add_years", "date(2026, 11, 19).addYears(1)", 2),
        ("subtract_date_date", "date(2026, 11, 19) - date(2026, 11, 18)", 3),
        ("subtract_time_of_day_time_of_day", "time_of_day('09:30') - time_of_day('09:00')", 5),
        ("less_date", "date(2026, 11, 19) < date(2026, 11, 20)", 3),
        ("less_equals_date", "date(2026, 11, 19) <= date(2026, 11, 20)", 3),
        ("greater_date", "date(2026, 11, 19) > date(2026, 11, 20)", 3),
        ("greater_equals_date", "date(2026, 11, 19) >= date(2026, 11, 20)", 3),
        ("less_time_of_day", "time_of_day('09:30') < time_of_day('09:31')", 5),
        ("less_equals_time_of_day", "time_of_day('09:30') <= time_of_day('09:31')", 5),
        ("greater_time_of_day", "time_of_day('09:30') > time_of_day('09:31')", 5),
        ("greater_equals_time_of_day", "time_of_day('09:30') >= time_of_day('09:31')", 5),
    ];

    public static TheoryData<string, string, ulong> CostRows
    {
        get
        {
            var data = new TheoryData<string, string, ulong>();
            foreach ((string id, string expression, ulong cost) in Costs)
            {
                data.Add(id, expression, cost);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CostRows))]
    public static void EveryOverload_HasItsCost(string id, string expression, ulong cost)
    {
        Assert.NotEmpty(id);
        CelProgram<CelValue> program = Environment.Compile<CelValue>(expression).GetProgramOrThrow();

        CelEvalResult<CelValue> result = program.Evaluate(Bindings, new CelEvalOptions { CostLimit = 1000 }, CancellationToken.None);

        Assert.True(result.IsSuccess, expression);
        Assert.Equal(cost, result.Cost);
    }

    [Fact]
    public static void TheCostRows_NameEveryOverloadOfTheSetOnce()
    {
        string[] declared = [.. Environment.Functions
            .SelectMany(function => function.Overloads)
            .Select(overload => overload.OverloadId)
            .Where(id => id.Contains("date", StringComparison.Ordinal) || id.Contains("time_of_day", StringComparison.Ordinal))
            .Where(id => !IsOperator(id))
            .Order(StringComparer.Ordinal)];
        string[] named = [.. Costs.Select(row => row.Id).Order(StringComparer.Ordinal)];

        Assert.Equal(36, named.Length);
        Assert.Equal(named.Where(id => !IsOperator(id)), declared);
    }

    // The description of the environment lists the functions that have a name, not the operators.
    private static bool IsOperator(string id) =>
        id.StartsWith("less", StringComparison.Ordinal) || id.StartsWith("greater", StringComparison.Ordinal) || id.StartsWith("subtract", StringComparison.Ordinal);

    private static CelValue Evaluate(string expression) =>
        Environment.Compile<CelValue>(expression).GetProgramOrThrow().Evaluate(Bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

    private static CelError EvaluationError(string expression)
    {
        CelEvalResult<CelValue> result = Environment.Compile<CelValue>(expression).GetProgramOrThrow().Evaluate(Bindings, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess, expression);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        return error;
    }
}
