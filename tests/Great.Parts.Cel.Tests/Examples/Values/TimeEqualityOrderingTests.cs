using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// Equality and order of the values of time. Moments are equal when they are the same point in time (the
// offset of a string is not kept, so 23:31:30+01:00 and 22:31:30Z are equal), durations when they have the
// same nanoseconds; the nanosecond counts. Values of different kinds are never equal and never an error
// (a timestamp and a duration, a timestamp and a string, a number and a duration); time as a list element,
// a map value and an operand of "in" compares the same way. Order is defined within a kind of time: a moment
// by the point in time, a duration by the length; the order across kinds, or against a number, has no
// overload. Rows are in the form of TimeRows.
[Trait("Requirement", "F-1")]
[Trait("Requirement", "R-1")]
public sealed class TimeEqualityOrderingTests
{
    private const string Rows = """
        timestamp(0) == timestamp(0) | bool true
        timestamp(0) == timestamp('1970-01-01T00:00:00Z') | bool true
        timestamp('2009-02-13T23:31:30+01:00') == timestamp('2009-02-13T22:31:30Z') | bool true
        timestamp(0) == timestamp(1) | bool false
        timestamp(0) != timestamp(1) | bool true
        timestamp(0) != timestamp(0) | bool false
        timestamp('2009-02-13T23:31:30.000000001Z') == timestamp('2009-02-13T23:31:30Z') | bool false
        duration('1h') == duration('3600s') | bool true
        duration('1h') != duration('60m') | bool false
        duration('1ns') == duration('0s') | bool false
        duration('0') == duration('-0s') | bool true
        duration('1s') != duration('-1s') | bool true
        dyn(timestamp(0)) == dyn(timestamp(0)) | bool true
        dyn(duration('1s')) == dyn(duration('1000ms')) | bool true
        dyn(timestamp(0)) == dyn(duration('0s')) | bool false
        dyn(timestamp(0)) != dyn(duration('0s')) | bool true
        dyn(timestamp(0)) == dyn(0) | bool false
        dyn(duration('0s')) == dyn(0) | bool false
        dyn(duration('0s')) == dyn(0.0) | bool false
        dyn(timestamp(0)) == dyn('1970-01-01T00:00:00Z') | bool false
        dyn(duration('1s')) == dyn('1s') | bool false
        dyn(timestamp(0)) == dyn(null) | bool false
        timestamp(1) in [timestamp(0), timestamp(1)] | bool true
        duration('1s') in [duration('2s')] | bool false
        [timestamp(0), duration('1s')] == [timestamp(0), duration('1s')] | bool true
        [timestamp(0), duration('1s')] == [timestamp(0), duration('2s')] | bool false
        {'a': timestamp(0)} == {'a': timestamp(0)} | bool true
        {'a': timestamp(0)}.a == timestamp(0) | bool true
        {'a': duration('1s')}['a'] == duration('1s') | bool true
        {'a': duration('1s')} == {'a': duration('2s')} | bool false
        timestamp(0) < timestamp(1) | bool true
        timestamp(1) < timestamp(0) | bool false
        timestamp(0) < timestamp(0) | bool false
        timestamp(0) <= timestamp(0) | bool true
        timestamp(0) > timestamp(-1) | bool true
        timestamp(0) >= timestamp(1) | bool false
        timestamp(0) >= timestamp(0) | bool true
        timestamp('2009-02-13T23:31:30.000000001Z') > timestamp('2009-02-13T23:31:30Z') | bool true
        timestamp('0001-01-01T00:00:00Z') < timestamp('9999-12-31T23:59:59.999999999Z') | bool true
        timestamp('2009-02-13T23:31:30+01:00') < timestamp('2009-02-13T23:31:30Z') | bool true
        timestamp('1969-12-31T23:59:59.999999999Z') < timestamp('1970-01-01T00:00:00Z') | bool true
        duration('1s') < duration('1.000000001s') | bool true
        duration('-1s') < duration('0s') | bool true
        duration('-9223372036854775808ns') < duration('9223372036854775807ns') | bool true
        duration('1h') >= duration('60m') | bool true
        duration('1h') > duration('60m') | bool false
        duration('1h') <= duration('59m') | bool false
        duration('-1s') > duration('-2s') | bool true
        dyn(timestamp(0)) < dyn(timestamp(1)) | bool true
        dyn(duration('1s')) >= dyn(duration('1s')) | bool true
        timestamp(0) < duration('1s') | compile NoMatchingOverload
        timestamp(0) < 1 | compile NoMatchingOverload
        duration('1s') > 1.5 | compile NoMatchingOverload
        dyn(timestamp(0)) < dyn(duration('1s')) | error EvalNoMatchingOverload 18: *
        dyn(timestamp(0)) < dyn(0) | error EvalNoMatchingOverload 18: *
        dyn(duration('1s')) >= dyn('1s') | error EvalNoMatchingOverload 20: *
        dyn(duration('1s')) <= dyn(1.5) | error EvalNoMatchingOverload 20: *
        dyn(timestamp(0)) > dyn(0u) | error EvalNoMatchingOverload 18: *
        """;

    private readonly CelEnvironment environment =
        new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    [Fact]
    public void EveryRow_IsItsOutcome()
    {
        Assert.Empty(TimeRows.Check(environment, Rows));
    }
}
