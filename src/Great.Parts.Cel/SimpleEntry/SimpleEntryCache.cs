using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Great.Parts.Cel;

// The cache of the simple entry: two levels, both bounded, both evicting by the clock algorithm of CelCompilationCache at
// insertion. Level 1 keeps the hidden environments by the shape of the data, level 2 the programs by (environment, text, T).
// Evicting an environment evicts its programs, so no program outlives its environment. Reads take no lock; an insertion takes one
// lock, checks again, evicts and adds. No thread, timer or finalizer.
internal sealed class SimpleEntryCache
{
    internal const int MaxEnvironments = 64;
    internal const int MaxPrograms = 1_024;
    internal const int MaxTotalTextLength = 100_000;

    // The cache of the process; the tests make their own with small capacities.
    internal static readonly SimpleEntryCache Shared = new();

    private readonly int _maxEnvironments;
    private readonly int _maxPrograms;
    private readonly int _maxTotalTextLength;

    private readonly ConcurrentDictionary<ShapeKey, EnvironmentEntry> _environments = new(ShapeComparer.Instance);
    private readonly ConcurrentDictionary<ShapeKey, EnvironmentEntry>.AlternateLookup<ShapeProbe> _environmentsByProbe;
    private readonly ConcurrentDictionary<ProgramKey, ProgramEntry> _programs = new(ProgramComparer.Instance);
    private readonly Lock _lock = new();

    // Under _lock.
    private readonly Clock<EnvironmentEntry> _environmentRing = new();
    private readonly Clock<ProgramEntry> _programRing = new();
    private long _totalTextLength;

    internal SimpleEntryCache()
        : this(MaxEnvironments, MaxPrograms, MaxTotalTextLength)
    {
    }

    internal SimpleEntryCache(int maxEnvironments, int maxPrograms, int maxTotalTextLength)
    {
        _maxEnvironments = maxEnvironments;
        _maxPrograms = maxPrograms;
        _maxTotalTextLength = maxTotalTextLength;
        _environmentsByProbe = _environments.GetAlternateLookup<ShapeProbe>();
    }

    // The numbers that the tests check: environments and programs kept, and the sum of the lengths of the kept texts.
    internal (int Environments, int Programs, long TotalTextLength) Kept
    {
        get
        {
            lock (_lock)
            {
                return (_environmentRing.Count, _programRing.Count, _totalTextLength);
            }
        }
    }

    internal bool TryGetEnvironment(ShapeKey key, [NotNullWhen(true)] out EnvironmentEntry? entry)
    {
        if (_environments.TryGetValue(key, out entry))
        {
            entry.Referenced = true;
            return true;
        }

        return false;
    }

    internal bool TryGetEnvironment(ShapeProbe probe, [NotNullWhen(true)] out EnvironmentEntry? entry)
    {
        if (_environmentsByProbe.TryGetValue(probe, out entry))
        {
            entry.Referenced = true;
            return true;
        }

        return false;
    }

    // Adds a built environment and returns the entry in the cache: another one when a thread added the same shape first.
    internal EnvironmentEntry AddEnvironment(ShapeKey key, CelEnvironment environment)
    {
        var entry = new EnvironmentEntry(key, environment);
        lock (_lock)
        {
            EnvironmentEntry present = _environments.GetOrAdd(key, entry);
            if (!ReferenceEquals(present, entry))
            {
                return present;
            }

            _environmentRing.Add(entry);
            while (_environmentRing.Count > _maxEnvironments)
            {
                EvictEnvironment(entry);
            }

            return entry;
        }
    }

    // The program of the text and T in the environment: from level 2, or compiled and added. A failed compilation is kept as its
    // errors. A text longer than the capacity of the texts is compiled and not kept; so is a program of an evicted environment.
    internal CelCompileResult<T> GetOrCompile<T>(EnvironmentEntry environment, string expression)
    {
        var key = new ProgramKey(environment, expression, typeof(T));
        if (_programs.TryGetValue(key, out ProgramEntry? entry))
        {
            entry.Referenced = true;
            return (CelCompileResult<T>)entry.Result;
        }

        CelCompileResult<T> result = environment.Environment.Compile<T>(expression);
        return expression.Length > _maxTotalTextLength ? result : (CelCompileResult<T>)Insert(new ProgramEntry(key, result)).Result;
    }

    private ProgramEntry Insert(ProgramEntry entry)
    {
        lock (_lock)
        {
            if (entry.Key.Environment.Evicted)
            {
                return entry;
            }

            ProgramEntry present = _programs.GetOrAdd(entry.Key, entry);
            if (!ReferenceEquals(present, entry))
            {
                return present;
            }

            _programRing.Add(entry);
            _totalTextLength += entry.Key.Text.Length;
            while (_programRing.Count > _maxPrograms || _totalTextLength > _maxTotalTextLength)
            {
                ProgramEntry victim = _programRing.EvictOne(entry);
                _programs.TryRemove(victim.Key, out _);
                _totalTextLength -= victim.Key.Text.Length;
            }

            return entry;
        }
    }

    // Under _lock: removes one environment by the clock, never the one just added, with all its programs.
    private void EvictEnvironment(EnvironmentEntry inserted)
    {
        EnvironmentEntry victim = _environmentRing.EvictOne(inserted);
        victim.Evicted = true;
        _environments.TryRemove(victim.Key, out _);
        for (int i = _programRing.Count - 1; i >= 0; i--)
        {
            ProgramEntry program = _programRing[i];
            if (ReferenceEquals(program.Key.Environment, victim))
            {
                _programRing.RemoveAt(i);
                _programs.TryRemove(program.Key, out _);
                _totalTextLength -= program.Key.Text.Length;
            }
        }
    }

    // An entry of a ring: a mark set by a hit and cleared by the hand.
    internal abstract class RingEntry
    {
        private volatile bool _referenced;

        internal bool Referenced
        {
            get => _referenced;
            set => _referenced = value;
        }
    }

    internal sealed class EnvironmentEntry(ShapeKey key, CelEnvironment environment) : RingEntry
    {
        private volatile bool _evicted;

        internal ShapeKey Key { get; } = key;

        internal CelEnvironment Environment { get; } = environment;

        // Set under the lock when the environment leaves level 1; a program of it is then not kept.
        internal bool Evicted
        {
            get => _evicted;
            set => _evicted = value;
        }
    }

    internal readonly record struct ProgramKey(EnvironmentEntry Environment, string Text, Type ResultType);

    private sealed class ProgramComparer : IEqualityComparer<ProgramKey>
    {
        internal static readonly ProgramComparer Instance = new();

        public bool Equals(ProgramKey x, ProgramKey y) =>
            ReferenceEquals(x.Environment, y.Environment) && x.ResultType == y.ResultType && string.Equals(x.Text, y.Text, StringComparison.Ordinal);

        public int GetHashCode(ProgramKey obj) =>
            HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Environment), obj.ResultType, StringComparer.Ordinal.GetHashCode(obj.Text));
    }

    private sealed class ProgramEntry(ProgramKey key, object result) : RingEntry
    {
        internal ProgramKey Key { get; } = key;

        // The CelCompileResult<T> of the key.
        internal object Result { get; } = result;
    }

    // The entries of one level for the clock algorithm: the hand clears the mark of an entry read since it last passed and removes the
    // first entry without a mark, never the new one. A hit only sets a mark, so the work of an insertion is constant on average.
    private sealed class Clock<T>
        where T : RingEntry
    {
        private readonly List<T> _items = [];
        private int _hand;

        internal int Count => _items.Count;

        internal T this[int index] => _items[index];

        internal void Add(T entry)
        {
            _items.Add(entry);
        }

        // Removes the entry at a place by moving the last entry into it.
        internal void RemoveAt(int index)
        {
            int last = _items.Count - 1;
            T moved = _items[last];
            _items[index] = moved;
            _items.RemoveAt(last);
            if (_hand >= _items.Count)
            {
                _hand = 0;
            }
        }

        internal T EvictOne(T inserted)
        {
            while (true)
            {
                if (_hand >= _items.Count)
                {
                    _hand = 0;
                }

                T candidate = _items[_hand];
                if (ReferenceEquals(candidate, inserted) || candidate.Referenced)
                {
                    candidate.Referenced = false;
                    _hand++;
                    continue;
                }

                RemoveAt(_hand);
                return candidate;
            }
        }
    }
}
