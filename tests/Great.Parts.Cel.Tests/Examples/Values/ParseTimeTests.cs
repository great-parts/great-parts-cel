using System;
using System.Text;
using System.Threading;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// CelTimestamp.TryParse and CelDuration.TryParse read a string exactly when the functions timestamp(string) and duration(string)
// return a value, and to the same value.
[Trait("Requirement", "F-32")]
public sealed class ParseTimeTests
{
    private const int Cases = 1_000;

    private static readonly CelEnvironment Env = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("s", CelType.String)
        .AddVariable("t", CelType.Timestamp)
        .AddVariable("d", CelType.Duration)
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelProgram<bool> TimestampSame = Env.Compile<bool>("timestamp(s) == t").GetProgramOrThrow();

    private static readonly CelProgram<bool> DurationSame = Env.Compile<bool>("duration(s) == d").GetProgramOrThrow();

    // Texts that are timestamps and durations, which the random texts start from.
    private static readonly string[] ValidTimestamps =
    [
        "2026-01-01T00:00:00Z",
        "2026-01-01T03:00:00+03:00",
        "0001-01-01T00:00:00Z",
        "9999-12-31T23:59:59.999999999Z",
        "2024-02-29T12:30:45.5-08:30",
    ];

    private static readonly string[] ValidDurations = ["20s", "1.5s", "-1h30m", "0", "100ms", "2h45m30.5s", "1us1ns"];

    [Fact]
    public void TryParse_OfATimestamp_GivesTheValue()
    {
        Assert.True(CelTimestamp.TryParse("2026-01-01T00:00:00Z", out CelTimestamp utc));
        Assert.True(CelTimestamp.TryParse("2026-01-01T03:00:00+03:00", out CelTimestamp offset));

        Assert.Equal(1_767_225_600L, utc.Seconds);
        Assert.Equal(utc, offset);
        Assert.False(CelDuration.TryParse("2026-01-01T00:00:00Z", out CelDuration none));
        Assert.Equal(default, none);
    }

    [Fact]
    public void TryParse_OfATextThatIsNotATimestamp_IsFalseWithTheDefault()
    {
        foreach (string text in new[] { "2026-13-01T00:00:00Z", "2026-01-01", string.Empty, "20s", "20" })
        {
            Assert.False(CelTimestamp.TryParse(text, out CelTimestamp value));
            Assert.Equal(default, value);
        }
    }

    [Fact]
    public void TryParse_OfADuration_GivesTheValue()
    {
        Assert.True(CelDuration.TryParse("20s", out CelDuration twenty));
        Assert.True(CelDuration.TryParse("1.5s", out CelDuration fraction));
        Assert.True(CelDuration.TryParse("-1h30m", out CelDuration negative));

        Assert.Equal(new CelDuration(20, 0), twenty);
        Assert.Equal(new CelDuration(1, 500_000_000), fraction);
        Assert.Equal(new CelDuration(-5_400, 0), negative);
    }

    [Fact]
    public void TryParse_OfATextThatIsNotADuration_IsFalseWithTheDefault()
    {
        foreach (string text in new[] { "20", "1d", string.Empty, "2026-01-01T00:00:00Z", "s" })
        {
            Assert.False(CelDuration.TryParse(text, out CelDuration value));
            Assert.Equal(default, value);
        }
    }

    [Fact]
    public void TryParse_OfNull_IsAnArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CelTimestamp.TryParse(null!, out _));
        Assert.Throws<ArgumentNullException>(() => CelDuration.TryParse(null!, out _));
    }

    [Fact]
    public void TimestampTryParse_OfRandomTexts_ReadsExactlyWhatTheFunctionReads()
    {
        var random = new Source(20261123);
        for (int i = 0; i < Cases; i++)
        {
            string text = RandomText(random, ValidTimestamps, "0123456789-:TZ.+tz ");
            bool parsed = CelTimestamp.TryParse(text, out CelTimestamp value);

            CelBindings bindings = new CelBindings().Add("s", text).Add("t", value);
            CelEvalResult<bool> same = TimestampSame.Evaluate(bindings, cancellationToken: CancellationToken.None);

            Assert.True(parsed == (same.IsSuccess && same.Value), "timestamp: " + text);
        }
    }

    [Fact]
    public void DurationTryParse_OfRandomTexts_ReadsExactlyWhatTheFunctionReads()
    {
        var random = new Source(20261124);
        for (int i = 0; i < Cases; i++)
        {
            string text = RandomText(random, ValidDurations, "0123456789.-+hmsun ");
            bool parsed = CelDuration.TryParse(text, out CelDuration value);

            CelBindings bindings = new CelBindings().Add("s", text).Add("d", value);
            CelEvalResult<bool> same = DurationSame.Evaluate(bindings, cancellationToken: CancellationToken.None);

            Assert.True(parsed == (same.IsSuccess && same.Value), "duration: " + text);
        }
    }

    // A random text of the alphabet, or a valid text with one character changed, dropped or added, so that both the valid and the
    // invalid texts are met.
    private static string RandomText(Source random, string[] valid, string alphabet)
    {
        if (random.Next(2) == 0)
        {
            var text = new StringBuilder();
            for (int length = random.Next(0, 36); length > 0; length--)
            {
                text.Append(alphabet[random.Next(alphabet.Length)]);
            }

            return text.ToString();
        }

        var chars = new StringBuilder(valid[random.Next(valid.Length)]);
        int at = random.Next(chars.Length + 1);
        switch (random.Next(3))
        {
            case 0 when at < chars.Length:
                chars[at] = alphabet[random.Next(alphabet.Length)];
                break;
            case 1 when at < chars.Length:
                chars.Remove(at, 1);
                break;
            case 2:
                chars.Insert(at, alphabet[random.Next(alphabet.Length)]);
                break;
            default:
                break;
        }

        return chars.ToString();
    }

    // A repeatable source of numbers (SplitMix64), so that a failing text is the same on every run.
    private sealed class Source(ulong seed)
    {
        private ulong _state = seed;

        // A number from 0 to the limit, the limit excluded.
        internal int Next(int limit)
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (int)(z % (ulong)limit);
        }

        // A number from the first limit, included, to the second, excluded.
        internal int Next(int from, int limit) => from + Next(limit - from);
    }
}
