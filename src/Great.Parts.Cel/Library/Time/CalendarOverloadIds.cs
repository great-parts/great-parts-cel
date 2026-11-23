namespace Great.Parts.Cel.Library.Time;

// The overload identifiers of the set calendar, version 1: the 36 overloads of its page, in the order of the page. The set is not in
// cel-go, so the identifiers are the library's own, built as cel-go builds them: the kinds of the arguments, to and the result.
internal static class CalendarOverloadIds
{
    internal const string StringToDate = "string_to_date";
    internal const string Int64Int64Int64ToDate = "int64_int64_int64_to_date";
    internal const string TimestampToDate = "timestamp_to_date";
    internal const string TimestampToDateWithTz = "timestamp_to_date_with_tz";
    internal const string DateToDate = "date_to_date";
    internal const string StringToTimeOfDay = "string_to_time_of_day";
    internal const string TimestampToTimeOfDay = "timestamp_to_time_of_day";
    internal const string TimestampToTimeOfDayWithTz = "timestamp_to_time_of_day_with_tz";
    internal const string TimeOfDayToTimeOfDay = "time_of_day_to_time_of_day";
    internal const string DateToString = "date_to_string";
    internal const string TimeOfDayToString = "time_of_day_to_string";
    internal const string DateTimeOfDayToTimestamp = "date_time_of_day_to_timestamp";
    internal const string DateTimeOfDayToTimestampWithTz = "date_time_of_day_to_timestamp_with_tz";

    internal const string DateToYear = "date_to_year";
    internal const string DateToMonth = "date_to_month";
    internal const string DateToDayOfMonth1Based = "date_to_day_of_month_1_based";
    internal const string DateToDayOfMonth = "date_to_day_of_month";
    internal const string DateToDayOfWeek = "date_to_day_of_week";
    internal const string DateToDayOfYear = "date_to_day_of_year";
    internal const string TimeOfDayToHours = "time_of_day_to_hours";
    internal const string TimeOfDayToMinutes = "time_of_day_to_minutes";
    internal const string TimeOfDayToSeconds = "time_of_day_to_seconds";
    internal const string TimeOfDayToMilliseconds = "time_of_day_to_milliseconds";

    internal const string DateAddDays = "date_add_days";
    internal const string DateAddMonths = "date_add_months";
    internal const string DateAddYears = "date_add_years";
    internal const string SubtractDateDate = "subtract_date_date";
    internal const string SubtractTimeOfDayTimeOfDay = "subtract_time_of_day_time_of_day";

    internal const string LessDate = "less_date";
    internal const string LessEqualsDate = "less_equals_date";
    internal const string GreaterDate = "greater_date";
    internal const string GreaterEqualsDate = "greater_equals_date";
    internal const string LessTimeOfDay = "less_time_of_day";
    internal const string LessEqualsTimeOfDay = "less_equals_time_of_day";
    internal const string GreaterTimeOfDay = "greater_time_of_day";
    internal const string GreaterEqualsTimeOfDay = "greater_equals_time_of_day";
}
