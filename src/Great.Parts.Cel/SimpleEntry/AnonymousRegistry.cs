using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Great.Parts.Cel;

// The registry of anonymous types: a dictionary Type -> declarations and binding that readers take without a lock and that is never
// changed once it is published. Register makes a copy that has one more entry, under a lock, and publishes the copy, unless the type is
// there: the first registration wins. A read is one lookup, O(1), whatever the number of types. The registry has no limit: its size is the
// number of anonymous types at the call sites of the application assemblies, and a registration comes only from a module initializer, so
// copying the dictionary costs once for each type at the start of the process. Nothing here reads a type by reflection.
internal static class AnonymousRegistry
{
    private static readonly Lock WriteLock = new();

    private static Dictionary<Type, Entry> s_entries = new();

    internal static void Register<TData>(Action<CelEnvironmentBuilder> declare, Action<TData, CelBindings> bind)
        where TData : class
    {
        lock (WriteLock)
        {
            Dictionary<Type, Entry> current = s_entries;
            if (current.ContainsKey(typeof(TData)))
            {
                return;
            }

            // The binder casts: the registry holds delegates of one shape, whatever the anonymous type.
            var entry = new Entry(declare, (data, bindings) => bind((TData)data, bindings), new ShapeKey(typeof(TData)));
            var copy = new Dictionary<Type, Entry>(current) { [typeof(TData)] = entry };
            Volatile.Write(ref s_entries, copy);
        }
    }

    internal static bool TryGet(Type type, [NotNullWhen(true)] out Entry? entry) => Volatile.Read(ref s_entries).TryGetValue(type, out entry);

    // The entries of the registry now; the dictionary that is read is never changed, so the enumeration needs no lock.
    internal static IEnumerable<Entry> Entries() => Volatile.Read(ref s_entries).Values;

    // The delegates of a registration, the key of level 1 of the cache for its shape and the reference to the current environment of the type.
    internal sealed class Entry(Action<CelEnvironmentBuilder> declare, Action<object, CelBindings> bind, ShapeKey key)
    {
        private SimpleEntryCache.EnvironmentEntry? _environment;

        internal Action<CelEnvironmentBuilder> Declare { get; } = declare;

        internal Action<object, CelBindings> Bind { get; } = bind;

        internal ShapeKey Key { get; } = key;

        // The environment of level 1 that a call used last, or null: one reference, written and read atomically.
        internal SimpleEntryCache.EnvironmentEntry? Environment => Volatile.Read(ref _environment);

        // Stores the reference with a full barrier and then reads the mark of eviction: an environment evicted meanwhile may have been
        // cleared by the evicting thread before this store, so the writer takes the reference back, if it is still its own.
        internal void SetEnvironment(SimpleEntryCache.EnvironmentEntry environment)
        {
            Interlocked.Exchange(ref _environment, environment);
            if (environment.Evicted)
            {
                ClearEnvironment(environment);
            }
        }

        // Clears the reference when it still names the environment.
        internal void ClearEnvironment(SimpleEntryCache.EnvironmentEntry environment) =>
            Interlocked.CompareExchange(ref _environment, null, environment);
    }
}
