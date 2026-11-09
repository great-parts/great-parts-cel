using System.Collections.Generic;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// The evaluation of the receivers of time with a zone and of time with dyn. A zone name is resolved at
// evaluation, never at compilation: a constant zone that is not valid compiles, and the error comes when
// the call is evaluated, at the call (the "(" after the name); a branch that is not evaluated raises
// nothing, and || absorbs the error. The zone may be a variable or dyn. UTC and fixed offsets need no
// database; any other name without a database, or with Empty, is TimeZoneNotFound (the text of that error
// is the library's own there, so the rows state the start of the message). UTC is built in: a database
// entry of that name does not replace it. A receiver or an argument of dyn is matched against the
// overloads at evaluation: a kind that has none is EvalNoMatchingOverload at the call; with static
// types the checker says NoMatchingOverload. The internal entry that evaluates without type checking gives
// the same values and errors. The database of the rows with a version is that of TimeZoneData.
[Trait("Requirement", "R-5")]
[Trait("Requirement", "+R-4")]
public sealed class TimeZoneEvaluationTests
{
    private static readonly string[] TwoThrees = ["int 3", "int 3"];

    private const string WithDatabase = """
        timestamp(0).getHours('Mars/Base') | error TimeZoneNotFound 21: time zone 'Mars/Base' is not found in the time zone database of the environment (version 2026a)
        timestamp(0).getHours('+1:5') | error InvalidTimeZone 21: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        true ? 1 : timestamp(0).getHours('Mars/Base') | int 1
        false && timestamp(0).getHours('Mars/Base') == 0 | bool false
        true || timestamp(0).getHours('Mars/Base') == 0 | bool true
        timestamp(0).getHours('Mars/Base') == 0 || true | bool true
        timestamp(0).getHours('+1:5') == 0 || true | bool true
        [1].exists(x, timestamp(0).getHours('Mars') == 0) | error TimeZoneNotFound 35: time zone 'Mars' is not found in the time zone database of the environment (version 2026a)
        ts.getHours(z) | int 18
        timestamp(0).getHours(zd) | int 5
        timestamp(0).getHours(zn) | error EvalNoMatchingOverload 21: *
        timestamp(0).getHours(dyn('+03:00')) | int 3
        timestamp(0).getHours(dyn(5)) | error EvalNoMatchingOverload 21: *
        dyn(timestamp(0)).getHours('+03:00') | int 3
        dyn(timestamp(0)).getHours(dyn('Test/Moved')) | int 2
        dyn(timestamp(0)).getHours() | int 0
        dyn(duration('3723s')).getHours() | int 1
        dyn(duration('3723s')).getMinutes() | int 62
        dyn(1).getHours() | error EvalNoMatchingOverload 15: *
        dyn('x').getFullYear() | error EvalNoMatchingOverload 20: *
        dyn(duration('1s')).getHours('UTC') | error EvalNoMatchingOverload 28: *
        dyn(duration('1s')).getFullYear() | error EvalNoMatchingOverload 31: *
        duration('1s').getHours('UTC') | compile NoMatchingOverload
        duration('1s').getFullYear() | compile NoMatchingOverload
        timestamp(0).getHours(5) | compile NoMatchingOverload
        timestamp(0).getHours('UTC', 'UTC') | compile NoMatchingOverload
        'x'.getHours() | compile NoMatchingOverload
        (1).getHours() | compile NoMatchingOverload
        """;

    private const string WithoutTypeCheck = """
        timestamp(0) + duration('1s') | ts 1 0
        duration('1s') + duration('1s') | du 2000000000
        timestamp('2009-02-13T23:31:30Z') - timestamp('2009-02-13T22:31:30Z') | du 3600000000000
        timestamp('2009-02-13T23:31:30Z').getHours('+03:00') | int 2
        timestamp('2009-02-13T23:31:30Z').getHours(z) | int 18
        duration('3723.456s').getMilliseconds() | int 456
        int(timestamp(1234567890)) | int 1234567890
        string(duration('1.5s')) | str 1.5s
        timestamp(0) < timestamp(1) | bool true
        duration('1s') == duration('1000ms') | bool true
        timestamp(0) + 1 | error EvalNoMatchingOverload 13: *
        timestamp(0) < duration('1s') | error EvalNoMatchingOverload 13: *
        timestamp(0).getHours(5) | error EvalNoMatchingOverload 21: *
        duration('1s').getHours('UTC') | error EvalNoMatchingOverload 23: *
        timestamp('x') | error InvalidArgument 9: invalid argument to 'timestamp': *
        timestamp('1').getHours('Mars') | error InvalidArgument 9: invalid argument to 'timestamp': *
        timestamp(253402300800) | error ConversionRange 9: 253402300800 is out of the range of timestamp
        timestamp('2009-02-13T23:31:30Z').getHours('Mars') | error TimeZoneNotFound 42: time zone 'Mars' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('+1:5') | error InvalidTimeZone 42: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('9999-12-31T23:59:59Z') + duration('1s') | error Overflow 34: timestamp overflow
        """;

    private const string NoDatabase = """
        timestamp('2009-02-13T23:31:30Z').getHours('UTC') | int 23
        timestamp('2009-02-13T23:31:30Z').getHours('+03:00') | int 2
        timestamp('2009-02-13T23:31:30Z').getHours('-03:00') | int 20
        timestamp('2009-02-13T23:31:30Z').getHours('Test/Fixed') | error TimeZoneNotFound 42: *
        timestamp('2009-02-13T23:31:30Z').getHours('') | error TimeZoneNotFound 42: *
        timestamp('2009-02-13T23:31:30Z').getHours('Local') | error TimeZoneNotFound 42: *
        timestamp('2009-02-13T23:31:30Z').getHours('+1:5') | error InvalidTimeZone 42: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        """;

    private const string UtcIsBuiltIn = """
        timestamp('2009-02-13T23:31:30Z').getHours('UTC') | int 23
        timestamp('2009-02-13T23:31:30Z').getHours('+03:00') | int 2
        """;

    private readonly CelEnvironment withDatabase = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .UseTimeZones(TimeZoneData.Database())
        .AddVariable<CelTimestamp>("ts")
        .AddVariable<string>("z")
        .AddVariable("zd", CelType.Dyn)
        .AddVariable("zn", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("ts", new CelTimestamp(1234567890, 0))
        .Add("z", "Test/Rule")
        .Add("zd", "Test/Fixed")
        .Add("zn", 5L);

    [Fact]
    public void ZoneAndDynRows_AreTheirOutcomes()
    {
        Assert.Empty(TimeRows.Check(withDatabase, WithDatabase, bindings));
    }

    [Fact]
    public void EvaluationWithoutTypeCheck_GivesTheSameValuesAndErrors()
    {
        Assert.Empty(TimeRows.Check(withDatabase, WithoutTypeCheck, bindings, checkTypes: false));
    }

    // A constant zone is not looked up at compilation: the program of every row compiles.
    [Theory]
    [InlineData("Mars/Base")]
    [InlineData("+1:5")]
    [InlineData("")]
    [InlineData("Local")]
    public void ConstantZoneThatIsNotValid_CompilesAndFailsAtEvaluation(string zone)
    {
        CelCompileResult<long> compiled = withDatabase.Compile<long>("timestamp(0).getHours('" + zone + "')");

        Assert.True(compiled.IsSuccess);
        Assert.False(compiled.GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).IsSuccess);
    }

    // Without a database and with Empty only UTC and fixed offsets resolve.
    [Fact]
    public void WithoutADatabase_OnlyUtcAndOffsetsResolve()
    {
        CelEnvironment bare = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();
        CelEnvironment empty = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .UseTimeZones(CelTimeZoneDatabase.Empty)
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Empty(TimeRows.Check(bare, NoDatabase));
        Assert.Empty(TimeRows.Check(empty, NoDatabase));
    }

    // UTC is built in: a database with an identifier UTC, here holding the data of +05:45, does not change it.
    [Fact]
    public void Utc_IsNotReplacedByADatabaseEntry()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .UseTimeZones(CelTimeZoneDatabase.Create([TimeZoneData.Pair("UTC", TimeZoneData.Fixed)]))
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Empty(TimeRows.Check(environment, UtcIsBuiltIn));
    }

    // The same expression gives the same value in two environments with databases of the same data: the
    // database is part of the immutable environment, the result does not depend on the order of calls or
    // on anything of the process.
    [Fact]
    public void TwoEnvironmentsWithTheSameData_GiveTheSameValues()
    {
        var results = new List<string>();
        foreach (CelTimeZoneDatabase database in new[] { TimeZoneData.Database(), TimeZoneData.Database() })
        {
            CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).UseTimeZones(database).Build().GetEnvironmentOrThrow();
            results.Add(TimeRows.Outcome(environment, "timestamp('2023-03-12T07:00:00Z').getHours('Test/Rule')"));
        }

        Assert.Equal<IEnumerable<string>>(TwoThrees, results);
    }
}
