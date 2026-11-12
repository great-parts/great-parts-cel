# Extension sets: the model, the catalog and the build checks

An extension set is a named, versioned group of declarations that an environment adds as a whole: overloads with their implementations and costs, macros, abstract types and, for the internal sets of the library, variables. This page describes the model, the catalog of the sets that the library implements, the checks that `Build` makes over the sets of an environment, and the sets `bindings` and `sets`. The other sets have their pages: [macros of the sets](extension-macros.md), [strings](extension-strings.md) and [format](extension-format.md), [math, lists and encoders](extension-library.md), [network](extension-network.md). The public side — `CelExtensions`, `CelExtension`, `CelEnvironmentBuilder.AddExtension` — is in the facade; the sets, their versions and their functions are on the `docs/extensions.md` page, the costs on [cost](../cost.md), the codes on [error codes](../error-codes.md).

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Declarations/ExtensionDefinition.cs` | the abstract model of one version of one set: name, version, source label, overloads, abstract types, variables, dependencies |
| `src/Great.Parts.Cel/Declarations/ApplicationExtension.cs` | the definition that `CelExtension.Create` builds |
| `src/Great.Parts.Cel/Declarations/ExtensionChecks.cs` | the checks of the sets of an environment (4003, 4004, 4005, 4018) and the survivors that go on to the declaration checks |
| `src/Great.Parts.Cel/Declarations/AbstractTypeDeclaration.cs` | a name with its abstract `CelType` |
| `src/Great.Parts.Cel/Library/CatalogExtension.cs` | the definition of a set of the catalog: `ExtensionDefinition` plus macros and syntax options |
| `src/Great.Parts.Cel/Library/Extensions/ExtensionCatalog.cs` | the catalog: name → versions → factory, one definition per pair |
| `src/Great.Parts.Cel/Library/Extensions/BindingsExtension.cs`, `SetsExtension.cs` | the sets `bindings` and `sets` (this page) |
| `src/Great.Parts.Cel/Library/Extensions/MathExtension.cs`, `ListsExtension.cs`, `EncodersExtension.cs` and their function files | [extension-library](extension-library.md) |
| `src/Great.Parts.Cel/Library/Extensions/Strings/` | [extension-strings](extension-strings.md), [extension-format](extension-format.md) |
| `src/Great.Parts.Cel/Library/Extensions/Network/` | [extension-network](extension-network.md) |
| `src/Great.Parts.Cel/Syntax/ExtensionMacros.cs` | the macros of the sets: [extension-macros](extension-macros.md) |

## Layers

Three namespaces join `eng/layers.txt`, and the facade and `Library` stay as they are:

```text
Great.Parts.Cel.Library.Extensions.Strings -> Great.Parts.Cel.Library, Great.Parts.Cel.Library.Time, Great.Parts.Cel.Declarations, Great.Parts.Cel.Values, Great.Parts.Cel.Types, Great.Parts.Cel.Diagnostics
Great.Parts.Cel.Library.Extensions.Network -> Great.Parts.Cel.Library, Great.Parts.Cel.Declarations, Great.Parts.Cel.Values, Great.Parts.Cel.Types, Great.Parts.Cel.Diagnostics
Great.Parts.Cel.Library.Extensions -> Great.Parts.Cel.Library.Extensions.Strings, Great.Parts.Cel.Library.Extensions.Network, Great.Parts.Cel.Library, Great.Parts.Cel.Library.Time, Great.Parts.Cel.Syntax, Great.Parts.Cel.Declarations, Great.Parts.Cel.Values, Great.Parts.Cel.Types, Great.Parts.Cel.Diagnostics
```

`Great.Parts.Cel` (the facade) adds the three namespaces to its line. `Library` does not reference `Library.Extensions`: the catalog sits above the standard library, and `CatalogExtension`, which every set derives from, sits in `Library`, so the namespaces have no cycle. `Declarations` does not reference `Syntax` or `Library`, so `ExtensionDefinition` knows no macros: the macros belong to `CatalogExtension`, which `Library` may declare because `Library` references `Syntax`. An application set has none. Everything under `Library/` is core ([core](core.md)); the macros are parser code and are not.

## The model

`ExtensionDefinition` is immutable and has no state of an environment, so any number of environments and threads share it:

| Member | Content |
| :- | :- |
| `Name`, `Version` | the short name of the set, as an environment description writes it, and the number |
| `Source` | the label that build errors and the declaration index use: `extension <name> v<version>` for a set of the catalog, `application` for a set of the application, as for every other declaration of the application |
| `Overloads` | `ImmutableArray<OverloadBinding>` in the order of the table of the set: function, declaration, implementation, `FixedCost`, `ArgumentCost`, `DynOnly`, `Source`, `IsApplication` ([library](library.md#tables)) |
| `AbstractTypes` | the abstract types the set declares, each a name and its `CelType` of the kind `Abstract` ([extension-network](extension-network.md#abstract-types)): the identifiers `net.IP` and `net.CIDR` |
| `Variables` | declarations of variables, for internal sets only; none in this part |
| `Dependencies` | the names of the sets that this one needs |

`CatalogExtension : ExtensionDefinition` adds `Macros` (`ImmutableArray<Macro>`) and, for the sets that bring syntax, the flags of `SyntaxOptions`. `ApplicationExtension` takes the declarations of `CelExtension.Create`: its overloads are application overloads (`IsApplication`), so they get everything an application function gets — the exception boundary, the check of arguments and results against the declared types, the cost function — and their names follow the rules of `CelFunctionDecl`. It has no macros, abstract types or variables: `Create` takes none, and a later minor version may add them.

`CelExtension` (public, in `Declarations`) holds one `ExtensionDefinition` or, for a version the catalog lacks, only the name and the number: `CelExtensions.Strings(7)` is a `CelExtension` whose definition is `null`, and `Build` turns it into 4004. Nothing throws before `Build`.

**The same set.** Two `CelExtension`s are the same set when they hold the same definition object, or when both come from the catalog and have the same name and version, a missing definition included. The catalog creates one definition per pair of name and version, lazily and once per process (`Lazy<T>` with `ExecutionAndPublication`), and keeps one `CelExtension` for each known pair in a frozen dictionary built at the first use, so `CelExtensions.Strings(5)` returns the same instance every time. A `CelExtension` for an unknown version is created at each call and kept nowhere. The builder keeps its sets in an immutable set under the rule above, so adding one set twice adds nothing, and `ToBuilder` keeps the same sets.

## The catalog

| Set | Name in a description | Version | Dependencies | Functions | Macros | Abstract types |
| :- | :- | :- | :- | :- | :- | :- |
| `strings` | `strings` | 5 | — | 19 overloads | — | — |
| `math` | `math` | 3 | — | 58 overloads | `math.greatest`, `math.least` | — |
| `lists` | `lists` | 4 | — | 22 overloads | `sortBy` | — |
| `sets` | `sets` | 0 | — | 3 overloads | — | — |
| `encoders` | `encoders` | 1 | — | 3 overloads | — | — |
| `bindings` | `bindings` | 1 | — | 1 overload, `cel_block_list` | `cel.bind` | — |
| two-variable macros | `two-var-comprehensions` | 0 | — | 2 overloads of `cel.@mapInsert` | 7 macros of 3 and 4 arguments | — |
| `network` | `network` | 1 | — | 21 overloads | — | `net.IP`, `net.CIDR` |

The catalog is `FrozenDictionary<string, ExtensionFamily>` by the short name, ordinal; a family has the table of its versions — in this release one row — and the factory `Func<uint, ExtensionDefinition?>`: the definition of that version, or `null`. `ExtensionCatalog.Names` is the frozen set of names that an environment description accepts ([configuration](configuration.md#extension-sets)), and `CelExtensions` and the reader of descriptions both read it. The counts are those of the tables of the documentation; a test compares the identifiers of each set with those tables, and the number of overloads, macros and abstract types with the counts above.

The sets of the catalog exist in both targets, `net10.0` and `net11.0`, with the same declarations, and a definition has no target of its own; only `decimal`, in its own part, is for `net11.0` alone and is compiled under `#if NET11_0_OR_GREATER`.

`CelExtensions.<Set>(version)` calls the catalog and wraps the result, whatever the number: a version outside the table of the family is the missing definition. The XML documentation names the version of each set, as the placeholders of the skeleton do.

A later part adds a family with its row, and the name joins `ExtensionCatalog.Names` with it; `optional`, `protos` and `decimal` are such parts. The internal set `block` is not in the catalog and has no name in a description.

## Build checks

`CelEnvironmentBuilder.Build` collects the sets of the builder — the explicit `AddExtension` calls and those of a description — and runs `ExtensionChecks` before the declaration checks of [declarations](declarations.md#build). The checks run in this order, and each reports every error it finds:

| Step | Code | Condition | Message | Leaves out |
| :- | :- | :- | :- | :- |
| 1 | `UnknownExtensionVersion` (4004) | a set whose definition is missing | `extension '{name}' has no version {version}; available versions: {versions}`, `{versions}` the numbers of the family in ascending order separated by `, `; one error per set | that set |
| 2 | `ExtensionVersionConflict` (4003) | one name in two versions that both exist | `extension '{name}' is added in versions {version1} and {version2}`, the smaller number first; with three versions, one error for each pair in ascending order | every set of the name |
| 3 | `DuplicateExtension` (4018) | two sets of one name and one version that are not the same set | `extension '{name}' version {version} is added as two different sets: {source1} and {source2}`, the sources in ordinal order, so `application` stands before `extension strings v5`; two sets of the application give `application and application` | both sets |
| 4 | `MissingExtensionDependency` (4005) | a set whose dependency name no remaining set has | `{dependent} requires extension '{name}', which is not added to the environment`, `{dependent}` is `extension {name} v{version}` of the set that asks, for a set of the application as for one of the catalog; one error for each missing name | nothing |

A set that fails step 1, 2 or 3 contributes no declaration, macro or type, so it causes no second error: the version conflict of `strings` 4 and 5 gives 4003 and not also the overlaps of their overloads. A dependency is met by a set of the name in any version; a set that step 1–3 left out does not meet one, since it is out of the environment. All four codes have the stage `Build` and no place in an expression; in a description the place is the `{` of the element that declared the set ([configuration](configuration.md#extension-sets)).

What the checks compare is the same whatever the order of the calls. The errors of each step are formed from the sets by name, then by version, then by source text, ordinal; `Build` sorts all its errors by code and then by message ([declarations](declarations.md#build)), so the order of `AddExtension` and of the other builder calls changes neither the list nor the text of any error nor the environment. A test permutes the calls ([tests](tests.md#permutations-of-the-builder)).

The surviving sets then join the declaration checks of [declarations](declarations.md#extension-sets-in-the-declaration-checks): their overloads are checked against each other, the standard library and the application.

## What an environment keeps of the sets

The environment keeps the surviving sets, ordered by name (ordinal) and then by version. They give:

- **Declaration index.** The overloads of the sets follow those of the standard library and precede those of the application, in the order of the sets and of each table, so the candidates of a call have an order that does not depend on the order of the builder calls ([checking](checking.md#extension-sets-in-the-index)). The abstract types join the type identifiers and the variables of the internal sets the variables.
- **Syntax options.** `SyntaxOptions` of the environment is `SyntaxOptions.Standard` with the macros of the sets added to its `MacroTable` ([extension-macros](extension-macros.md#the-macro-table-of-an-environment)); the optional syntax flag will come with `optional`.
- **Costs and implementations.** Each overload carries its own, as for the standard library.

`Compile`, `Evaluate`, the cache of compilations and the internal `Parse`, which takes the options of the environment when it gets none, use these syntax options, so a macro of a set is expanded in every path and only in an environment that has the set.

## `bindings`, version 1

The macro `cel.bind` is on [extension-macros](extension-macros.md). The set has one function, `cel.@block` with the overload `cel_block_list`, `cel.@block(list(dyn), T) -> T`, declared with the type parameter `T` and no implementation: like `conditional`, it is a special form that the planner evaluates itself ([evaluation](evaluation.md#extension-sets-in-the-evaluator)). The name has `@` in its last segment, which no `SELECTOR` or `IDENT` of the language has, so text cannot call it, and an application cannot declare it (`InvalidFunctionName`, 4015). It is in the table of the public set so that the list of overloads equals that of cel-go version 1. The tests build a tree with `NodeFactory`, as [evaluation](evaluation.md#blocks) says.

| Overload | Cost |
| :- | :- |
| `cel_block_list` | 0 beyond the slots and the result |

## `sets`, version 0

Three overloads over two lists of the same element type `T`, each a global call on the receiver `sets`: the receiver call `sets.contains(a, b)` is the qualified function name `sets.contains` ([checking](checking.md#rules-per-node), the receiver call rule). Equality is CEL equality (`EqualityRules`): `1`, `1u` and `1.0` are equal, a NaN equals nothing, lists and maps compare by content. The order of comparisons is that of cel-go, which the cost rows depend on:

| Overload | Result | Comparisons, in order |
| :- | :- | :- |
| `list_sets_contains_list`, `sets.contains(a, b)` | `true` when every element of `b` is in `a` | for each element `y` of `b` in order: each `x` of `a` in order against `y`, until the first equal; the first `y` with no equal gives `false` without looking at the rest; the empty `b` is `true` |
| `list_sets_intersects_list`, `sets.intersects(a, b)` | `true` when some element of `a` is in `b` | for each `x` of `a` in order: each `y` of `b` in order, until the first equal; the first `x` with an equal gives `true`; the empty `a` or `b` is `false` |
| `list_sets_equivalent_list`, `sets.equivalent(a, b)` | `contains(a, b)` and `contains(b, a)` | `contains(a, b)`; when it is `false` the result is `false`, otherwise `contains(b, a)` |

Each element that is looked up is an `in` over the other list, which costs as `in_list` does: 1 + the eq of each comparison made up to the first equal element, each eq charged before its comparison ([evaluation](evaluation.md#cost)). A call is 1 plus the sum of those `in` costs: `sets.contains([1, 2, 3], [1, 2])` is 1 + (1 + 1) + (1 + 2) = 6 and `sets.intersects([1, 2], [3, 2])` is 1 + (1 + 2) + (1 + 2) = 7; `equivalent` is 1 plus the sum of the first `contains` and, when it is `true`, of the second, each without its constant 1: `sets.equivalent([1, 2], [2, 1])` is 1 + 5 + 5 = 11. An element pair of lists or maps is compared by `EqualityRules` with the meter of the call site, which charges pair by pair. A map or list view over application data is read by that comparison; an error it returns is the value of the call. Element kinds that cannot be compared are not an error: unequal kinds are unequal. The checker types the three functions with `list(T)` twice, so `sets.contains([1], [1.0])` has no overload; with a `dyn` list the numbers of different kinds compare on one line.

## The sets `optional` and `block`

This section adds a set to the catalog and an internal set beside it. It supersedes "Not in this part" and the sentences of this page that leave `optional` and `block` to a later part.

**`optional`, version 2** ([extension-optional](extension-optional.md)). The catalog table gets the row below, and the eight sets of the first row of "The catalog" become nine: `ExtensionCatalog.Names` and so the names of a description gain `optional`, and `CelExtensions.Optional(version)` — whose body the skeleton left to this part — calls the catalog as the other members do. The counts below are those of the tables of the documentation; the test of the catalog compares them.

| Set | Name in a description | Version | Dependencies | Functions | Macros | Abstract types |
| :- | :- | :- | :- | :- | :- | :- |
| `optional` | `optional` | 2 | — | 18 overloads | `optMap`, `optFlatMap` | `optional_type` |

The definition is a `CatalogExtension` with `optionalSyntax: true`: `CatalogExtension.SyntaxOf` gives the environment `SyntaxOptions` with the optional syntax on, and the parser, which refuses `.?`, `[?`, `[?e]`, `{?k: v}` and `T{?f: v}` with `SyntaxNotEnabled` (1007) in an environment without it, accepts them in one with it. The abstract type is the declaration `optional_type` with a type of the kind `Optional`: the model of `AbstractTypeDeclaration` is a name and a `CelType`, and the kind is not restricted to `Abstract`; `DeclarationChecks` reads the name for the identifier and the check of `TypeNotDeclared`, and `ExtensionCatalog.SetOfAbstractType` answers `optional` for `optional_type`, which gives the hint of 4008. The surviving sets of an environment that has `optional` give the declaration index the 18 overloads and the identifier `optional_type`; the planner learns of the special forms from the candidates ([evaluation](evaluation.md#optional-values-and-the-optional-syntax)).

**`block`, version 1, an internal set.** The set exists for the runner of the cel-spec cases, and for nothing else: `block_ext` has 37 cases that use the test macros `cel.block`, `cel.index`, `cel.iterVar` and `cel.accuVar`, which cel-go declares in its conformance runner and not in a library. The set is internal, has the version 1, depends on `bindings`, and has no member of `CelExtensions`, no name in a description and no snapshot of its declarations: the number of an internal set is no contract, and a later minor version may publish it.

| | `block` |
| :- | :- |
| code | `src/Great.Parts.Cel/Library/Extensions/BlockExtension.cs`, an `internal static class` of the internal namespace `Great.Parts.Cel.Library.Extensions`, a sub-namespace of `Library` that `eng/layers.txt` has; no namespace is added |
| definition | a `CatalogExtension`: name `block`, version 1, source `extension block v1`, no overloads, no abstract types, the dependency `bindings`, the four macros of `ExtensionMacros.Block` ([extension-macros](extension-macros.md#optional-and-block)), and the 30 variables below |
| variables | `@index0` to `@index29`, each a `CelVariableDecl` of the type `dyn`; the first use of `ExtensionDefinition.Variables`, which the model reserved for internal sets |
| entry | `ExtensionCatalog.Block`, an `internal static CelExtension`, made once with `Lazy<T>` from `BlockExtension.Create(1)` and the table of versions `[1]`; it is not in `ExtensionFamilies`, so not in `Names`, and `CelExtensions.Create("block", 1)` finds no factory |
| access | the runner reads `ExtensionCatalog.Block` through `InternalsVisibleTo`, which `Great.Parts.Cel.Conformance` already has; no new `InternalsVisibleTo` is added and no member of the public API |
| not there | a member `CelExtensions.Block`, which the public API snapshot lacks; the name `block` in `ExtensionCatalog.Names`; a file `block-v1.txt` among the snapshots of the sets |

**The variables.** A variable of an internal set is an entry of the declaration index like a variable of the application. The name check of a variable of the application — an identifier or a qualified name, `InvalidVariableName` (4016) — does not apply to a set of the catalog, as the name check of a function does not (4015, "Extension sets in the declaration checks" of [declarations](declarations.md)): `@index0` is not an identifier. A variable of the application cannot have the name, since `@` fails 4016, so the sets have no conflict. The planner takes `@index`N as a slot of the innermost block that has N slots ([evaluation](evaluation.md#blocks)) before it looks at the declarations; outside any block it is a variable that nobody binds, `Unbound` (3009) `no value is bound to variable '@index3'`. The checker has no rule for it: it is an identifier of the type `dyn`, so `cel.block([1, cel.index(0) + 1], cel.index(1))` is `dyn` throughout and its type is that of the second argument, `T` of `cel.@block`. The cases that need a more precise type — an operand of `?:`, an optional element — see `dyn`, which the rules of the optional syntax accept.

**`cel.index(30)` and above.** The macro accepts any non-negative number and expands to `@index30`, which no set declares, so the check gives `UndeclaredReference` (2000) `undeclared reference to '@index30' (in container '')` at the identifier, whose offset is the `(` of the call ([extension-macros](extension-macros.md#optional-and-block)). The test of the runner does not reach it; a test of the set does. The largest number in the cel-spec cases is 25.

**The dependency.** `block` expands to a call of `cel.@block`, which the set `bindings` declares: the set has `Dependencies = ["bindings"]` and step 4 of the build checks reports `MissingExtensionDependency` (4005) `extension block v1 requires extension 'bindings', which is not added to the environment` when the environment lacks it, as for any other set. No public environment can add `block`, so the check is internal in practice: its test builds an environment with `ExtensionCatalog.Block` alone. `ExtensionChecks` needs no change.

**Without the set.** `cel.block(...)`, `cel.index(0)` and the other three are ordinary receiver calls in every public environment, and the checker answers as for any receiver call whose target is a name that nothing declares: `UndeclaredReference` at `cel`, `undeclared reference to 'cel' (in container '')`, as cel-go does. The macros of `bindings` have the same namespace and are not affected. A description with `"name": "block"` is `InvalidConfiguration` (4009) at the value of `name`, the text of any name outside the catalog; the test that checks it exists beside that of `regex` and stays.

**Public API snapshot.** The public API has no member for `block`: `CelExtensions` has `Strings`, `Math`, `Lists`, `Sets`, `Encoders`, `Bindings`, `Optional`, `TwoVarComprehensions`, `Network` and `Protos` and nothing named `Block`, and `PublicAPI.Unshipped.txt` is unchanged by the part. The snapshot of the declarations of `optional` version 2 is the file `optional-v2.txt`, written once the set is complete ([tests](tests.md#tests-of-optional-and-block)).

**Files that two groups of changes edit** ([extension-optional](extension-optional.md)): `ExtensionCatalog.cs` gets the row `optional` and `SetOfAbstractType` in the part of `optional`, and `Block` in the part of `block`; `ExtensionMacros.cs` gets `Optional` and `Block` as two arrays beside `Math`, `Lists`, `Bindings`, so that the second edit adds a property and a case to what the first left.

## Abstract types

The abstract types of a set are declared by the set and used by the checker and the values; the rules of the kind `Abstract` — assignability, `null`, equality, the runtime type, the fit — and the public enumeration members they need are on [extension-network](extension-network.md#abstract-types).

## Implementations of the sets

The implementations of the catalog follow the rules of the standard library ([library](library.md#implementations-and-cost)) and add these:

- **Elements that are errors.** A list or map over the data of the application may hold an element that cannot be read, which is an error value ([values](values.md)). An implementation that reads elements and meets one returns that error value as the value of its call, with the place of the call when the error has none; it never takes it for a kind it does not handle. This holds for `join`, `sort`, `sortBy`, `distinct`, `flatten`, `slice`, `reverse`, `sets.*`, the list forms of `math.@max` and `math.@min`, `json.encode` and the comprehensions of two variables.
- **Checked kinds.** Every implementation checks the kinds it reads and answers `EvalNoMatchingOverload` for another, whatever the static types say: a static type is no guarantee of the value, and the overloads with `ShallowArguments` rely on it ([extension-library](extension-library.md)).
- **No exceptions, no state.** No implementation throws for a value of the language or keeps state, so a program calls it from any number of threads. An argument error is an error value of the code named in the page of the set.
- **No banned APIs.** The code does not use .NET `Regex`, `[GeneratedRegex]`, `SearchValues`, the search and the split of `string` with a string pattern, `Array.Sort` or `List.Sort` with a comparison, LINQ, or a closure that captures on the evaluation path; a result is built as it is charged ([extension-strings](extension-strings.md#conventions)); `stackalloc` takes at most 256 bytes. Frozen collections are built only from names the library itself gives, such as the table of the catalog, never from the text of an expression or from values.
- **Error values.** An error value of a set takes at most 64 bytes: its detail is formed when it is read, from references to existing objects and from numbers in its fields, never from a string built at the time of the error ([evaluation](evaluation.md#errors-as-values)). A detail names at most one number that comes from the arguments; another number it needs is read when the error is formed from an object that exists, such as the length of a list argument, or is a constant of the set.

## Not in this part

The sets `protos` and `decimal`: each comes with its part; `optional` and the internal set `block` are in "The sets `optional` and `block`" above, and the name of a set in a description is in [configuration](configuration.md#extension-sets). A set cannot be removed from an environment; there is no replacement of one version by another.

Related pages: [declarations](declarations.md), [checking](checking.md), [evaluation](evaluation.md), [library](library.md), [configuration](configuration.md), [core](core.md), [facade](facade.md).
