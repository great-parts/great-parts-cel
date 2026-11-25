using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Great.Parts.Cel;

// The cache of the simple entry: two levels, both bounded, both evicting by the clock algorithm of CelCompilationCache at
// insertion. Level 1 keeps the hidden environments by the shape of the data, level 2 the programs by (environment, text, T).
// Evicting an environment evicts its programs, so no program outlives its environment. Reads take no lock; an insertion takes one
// lock, checks again, evicts and adds. No thread, timer or finalizer.
//
// Two fast paths stand before the levels and take no lock either. An entry of the registry of anonymous types keeps a reference to the
// environment of its type, so that a call with an anonymous object does not search level 1. An environment keeps a table of its recent
// programs, eight places chosen by the identity hash of the text: a call whose text is the same string instance as the one that stands
// in a place does not hash the text and does not compare strings. The entries of both fast paths are entries of the levels: an entry
// that leaves a level is marked evicted first (the mark is never cleared) and then removed from its place, a reader treats a marked
// entry as a miss, and a writer that stored an entry reads the mark after the store and takes the entry back if the mark stands. So an
// evicted entry is never handed out, and the capacities hold.
internal sealed class SimpleEntryCache
{
    internal const int MaxEnvironments = 64;
    internal const int MaxPrograms = 1_024;
    internal const int MaxTotalTextLength = 100_000;

    // The places of the table of the recent programs of an environment; a power of two, so that a hash is masked.
    internal const int RecentPrograms = 8;

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

    // The number of entries that a fast path of this cache would hand out although they are evicted: places of the tables of the
    // kept environments and references of the registry that name an environment of this cache. The tests call it when no call is
    // under way; it is 0 whenever the fast paths are right.
    internal int EvictedReachable()
    {
        int count = 0;
        lock (_lock)
        {
            for (int i = 0; i < _environmentRing.Count; i++)
            {
                foreach (ProgramEntry? place in _environmentRing[i].Recent)
                {
                    if (place is { Evicted: true })
                    {
                        count++;
                    }
                }
            }
        }

        foreach (AnonymousRegistry.Entry registered in AnonymousRegistry.Entries())
        {
            if (registered.Environment is { Evicted: true } environment && ReferenceEquals(environment.Owner, this))
            {
                count++;
            }
        }

        return count;
    }

    internal bool TryGetEnvironment(ShapeKey key, [NotNullWhen(true)] out EnvironmentEntry? entry)
    {
        if (_environments.TryGetValue(key, out entry))
        {
            entry.Touch();
            return true;
        }

        return false;
    }

    internal bool TryGetEnvironment(ShapeProbe probe, [NotNullWhen(true)] out EnvironmentEntry? entry)
    {
        if (_environmentsByProbe.TryGetValue(probe, out entry))
        {
            entry.Touch();
            return true;
        }

        return false;
    }

    // The environment of a registered anonymous type. The fast path is the reference that the entry of the registry keeps: it counts
    // when the environment belongs to this cache and is not evicted, and level 1 is not searched. Otherwise the environment is found
    // in level 1 or built and added, and the reference is set to it.
    internal EnvironmentEntry GetEnvironment(AnonymousRegistry.Entry registered)
    {
        EnvironmentEntry? environment = registered.Environment;
        if (environment is not null && !environment.Evicted && ReferenceEquals(environment.Owner, this))
        {
            environment.Touch();
            return environment;
        }

        if (!TryGetEnvironment(registered.Key, out environment))
        {
            var builder = new CelEnvironmentBuilder();
            registered.Declare(builder);
            environment = AddEnvironment(registered.Key, builder.Build().GetEnvironmentOrThrow());
        }

        registered.SetEnvironment(environment);
        return environment;
    }

    // Adds a built environment and returns the entry in the cache: another one when a thread added the same shape first.
    internal EnvironmentEntry AddEnvironment(ShapeKey key, CelEnvironment environment)
    {
        var entry = new EnvironmentEntry(this, key, environment);
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

    // The program of the text and T in the environment: from the table of the recent programs of the environment, from level 2, or
    // compiled and added. A failed compilation is kept as its errors. A text longer than the capacity of the texts is compiled and
    // not kept; so is a program of an evicted environment.
    internal CelCompileResult<T> GetOrCompile<T>(EnvironmentEntry environment, string expression)
    {
        ProgramEntry? recent = Volatile.Read(ref environment.Recent[PlaceOf(expression)]);
        if (recent is not null && !recent.Evicted && ReferenceEquals(recent.Key.Text, expression) && recent.Key.ResultType == typeof(T))
        {
            recent.Touch();
            return (CelCompileResult<T>)recent.Result;
        }

        return GetOrCompileByValue<T>(environment, expression);
    }

    // The miss of the table of recent programs: the program by the value of the text from level 2, or compiled and added, and then
    // the place of the table is written.
    private CelCompileResult<T> GetOrCompileByValue<T>(EnvironmentEntry environment, string expression)
    {
        var key = new ProgramKey(environment, expression, typeof(T));
        if (_programs.TryGetValue(key, out ProgramEntry? entry))
        {
            entry.Touch();
            PublishProgram(environment, entry);
            return (CelCompileResult<T>)entry.Result;
        }

        CelCompileResult<T> result = environment.Environment.Compile<T>(expression);
        if (expression.Length > _maxTotalTextLength)
        {
            return result;
        }

        ProgramEntry inserted = Insert(new ProgramEntry(key, result), out bool kept);
        if (kept)
        {
            PublishProgram(environment, inserted);
        }

        return (CelCompileResult<T>)inserted.Result;
    }

    // The program of level 2 for the value of the text and the result type, without touching the table of recent programs: the search
    // that a writer of the table has made before it stores a place.
    internal ProgramEntry? FindProgram(EnvironmentEntry environment, string expression, Type resultType) =>
        _programs.TryGetValue(new ProgramKey(environment, expression, resultType), out ProgramEntry? entry) ? entry : null;

    // The program that stands in the place of the table for a text, evicted or not; null when the place is empty.
    internal static ProgramEntry? RecentProgram(EnvironmentEntry environment, string expression) =>
        Volatile.Read(ref environment.Recent[PlaceOf(expression)]);

    // Stores a program in its place of the table of its environment with a full barrier, then reads the mark: when the program was
    // evicted meanwhile, the evicting thread may have cleared the place before this store, so the writer takes its entry back. The
    // entry is removed only if it is still the one stored here.
    internal static void PublishProgram(EnvironmentEntry environment, ProgramEntry entry)
    {
        int place = PlaceOf(entry.Key.Text);
        Interlocked.Exchange(ref environment.Recent[place], entry);
        if (entry.Evicted)
        {
            Interlocked.CompareExchange(ref environment.Recent[place], null, entry);
        }
    }

    private static int PlaceOf(string expression) => RuntimeHelpers.GetHashCode(expression) & (RecentPrograms - 1);

    // Under _lock: the evicted program leaves its place of the table, if it is still there.
    private static void UnplaceProgram(ProgramEntry victim) =>
        Interlocked.CompareExchange(ref victim.Key.Environment.Recent[PlaceOf(victim.Key.Text)], null, victim);

    private ProgramEntry Insert(ProgramEntry entry, out bool kept)
    {
        lock (_lock)
        {
            if (entry.Key.Environment.Evicted)
            {
                kept = false;
                return entry;
            }

            ProgramEntry present = _programs.GetOrAdd(entry.Key, entry);
            kept = true;
            if (!ReferenceEquals(present, entry))
            {
                return present;
            }

            _programRing.Add(entry);
            _totalTextLength += entry.Key.Text.Length;
            while (_programRing.Count > _maxPrograms || _totalTextLength > _maxTotalTextLength)
            {
                ProgramEntry victim = _programRing.EvictOne(entry);
                victim.MarkEvicted();
                UnplaceProgram(victim);
                _programs.TryRemove(victim.Key, out _);
                _totalTextLength -= victim.Key.Text.Length;
            }

            return entry;
        }
    }

    // Under _lock: removes one environment by the clock, never the one just added, with all its programs. The mark comes first, then
    // the reference of the registry and the places of the table are cleared.
    private void EvictEnvironment(EnvironmentEntry inserted)
    {
        EnvironmentEntry victim = _environmentRing.EvictOne(inserted);
        victim.MarkEvicted();
        if (victim.Key.AnonymousType is { } type && AnonymousRegistry.TryGet(type, out AnonymousRegistry.Entry? registered))
        {
            registered.ClearEnvironment(victim);
        }

        _environments.TryRemove(victim.Key, out _);
        for (int i = _programRing.Count - 1; i >= 0; i--)
        {
            ProgramEntry program = _programRing[i];
            if (ReferenceEquals(program.Key.Environment, victim))
            {
                program.MarkEvicted();
                UnplaceProgram(program);
                _programRing.RemoveAt(i);
                _programs.TryRemove(program.Key, out _);
                _totalTextLength -= program.Key.Text.Length;
            }
        }
    }

    // An entry of a ring: a mark set by a hit and cleared by the hand, and the mark of eviction, set once under the lock of the cache
    // before the entry is taken out of the places that a fast path reads, and never cleared.
    internal abstract class RingEntry
    {
        private volatile bool _referenced;
        private volatile bool _evicted;

        internal bool Referenced
        {
            get => _referenced;
            set => _referenced = value;
        }

        internal bool Evicted => _evicted;

        // A hit marks the entry only when the mark is clear: a hot entry is read by many threads, and a write to the line of memory that
        // they all read would cost each of them.
        internal void Touch()
        {
            if (!_referenced)
            {
                _referenced = true;
            }
        }

        internal void MarkEvicted() => _evicted = true;
    }

    internal sealed class EnvironmentEntry(SimpleEntryCache owner, ShapeKey key, CelEnvironment environment) : RingEntry
    {
        // The table of the recent programs: one place for each value of the identity hash of a text modulo RecentPrograms. A place holds
        // an entry of level 2 of this environment or null; it is written by one atomic store of a reference.
        internal readonly ProgramEntry?[] Recent = new ProgramEntry?[RecentPrograms];

        // The cache that holds the entry: the reference that the registry keeps counts only for the cache that it names.
        internal SimpleEntryCache Owner { get; } = owner;

        internal ShapeKey Key { get; } = key;

        internal CelEnvironment Environment { get; } = environment;
    }

    internal readonly record struct ProgramKey(EnvironmentEntry Environment, string Text, Type ResultType);

    private sealed class ProgramComparer : IEqualityComparer<ProgramKey>
    {
        internal static readonly ProgramComparer Instance = new();

        public bool Equals(ProgramKey x, ProgramKey y) =>
            ReferenceEquals(x.Environment, y.Environment) && x.ResultType == y.ResultType && string.Equals(x.Text, y.Text, StringComparison.Ordinal);

        public int GetHashCode(ProgramKey obj) =>
            HashCode.Combine(RuntimeHelpers.GetHashCode(obj.Environment), obj.ResultType, StringComparer.Ordinal.GetHashCode(obj.Text));
    }

    internal sealed class ProgramEntry(ProgramKey key, object result) : RingEntry
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
