using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// The input rows of time of the type mapping: a binding of the .NET kind becomes the CEL value of the
// row. CelTimestamp and DateTimeOffset are a timestamp (a DateTimeOffset is the moment in UTC, its
// offset is not kept); DateTime of the kind Utc is a timestamp, and of any other kind is refused by
// CelBindings.Add with ArgumentException, since its meaning would depend on the zone of the process;
// CelDuration and TimeSpan are a duration (a tick is 100 nanoseconds); a TimeSpan outside the range of
// duration (an int64 of nanoseconds) is the evaluation error ConversionRange at the node that read the
// value. The nullable forms have the type of the value, and a null binding of such a variable is
// BindingTypeMismatch at the identifier. A value of time of the type dyn, as a list element and as a map
// value, is read the same way; a typed collection of time is outside the closed list of the elements:
// TypeNotDeclared for AddVariable<T> and ArgumentException for CelBindings.Add<T>.
[Trait("Requirement", "+R-10")]
[Trait("Requirement", "F-3")]
public sealed class TimeInputRowTests
{
    private static readonly DateTimeOffset Moment = new(2009, 2, 13, 23, 31, 30, TimeSpan.Zero);

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<DateTimeOffset>("dto")
        .AddVariable<DateTime>("dt")
        .AddVariable<CelTimestamp>("ct")
        .AddVariable<TimeSpan>("ts")
        .AddVariable<CelDuration>("cd")
        .AddVariable<DateTimeOffset?>("ndto")
        .AddVariable<TimeSpan?>("nts")
        .AddVariable("d", CelType.Dyn)
        .AddVariable("l", CelType.ListOf(CelType.Dyn))
        .AddVariable("m", CelType.MapOf(CelType.String, CelType.Dyn))
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void DateTimeOffset_IsTheMomentInUtc()
    {
        var shifted = new DateTimeOffset(2009, 2, 14, 0, 31, 30, TimeSpan.FromHours(1));

        Assert.True(Evaluate<bool>("dto == timestamp('2009-02-13T23:31:30Z')", new CelBindings().Add("dto", Moment)));
        Assert.True(Evaluate<bool>("dto == timestamp('2009-02-13T23:31:30Z') && dto.getHours() == 23", new CelBindings().Add("dto", shifted)));
        Assert.Equal("2009-02-13T23:31:30.0000001Z", Evaluate<string>("string(dto)", new CelBindings().Add("dto", Moment.AddTicks(1))));
        Assert.Equal("0001-01-01T00:00:00Z", Evaluate<string>("string(dto)", new CelBindings().Add("dto", DateTimeOffset.MinValue)));
        Assert.Equal("9999-12-31T23:59:59.9999999Z", Evaluate<string>("string(dto)", new CelBindings().Add("dto", DateTimeOffset.MaxValue)));
    }

    [Fact]
    public void DateTimeOfKindUtc_IsATimestamp_AndOfAnotherKindIsRefusedByAdd()
    {
        var utc = new DateTime(2009, 2, 13, 23, 31, 30, DateTimeKind.Utc);

        Assert.True(Evaluate<bool>("dt == timestamp('2009-02-13T23:31:30Z')", new CelBindings().Add("dt", utc)));
        Assert.Throws<ArgumentException>(() => new CelBindings().Add("dt", new DateTime(2009, 2, 13, 23, 31, 30, DateTimeKind.Local)));
        Assert.Throws<ArgumentException>(() => new CelBindings().Add("dt", new DateTime(2009, 2, 13, 23, 31, 30, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void CelTimestamp_IsKeptToTheNanosecond()
    {
        var bindings = new CelBindings().Add("ct", new CelTimestamp(1234567890, 123456789));

        Assert.Equal("2009-02-13T23:31:30.123456789Z", Evaluate<string>("string(ct)", bindings));
        Assert.Equal(123L, Evaluate<long>("ct.getMilliseconds()", bindings));
        Assert.Equal(1234567890L, Evaluate<long>("int(ct)", bindings));
    }

    [Fact]
    public void TimeSpanAndCelDuration_AreADuration()
    {
        Assert.True(Evaluate<bool>("ts == duration('1h30m')", new CelBindings().Add("ts", TimeSpan.FromMinutes(90))));
        Assert.Equal(100L, Evaluate<long>("int(ts)", new CelBindings().Add("ts", TimeSpan.FromTicks(1))));
        Assert.Equal(-1500000000L, Evaluate<long>("int(ts)", new CelBindings().Add("ts", TimeSpan.FromSeconds(-1.5))));
        Assert.Equal(1234567891L, Evaluate<long>("int(cd)", new CelBindings().Add("cd", new CelDuration(1234567891))));
        Assert.True(Evaluate<bool>("cd == duration('9223372036854775807ns')", new CelBindings().Add("cd", new CelDuration(long.MaxValue))));
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void TimeSpanOutsideTheRangeOfDuration_IsConversionRangeAtTheIdentifier()
    {
        foreach (TimeSpan value in new[] { TimeSpan.MaxValue, TimeSpan.MinValue, TimeSpan.FromTicks((long.MaxValue / 100) + 1) })
        {
            CelEvalResult<CelValue> result = Run("1 + int(ts)", new CelBindings().Add("ts", value));

            CelError error = Assert.Single(result.Errors);
            Assert.Equal((CelErrorCode.ConversionRange, CelErrorStage.Evaluate, 8), (error.Code, error.Stage, error.Location.Offset));
        }

        Assert.Equal(long.MaxValue / 100 * 100, Evaluate<long>("int(ts)", new CelBindings().Add("ts", TimeSpan.FromTicks(long.MaxValue / 100))));
    }

    [Fact]
    public void NullableOfTime_HasTheTypeOfTheValue_AndANullBindingIsBindingTypeMismatch()
    {
        Assert.True(Evaluate<bool>("ndto == timestamp('2009-02-13T23:31:30Z')", new CelBindings().Add<DateTimeOffset?>("ndto", Moment)));
        Assert.True(Evaluate<bool>("nts == duration('1h')", new CelBindings().Add<TimeSpan?>("nts", TimeSpan.FromHours(1))));

        foreach (string name in new[] { "ndto", "nts" })
        {
            CelBindings bindings = name == "ndto" ? new CelBindings().Add<DateTimeOffset?>(name, null) : new CelBindings().Add<TimeSpan?>(name, null);

            CelError error = Assert.Single(Run("[" + name + "]", bindings).Errors);
            Assert.Equal((CelErrorCode.BindingTypeMismatch, CelErrorStage.Evaluate, 1), (error.Code, error.Stage, error.Location.Offset));
        }
    }

    [Fact]
    public void TimeAsDyn_AListElementAndAMapValue_IsRead()
    {
        var bindings = new CelBindings()
            .Add("d", Moment)
            .Add("l", CelList.Create(CelValue.FromTimestamp(CelTimestamp.FromDateTimeOffset(Moment)), CelValue.FromDuration(new CelDuration(5))))
            .Add("m", CelMap.Create([new KeyValuePair<CelValue, CelValue>(CelValue.FromString("a"), CelValue.FromDuration(new CelDuration(1_000_000_000)))]));

        Assert.True(Evaluate<bool>("d == timestamp('2009-02-13T23:31:30Z') && type(d) == google.protobuf.Timestamp", bindings));
        Assert.True(Evaluate<bool>("l[0] == timestamp('2009-02-13T23:31:30Z') && l[1] == duration('5ns')", bindings));
        Assert.True(Evaluate<bool>("m.a == duration('1s') && type(m.a) == google.protobuf.Duration", bindings));
        Assert.True(Evaluate<bool>("dyn(d).getFullYear() == 2009", bindings));
        Assert.True(Evaluate<bool>("type(d) == google.protobuf.Timestamp", new CelBindings().Add("d", Moment)));
        Assert.True(Evaluate<bool>("type(d) == google.protobuf.Duration", new CelBindings().Add("d", TimeSpan.FromSeconds(1))));
        Assert.True(Evaluate<bool>("type(d) == google.protobuf.Duration", new CelBindings().Add("d", new CelDuration(1))));
        Assert.True(Evaluate<bool>("type(d) == google.protobuf.Timestamp", new CelBindings().Add("d", new CelTimestamp(1, 1))));
    }

    // The closed list of the elements of a typed collection has no time: the element of a list or a map
    // value is read through CelList and CelMap (above) or dyn.
    [Fact]
    public void TypedCollectionOfTime_IsTypeNotDeclaredAndArgumentException()
    {
        AssertOutsideTheRows<IReadOnlyList<DateTimeOffset>>(
            [Moment],
            "System.Collections.Generic.IReadOnlyList`1[System.DateTimeOffset]");
        AssertOutsideTheRows<IReadOnlyDictionary<string, TimeSpan>>(
            new Dictionary<string, TimeSpan> { ["a"] = TimeSpan.FromHours(1) },
            "System.Collections.Generic.IReadOnlyDictionary`2[System.String,System.TimeSpan]");
    }

    private static void AssertOutsideTheRows<T>(T value, string typeName)
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<T>("v").Build();

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            (CelErrorCode.TypeNotDeclared, CelErrorStage.Build, "variable 'v' has type '" + typeName + "', which no type source or extension of the environment declares"),
            (error.Code, error.Stage, error.Message));
        Assert.Throws<ArgumentException>(() => new CelBindings().Add("v", value));
    }

    private CelEvalResult<CelValue> Run(string expression, CelBindings bindings) =>
        environment.Compile<CelValue>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);

    private T Evaluate<T>(string expression, CelBindings bindings) =>
        environment.Compile<T>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
}
