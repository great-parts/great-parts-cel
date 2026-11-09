using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// The result rows of time of the type mapping table: a timestamp becomes CelTimestamp exactly or
// DateTimeOffset with the offset zero; a duration becomes CelDuration exactly or TimeSpan exactly. The
// .NET structures count in ticks of 100 nanoseconds, so a value whose nanoseconds are not a multiple of 100
// is ResultConversion at the root when T is DateTimeOffset or TimeSpan; there is no rounding. The
// nullable forms take a null result and the value of the row. CelValue and object take the value as it is
// (CelTimestamp and CelDuration for object). A static type that is not assignable to the type of T is the
// compile error ExpectedTypeMismatch at the root; the type of dyn passes, and a value of the other kind
// is ResultConversion at the root. The texts of these errors name the types and are the library's.
[Trait("Requirement", "+R-10")]
public sealed class TimeResultRowTests
{
    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("d", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void Timestamp_BecomesCelTimestampExactly()
    {
        CelTimestamp value = Evaluate<CelTimestamp>("timestamp('2009-02-13T23:31:30.123456789Z')").GetValueOrThrow();

        Assert.Equal((1234567890L, 123456789), (value.Seconds, value.Nanoseconds));
        Assert.Equal(new CelTimestamp(-62135596800, 0), Evaluate<CelTimestamp>("timestamp('0001-01-01T00:00:00Z')").GetValueOrThrow());
        Assert.Equal(new CelTimestamp(253402300799, 999999999), Evaluate<CelTimestamp>("timestamp('9999-12-31T23:59:59.999999999Z')").GetValueOrThrow());
    }

    [Fact]
    public void Timestamp_BecomesDateTimeOffsetWithTheOffsetZero()
    {
        DateTimeOffset value = Evaluate<DateTimeOffset>("timestamp('2009-02-13T23:31:30.0000001Z')").GetValueOrThrow();

        Assert.Equal(new DateTimeOffset(2009, 2, 13, 23, 31, 30, TimeSpan.Zero).AddTicks(1), value);
        Assert.Equal(TimeSpan.Zero, value.Offset);
        Assert.Equal(DateTimeOffset.MinValue, Evaluate<DateTimeOffset>("timestamp('0001-01-01T00:00:00Z')").GetValueOrThrow());
        Assert.Equal(DateTimeOffset.MaxValue, Evaluate<DateTimeOffset>("timestamp('9999-12-31T23:59:59.9999999Z')").GetValueOrThrow());
        Assert.Equal(new DateTimeOffset(2009, 2, 13, 22, 31, 30, TimeSpan.Zero), Evaluate<DateTimeOffset>("timestamp('2009-02-13T23:31:30+01:00')").GetValueOrThrow());
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void TimestampWithNanosecondsNotAMultipleOf100_IsResultConversionForDateTimeOffset()
    {
        AssertResultConversion(Evaluate<DateTimeOffset>("timestamp('2009-02-13T23:31:30.000000001Z')").Errors, 9);
        AssertResultConversion(Evaluate<DateTimeOffset>("timestamp('9999-12-31T23:59:59.999999999Z')").Errors, 9);
        AssertResultConversion(Evaluate<DateTimeOffset?>("timestamp('2009-02-13T23:31:30.00000015Z')").Errors, 9);
        Assert.True(Evaluate<CelTimestamp>("timestamp('2009-02-13T23:31:30.000000001Z')").IsSuccess);
    }

    [Fact]
    public void Duration_BecomesCelDurationAndTimeSpanExactly()
    {
        Assert.Equal(new CelDuration(5400000000000), Evaluate<CelDuration>("duration('1h30m')").GetValueOrThrow());
        Assert.Equal(new CelDuration(long.MinValue), Evaluate<CelDuration>("duration('-9223372036854775808ns')").GetValueOrThrow());
        Assert.Equal(TimeSpan.FromMinutes(90), Evaluate<TimeSpan>("duration('1h30m')").GetValueOrThrow());
        Assert.Equal(TimeSpan.FromTicks(-1), Evaluate<TimeSpan>("duration('-100ns')").GetValueOrThrow());
        Assert.Equal(TimeSpan.FromTicks(long.MaxValue / 100), Evaluate<TimeSpan>("duration('9223372036854775800ns')").GetValueOrThrow());
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void DurationWithNanosecondsNotAMultipleOf100_IsResultConversionForTimeSpan()
    {
        AssertResultConversion(Evaluate<TimeSpan>("duration('1ns')").Errors, 8);
        AssertResultConversion(Evaluate<TimeSpan>("duration('9223372036854775807ns')").Errors, 8);
        AssertResultConversion(Evaluate<TimeSpan?>("duration('-150ns')").Errors, 8);
        Assert.True(Evaluate<CelDuration>("duration('1ns')").IsSuccess);
    }

    [Fact]
    public void NullableForms_TakeTheValueOrNull()
    {
        Assert.Equal(new DateTimeOffset(2009, 2, 13, 23, 31, 30, TimeSpan.Zero), Evaluate<DateTimeOffset?>("timestamp('2009-02-13T23:31:30Z')").GetValueOrThrow());
        Assert.Null(Run<DateTimeOffset?>("d", new CelBindings().Add("d", CelValue.Null)).GetValueOrThrow());
        Assert.Equal(TimeSpan.FromHours(1), Evaluate<TimeSpan?>("duration('1h')").GetValueOrThrow());
        Assert.Null(Run<TimeSpan?>("d", new CelBindings().Add("d", CelValue.Null)).GetValueOrThrow());
    }

    // The literal null has the type null_type, which the type checker takes as assignable to timestamp and
    // duration, the types that the nullable forms expect (cel-go v0.32.0 isLegacyNullable: the kinds of
    // timestamp and duration are nullable): the program compiles, and the null value becomes null.
    [Fact]
    [Trait("Requirement", "+R-10")]
    public void LiteralNull_CompilesAndIsNullForTheNullableForms()
    {
        Assert.Null(Evaluate<DateTimeOffset?>("null").GetValueOrThrow());
        Assert.Null(Evaluate<TimeSpan?>("null").GetValueOrThrow());
        Assert.Null(Evaluate<CelTimestamp?>("null").GetValueOrThrow());
        Assert.Null(Evaluate<CelDuration?>("null").GetValueOrThrow());
    }

    // The static rule is the same for the forms without the question mark: null is assignable to timestamp
    // and duration, so Compile<DateTimeOffset>("null") compiles. The null_type row of the result table has
    // no entry for a value type that cannot hold null, so the evaluation is ResultConversion at the root,
    // and {celType} is null_type. A type that takes no null as a static type, long, is the compile error
    // ExpectedTypeMismatch instead.
    [Fact]
    [Trait("Requirement", "F-4")]
    public void LiteralNull_CompilesAndIsResultConversionForTheFormsWithoutNull()
    {
        AssertResultConversion(Evaluate<DateTimeOffset>("null").Errors, 0);
        AssertResultConversion(Evaluate<TimeSpan>("null").Errors, 0);
        AssertResultConversion(Evaluate<CelTimestamp>("null").Errors, 0);
        AssertResultConversion(Evaluate<CelDuration>("null").Errors, 0);
        Assert.Equal(
            "result of type 'null_type' cannot be converted to .NET type 'System.DateTimeOffset'",
            Assert.Single(Evaluate<DateTimeOffset>("null").Errors).Message);
        Assert.Equal(
            "result of type 'null_type' cannot be converted to .NET type 'System.TimeSpan'",
            Assert.Single(Evaluate<TimeSpan>("null").Errors).Message);

        CelError error = Assert.Single(environment.Compile<long>("null").Errors);
        Assert.Equal((CelErrorCode.ExpectedTypeMismatch, CelErrorStage.Check), (error.Code, error.Stage));
    }

    [Fact]
    public void CelValueAndObject_TakeTheValueAsItIs()
    {
        Assert.True(Evaluate<CelValue>("timestamp(1234567890)").GetValueOrThrow().TryGetTimestamp(out CelTimestamp timestamp));
        Assert.Equal(new CelTimestamp(1234567890, 0), timestamp);
        Assert.True(Evaluate<CelValue>("duration('1.5s')").GetValueOrThrow().TryGetDuration(out CelDuration duration));
        Assert.Equal(new CelDuration(1500000000), duration);
        Assert.Equal(new CelTimestamp(1234567890, 1), Assert.IsType<CelTimestamp>(Evaluate<object>("timestamp('2009-02-13T23:31:30.000000001Z')").GetValueOrThrow()));
        Assert.Equal(new CelDuration(1), Assert.IsType<CelDuration>(Evaluate<object>("duration('1ns')").GetValueOrThrow()));
        IReadOnlyList<object?> list = Assert.IsAssignableFrom<IReadOnlyList<object?>>(Evaluate<object>("[timestamp(0), duration('1s')]").GetValueOrThrow());
        Assert.Equal(new CelTimestamp(0, 0), Assert.IsType<CelTimestamp>(list[0]));
        Assert.Equal(new CelDuration(1_000_000_000), Assert.IsType<CelDuration>(list[1]));
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void StaticTypeThatIsNotAssignable_IsExpectedTypeMismatchAtTheRoot()
    {
        foreach (CelError error in new[]
        {
            Assert.Single(environment.Compile<DateTimeOffset>("duration('1s')").Errors),
            Assert.Single(environment.Compile<TimeSpan>("timestamp(0)").Errors),
            Assert.Single(environment.Compile<CelTimestamp>("1").Errors),
            Assert.Single(environment.Compile<CelDuration>("'1s'").Errors),
        })
        {
            Assert.Equal((CelErrorCode.ExpectedTypeMismatch, CelErrorStage.Check), (error.Code, error.Stage));
        }

        Assert.True(environment.Compile<DateTimeOffset>("dyn(timestamp(0))").IsSuccess);
        Assert.True(environment.Compile<TimeSpan>("d").IsSuccess);
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void DynValueOfAnotherKind_IsResultConversionAtTheRoot()
    {
        var bindings = new CelBindings().Add("d", 5L);

        AssertResultConversion(Run<DateTimeOffset>("  d", bindings).Errors, 2);
        AssertResultConversion(Run<TimeSpan>("  d", bindings).Errors, 2);
        AssertResultConversion(Run<CelTimestamp>("  d", new CelBindings().Add("d", new CelDuration(1))).Errors, 2);
        AssertResultConversion(Run<CelDuration>("  d", new CelBindings().Add("d", new CelTimestamp(1, 0))).Errors, 2);
        Assert.Equal(new CelTimestamp(1, 0), Run<CelTimestamp>("  d", new CelBindings().Add("d", new CelTimestamp(1, 0))).GetValueOrThrow());
    }

    private static void AssertResultConversion(IReadOnlyList<CelError> errors, int offset)
    {
        CelError error = Assert.Single(errors);
        Assert.Equal((CelErrorCode.ResultConversion, CelErrorStage.Evaluate, offset), (error.Code, error.Stage, error.Location.Offset));
    }

    private CelEvalResult<T> Evaluate<T>(string expression) => Run<T>(expression, new CelBindings());

    private CelEvalResult<T> Run<T>(string expression, CelBindings bindings) =>
        environment.Compile<T>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);
}
