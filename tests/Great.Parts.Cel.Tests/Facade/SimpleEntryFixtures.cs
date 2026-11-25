using System;

namespace Great.Parts.Cel.Tests.Facade;

// Entries of the cache and of the registry of the simple entry that the tests of the fast paths make without a call of the entry: an
// environment under the key of a type that only makes the key, and the entry of the registry of a type that the test registers.
internal static class SimpleEntryFixtures
{
    // An environment with the variable x, added to the cache under the key of the type.
    internal static SimpleEntryCache.EnvironmentEntry AddEnvironment(SimpleEntryCache cache, Type key)
    {
        CelEnvironment environment = new CelEnvironmentBuilder().AddVariable<long>("x").Build().GetEnvironmentOrThrow();
        return cache.AddEnvironment(new ShapeKey(key), environment);
    }

    // Registers a type and returns its entry of the registry; the example only fixes the type.
    internal static AnonymousRegistry.Entry Register<TData>(TData example, Action<CelEnvironmentBuilder> declare, Action<TData, CelBindings> bind)
        where TData : class
    {
        CelAnonymousData.Register(example, declare, bind);
        return AnonymousRegistry.TryGet(typeof(TData), out AnonymousRegistry.Entry? entry)
            ? entry
            : throw new InvalidOperationException("The type is not in the registry after its registration.");
    }
}
