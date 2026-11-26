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
//
// The entry of the type that the last successful read found stands in a field of its own, read before the dictionary: an application
// that calls the simple entry with one anonymous type again and again, the usual case, pays one reference comparison instead of a hash
// of the type and a search. An entry is never changed or removed once published, so the field cannot hold a stale one; a thread that
// reads another type writes its entry there, and a read of the first type goes to the dictionary again.
internal static class AnonymousRegistry
{
    private static readonly Lock WriteLock = new();

    private static Dictionary<Type, Entry> s_entries = new();

    private static Entry? s_last;

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

            var entry = new Entry<TData>(declare, bind, new ShapeKey(typeof(TData)));
            var copy = new Dictionary<Type, Entry>(current) { [typeof(TData)] = entry };
            Volatile.Write(ref s_entries, copy);
        }
    }

    internal static bool TryGet(Type type, [NotNullWhen(true)] out Entry? entry)
    {
        Entry? last = Volatile.Read(ref s_last);
        if (last is not null && ReferenceEquals(last.DataType, type))
        {
            entry = last;
            return true;
        }

        if (Volatile.Read(ref s_entries).TryGetValue(type, out entry))
        {
            Volatile.Write(ref s_last, entry);
            return true;
        }

        return false;
    }

    // The entries of the registry now; the dictionary that is read is never changed, so the enumeration needs no lock.
    internal static IEnumerable<Entry> Entries() => Volatile.Read(ref s_entries).Values;

    // The declaration of a registration, its binding, the key of level 1 of the cache for its shape and the reference to the current
    // environment of the type. The binding is a method of the entry of the type, so a call is one virtual call and one call of the
    // delegate of the registration, not two delegates one after the other.
    internal abstract class Entry
    {
        private SimpleEntryCache.EnvironmentEntry? _environment;

        private protected Entry(Type dataType, Action<CelEnvironmentBuilder> declare, ShapeKey key)
        {
            DataType = dataType;
            Declare = declare;
            Key = key;
        }

        // The anonymous type of the registration.
        internal Type DataType { get; }

        internal Action<CelEnvironmentBuilder> Declare { get; }

        internal ShapeKey Key { get; }

        // Binds the values of the properties of data, which is an instance of the type of this entry.
        internal abstract void Bind(object data, CelBindings bindings);

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

    // The entry of one anonymous type: the binding casts the data once, to the type that the registry found the entry by.
    private sealed class Entry<TData>(Action<CelEnvironmentBuilder> declare, Action<TData, CelBindings> bind, ShapeKey key)
        : Entry(typeof(TData), declare, key)
        where TData : class
    {
        internal override void Bind(object data, CelBindings bindings) => bind((TData)data, bindings);
    }
}
