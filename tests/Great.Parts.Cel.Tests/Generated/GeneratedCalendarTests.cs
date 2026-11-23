using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Schedules;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Generated.GeneratedExample;

namespace Great.Parts.Cel.Tests.Generated;

// DateOnly and TimeOnly as members of a type that the generator described (the sample Shipment): the types date and time_of_day in
// members, elements, values and sets, and the Nullable<> that has no value (UnexpectedNull at the read, absent for has()). Selecting such a
// member needs the calendar set; without it the selection is FieldNotAccessible and names the set.
[Trait("Requirement", "F-24")]
public sealed class GeneratedCalendarTests
{
    private static readonly CelEnvironment WithSet = Builder(SchedulesContext.Default)
        .AddExtension(CelExtensions.Calendar(1))
        .AddVariable<Shipment>("s")
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelEnvironment WithoutSet = Builder(SchedulesContext.Default)
        .AddVariable<Shipment>("s")
        .Build()
        .GetEnvironmentOrThrow();

    private static CelBindings Bind(Action<Shipment>? change = null)
    {
        var shipment = new Shipment { ShipDate = new DateOnly(2026, 11, 19), Cutoff = new TimeOnly(9, 30) };
        change?.Invoke(shipment);
        return new CelBindings().Add("s", shipment);
    }

    [Fact]
    public static void Members_AreTheDateAndTheTimeOfDay()
    {
        CelBindings bindings = Bind();

        Assert.True(Evaluate<bool>(WithSet, "s.shipDate <= date('2026-11-20') && s.shipDate == date(2026, 11, 19)", bindings));
        Assert.True(Evaluate<bool>(WithSet, "s.cutoff < time_of_day('18:00') && s.cutoff == time_of_day('09:30')", bindings));
        Assert.True(Evaluate<bool>(WithSet, "type(s.shipDate) == date && type(s.cutoff) == time_of_day", bindings));
        Assert.Equal(19, Evaluate<long>(WithSet, "s.shipDate.getDate()", bindings));
    }

    [Fact]
    public static void Members_AtTheEndsOfTheRanges_AreRead()
    {
        CelBindings low = Bind(shipment => (shipment.ShipDate, shipment.Cutoff) = (DateOnly.MinValue, TimeOnly.MinValue));
        CelBindings high = Bind(shipment => (shipment.ShipDate, shipment.Cutoff) = (DateOnly.MaxValue, TimeOnly.MaxValue));

        Assert.True(Evaluate<bool>(WithSet, "s.shipDate == date('0001-01-01') && s.cutoff == time_of_day('00:00')", low));
        Assert.True(Evaluate<bool>(WithSet, "s.shipDate == date('9999-12-31') && s.cutoff == time_of_day('23:59:59.9999999')", high));
    }

    [Fact]
    public static void NullableMember_WithoutAValue_IsAbsentAndUnexpectedNull()
    {
        CelBindings bindings = Bind();

        Assert.True(Evaluate<bool>(WithSet, "!has(s.delivered) && !has(s.window)", bindings));
        Assert.Equal(CelErrorCode.UnexpectedNull, EvaluationError(WithSet, "s.delivered", bindings).Code);
        Assert.Equal(CelErrorCode.UnexpectedNull, EvaluationError(WithSet, "s.window", bindings).Code);
    }

    [Fact]
    public static void NullableMember_WithAValue_IsTheValue()
    {
        CelBindings bindings = Bind(shipment =>
        {
            shipment.Delivered = new DateOnly(2026, 12, 1);
            shipment.Window = new TimeOnly(6, 0);
        });

        Assert.True(Evaluate<bool>(WithSet, "has(s.delivered) && s.delivered == date('2026-12-01') && s.window == time_of_day('06:00')", bindings));
    }

    [Fact]
    public static void ListSetAndDictionary_OfTheTypes_AreReadByElement()
    {
        CelBindings bindings = Bind(shipment =>
        {
            shipment.Holidays = [new DateOnly(2026, 12, 25), new DateOnly(2026, 1, 1)];
            shipment.Slots["a"] = new TimeOnly(18, 0);
            shipment.Closed.Add(new DateOnly(2026, 12, 25));
            shipment.Closed.Add(new DateOnly(2026, 1, 1));
        });

        Assert.True(Evaluate<bool>(WithSet, "s.holidays[0] == date('2026-12-25') && s.holidays.size() == 2", bindings));
        Assert.True(Evaluate<bool>(WithSet, "s.slots['a'] == time_of_day('18:00') && s.slots['a'] > time_of_day('17:59:59')", bindings));
        Assert.True(Evaluate<bool>(WithSet, "s.closed.map(d, string(d)) == ['2026-01-01', '2026-12-25']", bindings));
        Assert.True(Evaluate<bool>(WithSet, "s.holidays.exists(d, d.getMonth() == 11)", bindings));
    }

    [Theory]
    [InlineData("s.shipDate")]
    [InlineData("s.cutoff")]
    [InlineData("s.delivered")]
    [InlineData("s.holidays")]
    [InlineData("s.slots")]
    public static void WithoutTheSet_SelectingAMemberOfTheTypes_IsFieldNotAccessibleAndNamesTheSet(string expression)
    {
        CelError error = CompileError(WithoutSet, expression);

        Assert.Equal(CelErrorCode.FieldNotAccessible, error.Code);
        Assert.Contains("the extension set 'calendar'", error.Message, StringComparison.Ordinal);
    }
}
