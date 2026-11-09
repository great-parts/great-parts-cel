namespace Great.Parts.Cel.Library.Time;

// The overload identifiers of time, letter for letter as in cel-go, so that the overload lists of the library and of the
// reference implementation compare without exceptions. They are constants of their own because Library.Time cannot refer
// to the identifiers of the Library namespace.
internal static class TimeOverloadIds
{
    internal const string TimestampToTimestamp = "timestamp_to_timestamp";
    internal const string DurationToDuration = "duration_to_duration";
    internal const string Int64ToTimestamp = "int64_to_timestamp";
    internal const string StringToTimestamp = "string_to_timestamp";
    internal const string TimestampToInt64 = "timestamp_to_int64";
    internal const string DurationToInt64 = "duration_to_int64";
    internal const string StringToDuration = "string_to_duration";
    internal const string TimestampToString = "timestamp_to_string";
    internal const string DurationToString = "duration_to_string";

    internal const string AddTimestampDuration = "add_timestamp_duration";
    internal const string AddDurationTimestamp = "add_duration_timestamp";
    internal const string AddDurationDuration = "add_duration_duration";
    internal const string SubtractTimestampDuration = "subtract_timestamp_duration";
    internal const string SubtractTimestampTimestamp = "subtract_timestamp_timestamp";
    internal const string SubtractDurationDuration = "subtract_duration_duration";

    internal const string LessTimestamp = "less_timestamp";
    internal const string LessDuration = "less_duration";
    internal const string LessEqualsTimestamp = "less_equals_timestamp";
    internal const string LessEqualsDuration = "less_equals_duration";
    internal const string GreaterTimestamp = "greater_timestamp";
    internal const string GreaterDuration = "greater_duration";
    internal const string GreaterEqualsTimestamp = "greater_equals_timestamp";
    internal const string GreaterEqualsDuration = "greater_equals_duration";

    internal const string TimestampToYear = "timestamp_to_year";
    internal const string TimestampToYearWithTz = "timestamp_to_year_with_tz";
    internal const string TimestampToMonth = "timestamp_to_month";
    internal const string TimestampToMonthWithTz = "timestamp_to_month_with_tz";
    internal const string TimestampToDayOfYear = "timestamp_to_day_of_year";
    internal const string TimestampToDayOfYearWithTz = "timestamp_to_day_of_year_with_tz";
    internal const string TimestampToDayOfMonth = "timestamp_to_day_of_month";
    internal const string TimestampToDayOfMonthWithTz = "timestamp_to_day_of_month_with_tz";
    internal const string TimestampToDayOfMonth1Based = "timestamp_to_day_of_month_1_based";
    internal const string TimestampToDayOfMonth1BasedWithTz = "timestamp_to_day_of_month_1_based_with_tz";
    internal const string TimestampToDayOfWeek = "timestamp_to_day_of_week";
    internal const string TimestampToDayOfWeekWithTz = "timestamp_to_day_of_week_with_tz";
    internal const string TimestampToHours = "timestamp_to_hours";
    internal const string TimestampToHoursWithTz = "timestamp_to_hours_with_tz";
    internal const string DurationToHours = "duration_to_hours";
    internal const string TimestampToMinutes = "timestamp_to_minutes";
    internal const string TimestampToMinutesWithTz = "timestamp_to_minutes_with_tz";
    internal const string DurationToMinutes = "duration_to_minutes";
    internal const string TimestampToSeconds = "timestamp_to_seconds";
    internal const string TimestampToSecondsTz = "timestamp_to_seconds_tz";
    internal const string DurationToSeconds = "duration_to_seconds";
    internal const string TimestampToMilliseconds = "timestamp_to_milliseconds";
    internal const string TimestampToMillisecondsWithTz = "timestamp_to_milliseconds_with_tz";
    internal const string DurationToMilliseconds = "duration_to_milliseconds";
}
