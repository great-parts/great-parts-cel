using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Examples.Facade;

// One environment and its programs are used from 64 threads at once, without synchronization, and every
// result equals the one of the same call made alone. The threads are started together by a barrier and run
// four kinds of calls on threads of their own, each over the same fixed texts and bindings in an order of their own: the evaluation
// of one program per text, Compile and the evaluation of its result, the one-step call, and one
// CelCompilationCache whose capacity of 4 entries makes it evict while the threads use it. Half of the
// evaluations have a cost limit. The environment has an application function, so its calls run in
// parallel too.
[Trait("Requirement", "R-4")]
public sealed class ConcurrentCallsTests
{
    private const int ThreadCount = 64;

    private static readonly string[] Texts =
    [
        "x + y * 2",
        "s + '-' + string(x)",
        "l.map(e, e * x)",
        "l.filter(e, e % 2 == 0).size()",
        "m.a + m.b",
        "has(m.c) ? m.c : -1",
        "twice(x) + twice(y)",
        "x / y",
        "l[y]",
        "size(s) > 3 && s.contains('b')",
        "[x, y, x + y] == [1, 2, 3]",
        "d + 1",
        "l.all(e, e > 0) || x > 100",
        "type(d) == int ? 'int' : 'other'",
        "{'k': x, 'v': s}.size()",
        "x +",
    ];

    private static readonly CelBindings[] Bindings =
    [
        Make(1, 2, "abc", 5L),
        Make(-7, 0, "", "text"),
        Make(40, 3, "bbbb", 1.5),
        Make(long.MaxValue, 1, "b", 2UL),
    ];

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("x")
        .AddVariable<long>("y")
        .AddVariable<string>("s")
        .AddVariable("l", CelType.ListOf(CelType.Int))
        .AddVariable("m", CelType.MapOf(CelType.String, CelType.Int))
        .AddVariable("d", CelType.Dyn)
        .AddFunction<long, long>("twice", value => checked(value * 2))
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public async Task ParallelCalls_EqualTheCallsMadeAlone()
    {
        CelCompilationCache cache = new(environment, 4, 1000);
        var programs = Texts.Select(text => environment.Compile<CelValue>(text)).ToArray();
        var expected = new Dictionary<(int Text, int Bindings, bool Limited), string>();
        for (int text = 0; text < Texts.Length; text++)
        {
            for (int bindings = 0; bindings < Bindings.Length; bindings++)
            {
                foreach (bool limited in new[] { false, true })
                {
                    expected[(text, bindings, limited)] = Describe(environment.Evaluate<CelValue>(Texts[text], Bindings[bindings], evalOptions: Options(limited), cancellationToken: CancellationToken.None));
                }
            }
        }

        var failures = new ConcurrentQueue<string>();
        using var barrier = new Barrier(ThreadCount);
        Task[] threads = Enumerable.Range(0, ThreadCount).Select(thread => Task.Factory.StartNew(
            () =>
            {
                barrier.SignalAndWait();
                for (int step = 0; step < Texts.Length * Bindings.Length * 2; step++)
                {
                    int item = (step * 7 + thread * 13) % (Texts.Length * Bindings.Length * 2);
                    (int text, int bindings, bool limited) = (item / (Bindings.Length * 2), item / 2 % Bindings.Length, item % 2 == 1);
                    CelEvalOptions options = Options(limited);
                    string want = expected[(text, bindings, limited)];
                    var results = new (string Kind, string Actual)[]
                    {
                        ("program", programs[text].IsSuccess ? Describe(programs[text].GetProgramOrThrow().Evaluate(Bindings[bindings], options, CancellationToken.None)) : Describe(programs[text])),
                        ("compile", Describe(environment.Compile<CelValue>(Texts[text]), Bindings[bindings], options)),
                        ("one step", Describe(environment.Evaluate<CelValue>(Texts[text], Bindings[bindings], evalOptions: options, cancellationToken: CancellationToken.None))),
                        ("cache", Describe(cache.Evaluate<CelValue>(Texts[text], Bindings[bindings], evalOptions: options, cancellationToken: CancellationToken.None))),
                    };
                    foreach ((string kind, string actual) in results.Where(result => result.Actual != want))
                    {
                        failures.Enqueue("thread " + thread.ToString(CultureInfo.InvariantCulture) + ", " + kind + ", '" + Texts[text] + "', bindings " + bindings.ToString(CultureInfo.InvariantCulture) + ": " + actual + " instead of " + want);
                    }
                }
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)).ToArray();

        // An exception of a thread fails the test with the thread and its call.
        await Task.WhenAll(threads).ConfigureAwait(true);

        Assert.Empty(failures);
    }

    private static CelEvalOptions Options(bool limited) => new() { CostLimit = limited ? 12UL : null };

    private static CelBindings Make(long x, long y, string s, object d) => new CelBindings()
        .Add("x", x)
        .Add("y", y)
        .Add("s", s)
        .Add("l", CelList.Create(CelValue.FromInt64(1), CelValue.FromInt64(2), CelValue.FromInt64(x % 5)))
        .Add("m", CelMap.Create(
        [
            new KeyValuePair<CelValue, CelValue>(CelValue.FromString("a"), CelValue.FromInt64(y)),
            new KeyValuePair<CelValue, CelValue>(CelValue.FromString("b"), CelValue.FromInt64(x % 3)),
        ]))
        .Add("d", d);

    private static string Describe(CelCompileResult<CelValue> compiled, CelBindings bindings, CelEvalOptions options) =>
        compiled.IsSuccess ? Describe(compiled.GetProgramOrThrow().Evaluate(bindings, options, CancellationToken.None)) : Describe(compiled);

    private static string Describe(CelCompileResult<CelValue> compiled) =>
        "error " + string.Join("; ", compiled.Errors.Select(error =>
            error.Code + " " + error.Message + " at " + error.Location.Offset.ToString(CultureInfo.InvariantCulture)));

    private static string Describe(CelEvalResult<CelValue> result) =>
        result.IsSuccess
            ? Print(result.GetValueOrThrow())
            : "error " + string.Join("; ", result.Errors.Select(error =>
                error.Code + " " + error.Message + " at " + error.Location.Offset.ToString(CultureInfo.InvariantCulture)));

    // A text of a value that tells kinds and contents apart: lists element by element, maps entry by entry
    // in their order, type values by name.
    private static string Print(CelValue value)
    {
        if (value.TryGetList(out CelList? list))
        {
            return "[" + string.Join(", ", list.Select(Print)) + "]";
        }

        if (value.TryGetMap(out CelMap? map))
        {
            return "{" + string.Join(", ", map.Select(entry => Print(entry.Key) + ": " + Print(entry.Value))) + "}";
        }

        if (value.TryGetType(out CelType? type))
        {
            return "type " + type.Name;
        }

        if (value.TryGetString(out string? text))
        {
            return "'" + text + "'";
        }

        if (value.TryGetDouble(out double number))
        {
            return number.ToString("R", CultureInfo.InvariantCulture);
        }

        if (value.TryGetInt64(out long integer))
        {
            return integer.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetUInt64(out ulong unsigned))
        {
            return unsigned.ToString(CultureInfo.InvariantCulture) + "u";
        }

        return value.TryGetBoolean(out bool flag) ? (flag ? "true" : "false") : value.Kind.ToString();
    }
}
