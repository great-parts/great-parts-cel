using System;
using System.Linq;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Declarations;

// Typed functions of the application with arguments and results of time: the overload identifier is written with the short
// names of the types of time, the declared types are the types of time, and the build compares them with the delegate.
public sealed class TimeFunctionDeclarationTests
{
    [Fact]
    public static void AddFunction_WritesTheTypesOfTimeWithTheirShortNamesInTheIdentifier()
    {
        DeclarationIndex index = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction<DateTimeOffset, long>("unix", moment => moment.ToUnixTimeSeconds())
            .AddFunction<TimeSpan, TimeSpan>("twice", span => span + span)
            .AddFunction<CelTimestamp, CelDuration, CelTimestamp>("shift", (moment, span) => moment)
            .Build()
            .GetEnvironmentOrThrow()
            .Declarations;

        Assert.Equal(["unix_timestamp"], index.GetOverloads("unix", 1).Select(overload => overload.Declaration.OverloadId));
        Assert.Equal(["twice_duration"], index.GetOverloads("twice", 1).Select(overload => overload.Declaration.OverloadId));
        Assert.Equal(["shift_timestamp_duration"], index.GetOverloads("shift", 2).Select(overload => overload.Declaration.OverloadId));
        Assert.Equal([CelType.Timestamp], index.GetOverloads("unix", 1)[0].Declaration.ParameterTypes);
        Assert.Equal(CelType.Duration, index.GetOverloads("twice", 1)[0].Declaration.ResultType);
        Assert.Equal(CelType.Timestamp, index.GetOverloads("shift", 2)[0].Declaration.ResultType);
    }

    [Fact]
    public static void FunctionOfTime_IsCalledWithAndWithoutTypeCheck()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction<DateTimeOffset, long>("unix", moment => moment.ToUnixTimeSeconds())
            .Build()
            .GetEnvironmentOrThrow();

        CelValue checkedValue = environment.Evaluate<CelValue>("unix(timestamp('2009-02-13T23:31:30Z'))", new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).GetValueOrThrow();
        CelValue uncheckedValue = environment.EvaluateWithoutTypeCheck(environment.Parse("unix(timestamp(1234567890))").Expression!, new CelBindings()).GetValueOrThrow();

        Assert.True(checkedValue.TryGetInt64(out long fromString));
        Assert.True(uncheckedValue.TryGetInt64(out long fromNumber));
        Assert.Equal((1_234_567_890L, 1_234_567_890L), (fromString, fromNumber));
    }
}
