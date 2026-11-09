using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Library.Time;
using Great.Parts.Cel.Types;
using Xunit;
using static Great.Parts.Cel.Tests.Library.TimeCalls;

namespace Great.Parts.Cel.Tests.Library;

// The 47 overloads of time in the table of version 1: their identifiers are the constants of TimeOverloadIds, the functions
// and the order are those of the standard library page, every overload has an implementation and the fixed unit, and the costs
// that grow with the arguments are the cost functions of the page.
public sealed class TimeOverloadTableTests
{
    private static readonly string[] TimeFunctions =
    [
        "timestamp", "duration", "getFullYear", "getMonth", "getDayOfYear", "getDayOfMonth", "getDate", "getDayOfWeek", "getHours",
        "getMinutes", "getSeconds", "getMilliseconds",
    ];

    [Fact]
    public static void TimeOverloadIds_AreTheFortySevenOfTheTable()
    {
        string[] constants = [.. typeof(TimeOverloadIds).GetFields(BindingFlags.Static | BindingFlags.NonPublic).Select(field => (string)field.GetRawConstantValue()!)];
        HashSet<string> inTable = [.. StandardLibraryV1.Overloads.Where(IsTime).Select(overload => overload.Declaration.OverloadId)];

        Assert.Equal(47, constants.Length);
        Assert.Equal(47, inTable.Count);
        Assert.Equal([.. constants.Order(StringComparer.Ordinal)], [.. inTable.Order(StringComparer.Ordinal)]);
        Assert.Equal(160, StandardLibraryV1.Overloads.Length);
        Assert.Equal(160, StandardLibraryV1.Overloads.Select(overload => overload.Declaration.OverloadId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public static void EveryTimeOverload_HasAnImplementationAndOneFixedUnit()
    {
        foreach (OverloadBinding overload in StandardLibraryV1.Overloads.Where(IsTime))
        {
            Assert.NotNull(overload.Implementation);
            Assert.Equal(1UL, overload.FixedCost);
            Assert.False(overload.DynOnly);
            Assert.False(overload.IsApplication);
        }
    }

    // The cost functions: the string of a conversion, the zone of a receiver and the text of a result; nothing else.
    [Fact]
    public static void CostFunctions_AreOnlyThoseOfTheCostPage()
    {
        string[] withCost =
        [
            "string_to_timestamp", "string_to_duration", "timestamp_to_string", "duration_to_string",
            "timestamp_to_year_with_tz", "timestamp_to_month_with_tz", "timestamp_to_day_of_year_with_tz", "timestamp_to_day_of_month_with_tz",
            "timestamp_to_day_of_month_1_based_with_tz", "timestamp_to_day_of_week_with_tz", "timestamp_to_hours_with_tz",
            "timestamp_to_minutes_with_tz", "timestamp_to_seconds_tz", "timestamp_to_milliseconds_with_tz",
        ];

        string[] actual = [.. StandardLibraryV1.Overloads.Where(IsTime).Where(overload => overload.ArgumentCost is not null).Select(overload => overload.Declaration.OverloadId)];

        Assert.Equal([.. withCost.Order(StringComparer.Ordinal)], [.. actual.Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public static void ReceiversWithAZone_TakeATimestampAndAString()
    {
        foreach (OverloadBinding overload in StandardLibraryV1.Overloads.Where(candidate => candidate.Declaration.OverloadId.EndsWith("_tz", StringComparison.Ordinal)
            || candidate.Declaration.OverloadId.EndsWith("_with_tz", StringComparison.Ordinal)))
        {
            Assert.True(overload.Declaration.IsReceiverStyle);
            Assert.Equal([CelType.Timestamp, CelType.String], overload.Declaration.ParameterTypes);
            Assert.Equal(CelType.Int, overload.Declaration.ResultType);
        }
    }

    [Fact]
    public static void FunctionsOfTime_HaveTheirOverloadsInTheOrderOfThePage()
    {
        Assert.Equal(["timestamp_to_timestamp", "int64_to_timestamp", "string_to_timestamp"], Ids("timestamp"));
        Assert.Equal(["duration_to_duration", "string_to_duration"], Ids("duration"));
        Assert.Equal(
            ["timestamp_to_seconds", "timestamp_to_seconds_tz", "duration_to_seconds"],
            Ids("getSeconds"));
        Assert.Equal(
            ["add_bytes", "add_double", "add_duration_duration", "add_duration_timestamp", "add_timestamp_duration", "add_int64", "add_list", "add_string", "add_uint64"],
            Ids("_+_"));
        Assert.Equal(
            ["int64_to_int64", "double_to_int64", "duration_to_int64", "string_to_int64", "timestamp_to_int64", "uint64_to_int64"],
            Ids("int"));
        Assert.Equal(
            ["string_to_string", "bool_to_string", "bytes_to_string", "double_to_string", "duration_to_string", "int64_to_string", "timestamp_to_string", "uint64_to_string"],
            Ids("string"));
        Assert.Equal(
            ["subtract_double", "subtract_duration_duration", "subtract_int64", "subtract_timestamp_duration", "subtract_timestamp_timestamp", "subtract_uint64"],
            Ids("_-_"));
        Assert.Equal(["less_timestamp", "less_duration"], Ids("_<_").TakeLast(2));
        Assert.Equal(["greater_equals_timestamp", "greater_equals_duration"], Ids("_>=_").TakeLast(2));
    }

    // The overloads of the functions of time that no other overload shares: the receivers are on a timestamp or a duration.
    [Fact]
    public static void ReceiversOfTime_AreCalledOnTheirReceiver()
    {
        foreach (string function in TimeFunctions.Skip(2))
        {
            Assert.All(
                StandardLibraryV1.Overloads.Where(overload => overload.Function == function),
                overload => Assert.True(overload.Declaration.IsReceiverStyle && overload.Declaration.ParameterTypes[0].Kind is CelTypeKind.Timestamp or CelTypeKind.Duration));
        }
    }

    private static bool IsTime(OverloadBinding overload) =>
        overload.Declaration.ParameterTypes.Any(type => type.Kind is CelTypeKind.Timestamp or CelTypeKind.Duration)
        || overload.Declaration.ResultType.Kind is CelTypeKind.Timestamp or CelTypeKind.Duration;
}
