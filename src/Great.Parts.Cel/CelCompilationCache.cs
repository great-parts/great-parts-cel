using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel;

/// <summary>
/// A bounded, thread-safe cache of compilations in one environment, for applications that evaluate
/// expression text received at run time, such as rules stored in a database.
/// </summary>
/// <remarks>
/// <para>
/// The application creates the cache explicitly from an environment and chooses its capacity: the
/// environment itself caches no compilations. The key of an entry is the expression text, the result
/// type <c>T</c> and the values of the compilation options. Unsuccessful compilations are cached too, so
/// untrusted text with an error is not compiled again and again. Entries are evicted when a new entry is
/// added, so that both capacities hold; which entries are evicted is not specified. A text longer than the
/// capacity for text length is compiled but not kept. The cache creates no threads and no timers.
/// </para>
/// <para>
/// A result from the cache is the same as the result of the environment for the same text, type and
/// options: the cache changes neither results, nor errors, nor evaluation costs. All members can be
/// called from any number of threads at once.
/// </para>
/// </remarks>
public sealed class CelCompilationCache
{
    private readonly CelEnvironment _environment;
    private readonly int _maxEntryCount;
    private readonly int _maxTotalTextLength;

    // Lookups take no lock; insertions and evictions take _lock.
    private readonly ConcurrentDictionary<Key, Entry> _entries = new();
    private readonly Lock _lock = new();

    // The kept entries for the clock algorithm of eviction, and its hand; under _lock.
    private readonly List<Entry> _ring = [];
    private int _hand;
    private long _totalTextLength;

    /// <summary>
    /// Creates an empty cache for compilations in <paramref name="environment"/>.
    /// </summary>
    /// <remarks>
    /// A cache entry keeps the program of its text, which grows with the text. Up to 80,000 entries and a total
    /// text length of 900,000 keep a full cache within about 64 MiB; the page
    /// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/limits.md#compilation-cache">Limits</see>
    /// gives how the values were measured.
    /// </remarks>
    /// <param name="environment">The environment in which the cache compiles expressions.</param>
    /// <param name="maxEntryCount">The largest number of entries the cache keeps, 1 or more.</param>
    /// <param name="maxTotalTextLength">
    /// The largest total length of the expression texts the cache keeps, in UTF-16 code units, as
    /// <see cref="string.Length"/> counts them; 1 or more.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEntryCount"/> or <paramref name="maxTotalTextLength"/> is less than 1.</exception>
    public CelCompilationCache(CelEnvironment environment, int maxEntryCount, int maxTotalTextLength)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEntryCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTotalTextLength, 1);
        _environment = environment;
        _maxEntryCount = maxEntryCount;
        _maxTotalTextLength = maxTotalTextLength;
    }

    /// <summary>
    /// Returns the cached compilation of an expression, or compiles it as
    /// <see cref="CelEnvironment.Compile{T}"/> does and caches the result.
    /// </summary>
    /// <typeparam name="T">The .NET type in which the program returns its result.</typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="options">The compilation options, or <see langword="null"/> for none.</param>
    /// <returns>The program or the compilation errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="T"/> is not in the result column of
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// or the expected type of <paramref name="options"/> cannot be converted to <typeparamref name="T"/>.
    /// </exception>
    public CelCompileResult<T> GetOrCompile<T>(string expression, CelCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var key = new Key(expression, typeof(T), options?.ExpectedType, options?.SourceLocation);
        if (_entries.TryGetValue(key, out Entry? entry))
        {
            entry.Referenced = true;
            return (CelCompileResult<T>)entry.Result;
        }

        CelCompileResult<T> result = _environment.Compile<T>(expression, options);
        return expression.Length > _maxTotalTextLength ? result : (CelCompileResult<T>)Insert(new Entry(key, result)).Result;
    }

    /// <summary>
    /// Evaluates an expression on the bindings in one call through the cache: the same result and errors as
    /// <see cref="CelEnvironment.Evaluate{T}"/>, compiling the text only when the cache does not hold it.
    /// </summary>
    /// <remarks>
    /// The result holds either the compilation errors or the one evaluation error;
    /// <see cref="CelError.Stage"/> tells which step failed. The token is checked before the compilation
    /// and between the compilation and the evaluation, and during evaluation as
    /// <see cref="CelProgram{T}.Evaluate"/> describes.
    /// </remarks>
    /// <typeparam name="T">The .NET type in which the result is returned.</typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="bindings">The values of the variables.</param>
    /// <param name="compileOptions">The compilation options, or <see langword="null"/> for none.</param>
    /// <param name="evalOptions">The evaluation options, such as the cost limit, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The result converted to <typeparamref name="T"/>, or the errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> or <paramref name="bindings"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="T"/> is not in the result column of
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// or the expected type of <paramref name="compileOptions"/> cannot be converted to <typeparamref name="T"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public CelEvalResult<T> Evaluate<T>(
        string expression,
        CelBindings bindings,
        CelCompileOptions? compileOptions = null,
        CelEvalOptions? evalOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(bindings);
        cancellationToken.ThrowIfCancellationRequested();
        return CelEnvironment.EvaluateCompiled(GetOrCompile<T>(expression, compileOptions), bindings, evalOptions, cancellationToken);
    }

    // The number of kept entries and the sum of their text lengths, for the tests.
    internal (int Count, long TotalTextLength) Kept
    {
        get
        {
            lock (_lock)
            {
                return (_ring.Count, _totalTextLength);
            }
        }
    }

    // Inserts an entry and evicts others until both capacities hold; returns the entry in the cache, which is another
    // one when a thread inserted the same key first.
    private Entry Insert(Entry entry)
    {
        lock (_lock)
        {
            Entry present = _entries.GetOrAdd(entry.Key, entry);
            if (!ReferenceEquals(present, entry))
            {
                return present;
            }

            _ring.Add(entry);
            _totalTextLength += entry.Key.Text.Length;
            while (_ring.Count > _maxEntryCount || _totalTextLength > _maxTotalTextLength)
            {
                EvictOne(entry);
            }

            return entry;
        }
    }

    // The clock algorithm: the hand clears the mark of an entry read since the hand last passed it and removes the
    // first entry without a mark, never the new one. A hit only sets a mark, so the work per insertion is constant on
    // average.
    private void EvictOne(Entry inserted)
    {
        while (true)
        {
            if (_hand >= _ring.Count)
            {
                _hand = 0;
            }

            Entry candidate = _ring[_hand];
            if (ReferenceEquals(candidate, inserted) || candidate.Referenced)
            {
                candidate.Referenced = false;
                _hand++;
                continue;
            }

            int last = _ring.Count - 1;
            _ring[_hand] = _ring[last];
            _ring.RemoveAt(last);
            _entries.TryRemove(candidate.Key, out _);
            _totalTextLength -= candidate.Key.Text.Length;
            return;
        }
    }

    // The text by ordinal comparison, the result type, the expected type by CelType.Equals and the source location by
    // value; null options are options with both properties null.
    private readonly record struct Key(string Text, Type ResultType, CelType? ExpectedType, CelSourceLocation? SourceLocation);

    private sealed class Entry(Key key, object result)
    {
        // Set by a hit, cleared by the hand of the clock.
        private volatile bool _referenced;

        internal Key Key { get; } = key;

        // The CelCompileResult<T> of the key.
        internal object Result { get; } = result;

        internal bool Referenced
        {
            get => _referenced;
            set => _referenced = value;
        }
    }
}
