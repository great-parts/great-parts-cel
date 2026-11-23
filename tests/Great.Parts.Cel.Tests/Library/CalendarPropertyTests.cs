using System;
using System.Threading;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// The properties of the set calendar over random dates and times of day of a fixed seed: the text reads back to the value, the parts
// agree with DateOnly, the shifts agree with DateOnly.AddDays, AddMonths and AddYears (and are Overflow where those throw), the
// difference of two dates is the difference of their day numbers, and the instant of a local noon in a zone gives the same date in
// that zone. Noon is never in the gap or the overlap of a zone of the years tested.
[Trait("Requirement", "F-24")]
public sealed class CalendarPropertyTests
{
    private const int Rounds = 1500;
    private const int MaxDayNumber = 3_652_058;

    private static readonly string[] Zones = ["UTC", "Europe/Moscow", "America/New_York", "America/Sao_Paulo", "Asia/Kathmandu", "+05:45", "-11:00"];

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Calendar(1))
        .UseTimeZones(ZoneInfoFile.Database())
        .AddVariable<DateOnly>("d")
        .AddVariable<DateOnly>("e")
        .AddVariable<TimeOnly>("x")
        .AddVariable<long>("n")
        .AddVariable<string>("z")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public static void TextOfADateAndOfATimeOfDay_ReadsBackToTheValue()
    {
        CelProgram<bool> program = Compile<bool>("date(string(d)) == d && time_of_day(string(x)) == x");
        var random = new Sequence(20261123);

        for (int round = 0; round < Rounds; round++)
        {
            CelBindings bindings = new CelBindings().Add("d", RandomDate(random)).Add("x", RandomTime(random));

            Assert.True(Run(program, bindings));
        }
    }

    [Fact]
    public static void PartsOfADate_AgreeWithDateOnly()
    {
        CelProgram<long> year = Compile<long>("d.getFullYear()");
        CelProgram<long> month = Compile<long>("d.getMonth()");
        CelProgram<long> day = Compile<long>("d.getDate()");
        CelProgram<long> dayOfMonth = Compile<long>("d.getDayOfMonth()");
        CelProgram<long> dayOfWeek = Compile<long>("d.getDayOfWeek()");
        CelProgram<long> dayOfYear = Compile<long>("d.getDayOfYear()");
        var random = new Sequence(1);

        for (int round = 0; round < Rounds; round++)
        {
            DateOnly date = RandomDate(random);
            CelBindings bindings = new CelBindings().Add("d", date);

            Assert.Equal(date.Year, Run(year, bindings));
            Assert.Equal(date.Month - 1, Run(month, bindings));
            Assert.Equal(date.Day, Run(day, bindings));
            Assert.Equal(date.Day - 1, Run(dayOfMonth, bindings));
            Assert.Equal((long)date.DayOfWeek, Run(dayOfWeek, bindings));
            Assert.Equal(date.DayOfYear - 1, Run(dayOfYear, bindings));
        }
    }

    [Fact]
    public static void PartsOfATimeOfDay_AgreeWithTimeOnly()
    {
        CelProgram<long> hours = Compile<long>("x.getHours()");
        CelProgram<long> minutes = Compile<long>("x.getMinutes()");
        CelProgram<long> seconds = Compile<long>("x.getSeconds()");
        CelProgram<long> milliseconds = Compile<long>("x.getMilliseconds()");
        var random = new Sequence(2);

        for (int round = 0; round < Rounds; round++)
        {
            TimeOnly time = RandomTime(random);
            CelBindings bindings = new CelBindings().Add("x", time);

            Assert.Equal(time.Hour, Run(hours, bindings));
            Assert.Equal(time.Minute, Run(minutes, bindings));
            Assert.Equal(time.Second, Run(seconds, bindings));
            Assert.Equal(time.Millisecond, Run(milliseconds, bindings));
        }
    }

    [Fact]
    public static void Shifts_AgreeWithDateOnlyAndAreOverflowWhereItThrows()
    {
        CelProgram<CelValue> days = Compile<CelValue>("d.addDays(n)");
        CelProgram<CelValue> months = Compile<CelValue>("d.addMonths(n)");
        CelProgram<CelValue> years = Compile<CelValue>("d.addYears(n)");
        var random = new Sequence(3);

        for (int round = 0; round < Rounds; round++)
        {
            DateOnly date = RandomDate(random);
            long count = random.Next(-5000, 5000) * (round % 3 == 0 ? 100 : 1);
            CelBindings bindings = new CelBindings().Add("d", date).Add("n", count);

            AssertShift(days, bindings, () => date.AddDays(checked((int)count)));
            AssertShift(months, bindings, () => date.AddMonths(checked((int)count)));
            AssertShift(years, bindings, () => date.AddYears(checked((int)count)));
        }
    }

    [Fact]
    public static void DifferenceOfTwoDates_IsTheDifferenceOfTheirDayNumbers()
    {
        CelProgram<long> program = Compile<long>("d - e");
        var random = new Sequence(4);

        for (int round = 0; round < Rounds; round++)
        {
            DateOnly first = RandomDate(random);
            DateOnly second = RandomDate(random);
            CelBindings bindings = new CelBindings().Add("d", first).Add("e", second);

            Assert.Equal(first.DayNumber - second.DayNumber, Run(program, bindings));
        }
    }

    [Fact]
    public static void InstantOfALocalNoon_GivesTheSameDateAndTimeInTheZone()
    {
        CelProgram<bool> program = Compile<bool>(
            "date(timestamp(d, time_of_day('12:00'), z), z) == d && time_of_day(timestamp(d, time_of_day('12:00'), z), z) == time_of_day('12:00')");
        var random = new Sequence(5);

        for (int round = 0; round < Rounds; round++)
        {
            // From 1971 to 2099, where the zones have their rules, and a day away from the ends of the range of timestamp.
            var date = new DateOnly(1971, 1, 1).AddDays(random.Next(0, 129 * 365));
            CelBindings bindings = new CelBindings().Add("d", date).Add("z", Zones[random.Next(0, Zones.Length)]);

            Assert.True(Run(program, bindings));
        }
    }

    // Every transition of three zones of the test database: the local seconds around it (the last second before the gap or the
    // overlap, its first and last second, and the first second after) give the instant of the algorithm. A local time before the
    // transition uses the offset before it, a later one the offset after it, and in the gap (the clocks go forward) and in the
    // overlap (they go back) the offset before it: the gap is skipped forward and the earlier of the two instants is taken. One
    // formula covers the gap, the overlap and the transition at midnight. A transition with another one nearer than 36 hours is
    // left out, as its local times could belong to either.
    [Theory]
    [InlineData("Europe/Moscow")]
    [InlineData("America/Sao_Paulo")]
    [InlineData("America/New_York")]
    public static void InstantOfALocalTime_AroundEveryTransitionOfAZone_FollowsTheOffsetBeforeInTheGapAndTheOverlap(string zone)
    {
        ZoneData data = ZoneInfoFile.Database().Find(zone) ?? throw new InvalidOperationException("the zone is not in the database");
        CelProgram<long> program = Compile<long>("int(timestamp(d, x, z))");
        long minimum = -62_135_596_800L + 2 * 86_400L;
        long maximum = 253_402_300_799L - 2 * 86_400L;
        int tested = 0;

        for (int i = 1; i < data.Transitions.Length; i++)
        {
            long moment = data.Transitions[i];
            long before = data.TypeOffsets[data.TransitionTypes[i - 1]];
            long after = data.TypeOffsets[data.TransitionTypes[i]];
            bool near = Math.Abs(moment - data.Transitions[i - 1]) < 36 * 3600
                || (i + 1 < data.Transitions.Length && Math.Abs(data.Transitions[i + 1] - moment) < 36 * 3600);
            if (near || moment < minimum || moment > maximum)
            {
                continue;
            }

            foreach (long local in new[] { moment + before - 1, moment + before, moment + after - 1, moment + after })
            {
                long expected = local < moment + Math.Max(before, after) ? local - before : local - after;
                long days = Math.DivRem(local, 86_400L, out long rest);
                if (rest < 0)
                {
                    days--;
                    rest += 86_400L;
                }

                CelBindings bindings = new CelBindings()
                    .Add("d", DateOnly.FromDayNumber((int)(719_162 + days)))
                    .Add("x", new TimeOnly(rest * TimeSpan.TicksPerSecond))
                    .Add("z", zone);

                Assert.Equal(expected, Run(program, bindings));
                tested++;
            }
        }

        Assert.True(tested > 100, zone + ": the transitions tested are too few");
    }

    private static void AssertShift(CelProgram<CelValue> program, CelBindings bindings, Func<DateOnly> expected)
    {
        CelEvalResult<CelValue> result = program.Evaluate(bindings, cancellationToken: CancellationToken.None);
        DateOnly? shifted;
        try
        {
            shifted = expected();
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or OverflowException)
        {
            shifted = null;
        }

        if (shifted is null)
        {
            Assert.Equal(CelErrorCode.Overflow, Assert.Single(result.Errors).Code);
        }
        else
        {
            Assert.True(result.GetValueOrThrow().TryGetDate(out DateOnly actual));
            Assert.Equal(shifted.Value, actual);
        }
    }

    private static CelProgram<T> Compile<T>(string text) => Environment.Compile<T>(text).GetProgramOrThrow();

    private static T Run<T>(CelProgram<T> program, CelBindings bindings) =>
        program.Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

    private static DateOnly RandomDate(Sequence random) => DateOnly.FromDayNumber(random.Next(0, MaxDayNumber + 1));

    private static TimeOnly RandomTime(Sequence random) => new(random.NextInt64(0, TimeOnly.MaxValue.Ticks + 1));

    // SplitMix64: a fixed sequence for a seed, the same in every run on every platform.
    private sealed class Sequence(ulong seed)
    {
        private ulong _state = seed;

        internal int Next(int minInclusive, int maxExclusive) => minInclusive + (int)(NextBits() % (ulong)(maxExclusive - minInclusive));

        internal long NextInt64(long minInclusive, long maxExclusive) => minInclusive + (long)(NextBits() % (ulong)(maxExclusive - minInclusive));

        private ulong NextBits()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
