using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Schedules;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// DateOnly and TimeOnly in the bindings, the results, the declarations of variables and the one-step call, with and without the calendar
// set (the table of the mapping of the types of 2.0). Each converts exactly, to the type date and to the type time_of_day; a variable
// of these types, a result of these types and the type of a value need the set, as decimal needs its own. The names date, time_of_day and
// decimal belong to the types that a set declares: a variable or a member of the input with the name is DuplicateType with the set
// first and the variable second; without the set the name is free, and a qualified name is another name.
[Trait("Requirement", "F-24")]
public sealed class DateTimeOfDayMappingTests
{
    private const string CalendarHint = "which no type source or extension of the environment declares; add the extension set 'calendar'";

    private static readonly CelEnvironment WithSet = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Calendar(1))
        .AddVariable<DateOnly>("d")
        .AddVariable<TimeOnly>("x")
        .AddVariable<DateOnly?>("n")
        .AddVariable("o", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public static void Binding_OfDateOnlyAndTimeOnly_IsTheDateAndTheTimeOfDay()
    {
        CelBindings bindings = new CelBindings()
            .Add("d", new DateOnly(2026, 11, 19))
            .Add("x", new TimeOnly(9, 30))
            .Add("n", new DateOnly(2026, 1, 2));

        Assert.Equal(19, WithSet.Evaluate<long>("d.getDate()", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(30, WithSet.Evaluate<long>("x.getMinutes()", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(2, WithSet.Evaluate<long>("n.getDate()", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.True(WithSet.Evaluate<bool>("d <= date('2026-11-20') && x < time_of_day('18:00')", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.True(WithSet.Evaluate<bool>("type(d) == date && type(x) == time_of_day", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    [Fact]
    public static void IndexerOfTheBindings_ReadsTheBoxedValuesAsTheDateAndTheTimeOfDay()
    {
        var bindings = new CelBindings();
        bindings["o"] = new DateOnly(2026, 11, 19);

        Assert.Equal("date(\"2026-11-19\")", WithSet.Evaluate<CelValue>("o", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow().ToString());

        bindings["o"] = new TimeOnly(9, 30);
        Assert.Equal("time_of_day(\"09:30:00\")", WithSet.Evaluate<CelValue>("o", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow().ToString());
    }

    [Fact]
    public static void BoundValueOfAnotherKind_IsBindingTypeMismatch()
    {
        CelBindings bindings = new CelBindings().Add("d", "2026-11-19");

        CelEvalResult<CelValue> result = WithSet.Evaluate<CelValue>("d", bindings, cancellationToken: CancellationToken.None);

        Assert.Equal(CelErrorCode.BindingTypeMismatch, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public static void Result_OfTheTypes_IsExact()
    {
        var bindings = new CelBindings();

        Assert.Equal(new DateOnly(2026, 11, 19), WithSet.Evaluate<DateOnly>("date('2026-11-19')", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(new TimeOnly(9, 30), WithSet.Evaluate<TimeOnly>("time_of_day('09:30')", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(DateOnly.MinValue, WithSet.Evaluate<DateOnly>("date('0001-01-01')", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(DateOnly.MaxValue, WithSet.Evaluate<DateOnly>("date('9999-12-31')", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(TimeOnly.MaxValue, WithSet.Evaluate<TimeOnly>("time_of_day('23:59:59.9999999')", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(TimeOnly.MinValue, WithSet.Evaluate<TimeOnly>("time_of_day('00:00')", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(new DateOnly(2026, 11, 19), WithSet.Evaluate<DateOnly?>("date('2026-11-19')", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(new DateOnly(2026, 11, 19), WithSet.Evaluate<DateOnly>("dyn(date('2026-11-19'))", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    [Fact]
    public static void Result_OfObjectAndCelValue_IsTheDateOnlyAndTheTimeOnly()
    {
        var bindings = new CelBindings();

        Assert.Equal(new DateOnly(2026, 11, 19), WithSet.Evaluate<object>("date('2026-11-19')", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(new TimeOnly(9, 30), WithSet.Evaluate<object>("time_of_day('09:30')", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());

        CelValue value = WithSet.Evaluate<CelValue>("date('2026-11-19')", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
        Assert.Equal(CelValueKind.Date, value.Kind);
    }

    [Theory]
    [InlineData("'2026-11-19'")]
    [InlineData("time_of_day('09:30')")]
    [InlineData("1")]
    public static void ResultDateOnly_OfAnotherStaticType_IsExpectedTypeMismatch(string expression)
    {
        CelCompileResult<DateOnly> result = WithSet.Compile<DateOnly>(expression);

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, error.Code);
        Assert.Equal(CelType.Date, error.ExpectedType);
    }

    [Fact]
    public static void ResultDateOnly_OfAValueOfAnotherKind_IsResultConversion()
    {
        CelEvalResult<DateOnly> result = WithSet.Evaluate<DateOnly>("dyn(1)", new CelBindings(), cancellationToken: CancellationToken.None);

        Assert.Equal(CelErrorCode.ResultConversion, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public static void ValueOfTheKind_ReadsExactlyAndOnlyItself()
    {
        foreach (DateOnly date in new[] { DateOnly.MinValue, new DateOnly(1970, 1, 1), new DateOnly(2024, 2, 29), DateOnly.MaxValue })
        {
            CelValue value = CelValue.FromDate(date);

            Assert.Equal(CelValueKind.Date, value.Kind);
            Assert.True(value.TryGetDate(out DateOnly read));
            Assert.Equal(date, read);
            Assert.False(value.TryGetTimeOfDay(out _));
            Assert.Equal(value, CelValue.FromDate(date));
            Assert.Equal(value.GetHashCode(), CelValue.FromDate(date).GetHashCode());
        }

        foreach (TimeOnly time in new[] { TimeOnly.MinValue, new TimeOnly(9, 30), new TimeOnly(0, 0, 0).Add(TimeSpan.FromTicks(1)), TimeOnly.MaxValue })
        {
            CelValue value = CelValue.FromTimeOfDay(time);

            Assert.Equal(CelValueKind.TimeOfDay, value.Kind);
            Assert.True(value.TryGetTimeOfDay(out TimeOnly read));
            Assert.Equal(time, read);
            Assert.False(value.TryGetDate(out _));
        }

        Assert.False(CelValue.FromInt64(1).TryGetDate(out DateOnly none));
        Assert.Equal(default, none);
        Assert.False(CelValue.FromInt64(1).TryGetTimeOfDay(out _));
        Assert.NotEqual(CelValue.FromDate(DateOnly.MinValue), CelValue.FromTimeOfDay(TimeOnly.MinValue));
        Assert.NotEqual(CelValue.FromDate(DateOnly.MinValue), CelValue.FromInt64(0));
        Assert.Equal("date(\"2026-11-19\")", CelValue.FromDate(new DateOnly(2026, 11, 19)).ToString());
        Assert.Equal("time_of_day(\"09:30:15.25\")", CelValue.FromTimeOfDay(new TimeOnly(9, 30, 15, 250)).ToString());
    }

    [Fact]
    public static void CelEquals_OfTheKinds_IsByTheValueOfTheKindOnly()
    {
        Assert.Equal(CelValue.FromBoolean(true), CelValue.CelEquals(CelValue.FromDate(new DateOnly(2026, 1, 1)), CelValue.FromDate(new DateOnly(2026, 1, 1))));
        Assert.Equal(CelValue.FromBoolean(false), CelValue.CelEquals(CelValue.FromDate(new DateOnly(2026, 1, 1)), CelValue.FromDate(new DateOnly(2026, 1, 2))));
        Assert.Equal(CelValue.FromBoolean(false), CelValue.CelEquals(CelValue.FromDate(DateOnly.MinValue), CelValue.FromTimeOfDay(TimeOnly.MinValue)));
        Assert.Equal(CelValue.FromBoolean(false), CelValue.CelEquals(CelValue.FromDate(DateOnly.MinValue), CelValue.FromInt64(0)));
    }

    [Fact]
    public static void SimpleEntry_DoesNotReadDateOnly_AndNamesTheType()
    {
        CelException exception = Assert.Throws<CelException>(() => CelExpression.Evaluate<bool>(
            "d != null",
            new Dictionary<string, object?> { ["d"] = new DateOnly(2026, 1, 1) },
            cancellationToken: CancellationToken.None));

        CelError error = Assert.Single(exception.Errors);
        Assert.Equal(CelErrorCode.TypeNotDeclared, error.Code);
        Assert.Contains("System.DateOnly", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void WithoutTheSet_AVariableOfTheTypes_IsTypeNotDeclaredWithTheHint()
    {
        foreach (CelEnvironmentBuilder builder in new[]
        {
            new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<DateOnly>("d"),
            new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<TimeOnly>("d"),
            new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<DateOnly?>("d"),
            new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable("d", CelType.Date),
            new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable("d", CelType.ListOf(CelType.TimeOfDay)),
        })
        {
            CelError error = Assert.Single(builder.Build().Errors);

            Assert.Equal((CelErrorCode.TypeNotDeclared, CelErrorStage.Build), (error.Code, error.Stage));
            Assert.EndsWith(CalendarHint, error.Message, StringComparison.Ordinal);
            Assert.StartsWith("variable 'd' has type '", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public static void WithoutTheSet_ABoundValueInAPlaceOfDyn_IsTypeNotAccessible()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable("o", CelType.Dyn).Build().GetEnvironmentOrThrow();

        CelEvalResult<CelValue> date = environment.Evaluate<CelValue>("o", new CelBindings().Add("o", (object)new DateOnly(2026, 1, 1)), cancellationToken: CancellationToken.None);
        CelEvalResult<CelValue> time = environment.Evaluate<CelValue>("o", new CelBindings().Add("o", (object)new TimeOnly(9, 30)), cancellationToken: CancellationToken.None);

        Assert.Equal(CelErrorCode.TypeNotAccessible, Assert.Single(date.Errors).Code);
        Assert.Contains("System.DateOnly", Assert.Single(date.Errors).Message, StringComparison.Ordinal);
        Assert.Equal(CelErrorCode.TypeNotAccessible, Assert.Single(time.Errors).Code);
        Assert.Contains("System.TimeOnly", Assert.Single(time.Errors).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("date", "date")]
    [InlineData("time_of_day", "time_of_day")]
    public static void VariableWithTheNameOfATypeOfTheSet_IsDuplicateType(string name, string subject)
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Calendar(1))
            .AddVariable<string>(name)
            .Build();

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.DuplicateType, CelErrorStage.Build, subject), (error.Code, error.Stage, error.Subject));
        Assert.Equal("type '" + name + "' is declared by both extension calendar v1 and variable of application", error.Message);
    }

    [Fact]
    public static void VariableDecimal_WithTheSetDecimal_IsDuplicateType()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Decimal(1))
            .AddVariable<string>("decimal")
            .Build();

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.DuplicateType, "decimal"), (error.Code, error.Subject));
        Assert.Equal("type 'decimal' is declared by both extension decimal v1 and variable of application", error.Message);
    }

    [Fact]
    public static void VariableWithTheNameOfATypeOfTheSet_IsFreeWithoutTheSetAndWhenQualified()
    {
        Assert.True(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<string>("date").AddVariable<string>("time_of_day").AddVariable<string>("decimal").Build().IsSuccess);
        Assert.True(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddExtension(CelExtensions.Calendar(1)).AddVariable<string>("acme.date").AddVariable<string>("Date").Build().IsSuccess);
    }

    [Fact]
    public static void MemberOfTheInputWithTheNameOfTheType_IsDuplicateTypeNamingTheSetAndTheInput()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(SchedulesContext.Default)
            .AddExtension(CelExtensions.Calendar(1))
            .UseInput<Entry>()
            .Build();

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.DuplicateType, "date"), (error.Code, error.Subject));
        Assert.StartsWith("type 'date' is declared by both extension calendar v1 and variable of input ", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void Functions_OfTheApplication_TakeAndGiveTheTypes()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Calendar(1))
            .AddFunction("nextDay", (DateOnly day) => day.AddDays(1))
            .AddFunction("hourOf", (TimeOnly time) => (long)time.Hour)
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Equal(
            "date(\"2026-11-20\")",
            environment.Evaluate<CelValue>("nextDay(date('2026-11-19'))", new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow().ToString());
        Assert.Equal(
            CelErrorCode.EvalNoMatchingOverload,
            Assert.Single(environment.Evaluate<CelValue>("nextDay(dyn(1))", new CelBindings(), cancellationToken: CancellationToken.None).Errors).Code);
        Assert.Equal(
            9L,
            environment.Evaluate<long>("hourOf(time_of_day('09:30'))", new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }
}
