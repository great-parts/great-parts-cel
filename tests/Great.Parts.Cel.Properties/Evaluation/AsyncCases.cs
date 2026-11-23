using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CsCheck;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Properties.Evaluation;

/// <summary>An integer expression of the properties of the asynchronous driver.</summary>
/// <param name="Text">The text of the expression.</param>
/// <param name="HasCalls">Whether the text calls <c>inc</c> or <c>fail</c>.</param>
internal sealed record AsyncCase(string Text, bool HasCalls)
{
    public override string ToString() => Text;
}

/// <summary>The kinds of expression of <see cref="AsyncExpressions"/>: which functions and which operations may fail.</summary>
internal enum AsyncMode
{
    /// <summary>The functions <c>inc</c> and operations that cannot fail.</summary>
    Values,

    /// <summary>With one call of <c>fail</c> at most: the only error is the exception of a function.</summary>
    FailingFunctions,

    /// <summary>With one division by a divisor that is zero at most: the only error is that of an operation.</summary>
    FailingOperations,

    /// <summary>Any number of both sources of errors.</summary>
    Mixed,

    /// <summary>No function: a program that completes in its first pass.</summary>
    WithoutCalls,
}

/// <summary>
/// The functions of the properties of the asynchronous driver and their two environments. <c>inc(int) -&gt; int</c> adds one and
/// <c>fail(int) -&gt; int</c> throws <see cref="InvalidOperationException"/>; the environment <see cref="Async"/> declares them
/// asynchronous with delays that change from call to call, so the calls complete in another order each time, and
/// <see cref="Sync"/> declares their synchronous twins. <c>takes(list(int)) -&gt; int</c> is asynchronous with a typed argument and
/// counts the elements. The variables are <c>x</c> and <c>y</c> of int and <c>l</c>, <c>l1</c>, <c>l2</c>, <c>l3</c> of list(int).
/// The counters of the calls are those of <see cref="Async"/>.
/// </summary>
internal sealed class AsyncCalls
{
    private readonly ConcurrentDictionary<(string Function, long Argument), int> _calls = new();
    private int _sequence;
    private int _running;
    private int _started;
    private int _startedAfterCancel;

    internal AsyncCalls()
    {
        Async = Common()
            .AddAsyncFunction("inc", (long value, CancellationToken token) => Inc(value, token))
            .AddAsyncFunction("fail", (long value, CancellationToken token) => Fail(value, token))
            .AddAsyncFunction("takes", (IReadOnlyList<int> list, CancellationToken token) => Takes(list, token))
            .Build()
            .GetEnvironmentOrThrow();
        Sync = Common()
            .AddFunction("inc", (long value) => value + 1)
            .AddFunction("fail", (long value) => FailNow(value))
            .AddFunction("takes", (IReadOnlyList<int> list) => (long)list.Count)
            .Build()
            .GetEnvironmentOrThrow();
    }

    internal CelEnvironment Async { get; }

    internal CelEnvironment Sync { get; }

    /// <summary>Called at the start of every call of <c>inc</c> with its argument.</summary>
    internal Action<long>? OnStart { get; set; }

    /// <summary>The calls of this evaluation by function and argument, with their counts.</summary>
    internal IReadOnlyDictionary<(string Function, long Argument), int> Calls => _calls;

    /// <summary>The number of calls of <c>inc</c> that started.</summary>
    internal int Started => Volatile.Read(ref _started);

    /// <summary>The number of calls that are running now.</summary>
    internal int Running => Volatile.Read(ref _running);

    /// <summary>The number of calls that started with a canceled token.</summary>
    internal int StartedAfterCancel => Volatile.Read(ref _startedAfterCancel);

    /// <summary>Clears the counters; the delays go on from where they were, so the order of completion differs from run to run.</summary>
    internal void Reset()
    {
        _calls.Clear();
        _ = Interlocked.Exchange(ref _started, 0);
        _ = Interlocked.Exchange(ref _startedAfterCancel, 0);
    }

    /// <summary>The bindings of the properties: <c>x</c> is 2, <c>y</c> is 5, and <c>l</c> is the list 1, 2, 3.</summary>
    /// <returns>The bindings.</returns>
    internal static CelBindings Bindings() => new CelBindings()
        .Add("x", 2L)
        .Add("y", 5L)
        .Add("l", Ints(3, 0));

    /// <summary>A list of <paramref name="count"/> integers, 1 to count, with the last one replaced by <paramref name="last"/> when it is not 0.</summary>
    /// <param name="count">The number of elements.</param>
    /// <param name="last">The last element, or 0 to keep it.</param>
    /// <returns>The list.</returns>
    internal static CelValue Ints(int count, long last)
    {
        var items = new CelValue[count];
        for (int index = 0; index < count; index++)
        {
            items[index] = CelValue.FromInt64(last != 0 && index == count - 1 ? last : index + 1);
        }

        return CelValue.FromList(CelList.Create(items));
    }

    private static CelEnvironmentBuilder Common() => new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("x")
        .AddVariable<long>("y")
        .AddVariable("l", CelType.ListOf(CelType.Int))
        .AddVariable("l1", CelType.ListOf(CelType.Int))
        .AddVariable("l2", CelType.ListOf(CelType.Int))
        .AddVariable("l3", CelType.ListOf(CelType.Int));

    private async ValueTask<long> Inc(long value, CancellationToken token)
    {
        Enter("inc", value, token);
        _ = Interlocked.Increment(ref _started);
        OnStart?.Invoke(value);
        try
        {
            await Delay(token).ConfigureAwait(false);
            return value + 1;
        }
        finally
        {
            _ = Interlocked.Decrement(ref _running);
        }
    }

    private async ValueTask<long> Fail(long value, CancellationToken token)
    {
        Enter("fail", value, token);
        try
        {
            await Delay(token).ConfigureAwait(false);
            return FailNow(value);
        }
        finally
        {
            _ = Interlocked.Decrement(ref _running);
        }
    }

    private async ValueTask<long> Takes(IReadOnlyList<int> list, CancellationToken token)
    {
        Enter("takes", list.Count == 0 ? 0 : list[^1], token);
        try
        {
            await Delay(token).ConfigureAwait(false);
            return list.Count;
        }
        finally
        {
            _ = Interlocked.Decrement(ref _running);
        }
    }

    private void Enter(string function, long argument, CancellationToken token)
    {
        _ = _calls.AddOrUpdate((function, argument), 1, static (_, count) => count + 1);
        _ = Interlocked.Increment(ref _running);
        if (token.IsCancellationRequested)
        {
            _ = Interlocked.Increment(ref _startedAfterCancel);
        }
    }

    // A delay of 0 to 3 ms that varies from call to call.
    private Task Delay(CancellationToken token)
    {
        int milliseconds = Interlocked.Increment(ref _sequence) * 5 % 4;
        return milliseconds == 0 ? Task.CompletedTask : Task.Delay(milliseconds, token);
    }

    private static long FailNow(long value) => throw new InvalidOperationException("boom " + value.ToString(CultureInfo.InvariantCulture));
}

/// <summary>
/// Generates integer expressions over <c>x</c>, <c>y</c>, the list <c>l</c> of three elements and the functions <c>inc</c> and
/// <c>fail</c>: literals, calls, sums, differences, products, divisions, conditionals and a <c>map</c> over <c>l</c> that is indexed.
/// </summary>
internal static class AsyncExpressions
{
    private const int MaxDepth = 4;

    /// <summary>Expressions of a mode.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The generator.</returns>
    internal static Gen<AsyncCase> Of(AsyncMode mode) =>
        Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => Create(new PCG(stream, seed), mode));

    private static AsyncCase Create(PCG random, AsyncMode mode)
    {
        string text = new Writer(random, mode).Int(0, inLoop: false);
        return new AsyncCase(text, text.Contains("inc(", StringComparison.Ordinal) || text.Contains("fail(", StringComparison.Ordinal));
    }

    // A mode with one source of errors has one node that may fail, so the error of the expression is the error of that node whatever
    // the order in which the calls complete; with two, an error to the right of a call that waits takes precedence over the one that
    // call would give.
    private sealed class Writer(PCG random, AsyncMode mode)
    {
        private int _failingNodes = mode is AsyncMode.FailingFunctions or AsyncMode.FailingOperations ? 1 : int.MaxValue;

        private bool Calls => mode != AsyncMode.WithoutCalls;

        private bool Fails => mode is AsyncMode.FailingFunctions or AsyncMode.Mixed && _failingNodes > 0;

        private bool Divides => mode is AsyncMode.FailingOperations or AsyncMode.Mixed && _failingNodes > 0;

        internal string Int(int depth, bool inLoop)
        {
            if (depth >= MaxDepth || random.Next(4) == 0)
            {
                return Leaf(inLoop);
            }

            int next = depth + 1;
            switch (random.Next(9))
            {
                case 0 or 1 when Calls:
                    return $"inc({Int(next, inLoop)})";
                case 2 when Fails:
                    _failingNodes--;
                    return $"fail({Int(next, inLoop)})";
                case 3:
                    return $"({Int(next, inLoop)} + {Int(next, inLoop)})";
                case 4:
                    return $"({Int(next, inLoop)} * {Int(next, inLoop)})";
                case 5:
                    return $"({Bool(next, inLoop)} ? {Int(next, inLoop)} : {Int(next, inLoop)})";
                case 6 when Divides:
                    _failingNodes--;
                    return $"({Int(next, inLoop)} / {Zero(inLoop)})";
                case 6:
                    return $"({Int(next, inLoop)} / 2)";
                case 7 when !inLoop:
                    return $"l.map(i, {Int(next, inLoop: true)})[{random.Next(3)}]";
                default:
                    return $"({Int(next, inLoop)} - {Int(next, inLoop)})";
            }
        }

        // A divisor that is zero with the bindings of the properties: x is 2, y is 5.
        private string Zero(bool inLoop) => random.Next(inLoop ? 4u : 3u) switch
        {
            0 => "0",
            1 => "(x - 2)",
            2 => "(y - 5)",
            _ => "(i - i)",
        };

        private string Leaf(bool inLoop) => random.Next(inLoop ? 4u : 3u) switch
        {
            0 => random.Next(6).ToString(CultureInfo.InvariantCulture),
            1 => "x",
            2 => "y",
            _ => "i",
        };

        private string Bool(int depth, bool inLoop)
        {
            if (depth >= MaxDepth)
            {
                return random.Next(2) == 0 ? "true" : "false";
            }

            int next = depth + 1;
            return random.Next(5) switch
            {
                0 => $"({Int(next, inLoop)} < {Int(next, inLoop)})",
                1 => $"({Int(next, inLoop)} == {Int(next, inLoop)})",
                2 => $"({Bool(next, inLoop)} && {Bool(next, inLoop)})",
                3 => $"({Bool(next, inLoop)} || {Bool(next, inLoop)})",
                _ => random.Next(2) == 0 ? "true" : "false",
            };
        }
    }
}
