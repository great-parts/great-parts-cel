using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Library.Time;

namespace Great.Parts.Cel;

public static partial class CelExtensions
{
    /// <summary>
    /// The <c>calendar</c> extension set: the CEL types <c>date</c>, a calendar date, and <c>time_of_day</c>, a time of
    /// day, with the functions that create, read, shift and compare them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Version 1 has 36 overloads. <c>date('2026-11-19')</c>, <c>date(2026, 11, 19)</c>, <c>date(t)</c> and
    /// <c>date(t, 'Europe/Moscow')</c> create a date from text, from its parts and from a timestamp in UTC or in a time
    /// zone; <c>time_of_day('09:30')</c>, <c>time_of_day(t)</c> and <c>time_of_day(t, zone)</c> create a time of day;
    /// <c>timestamp(d, t)</c> and <c>timestamp(d, t, zone)</c> give the instant of a local date and time. The getters
    /// <c>getFullYear</c>, <c>getMonth</c>, <c>getDate</c>, <c>getDayOfMonth</c>, <c>getDayOfWeek</c>,
    /// <c>getDayOfYear</c>, <c>getHours</c>, <c>getMinutes</c>, <c>getSeconds</c> and <c>getMilliseconds</c> count as the
    /// getters of a timestamp do: <c>getMonth</c> from zero, <c>getDate</c> from one. <c>d.addDays(n)</c>,
    /// <c>d.addMonths(n)</c> and <c>d.addYears(n)</c> shift a date, a day missing in the target month becoming its last
    /// day; <c>d1 - d2</c> is the number of days between two dates and <c>t1 - t2</c> the duration between two times of
    /// day. Dates and times of day compare with <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>, <c>==</c> and
    /// <c>!=</c>; a date equals only a date and a time of day only a time of day.
    /// </para>
    /// <para>
    /// A date ranges from 0001-01-01 to 9999-12-31, the range of <see cref="DateOnly"/>. A time of day ranges from 00:00 to
    /// 23:59:59.9999999 with a precision of 100 nanoseconds, the precision of <see cref="TimeOnly"/>: creating one from a
    /// timestamp drops the smaller part, as the getters of a timestamp drop the parts they do not return. A local time
    /// that a time zone skips is moved forward by the length of the gap, and a local time that it repeats takes the
    /// earlier offset. Time zones are found as for the getters of a timestamp with a time zone, in the time zone database
    /// of the environment.
    /// </para>
    /// <para>
    /// The set also makes members of the .NET types <see cref="DateOnly"/> and <see cref="TimeOnly"/> available to
    /// expressions; without it, selecting such a member is a type checking error
    /// <see cref="Diagnostics.CelErrorCode.FieldNotAccessible"/> that names the set. The set declares the type names
    /// <c>date</c> and <c>time_of_day</c>, so a variable or a type of the application with one of these names is the
    /// build error <see cref="Diagnostics.CelErrorCode.DuplicateType"/> that names both sources once the set is added,
    /// as the name <c>decimal</c> is with the <c>decimal</c> extension. The set has no counterpart in
    /// cel-go and cannot be named in an environment description in JSON; add it in code. The page
    /// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/extensions.md#set-calendar-version-1">extensions</see>
    /// lists the overloads.
    /// </para>
    /// </remarks>
    /// <param name="version">
    /// The version of the set; version 1 is the only one. Another version gives a set that the environment rejects with
    /// the build error <see cref="Diagnostics.CelErrorCode.UnknownExtensionVersion"/>.
    /// </param>
    /// <returns>The extension set.</returns>
    public static CelExtension Calendar(uint version) => CalendarExtension.Get(version);
}
