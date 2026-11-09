using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;
using static Great.Parts.Cel.Library.Time.TimeReceivers;

namespace Great.Parts.Cel.Library.Time;

// The 47 bindings of the overloads of time: declaration, implementation and cost. StandardLibraryV1 takes them function by
// function and adds each group to the functions of cel-go in their order, so each method adds the overloads of one function.
// The source is the text that build errors name.
internal static class TimeOverloads
{
    private static readonly CelType Timestamp = CelType.Timestamp;
    private static readonly CelType Duration = CelType.Duration;
    private static readonly CelType Int = CelType.Int;
    private static readonly CelType String = CelType.String;

    // timestamp(timestamp), timestamp(int), timestamp(string).
    internal static void AddTimestampConversions(ImmutableArray<OverloadBinding>.Builder table, string source)
    {
        table.Add(Global(source, "timestamp", TimeOverloadIds.TimestampToTimestamp, [Timestamp], Timestamp, TimeConversions.TimestampIdentity, 1));
        table.Add(Global(source, "timestamp", TimeOverloadIds.Int64ToTimestamp, [Int], Timestamp, TimeConversions.Int64ToTimestamp, 1));
        table.Add(Global(source, "timestamp", TimeOverloadIds.StringToTimestamp, [String], Timestamp, TimeConversions.StringToTimestamp, 1, TimeCosts.Text));
    }

    // duration(duration), duration(string).
    internal static void AddDurationConversions(ImmutableArray<OverloadBinding>.Builder table, string source)
    {
        table.Add(Global(source, "duration", TimeOverloadIds.DurationToDuration, [Duration], Duration, TimeConversions.DurationIdentity, 1));
        table.Add(Global(source, "duration", TimeOverloadIds.StringToDuration, [String], Duration, TimeConversions.StringToDuration, 1, TimeCosts.Text));
    }

    // int(duration): in cel-go it follows double_to_int64.
    internal static void AddDurationToInt(ImmutableArray<OverloadBinding>.Builder table, string source) =>
        table.Add(Global(source, "int", TimeOverloadIds.DurationToInt64, [Duration], Int, TimeConversions.DurationToInt64, 1));

    // int(timestamp): in cel-go it follows string_to_int64.
    internal static void AddTimestampToInt(ImmutableArray<OverloadBinding>.Builder table, string source) =>
        table.Add(Global(source, "int", TimeOverloadIds.TimestampToInt64, [Timestamp], Int, TimeConversions.TimestampToInt64, 1));

    // string(duration): in cel-go it follows double_to_string. The cost of the text is known from the value, before the string is created.
    internal static void AddDurationToString(ImmutableArray<OverloadBinding>.Builder table, string source) =>
        table.Add(Global(source, "string", TimeOverloadIds.DurationToString, [Duration], String, TimeConversions.DurationToString, 1, TimeCosts.DurationText));

    // string(timestamp): in cel-go it follows int64_to_string. The cost of the text is known from the value, before the string is created.
    internal static void AddTimestampToString(ImmutableArray<OverloadBinding>.Builder table, string source) =>
        table.Add(Global(source, "string", TimeOverloadIds.TimestampToString, [Timestamp], String, TimeConversions.TimestampToString, 1, TimeCosts.TimestampText));

    // + of two durations, of a duration and a timestamp and of a timestamp and a duration: in cel-go they follow add_double.
    internal static void AddAdditions(ImmutableArray<OverloadBinding>.Builder table, string source)
    {
        table.Add(Global(source, "_+_", TimeOverloadIds.AddDurationDuration, [Duration, Duration], Duration, TimeArithmetic.AddDurationDuration, 1));
        table.Add(Global(source, "_+_", TimeOverloadIds.AddDurationTimestamp, [Duration, Timestamp], Timestamp, TimeArithmetic.AddDurationTimestamp, 1));
        table.Add(Global(source, "_+_", TimeOverloadIds.AddTimestampDuration, [Timestamp, Duration], Timestamp, TimeArithmetic.AddTimestampDuration, 1));
    }

    // - of two durations: in cel-go it follows subtract_double.
    internal static void AddDurationSubtraction(ImmutableArray<OverloadBinding>.Builder table, string source) =>
        table.Add(Global(source, "_-_", TimeOverloadIds.SubtractDurationDuration, [Duration, Duration], Duration, TimeArithmetic.SubtractDurationDuration, 1));

    // - of a timestamp and a duration and of two timestamps: in cel-go they follow subtract_int64.
    internal static void AddTimestampSubtractions(ImmutableArray<OverloadBinding>.Builder table, string source)
    {
        table.Add(Global(source, "_-_", TimeOverloadIds.SubtractTimestampDuration, [Timestamp, Duration], Timestamp, TimeArithmetic.SubtractTimestampDuration, 1));
        table.Add(Global(source, "_-_", TimeOverloadIds.SubtractTimestampTimestamp, [Timestamp, Timestamp], Duration, TimeArithmetic.SubtractTimestampTimestamp, 1));
    }

    // The two overloads of one relation, for timestamp and for duration. The implementations are those of the ordering of the
    // library, which the caller gives: the layers let this namespace read neither.
    internal static void AddRelations(
        ImmutableArray<OverloadBinding>.Builder table,
        string source,
        string function,
        string timestampId,
        string durationId,
        OverloadImplementation timestampOrdering,
        OverloadImplementation durationOrdering)
    {
        table.Add(Global(source, function, timestampId, [Timestamp, Timestamp], CelType.Bool, timestampOrdering, 1));
        table.Add(Global(source, function, durationId, [Duration, Duration], CelType.Bool, durationOrdering, 1));
    }

    // The 24 receivers, grouped by function in the order of the standard library page.
    internal static void AddReceivers(ImmutableArray<OverloadBinding>.Builder table, string source)
    {
        AddFieldReceivers(table, source, "getFullYear", Field.Year, TimeOverloadIds.TimestampToYear, TimeOverloadIds.TimestampToYearWithTz);
        AddFieldReceivers(table, source, "getMonth", Field.Month, TimeOverloadIds.TimestampToMonth, TimeOverloadIds.TimestampToMonthWithTz);
        AddFieldReceivers(table, source, "getDayOfYear", Field.DayOfYear, TimeOverloadIds.TimestampToDayOfYear, TimeOverloadIds.TimestampToDayOfYearWithTz);
        AddFieldReceivers(table, source, "getDayOfMonth", Field.DayOfMonth, TimeOverloadIds.TimestampToDayOfMonth, TimeOverloadIds.TimestampToDayOfMonthWithTz);
        AddFieldReceivers(table, source, "getDate", Field.Date, TimeOverloadIds.TimestampToDayOfMonth1Based, TimeOverloadIds.TimestampToDayOfMonth1BasedWithTz);
        AddFieldReceivers(table, source, "getDayOfWeek", Field.DayOfWeek, TimeOverloadIds.TimestampToDayOfWeek, TimeOverloadIds.TimestampToDayOfWeekWithTz);
        AddFieldReceivers(table, source, "getHours", Field.Hours, TimeOverloadIds.TimestampToHours, TimeOverloadIds.TimestampToHoursWithTz);
        table.Add(Member(source, "getHours", TimeOverloadIds.DurationToHours, [Duration], TimeReceivers.Duration(Part.Hours), 1, null));
        AddFieldReceivers(table, source, "getMinutes", Field.Minutes, TimeOverloadIds.TimestampToMinutes, TimeOverloadIds.TimestampToMinutesWithTz);
        table.Add(Member(source, "getMinutes", TimeOverloadIds.DurationToMinutes, [Duration], TimeReceivers.Duration(Part.Minutes), 1, null));
        AddFieldReceivers(table, source, "getSeconds", Field.Seconds, TimeOverloadIds.TimestampToSeconds, TimeOverloadIds.TimestampToSecondsTz);
        table.Add(Member(source, "getSeconds", TimeOverloadIds.DurationToSeconds, [Duration], TimeReceivers.Duration(Part.Seconds), 1, null));
        AddFieldReceivers(table, source, "getMilliseconds", Field.Milliseconds, TimeOverloadIds.TimestampToMilliseconds, TimeOverloadIds.TimestampToMillisecondsWithTz);
        table.Add(Member(source, "getMilliseconds", TimeOverloadIds.DurationToMilliseconds, [Duration], TimeReceivers.Duration(Part.Milliseconds), 1, null));
    }

    // The receiver of timestamp without a zone and the one with a zone string, whose cost is that of the zone string.
    private static void AddFieldReceivers(
        ImmutableArray<OverloadBinding>.Builder table,
        string source,
        string function,
        Field field,
        string withoutZoneId,
        string withZoneId)
    {
        table.Add(Member(source, function, withoutZoneId, [Timestamp], TimeReceivers.Timestamp(field), 1, null));
        table.Add(Member(source, function, withZoneId, [Timestamp, String], TimestampInZone(field), 1, TimeCosts.Text));
    }

    private static OverloadBinding Global(
        string source,
        string function,
        string overloadId,
        ImmutableArray<CelType> parameterTypes,
        CelType resultType,
        OverloadImplementation implementation,
        ulong fixedCost,
        CelCostFunction? argumentCost = null) =>
        new(function, new CelOverloadDecl(overloadId, false, parameterTypes, resultType), implementation, fixedCost, argumentCost, source);

    // An overload called on its receiver, which is the first parameter; the result is int.
    private static OverloadBinding Member(
        string source,
        string function,
        string overloadId,
        ImmutableArray<CelType> parameterTypes,
        OverloadImplementation implementation,
        ulong fixedCost,
        CelCostFunction? argumentCost) =>
        new(function, new CelOverloadDecl(overloadId, true, parameterTypes, Int), implementation, fixedCost, argumentCost, source);
}
