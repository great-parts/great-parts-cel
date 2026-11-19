# Simple entry: `CelExpression`, the registry of anonymous data and the hidden cache

`CelExpression.Evaluate<T>` evaluates a text on a dictionary or an anonymous object in one call, without an environment in the code of the application. The values it reads are the closed list of `docs/type-mapping.md` ("Data of the simple entry"); its numbers are in `docs/limits.md` ("Simple entry"). This page is the inside: the shape of the data, the hidden environments and programs, the registry, threads.

| File | Content | Core |
| :- | :- | :- |
| `src/Great.Parts.Cel/CelExpression.cs` | the two public methods: argument checks, the call of the entry, the exception | no |
| `src/Great.Parts.Cel/CelAnonymousData.cs` | the public `Register<TData>`; it calls the registry | no |
| `src/Great.Parts.Cel/SimpleEntry/SimpleEntryCall.cs` | one call: the shape, the two levels of the cache, the bindings, the evaluation | no |
| `src/Great.Parts.Cel/SimpleEntry/AnonymousRegistry.cs` | the registry: `Type` → declarations and binder | no |
| `src/Great.Parts.Cel/SimpleEntry/DataShape.cs` | the shape of the data: the key of the first level | no |
| `src/Great.Parts.Cel/SimpleEntry/SimpleEntryCache.cs` | the two levels, the clock, the lock | no |
| `src/Great.Parts.Cel/SimpleEntry/SimpleValues.cs` | the closed list of `is` checks: a value to its CEL type and its binding | yes: a conversion of values |

The files are in the directory `SimpleEntry/` and in the namespace `Great.Parts.Cel`, all internal. A namespace of their own would reference `Great.Parts.Cel` (the builder, the environment, `CelException`) and be referenced by it (`CelExpression`, `CelAnonymousData`), a cycle that `ARCH001` refuses; so `eng/layers.txt` has no row for them.

## One call

1. `expression` or `data` `null` — `ArgumentNullException`; the token is checked.
2. The shape. A dictionary (the dictionary overload, or `data` that is one of the dictionaries of the list) gives the shape "dictionary": the sorted list of pairs (name, CEL type of the value) by `string.CompareOrdinal`, the type from `SimpleValues` (for `IReadOnlyDictionary<string, X>` with `X` not `object` the one type of `X`). Anything else is looked up by `data.GetType()` in the registry: found, the shape is "anonymous type" with that `Type`; not found, the call fails with `TypeNotDeclared` (4008) for `data`. A value outside the list fails with 4008 for its key before any environment is built; every key with such a value gives its error, in the order of the names (ordinal), and the exception carries them all.
3. The environment of the shape from level 1, or built and inserted: `new CelEnvironmentBuilder()` with the variables of the shape (a dictionary: `AddVariable(name, type)`; an anonymous type: its `declare` delegate); `SetDefaultCostLimit` is not used — the limit is the one of the call (step 5). A build error (4016 for a key that is not a name; never 4000, since names are distinct) is thrown, and the shape is not cached.
4. The program of (environment, text, `T`) from level 2, or compiled and inserted; a failed compilation is cached as its errors.
5. The bindings: a dictionary is bound value by value through `SimpleValues` (no second type check: the shape came from the same values); an anonymous object through its `bind` delegate. The program is evaluated with the limit of the call, `CelExpression.DefaultCostLimit` or the `costLimit` argument.
6. Success gives the value; a compilation or evaluation error, `CelException.FromErrors`; `OperationCanceledException` passes.

The shape of a dictionary is computed from its values on every call: it is the key, so it has to be. The list of pairs is kept in a pooled buffer and compared with the keys of level 1 without allocating a key when the environment is found; a new key is allocated only on insertion.

## The registry

`AnonymousRegistry` holds an immutable dictionary `Type → Entry` (`Entry`: the `declare` and `bind` delegates, with `bind` stored as `Action<object, CelBindings>` that casts to `TData`). `Register<TData>` replaces the dictionary under a lock with a copy that has one more entry, unless the type is there: the first registration wins. Reads take the current dictionary without a lock. The registry has no limit: its size is the number of anonymous types at call sites of the application assemblies, which the build of the application fixes, and not the data at run time.

The name of an unregistered anonymous type in the error is `Type.FullName`, as the compiler gave it (``<>f__AnonymousType0`2[…]``), and the hint of 4008 tells the application to pass the anonymous object to `CelExpression.Evaluate` directly or to pass a dictionary. No member of the type is read to name it.

## The cache

Two levels, both bounded, both with the clock algorithm of `CelCompilationCache` at insertion (a reference bit per slot, a hand that clears bits and evicts the first slot whose bit is clear):

| Level | Key | Capacity | Value |
| :- | :- | :- | :- |
| 1 | the shape | 64 environments | the hidden environment and the slot range of its programs |
| 2 | (environment, text by ordinal, `T`) | 1 024 programs and 100 000 UTF-16 code units of text | the `CelCompileResult<T>` as `object` |

Evicting an environment evicts its programs, so no program outlives its environment and at most 64 hidden environments are alive. The environment of an anonymous type is built once while its shape is in level 1; after an eviction (more than 64 shapes in turn) it is built again from the registry, which keeps the delegates and not the environment. A text longer than 100 000 code units is compiled and not kept. The two levels are `ConcurrentDictionary` maps, as in `CelCompilationCache`: reads take no lock (level 1 finds the environment of a dictionary through an alternate lookup by the pooled list of pairs, without a key); an insertion takes one lock, re-checks, evicts and adds. Two threads that miss the same key may both compile; the second insertion keeps the first entry. No thread, timer or finalizer is used.

The cache is a static field of `SimpleEntryCache`; nothing is public. Its calibration fills both levels to capacity — 64 shapes of 16 variables and 1 024 programs, whichever limit of level 2 comes first — and measures the live heap before and after — the size of the heap less its fragmentation that `GC.GetGCMemoryInfo` reports after a full compacting collection — in a test collection that does not run in parallel with others. The calibration of version 2.0 read `GC.GetTotalMemory(true)`, as the calibration of the compilation cache did; the test does not, since on the garbage collector with segments, which `net10.0` has on macOS, it reported 8.1 to 8.2 MiB for a cache that the live heap measures at 3.6 MiB. The measurement of version 2.0 was 3.70 MiB against the limit of 8 MiB of `docs/limits.md`, so the capacities stay.

## The type rules

`SimpleValues` is the only code that turns a value of the simple entry into a CEL value. It is one method with a `switch` over `is` patterns in the order of the list; the rows of the lists and the dictionaries are `ConversionRules.FromRuntimeValue`, the same function that the indexer of `CelBindings` uses ([facade-2.0](facade-2.0.md)), so the two have one rule and not two copies. A list of `object` passes only for `IList<object>` and `object[]` of exactly that type; an element of such a list is read later through the `object` row, so an object of the application there is `TypeNotAccessible` (3012) at the read in the simple entry, where no type source exists. No reflection, `MakeGenericType` or `Activator` (`ARCH007`).

## Tests

The examples (`Examples/Facade/SimpleEntryTests.cs`) cover the calls through a dictionary, through a registration made in the test, the errors and the limit. The developer tests take the rest: the capacities and the eviction of both levels, the 64-thread test, `SimpleValues` row by row on the values the examples do not use, the reserve test of the built-in limit (100 times the cost of each scenario of the benchmark set) and the memory calibration.

Related pages: [generator-call-site](generator-call-site.md), [facade-2.0](facade-2.0.md), [facade](facade.md).
