using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Great.Parts.Cel;

// The registry of anonymous types: an immutable dictionary Type -> declarations and binding. Register replaces it under a lock
// with a copy that has one more entry, unless the type is there: the first registration wins. Reads take the current dictionary
// without a lock. The registry has no limit: its size is the number of anonymous types at the call sites of the application
// assemblies. Nothing here reads a type by reflection.
internal static class AnonymousRegistry
{
    private static readonly Lock WriteLock = new();

    private static ImmutableDictionary<Type, Entry> s_entries = ImmutableDictionary<Type, Entry>.Empty;

    internal static void Register<TData>(Action<CelEnvironmentBuilder> declare, Action<TData, CelBindings> bind)
        where TData : class
    {
        lock (WriteLock)
        {
            if (s_entries.ContainsKey(typeof(TData)))
            {
                return;
            }

            // The binder casts: the registry holds delegates of one shape, whatever the anonymous type.
            var entry = new Entry(declare, (data, bindings) => bind((TData)data, bindings), new ShapeKey(typeof(TData)));
            Volatile.Write(ref s_entries, s_entries.Add(typeof(TData), entry));
        }
    }

    internal static bool TryGet(Type type, [NotNullWhen(true)] out Entry? entry) => Volatile.Read(ref s_entries).TryGetValue(type, out entry);

    // The delegates of a registration and the key of level 1 of the cache for its shape.
    internal sealed class Entry(Action<CelEnvironmentBuilder> declare, Action<object, CelBindings> bind, ShapeKey key)
    {
        internal Action<CelEnvironmentBuilder> Declare { get; } = declare;

        internal Action<object, CelBindings> Bind { get; } = bind;

        internal ShapeKey Key { get; } = key;
    }
}
