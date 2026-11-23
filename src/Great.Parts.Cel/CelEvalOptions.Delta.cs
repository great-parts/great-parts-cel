using System;

namespace Great.Parts.Cel;

public sealed partial class CelEvalOptions
{
    /// <summary>
    /// The default of <see cref="MaxConcurrentAsyncCalls"/>: 100, as the default of cel-go.
    /// </summary>
    public const int DefaultMaxConcurrentAsyncCalls = 100;

    private readonly int _maxConcurrentAsyncCalls = DefaultMaxConcurrentAsyncCalls;

    /// <summary>
    /// The most asynchronous calls of one evaluation that are started and not yet awaited at one time; by default
    /// <see cref="DefaultMaxConcurrentAsyncCalls"/>.
    /// </summary>
    /// <remarks>
    /// An asynchronous evaluation starts the calls of a pass in the order the pass met them, keeps at most this many in flight,
    /// awaits them in the order they started and starts the next one as one completes. The limit does not change the result,
    /// the cost or the number of passes; it bounds the load that one evaluation puts on the services the functions call, for
    /// example an asynchronous function in <c>items.map(i, price(i))</c> over a long list. Synchronous evaluation ignores it.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int MaxConcurrentAsyncCalls
    {
        get => _maxConcurrentAsyncCalls;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maxConcurrentAsyncCalls = value;
        }
    }
}
