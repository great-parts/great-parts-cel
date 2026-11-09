using System;
using CsCheck;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Values;

// The text of time reads back what it prints (docs/design/tests.md, "Properties of evaluation"): the text of a timestamp or a
// duration, printed by string(), is read by timestamp() and duration() into the same value, and has the exact form of the page of
// the standard library — the letter Z or s at the end, no zeros at the end of the fraction.
public sealed class TimeTextProperties
{
    private const ulong TimestampSeed = 20261112;
    private const ulong DurationSeed = 20261113;
    private const int Cases = 2000;

    private const long FirstSecond = -62_135_596_800L;
    private const long LastSecond = 253_402_300_799L;

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("ts", CelType.Timestamp)
        .AddVariable("du", CelType.Duration)
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelProgram<string> TimestampText = Environment.Compile<string>("string(ts)").GetProgramOrThrow();

    private static readonly CelProgram<bool> TimestampBack = Environment.Compile<bool>("timestamp(string(ts)) == ts").GetProgramOrThrow();

    private static readonly CelProgram<string> DurationText = Environment.Compile<string>("string(du)").GetProgramOrThrow();

    private static readonly CelProgram<bool> DurationBack = Environment.Compile<bool>("duration(string(du)) == du").GetProgramOrThrow();

    // Seconds of the whole range and nanoseconds, with a share of cases whose fraction has few digits or none, so the printing
    // of the fraction meets every length.
    private static readonly Gen<CelTimestamp> Timestamps = Gen.Frequency(
        (3, Gen.Select(Gen.Long[FirstSecond, LastSecond], Gen.Int[0, 999_999_999], (seconds, nanoseconds) => new CelTimestamp(seconds, nanoseconds))),
        (2, Gen.Select(Gen.Long[FirstSecond, LastSecond], Gen.Int[0, 9], Gen.Int[0, 8], (seconds, digits, zeros) =>
            new CelTimestamp(seconds, digits * Power10(zeros)))),
        (1, Gen.Select(Gen.Bool, Gen.Int[0, 999_999_999], (last, nanoseconds) =>
            new CelTimestamp(last ? LastSecond : FirstSecond, nanoseconds))));

    // Nanoseconds of the whole range of long, and the whole seconds, and a few of them.
    private static readonly Gen<CelDuration> Durations = Gen.Frequency(
        (3, Gen.Long.Select(nanoseconds => new CelDuration(nanoseconds))),
        (2, Gen.Select(Gen.Long[-9_223_372_036L, 9_223_372_036L], Gen.Int[0, 9], (seconds, digit) =>
            new CelDuration((seconds * 1_000_000_000L) + (digit * 100_000_000L)))),
        (1, Gen.Long[-5_000L, 5_000L].Select(nanoseconds => new CelDuration(nanoseconds))));

    [Fact]
    [Trait("Requirement", "F-8")]
    public void TimestampText_IsReadBackAndHasTheExactForm() =>
        PropertyLoop.Check(nameof(TimestampText_IsReadBackAndHasTheExactForm), Timestamps, TimestampSeed, Cases, TimestampViolation, value => value.ToString());

    [Fact]
    [Trait("Requirement", "F-8")]
    public void DurationText_IsReadBackAndHasTheExactForm() =>
        PropertyLoop.Check(nameof(DurationText_IsReadBackAndHasTheExactForm), Durations, DurationSeed, Cases, DurationViolation, value => value.ToString());

    private static string? TimestampViolation(CelTimestamp value)
    {
        var bindings = new CelBindings().Add("ts", value);
        CelEvalResult<string> text = TimestampText.Evaluate(bindings);
        if (!text.IsSuccess)
        {
            return "string() failed: " + text.Errors[0].Message;
        }

        string printed = text.GetValueOrThrow();
        if (printed.Length < 20 || printed[^1] != 'Z' || printed[10] != 'T')
        {
            return "the text is not of the form of RFC 3339 in UTC: " + printed;
        }

        if (printed.Contains('.', StringComparison.Ordinal) && (printed[^2] == '0' || printed.Length > 30))
        {
            return "the fraction has a zero at its end or more than 9 digits: " + printed;
        }

        CelEvalResult<bool> back = TimestampBack.Evaluate(bindings);
        return back.IsSuccess && back.GetValueOrThrow() ? null : $"timestamp('{printed}') is not the value";
    }

    private static string? DurationViolation(CelDuration value)
    {
        var bindings = new CelBindings().Add("du", value);
        CelEvalResult<string> text = DurationText.Evaluate(bindings);
        if (!text.IsSuccess)
        {
            return "string() failed: " + text.Errors[0].Message;
        }

        string printed = text.GetValueOrThrow();
        if (printed.Length < 2 || printed[^1] != 's')
        {
            return "the text does not end with s: " + printed;
        }

        if (printed.Contains('.', StringComparison.Ordinal) && printed[^2] == '0')
        {
            return "the fraction has a zero at its end: " + printed;
        }

        CelEvalResult<bool> back = DurationBack.Evaluate(bindings);
        return back.IsSuccess && back.GetValueOrThrow() ? null : $"duration('{printed}') is not the value";
    }

    private static int Power10(int exponent)
    {
        int result = 1;
        for (int i = 0; i < exponent; i++)
        {
            result *= 10;
        }

        return result;
    }
}
