using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;
using static Great.Parts.Cel.Library.Time.CalendarFunctions;

namespace Great.Parts.Cel.Library.Time;

// The definition of the set calendar, version 1: the types date and time_of_day and 36 overloads. Like decimal it is not a
// CatalogExtension: cel-go has no such set, so its name is not among the names a description of an environment accepts.
internal sealed class CalendarDefinition(
    string name,
    uint version,
    string source,
    ImmutableArray<OverloadBinding> overloads,
    ImmutableArray<AbstractTypeDeclaration> abstractTypes)
    : ExtensionDefinition(name, version, source, overloads, abstractTypes);

// The table of the set calendar, version 1, in the order of its page: the conversions to date and to time_of_day, to string and to
// timestamp, the parts of a date and of a time of day, the shifts of a date, the two differences and the eight orderings. A call is
// 1 unit; the overloads that read a string add ⌈Lₐ/10⌉ for its length — the text of a date, the text of a time of day and the name of a
// time zone — and the two that write a string ⌈Lᵣ/10⌉ for the text; a time zone of the operating system adds its own charge when it is
// opened. The functions have descriptions of their own, since the functions of timestamp that share their names say timestamp.
internal static class CalendarExtension
{
    internal const string SetName = "calendar";
    internal const uint Version = 1;
    private const string Source = "extension calendar v1";

    private static readonly CelType Date = CelType.Date;
    private static readonly CelType TimeOfDay = CelType.TimeOfDay;
    private static readonly CelType Timestamp = CelType.Timestamp;
    private static readonly CelType Int = CelType.Int;
    private static readonly CelType String = CelType.String;
    private static readonly CelType Bool = CelType.Bool;

    private static readonly FrozenDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        ["date"] = "Creates a date from a string such as \"2026-11-19\", from a year, a month and a day, or from a timestamp in UTC or in a time zone, or returns a date as it is.",
        ["time_of_day"] = "Creates a time of day from a string such as \"09:30\", or from a timestamp in UTC or in a time zone, or returns a time of day as it is.",
        ["string"] = "Converts a date or a time of day to its text form.",
        ["timestamp"] = "Returns the instant of a date and a time of day, in UTC or in a time zone.",
        ["getFullYear"] = "Returns the year of a date.",
        ["getMonth"] = "Returns the zero-based month of a date.",
        ["getDate"] = "Returns the one-based day of the month of a date.",
        ["getDayOfMonth"] = "Returns the zero-based day of the month of a date.",
        ["getDayOfWeek"] = "Returns the day of the week of a date, from 0 for Sunday.",
        ["getDayOfYear"] = "Returns the zero-based day of the year of a date.",
        ["getHours"] = "Returns the hours of a time of day.",
        ["getMinutes"] = "Returns the minutes of a time of day.",
        ["getSeconds"] = "Returns the seconds of a time of day.",
        ["getMilliseconds"] = "Returns the milliseconds of a time of day.",
        ["addDays"] = "Returns a date shifted by a number of days.",
        ["addMonths"] = "Returns a date shifted by a number of months; a day that the target month does not have becomes its last day.",
        ["addYears"] = "Returns a date shifted by a number of years; a day that the target month does not have becomes its last day.",
    }.ToFrozenDictionary(System.StringComparer.Ordinal);

    private static readonly CelExtension VersionOne = new(
        SetName,
        Version,
        new CalendarDefinition(
            SetName,
            Version,
            Source,
            Overloads(),
            [new AbstractTypeDeclaration("date", Date), new AbstractTypeDeclaration("time_of_day", TimeOfDay)]),
        [Version]);

    // The set of the version: always the same instance, so that adding it twice is one set. A version the set does not have is a set
    // without a definition, which the build reports as UnknownExtensionVersion with the versions that exist.
    internal static CelExtension Get(uint version) =>
        version == Version ? VersionOne : new CelExtension(SetName, version, null, [Version]);

    private static ImmutableArray<OverloadBinding> Overloads()
    {
        ImmutableArray<OverloadBinding> table =
        [
            Global("date", CalendarOverloadIds.StringToDate, [String], Date, StringToDate, TimeCosts.Text),
            Global("date", CalendarOverloadIds.Int64Int64Int64ToDate, [Int, Int, Int], Date, PartsToDate),
            Global("date", CalendarOverloadIds.TimestampToDate, [Timestamp], Date, TimestampToDate),
            Global("date", CalendarOverloadIds.TimestampToDateWithTz, [Timestamp, String], Date, TimestampToDateWithTz, TimeCosts.Text),
            Global("date", CalendarOverloadIds.DateToDate, [Date], Date, DateIdentity),
            Global("time_of_day", CalendarOverloadIds.StringToTimeOfDay, [String], TimeOfDay, StringToTimeOfDay, TimeCosts.Text),
            Global("time_of_day", CalendarOverloadIds.TimestampToTimeOfDay, [Timestamp], TimeOfDay, TimestampToTimeOfDay),
            Global("time_of_day", CalendarOverloadIds.TimestampToTimeOfDayWithTz, [Timestamp, String], TimeOfDay, TimestampToTimeOfDayWithTz, TimeCosts.Text),
            Global("time_of_day", CalendarOverloadIds.TimeOfDayToTimeOfDay, [TimeOfDay], TimeOfDay, TimeOfDayIdentity),
            Global("string", CalendarOverloadIds.DateToString, [Date], String, DateToString, CalendarCosts.DateText),
            Global("string", CalendarOverloadIds.TimeOfDayToString, [TimeOfDay], String, TimeOfDayToString, CalendarCosts.TimeOfDayText),
            Global("timestamp", CalendarOverloadIds.DateTimeOfDayToTimestamp, [Date, TimeOfDay], Timestamp, DateTimeOfDayToTimestamp),
            Global("timestamp", CalendarOverloadIds.DateTimeOfDayToTimestampWithTz, [Date, TimeOfDay, String], Timestamp, DateTimeOfDayToTimestampWithTz, TimeCosts.Text),
            Member("getFullYear", CalendarOverloadIds.DateToYear, [Date], Int, Receiver(DateField.Year)),
            Member("getMonth", CalendarOverloadIds.DateToMonth, [Date], Int, Receiver(DateField.Month)),
            Member("getDate", CalendarOverloadIds.DateToDayOfMonth1Based, [Date], Int, Receiver(DateField.DayOfMonthFromOne)),
            Member("getDayOfMonth", CalendarOverloadIds.DateToDayOfMonth, [Date], Int, Receiver(DateField.DayOfMonth)),
            Member("getDayOfWeek", CalendarOverloadIds.DateToDayOfWeek, [Date], Int, Receiver(DateField.DayOfWeek)),
            Member("getDayOfYear", CalendarOverloadIds.DateToDayOfYear, [Date], Int, Receiver(DateField.DayOfYear)),
            Member("getHours", CalendarOverloadIds.TimeOfDayToHours, [TimeOfDay], Int, Receiver(TimePart.Hours)),
            Member("getMinutes", CalendarOverloadIds.TimeOfDayToMinutes, [TimeOfDay], Int, Receiver(TimePart.Minutes)),
            Member("getSeconds", CalendarOverloadIds.TimeOfDayToSeconds, [TimeOfDay], Int, Receiver(TimePart.Seconds)),
            Member("getMilliseconds", CalendarOverloadIds.TimeOfDayToMilliseconds, [TimeOfDay], Int, Receiver(TimePart.Milliseconds)),
            Member("addDays", CalendarOverloadIds.DateAddDays, [Date, Int], Date, Shift(0)),
            Member("addMonths", CalendarOverloadIds.DateAddMonths, [Date, Int], Date, Shift(1)),
            Member("addYears", CalendarOverloadIds.DateAddYears, [Date, Int], Date, Shift(12)),
            Global("_-_", CalendarOverloadIds.SubtractDateDate, [Date, Date], Int, SubtractDates),
            Global("_-_", CalendarOverloadIds.SubtractTimeOfDayTimeOfDay, [TimeOfDay, TimeOfDay], CelType.Duration, SubtractTimesOfDay),
            Global("_<_", CalendarOverloadIds.LessDate, [Date, Date], Bool, DateOrdering(Relation.Less)),
            Global("_<=_", CalendarOverloadIds.LessEqualsDate, [Date, Date], Bool, DateOrdering(Relation.LessEquals)),
            Global("_>_", CalendarOverloadIds.GreaterDate, [Date, Date], Bool, DateOrdering(Relation.Greater)),
            Global("_>=_", CalendarOverloadIds.GreaterEqualsDate, [Date, Date], Bool, DateOrdering(Relation.GreaterEquals)),
            Global("_<_", CalendarOverloadIds.LessTimeOfDay, [TimeOfDay, TimeOfDay], Bool, TimeOfDayOrdering(Relation.Less)),
            Global("_<=_", CalendarOverloadIds.LessEqualsTimeOfDay, [TimeOfDay, TimeOfDay], Bool, TimeOfDayOrdering(Relation.LessEquals)),
            Global("_>_", CalendarOverloadIds.GreaterTimeOfDay, [TimeOfDay, TimeOfDay], Bool, TimeOfDayOrdering(Relation.Greater)),
            Global("_>=_", CalendarOverloadIds.GreaterEqualsTimeOfDay, [TimeOfDay, TimeOfDay], Bool, TimeOfDayOrdering(Relation.GreaterEquals)),
        ];

        ImmutableArray<OverloadBinding>.Builder described = ImmutableArray.CreateBuilder<OverloadBinding>(table.Length);
        foreach (OverloadBinding binding in table)
        {
            described.Add(Descriptions.TryGetValue(binding.Function, out string? text)
                ? binding.WithDeclaration(binding.Declaration.WithDescription(text))
                : binding);
        }

        return described.MoveToImmutable();
    }

    // A function called as f(arguments), or an operator.
    private static OverloadBinding Global(
        string function,
        string overloadId,
        ImmutableArray<CelType> parameterTypes,
        CelType resultType,
        OverloadImplementation implementation,
        CelCostFunction? argumentCost = null) =>
        new(function, new CelOverloadDecl(overloadId, false, parameterTypes, resultType), implementation, 1, argumentCost, Source);

    // An overload called on its receiver, which is the first parameter.
    private static OverloadBinding Member(
        string function,
        string overloadId,
        ImmutableArray<CelType> parameterTypes,
        CelType resultType,
        OverloadImplementation implementation) =>
        new(function, new CelOverloadDecl(overloadId, true, parameterTypes, resultType), implementation, 1, null, Source);
}
