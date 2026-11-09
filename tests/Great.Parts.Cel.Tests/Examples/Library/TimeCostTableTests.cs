using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The cost of the functions of time, the rows of the cost page for time, checked as the rows of CostTableTests are: "row | expression | cost",
// the total cost of the expression is exact, so the evaluation succeeds with that cost as the limit and
// fails with CostLimitExceeded at one unit less. The total is the cost of the row plus the nodes around
// it: a literal 0, a read of a variable 1.
//   conversions and receivers without a zone, arithmetic, order, equality of time: 1;
//   timestamp(string) and duration(string): 1 + the length of the string over 10 rounded up;
//   string(timestamp) and string(duration): 1 + the length of the result over 10 rounded up, the result of
//     at most 30, known before the string is made;
//   a receiver with a zone: 1 + the length of the zone string over 10 rounded up; a constant zone, found
//     at planning, and a constant string of time folded at planning cost the same as a variable: the cost
//     is the text of the expression and the data, not what the planner did.
// Variables: i = 1 (int); ts = 2009-02-13T23:31:30Z (timestamp); du = 5400s (duration); tsstr =
// '2009-02-13T23:31:30Z' (20 code units), dustr = '1h30m' (5), z = '+03:00' (6), zlong = 'Test/ZeroBased'
// (14) (string). The database is that of TimeZoneData.
[Trait("Requirement", "R-3")]
[Trait("Requirement", "R-5")]
public sealed class TimeCostTableTests
{
    private const string Rows = """
        timestamp_to_timestamp | timestamp(ts) | 2
        int64_to_timestamp | timestamp(i) | 2
        string_to_timestamp, 20 | timestamp(tsstr) | 4
        string_to_timestamp, constant folded, 20 | timestamp('2009-02-13T23:31:30Z') | 3
        timestamp_to_int64 | int(ts) | 2
        timestamp_to_string, result of 20 | string(ts) | 4
        duration_to_duration | duration(du) | 2
        string_to_duration, 5 | duration(dustr) | 3
        string_to_duration, constant folded, 3 | duration('72h') | 2
        duration_to_int64 | int(du) | 2
        duration_to_string, result of 5 | string(du) | 3
        timestamp receiver getFullYear | ts.getFullYear() | 2
        timestamp receiver getMonth | ts.getMonth() | 2
        timestamp receiver getDayOfYear | ts.getDayOfYear() | 2
        timestamp receiver getDayOfMonth | ts.getDayOfMonth() | 2
        timestamp receiver getDate | ts.getDate() | 2
        timestamp receiver getDayOfWeek | ts.getDayOfWeek() | 2
        timestamp receiver getHours | ts.getHours() | 2
        timestamp receiver getMinutes | ts.getMinutes() | 2
        timestamp receiver getSeconds | ts.getSeconds() | 2
        timestamp receiver getMilliseconds | ts.getMilliseconds() | 2
        duration receiver getHours | du.getHours() | 2
        duration receiver getMinutes | du.getMinutes() | 2
        duration receiver getSeconds | du.getSeconds() | 2
        duration receiver getMilliseconds | du.getMilliseconds() | 2
        timestamp receiver getFullYear with a zone variable of 6 | ts.getFullYear(z) | 4
        timestamp receiver getMonth with a zone variable of 6 | ts.getMonth(z) | 4
        timestamp receiver getDayOfYear with a zone variable of 6 | ts.getDayOfYear(z) | 4
        timestamp receiver getDayOfMonth with a zone variable of 6 | ts.getDayOfMonth(z) | 4
        timestamp receiver getDate with a zone variable of 6 | ts.getDate(z) | 4
        timestamp receiver getDayOfWeek with a zone variable of 6 | ts.getDayOfWeek(z) | 4
        timestamp receiver getHours with a zone variable of 6 | ts.getHours(z) | 4
        timestamp receiver getMinutes with a zone variable of 6 | ts.getMinutes(z) | 4
        timestamp receiver getSeconds with a zone variable of 6 | ts.getSeconds(z) | 4
        timestamp receiver getMilliseconds with a zone variable of 6 | ts.getMilliseconds(z) | 4
        with a zone, constant of 3 (UTC) | ts.getHours('UTC') | 3
        with a zone, constant of 6 | ts.getHours('+03:00') | 3
        with a zone, constant of 10 (an IANA name) | ts.getHours('Test/Moved') | 3
        with a zone, constant of 14 (an IANA name) | ts.getHours('Test/ZeroBased') | 4
        with a zone, a variable of 14 | ts.getHours(zlong) | 5
        add_timestamp_duration | ts + du | 3
        add_duration_timestamp | du + ts | 3
        add_duration_duration | du + du | 3
        subtract_timestamp_duration | ts - du | 3
        subtract_timestamp_timestamp | ts - ts | 3
        subtract_duration_duration | du - du | 3
        less_timestamp | ts < ts | 3
        less_equals_duration | du <= du | 3
        greater_timestamp | ts > ts | 3
        greater_equals_duration | du >= du | 3
        equals timestamp | ts == ts | 3
        not_equals duration | du != du | 3
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .UseTimeZones(TimeZoneData.Database())
        .AddVariable<long>("i")
        .AddVariable<CelTimestamp>("ts")
        .AddVariable<CelDuration>("du")
        .AddVariable<string>("tsstr")
        .AddVariable<string>("dustr")
        .AddVariable<string>("z")
        .AddVariable<string>("zlong")
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("i", 1L)
        .Add("ts", new CelTimestamp(1234567890, 0))
        .Add("du", new CelDuration(5_400_000_000_000L))
        .Add("tsstr", "2009-02-13T23:31:30Z")
        .Add("dustr", "1h30m")
        .Add("z", "+03:00")
        .Add("zlong", "Test/ZeroBased");

    [Fact]
    public void EveryRow_CostsWhatTheCostPageSays()
    {
        var failures = new List<string>();
        foreach (string row in Rows.Split('\n'))
        {
            string[] cells = row.Trim().Split(" | ");
            string? failure = Check(cells[1], ulong.Parse(cells[2], CultureInfo.InvariantCulture));
            if (failure is not null)
            {
                failures.Add(row.Trim() + ": " + failure);
            }
        }

        Assert.Empty(failures);
    }

    private string? Check(string expression, ulong cost)
    {
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(expression);
        if (!compiled.IsSuccess)
        {
            return "compile error " + compiled.Errors[0].Code + ": " + compiled.Errors[0].Message;
        }

        CelProgram<CelValue> program = compiled.GetProgramOrThrow();
        CelEvalResult<CelValue> atCost = program.Evaluate(bindings, new CelEvalOptions { CostLimit = cost }, CancellationToken.None);
        if (!atCost.IsSuccess)
        {
            return "limit " + cost.ToString(CultureInfo.InvariantCulture) + ": " + atCost.Errors[0].Code + " at " + atCost.Errors[0].Location.Offset.ToString(CultureInfo.InvariantCulture);
        }

        CelEvalResult<CelValue> belowCost = program.Evaluate(bindings, new CelEvalOptions { CostLimit = cost - 1 }, CancellationToken.None);
        return belowCost.Errors.Length == 1 && belowCost.Errors[0].Code == CelErrorCode.CostLimitExceeded
            ? null
            : "limit " + (cost - 1).ToString(CultureInfo.InvariantCulture) + " is enough";
    }
}
