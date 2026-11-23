using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// The environments and the counters of the examples of asynchronous evaluation: the variables x and y of int and l of list(int), the set
// optional, and the functions inc(int) -> int (the argument plus 1 after a delay of 1 to 5 ms), name(string) -> string, fail(int) ->
// int (throws InvalidOperationException), slow(int) -> int (waits for the cancellation of the token), incd(dyn) -> int and the method
// upper() of a string. The synchronous environment has the same names with synchronous twins. probe(int) -> int is a synchronous function
// that counts its calls: an expression that adds it to the call under test evaluates it in every pass, so its count is the number of
// passes.
internal sealed class AsyncFunctions
{
    private readonly ConcurrentDictionary<long, int> _calls = new();
    private int _incdCalls;
    private int _current;
    private int _highest;
    private int _started;
    private int _probes;

    internal AsyncFunctions()
    {
        Async = Common()
            .AddAsyncFunction("inc", (long value, CancellationToken token) => Inc(value, token))
            .AddAsyncFunction("name", (string text, CancellationToken token) => ValueTask.FromResult(text))
            .AddAsyncFunction("fail", (long value, CancellationToken token) => Fail(value))
            .AddAsyncFunction("slow", (long value, CancellationToken token) => Slow(value, token))
            .AddAsyncMethod("upper", (string text, CancellationToken token) => ValueTask.FromResult(text.ToUpperInvariant()))
            .AddFunction(new CelFunctionDecl("incd", CelOverloadDecl.Global("incd_dyn", [CelType.Dyn], CelType.Int)))
            .AddAsyncFunctionImplementation("incd_dyn", (arguments, token) => Incd())
            .Build()
            .GetEnvironmentOrThrow();
        Sync = Common()
            .AddFunction("inc", (long value) => value + 1)
            .AddFunction("name", (string text) => text)
            .AddFunction("fail", (long value) => FailNow(value))
            .AddFunction("slow", (long value) => value)
            .AddMethod("upper", (string text) => text.ToUpperInvariant())
            .AddFunction(new CelFunctionDecl("incd", CelOverloadDecl.Global("incd_dyn", [CelType.Dyn], CelType.Int)))
            .AddFunctionImplementation("incd_dyn", arguments => CelValue.FromInt64(2))
            .Build()
            .GetEnvironmentOrThrow();
    }

    // The environment with the asynchronous functions, and the one with their synchronous twins.
    internal CelEnvironment Async { get; }

    internal CelEnvironment Sync { get; }

    // Called at the start of every call of inc with its argument; the test uses it to cancel at a chosen call.
    internal Action<long>? OnStart { get; set; }

    // The number of calls of inc with this argument.
    internal int CallsOf(long argument) => _calls.GetValueOrDefault(argument);

    // The number of calls of inc with any argument.
    internal int Started => Volatile.Read(ref _started);

    // The number of calls of incd.
    internal int IncdCalls => Volatile.Read(ref _incdCalls);

    // The greatest number of calls of inc that were running at one time.
    internal int Highest => Volatile.Read(ref _highest);

    // The number of evaluations of probe: one for each pass that evaluated it.
    internal int Probes => Volatile.Read(ref _probes);

    // The bindings of the examples: x = 1, y = 2, l = [1, 2, 3].
    internal static CelBindings Bindings() => new CelBindings().Add("x", 1L).Add("y", 2L).Add("l", PartialTestSupport.IntList(1, 2, 3));

    private CelEnvironmentBuilder Common() => new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("x")
        .AddVariable<long>("y")
        .AddVariable("l", CelType.ListOf(CelType.Int))
        .AddExtension(CelExtensions.Optional(2))
        .AddFunction("probe", (long value) => Probe(value));

    private long Probe(long value)
    {
        _ = Interlocked.Increment(ref _probes);
        return value;
    }

    private async ValueTask<long> Inc(long value, CancellationToken token)
    {
        _ = _calls.AddOrUpdate(value, 1, static (_, count) => count + 1);
        _ = Interlocked.Increment(ref _started);
        RaiseHighest(Interlocked.Increment(ref _current));
        OnStart?.Invoke(value);
        try
        {
            // A delay of 1 to 5 ms that varies from call to call, so the calls complete in another order each time.
            await Task.Delay(1 + (Volatile.Read(ref _started) * 7 % 5), token).ConfigureAwait(false);
            return value + 1;
        }
        finally
        {
            _ = Interlocked.Decrement(ref _current);
        }
    }

    private void RaiseHighest(int running)
    {
        int seen = Volatile.Read(ref _highest);
        while (running > seen)
        {
            int previous = Interlocked.CompareExchange(ref _highest, running, seen);
            if (previous == seen)
            {
                return;
            }

            seen = previous;
        }
    }

    private static async ValueTask<long> Fail(long value)
    {
        await Task.Yield();
        return FailNow(value);
    }

    private static long FailNow(long value) => throw new InvalidOperationException("boom " + value);

    private static async ValueTask<long> Slow(long value, CancellationToken token)
    {
        await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
        return value;
    }

    // incd(dyn): two for any argument; counts its calls.
    private ValueTask<CelValue> Incd()
    {
        _ = Interlocked.Increment(ref _incdCalls);
        return ValueTask.FromResult(CelValue.FromInt64(2));
    }
}
