using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using CsCheck;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Facade;

// An environment with a time zone database, called from many threads at once, gives the results of sequential calls
// (docs/design/tests.md, "Properties of evaluation"). The database is the one of the whole test archive; 64 threads, started together,
// each run every item of three scenarios in an order of their own: the evaluation of one program per text, the one-step call, and
// GetOrCompile with Evaluate of one compilation cache. The texts read zones of the pool — names of the database, UTC, offsets, names
// that give errors — and moments and durations near the limits, so the threads search the same zones at the same time.
public sealed class TimeZoneConcurrencyTests
{
    private const ulong Seed = 20261111;
    private const int Threads = 64;
    private const int BindingsPerText = 16;
    private const int CacheEntries = 4;

    private static readonly string[] Texts =
    [
        "ts.getHours(z)",
        "ts.getMinutes(z)",
        "ts.getFullYear('Asia/Kathmandu')",
        "ts.getDayOfYear('America/New_York')",
        "ts.getDayOfWeek('+05:45')",
        "ts.getDate('-03:30')",
        "ts.getSeconds('Australia/Lord_Howe')",
        "ts.getHours('Mars/Base')",
        "ts.getHours('+1:5')",
        "ts.getHours('')",
        "string(ts + du)",
        "ts - du < ts",
        "timestamp(string(ts)) == ts",
        "duration(string(du)) == du",
        "du.getMilliseconds() + int(ts)",
    ];

    // The names of the zones of the pool, the zone variable of a binding.
    private static readonly string[] Zones =
    [
        "UTC", "+05:45", "-03:30", "Asia/Kathmandu", "America/New_York", "Europe/Moscow", "Australia/Lord_Howe", "Mars/Base", "", "+1:5",
    ];

    // Moments of the range of timestamp that sit on its limits, and a transition of a zone with daylight saving time.
    private static readonly long[] FixedMoments = [-62_135_596_800L, 253_402_300_799L, 0L, 1_678_604_399L, 1_678_604_400L];

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .UseTimeZones(ZoneInfoFile.Database())
        .AddVariable("ts", CelType.Timestamp)
        .AddVariable("du", CelType.Duration)
        .AddVariable("z", CelType.String)
        .Build()
        .GetEnvironmentOrThrow();

    private enum Scenario
    {
        Program,
        OneStep,
        Cache,
    }

    [Fact]
    [Trait("Requirement", "R-4")]
    [Trait("Requirement", "R-5")]
    public void ParallelCallsWithADatabase_EqualSequentialCalls()
    {
        if (Violation() is { } reason)
        {
            Assert.Fail(string.Create(CultureInfo.InvariantCulture, $"{nameof(ParallelCallsWithADatabase_EqualSequentialCalls)}: seed {Seed}: {reason}"));
        }
    }

    // Runs the items on 64 threads and compares each result with its reference; the first difference, with its thread and item.
    private static string? Violation()
    {
        CelProgram<CelValue>[] programs = new CelProgram<CelValue>[Texts.Length];
        for (int text = 0; text < Texts.Length; text++)
        {
            programs[text] = Environment.Compile<CelValue>(Texts[text]).GetProgramOrThrow();
        }

        CelBindings[][] bindings = Bind();
        Item[] items = Items();
        string[] references = new string[items.Length];
        for (int index = 0; index < items.Length; index++)
        {
            references[index] = Outcome(items[index], programs, bindings, cache: null);
        }

        var cache = new CelCompilationCache(Environment, CacheEntries, CacheEntries * 4096);
        return ThreadRun.OnThreads(Threads, (thread, barrier) => Run(thread, items, references, programs, bindings, cache, barrier));
    }

    private static string? Run(
        int thread,
        Item[] items,
        string[] references,
        CelProgram<CelValue>[] programs,
        CelBindings[][] bindings,
        CelCompilationCache cache,
        Barrier barrier)
    {
        int[] order = ThreadRun.Shuffled(items.Length, new PCG((uint)thread, Seed));
        barrier.SignalAndWait();
        foreach (int index in order)
        {
            Item item = items[index];
            try
            {
                string actual = Outcome(item, programs, bindings, cache);
                if (!string.Equals(actual, references[index], StringComparison.Ordinal))
                {
                    return $"thread {thread}, {item.Scenario} of {Texts[item.Text]} on binding {item.Binding}: {actual}, sequential {references[index]}";
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                return $"thread {thread}, {item.Scenario} of {Texts[item.Text]} on binding {item.Binding}: {exception.GetType()}: {exception.Message}";
            }
        }

        return null;
    }

    private static Item[] Items()
    {
        var items = new List<Item>();
        for (int text = 0; text < Texts.Length; text++)
        {
            for (int binding = 0; binding < BindingsPerText; binding++)
            {
                items.Add(new Item(Scenario.Program, text, binding));
                items.Add(new Item(Scenario.OneStep, text, binding));
                items.Add(new Item(Scenario.Cache, text, binding));
            }
        }

        return [.. items];
    }

    // Bindings of every text: moments on the limits and in the range, durations in the whole range of long, a zone of the pool.
    private static CelBindings[][] Bind()
    {
        var all = new CelBindings[Texts.Length][];
        for (int text = 0; text < Texts.Length; text++)
        {
            var random = new PCG((uint)text, Seed);
            all[text] = new CelBindings[BindingsPerText];
            for (int binding = 0; binding < BindingsPerText; binding++)
            {
                long seconds = binding < FixedMoments.Length
                    ? FixedMoments[binding]
                    : (long)(Next64(random) % 315_537_897_600UL) - 62_135_596_800L;
                int nanoseconds = (int)random.Next(1_000_000_000);
                long duration = binding % 4 == 0 ? (long)(random.Next(100_000) * 1_000_000_000UL) : unchecked((long)Next64(random));
                all[text][binding] = new CelBindings()
                    .Add("ts", new CelTimestamp(seconds, nanoseconds))
                    .Add("du", new CelDuration(duration))
                    .Add("z", Zones[random.Next((uint)Zones.Length)]);
            }
        }

        return all;
    }

    private static ulong Next64(PCG random) => ((ulong)random.Next() << 32) | random.Next();

    private static string Outcome(Item item, CelProgram<CelValue>[] programs, CelBindings[][] bindings, CelCompilationCache? cache)
    {
        string text = Texts[item.Text];
        CelBindings values = bindings[item.Text][item.Binding];
        CelEvalResult<CelValue> result = item.Scenario switch
        {
            Scenario.Cache when cache is not null => cache.Evaluate<CelValue>(text, values),
            Scenario.OneStep or Scenario.Cache => Environment.Evaluate<CelValue>(text, values),
            _ => programs[item.Text].Evaluate(values),
        };
        return result.IsSuccess ? Show(result.Value) : Errors(result.Errors);
    }

    private static string Show(CelValue value)
    {
        if (value.TryGetInt64(out long integer))
        {
            return string.Create(CultureInfo.InvariantCulture, $"int {integer}");
        }

        if (value.TryGetBoolean(out bool boolean))
        {
            return boolean ? "bool true" : "bool false";
        }

        if (value.TryGetString(out string? text))
        {
            return "string " + text;
        }

        return value.Kind.ToString();
    }

    private static string Errors(IEnumerable<CelError> errors) => ThreadRun.Errors(errors);

    private readonly record struct Item(Scenario Scenario, int Text, int Binding);
}
