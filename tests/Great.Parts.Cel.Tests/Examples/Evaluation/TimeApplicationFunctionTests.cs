using System;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// Application functions with arguments and results of time. A typed delegate takes DateTimeOffset or
// CelTimestamp for a timestamp and TimeSpan or CelDuration for a duration, and gives the same; the .NET
// structures count in ticks of 100 nanoseconds, so an argument whose nanoseconds are not a multiple of 100
// does not fit a DateTimeOffset or a TimeSpan parameter: ArgumentConversion at the call, the "(" after the
// name, with the text of the table of codes (the CEL type of time is written timestamp or duration in it);
// CelTimestamp and CelDuration take every value. A result of time is a value of the CEL kind: an untyped
// implementation that gives a duration where a timestamp is declared is ImplementationResultMismatch at
// the call. Rows are in the form of TimeRows.
[Trait("Requirement", "F-3")]
[Trait("Requirement", "+R-10")]
public sealed class TimeApplicationFunctionTests
{
    private const string Rows = """
        unix(timestamp('2009-02-13T23:31:30Z')) | int 1234567890
        unix(timestamp('2009-02-13T23:31:30.0000001Z')) | int 1234567890
        unix(dyn(timestamp(1234567890))) | int 1234567890
        unix(timestamp('2009-02-13T23:31:30.000000001Z')) | error ArgumentConversion 4: argument '1' of overload 'unix_timestamp' of function 'unix' cannot be converted from 'timestamp' to .NET type 'System.DateTimeOffset'
        twice(duration('1h')) | du 7200000000000
        twice(duration('-1.5s')) | du -3000000000
        twice(duration('1ns')) | error ArgumentConversion 5: argument '1' of overload 'twice_duration' of function 'twice' cannot be converted from 'duration' to .NET type 'System.TimeSpan'
        sinceEpoch(timestamp('1970-01-01T00:00:01.5Z')) | du 1500000000
        sinceEpoch(timestamp('1970-01-01T00:00:00.000000001Z')) | du 1
        fromEpoch(duration('1.5s')) | ts 1 500000000
        at(1234567890) | ts 1234567890 0
        type(at(0)) == google.protobuf.Timestamp | bool true
        type(fromEpoch(duration('1s'))) == google.protobuf.Timestamp | bool true
        at(0).getFullYear() | int 1970
        at(0) + duration('1h') | ts 3600 0
        wrong(0) | error ImplementationResultMismatch 5: *
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddFunction<DateTimeOffset, long>("unix", moment => moment.ToUnixTimeSeconds())
        .AddFunction<TimeSpan, TimeSpan>("twice", span => span + span)
        .AddFunction<CelTimestamp, CelDuration>("sinceEpoch", moment => new CelDuration((moment.Seconds * 1_000_000_000L) + moment.Nanoseconds))
        .AddFunction<CelDuration, CelTimestamp>("fromEpoch", span => new CelTimestamp(span.TotalNanoseconds / 1_000_000_000L, (int)(span.TotalNanoseconds % 1_000_000_000L)))
        .AddFunction<long, DateTimeOffset>("at", seconds => DateTimeOffset.FromUnixTimeSeconds(seconds))
        .AddFunction(new CelFunctionDecl("wrong", CelOverloadDecl.Global("wrong_int", [CelType.Int], CelType.Timestamp)))
        .AddFunctionImplementation("wrong_int", _ => CelValue.FromDuration(new CelDuration(1)))
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void EveryRow_IsItsOutcome()
    {
        Assert.Empty(TimeRows.Check(environment, Rows));
    }

    // A time argument that the delegate cannot take is refused before the call: the delegate runs only when
    // the argument fits.
    [Fact]
    public void ArgumentThatTheParameterCannotTake_DoesNotRunTheDelegate()
    {
        int calls = 0;
        CelEnvironment counting = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction<DateTimeOffset, long>("unix", moment =>
            {
                calls++;
                return moment.ToUnixTimeSeconds();
            })
            .Build()
            .GetEnvironmentOrThrow();

        CelProgram<CelValue> program = counting.Compile<CelValue>("unix(timestamp('2009-02-13T23:31:30.000000001Z'))").GetProgramOrThrow();
        CelEvalResult<CelValue> result = program.Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);

        Assert.Equal(CelErrorCode.ArgumentConversion, Assert.Single(result.Errors).Code);
        Assert.Equal(0, calls);
    }
}
