using System;
using CsCheck;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Values;

// The calendar of the library agrees with DateTime (docs/design/tests.md, "Properties of evaluation"): for a moment of the whole
// range of timestamp, the receivers without a zone give the year, the month, the day, the day of the week and of the year, the hour,
// the minute, the second and the millisecond of the same moment as DateTime in UTC. The library's own calendar has no code in common
// with DateTime, so a difference is a defect of one of the two.
public sealed class CalendarProperties
{
    private const ulong Seed = 20261114;
    private const int Cases = 3000;

    private const long FirstSecond = -62_135_596_800L;
    private const long LastSecond = 253_402_300_799L;
    private const long SecondsFromYearOneToEpoch = 62_135_596_800L;

    // The ten receivers of timestamp in the order of the fields compared below.
    private const string Receivers =
        "[ts.getFullYear(), ts.getMonth(), ts.getDayOfMonth(), ts.getDate(), ts.getDayOfWeek(), ts.getDayOfYear(), ts.getHours(), ts.getMinutes(), ts.getSeconds(), ts.getMilliseconds()]";

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("ts", CelType.Timestamp)
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelProgram<CelValue> Program = Environment.Compile<CelValue>(Receivers).GetProgramOrThrow();

    // Moments of the whole range, and a share around the days of the end of a month and of a year, where a calendar errs.
    private static readonly Gen<CelTimestamp> Moments = Gen.Frequency(
        (3, Gen.Select(Gen.Long[FirstSecond, LastSecond], Gen.Int[0, 999_999_999], (seconds, nanoseconds) => new CelTimestamp(seconds, nanoseconds))),
        (2, Gen.Select(Gen.Int[1, 9999], Gen.Int[1, 12], Gen.Int[0, 86_399], Gen.Int[-2, 2], Gen.Int[0, 999_999_999], AroundEndOfMonth)));

    [Fact]
    [Trait("Requirement", "F-8")]
    public void ReceiversWithoutAZone_AgreeWithDateTimeInUtc() =>
        PropertyLoop.Check(nameof(ReceiversWithoutAZone_AgreeWithDateTimeInUtc), Moments, Seed, Cases, Violation, moment => moment.ToString());

    // The last day of a month and the days next to it, at a time of the day.
    private static CelTimestamp AroundEndOfMonth(int year, int month, int secondOfDay, int shift, int nanoseconds)
    {
        var firstOfNext = month == 12 ? new DateTime(Math.Min(year + 1, 9999), 1, 1, 0, 0, 0, DateTimeKind.Utc) : new DateTime(year, month + 1, 1, 0, 0, 0, DateTimeKind.Utc);
        long seconds = (firstOfNext.Ticks / TimeSpan.TicksPerSecond) - SecondsFromYearOneToEpoch + secondOfDay + (shift * 86_400L);
        return new CelTimestamp(Math.Clamp(seconds, FirstSecond, LastSecond), nanoseconds);
    }

    private static string? Violation(CelTimestamp moment)
    {
        CelEvalResult<CelValue> result = Program.Evaluate(new CelBindings().Add("ts", moment));
        if (!result.IsSuccess || !result.Value.TryGetList(out CelList? list) || list.Count != 10)
        {
            return "the receivers gave no list of ten values";
        }

        var expected = new DateTime(DateTime.MinValue.Ticks + ((moment.Seconds + SecondsFromYearOneToEpoch) * TimeSpan.TicksPerSecond), DateTimeKind.Utc);
        long[] reference =
        [
            expected.Year,
            expected.Month - 1,
            expected.Day - 1,
            expected.Day,
            (int)expected.DayOfWeek,
            expected.DayOfYear - 1,
            expected.Hour,
            expected.Minute,
            expected.Second,
            moment.Nanoseconds / 1_000_000,
        ];
        string[] names = ["year", "month", "day of the month from 0", "day of the month", "day of the week", "day of the year", "hour", "minute", "second", "millisecond"];
        for (int i = 0; i < reference.Length; i++)
        {
            if (!list[i].TryGetInt64(out long actual) || actual != reference[i])
            {
                return $"{names[i]} of {expected:O}: library {(list[i].TryGetInt64(out long shown) ? shown.ToString(System.Globalization.CultureInfo.InvariantCulture) : "no int")}, DateTime {reference[i]}";
            }
        }

        return null;
    }
}
