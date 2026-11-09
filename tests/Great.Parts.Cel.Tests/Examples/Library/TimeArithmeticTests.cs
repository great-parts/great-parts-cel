using Great.Parts.Cel.Tests.Examples.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The arithmetic of time of the standard library page: timestamp + duration and duration + timestamp
// move the moment, timestamp - duration moves it back, timestamp - timestamp is a duration, durations
// add and subtract. The results to the nanosecond, at the edges of the range of timestamp (0001-01-01T00:00:00Z
// to 9999-12-31T23:59:59.999999999Z) and of duration (a whole int64 of nanoseconds, about 292 years)
// that the page gives as the limits of time. The overflow of the results is Overflow, with its text and place, in
// TimeErrorTests. There is no other arithmetic of time: no sum of two moments, no product, no negation of
// a duration (the checker says NoMatchingOverload).
[Trait("Requirement", "F-1")]
[Trait("Requirement", "R-1")]
public sealed class TimeArithmeticTests
{
    private const string Rows = """
        timestamp('2009-02-13T23:31:30Z') + duration('1h') | ts 1234571490 0
        duration('1h') + timestamp('2009-02-13T23:31:30Z') | ts 1234571490 0
        timestamp('2009-02-13T23:31:30Z') - duration('1h') | ts 1234564290 0
        timestamp('2009-02-13T23:31:30Z') - timestamp('2009-02-13T22:31:30Z') | du 3600000000000
        timestamp('2009-02-13T22:31:30Z') - timestamp('2009-02-13T23:31:30Z') | du -3600000000000
        duration('1h') + duration('30m') | du 5400000000000
        duration('1h') - duration('90m') | du -1800000000000
        timestamp('2009-02-13T23:31:30.9Z') + duration('0.2s') | ts 1234567891 100000000
        timestamp('1970-01-01T00:00:00Z') - duration('1ns') | ts -1 999999999
        timestamp('9999-12-31T23:59:59.999999999Z') - duration('1ns') | ts 253402300799 999999998
        timestamp('9999-12-31T23:59:59.999999999Z') + duration('0s') | ts 253402300799 999999999
        timestamp('0001-01-01T00:00:00Z') + duration('1ns') | ts -62135596800 1
        timestamp('0001-01-01T00:00:00Z') + duration('9223372036854775807ns') | ts -52912224764 854775807
        timestamp('9999-12-31T23:59:59.999999999Z') - duration('9223372036854775807ns') | ts 244178928763 145224192
        timestamp('2262-04-11T23:47:16.854775807Z') - timestamp('1970-01-01T00:00:00Z') | du 9223372036854775807
        timestamp('1677-09-21T00:12:43.145224192Z') - timestamp('1970-01-01T00:00:00Z') | du -9223372036854775808
        duration('9223372036854775807ns') + duration('0s') | du 9223372036854775807
        duration('9223372036854775807ns') - duration('9223372036854775807ns') | du 0
        duration('-9223372036854775808ns') + duration('9223372036854775807ns') | du -1
        duration('-9223372036854775808ns') - duration('0s') | du -9223372036854775808
        duration('9223372036854775807ns') - duration('1ns') | du 9223372036854775806
        timestamp('2009-02-13T23:31:30Z') + timestamp('2009-02-13T23:31:30Z') | compile NoMatchingOverload
        duration('1s') - timestamp('2009-02-13T23:31:30Z') | compile NoMatchingOverload
        timestamp('2009-02-13T23:31:30Z') * 2 | compile NoMatchingOverload
        duration('1s') * 2 | compile NoMatchingOverload
        duration('1s') / duration('1s') | compile NoMatchingOverload
        -duration('1s') | compile NoMatchingOverload
        timestamp('2009-02-13T23:31:30Z') + 1 | compile NoMatchingOverload
        duration('1s') + 1 | compile NoMatchingOverload
        duration('1s') + 1.5 | compile NoMatchingOverload
        timestamp('2009-02-13T23:31:30Z') - 1 | compile NoMatchingOverload
        duration('1s') % duration('1s') | compile NoMatchingOverload
        timestamp('2009-02-13T23:31:30Z') < duration('1s') | compile NoMatchingOverload
        duration('1s') <= timestamp('2009-02-13T23:31:30Z') | compile NoMatchingOverload
        timestamp('2009-02-13T23:31:30Z') > 1 | compile NoMatchingOverload
        duration('1s') >= 1 | compile NoMatchingOverload
        """;

    private readonly CelEnvironment environment =
        new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    [Fact]
    public void EveryRow_IsItsOutcome()
    {
        Assert.Empty(TimeRows.Check(environment, Rows));
    }
}
