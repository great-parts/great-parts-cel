# Extension set `optional`, version 2

The set adds the type `optional_type(T)`, 18 overloads, the macros `optMap` and `optFlatMap`, and turns on the optional syntax: `e.?f`, `e[?k]`, `[?e]`, `{?k: v}` and `T{?f: v}`. The behavior is that of `cel/library.go` of cel-go v0.32.0 with `OptionalTypesVersion(2)`; the cases `optionals` and `type_deduction` of cel-spec, and four of `block_ext`, pin it, and "Differences from cel-go" lists what the library does otherwise. Identifiers and signatures are on the `docs/extensions.md` page, costs on [cost](../cost.md) and [library](library.md#cost-of-optional-and-block), codes on [error codes](../error-codes.md). The rest of the part is on these pages: the value and its rules in [values](values.md#optional), the type rules in [types](types.md#optional-types-in-the-rules), the checker in [checking](checking.md#optional-syntax-and-optional-types), the planner in [evaluation](evaluation.md#optional-values-and-the-optional-syntax), the macros in [extension-macros](extension-macros.md#optional-and-block), the catalog and the internal set `block` in [extensions](extensions.md#the-sets-optional-and-block), the runner in [conformance](conformance.md#the-sets-optional-and-block-in-the-runner), the tests in [tests](tests.md#tests-of-optional-and-block), the differential run in [eng-difftest](eng-difftest.md#profile-of-the-set-optional).

The files fall into three groups. A change belongs to the group of the file it edits; a file of another group is edited only as these tables say.

**The values and the functions**

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Values/CelOptional.cs` | the public struct, the bodies of its members |
| `src/Great.Parts.Cel/Values/OptionalBox.cs` | the internal object that holds the value of a non-empty optional |
| `src/Great.Parts.Cel/Values/CelValue.cs`, `CelValueKind.cs` | `FromOptional`, `TryGetOptional`, `Equals`, `GetHashCode` |
| `src/Great.Parts.Cel/Values/ValueTypeRules.cs`, `ValueTypeNames.cs`, `EqualityRules.cs`, `KeyHashRules.cs`, `ConversionRules*.cs` | the rows of the kind `Optional` ([values](values.md#optional)) |
| `src/Great.Parts.Cel/Types/CelType.cs` | `OptionalOf` |
| `src/Great.Parts.Cel/Declarations/DeclarationChecks.cs` | `TypeNotDeclared` for the kind `Optional` and its hint: the two places that name the kind, nothing else |
| `src/Great.Parts.Cel/Library/Extensions/OptionalExtension.cs` | the definition: the 18 bindings, the declaration of the type, the flag of the syntax |
| `src/Great.Parts.Cel/Library/Extensions/OptionalFunctions.cs` | the implementations of the 9 overloads that are plain functions: `optional_of`, `optional_ofNonZeroValue`, `optional_none`, `optional_value`, `optional_hasValue`, `list_first`, `list_last`, `optional_unwrap`, `optional_unwrapOpt`; the 3 special forms and the 6 indexes are not in this file |
| `src/Great.Parts.Cel/Library/Extensions/ExtensionCatalog.cs` | the row `optional`; `SetOfAbstractType` answers `optional_type` |
| `src/Great.Parts.Cel/Library/OverloadIds.cs`, `Costs.cs` | the identifiers and the cost functions of the 18 overloads |
| `src/Great.Parts.Cel/Syntax/ExtensionMacros.cs` | `optMap`, `optFlatMap` |
| `src/Great.Parts.Cel/Evaluation/OptionalOrPlanNode.cs` and the case of `or` and `orValue` in `Planner.VisitCall` | the two lazy forms |
| `src/Great.Parts.Cel/Configuration/EnvironmentReader*.cs`, `TypeRecordResolver.cs` | the record `optional_type<E>` and the name `optional` ([configuration](configuration.md#the-set-optional-in-a-description)) |
| `tests/Great.Parts.Cel.Tests/{Values,Types,Library,Syntax,Configuration}/` | the tests of this group |

**The syntax and the generalization of types**

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Types/AssignabilityRules.cs` | the rules of `optional_type` and of abstract types with parameters ([types](types.md#optional-types-in-the-rules)) |
| `src/Great.Parts.Cel/Checking/Checker.cs` | the rules of the syntax and of `has()` |
| `src/Great.Parts.Cel/Evaluation/OptionalSelectPlanNode.cs`, `OptionalListPlanNode.cs`, `OptionalMapPlanNode.cs`, `SelectPlanNode.cs`, the optional entries of the message node, `Planner.cs` | the nodes of the syntax |
| `src/Great.Parts.Cel/Library/Extensions/OptionalOperators.cs` | the 7 bindings of the syntax: the 6 overloads of indexes with their implementations ("Indexes") and `select_optional_field` with its validator and no implementation; `OptionalExtension.Overloads` appends them after `optional_orValue_value`, in the order of the table below |
| `tests/Great.Parts.Cel.Tests/{Checking,Evaluation,Types,Library}/` | the tests of this group |

**The internal set `block` and the runner**

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Library/Extensions/BlockExtension.cs` | the internal set ([extensions](extensions.md#the-sets-optional-and-block)) |
| `src/Great.Parts.Cel/Library/Extensions/ExtensionCatalog.cs` | the internal entry `Block`, outside `Names` |
| `src/Great.Parts.Cel/Syntax/ExtensionMacros.cs` | the four macros of `block` |
| `src/Great.Parts.Cel/Declarations/ExtensionChecks.cs` | the dependency of `block` on `bindings` (4005): no change expected |
| `tests/Great.Parts.Cel.Conformance/Runner/` | the sets in `ExtensionSets`, the declarations of a case, the types |

A file that two groups edit — `ExtensionCatalog.cs`, `ExtensionMacros.cs`, `OptionalExtension.cs` (the second group adds one line, the call of `OptionalOperators.Overloads`) — gets the rows of its group in separate places, so the later group reads the earlier one.

## The surface of the public API

Nothing is added to the public API. Everything the part needs is in the members that the skeleton declares: `CelOptional<T>` with its constructor, `HasValue`, `Value`, `Equals`, `GetHashCode` and the two operators; `CelValue.FromOptional` and `TryGetOptional`; `CelValueKind.Optional`; `CelType.OptionalOf` and `CelTypeKind.Optional`; `CelExtensions.Optional(uint)`; `CelErrorCode.OptionalHasNoValue`. The part replaces their bodies, and `PublicAPI.Unshipped.txt` does not change. `CelOptional<T>` gets no `ToString`, no conversion and no static factory: an application makes one with the constructor or `default`. `block` adds no member.

## The type `optional_type(T)`

`CelType.OptionalOf(T)` has the kind `Optional`, the name `optional_type` and the one parameter `T`; its text form is `optional_type(int)`, and its equality, hash and substitution are those of the model ([types](types.md)). The set declares the type: its definition has the one `AbstractTypeDeclaration("optional_type", CelType.OptionalOf(CelType.Dyn))`. An entry of that list is a name with a type of the kind `Abstract` for `net.IP` and with a type of the kind `Optional` here; the declaration index treats both alike, and gives the identifier `optional_type`, whose static type is `type(optional_type(dyn))` and whose type value is `optional_type(dyn)`. So `type(optional.none()) == optional_type` is `true`: type values compare by kind and name, and `type(…)` against `type(…)` is rule 6 of [types](types.md#assignability).

**Declared.** A type of the kind `Optional`, at any depth in a variable, an overload or a field, is declared when a surviving set of the environment declares the name `optional_type`. Without the set `Build` reports `TypeNotDeclared` (4008) with the hint `; add the extension set 'optional'`: `DeclarationChecks.Undeclared` no longer answers `Optional` unconditionally, and `HintOf` gives the hint for the kind `Optional` too, from `ExtensionCatalog.SetOfAbstractType("optional_type")`. A provider that describes an object type named `optional_type` is `DuplicateType` (4001), as for `net.IP`.

## The value

The storage, the rules and the conversions are in [values](values.md#optional). In short: an empty optional is a `CelValue` of the kind `Optional` with no reference, a non-empty one holds one `OptionalBox`; `optional.none()` and `optional.of(null)` differ; two optionals are equal when both are empty or both hold values that are equal by CEL equality; the runtime type is `optional_type(dyn)`.

## The set

The definition has the name `optional`, the version 2, no dependencies, the 18 bindings below in this order, the declaration of the type, the two macros and the flag `OptionalSyntax`. The catalog has the one version 2: `CelExtensions.Optional(1)` is `UnknownExtensionVersion` (4004), `extension 'optional' has no version 1; available versions: 2`. In cel-go the macro `optFlatMap` is of version 1 and `first`, `last`, `optional.unwrap`, `unwrapOpt` of version 2; a later minor version that adds the earlier ones takes them from the "since" column of the documentation page.

| Overload | Signature | Implementation | Errors | Units |
| :- | :- | :- | :- | :- |
| `optional_of` | `optional.of(V) -> optional_type(V)` | one `OptionalBox` over the argument, `null` included | — | 1 |
| `optional_ofNonZeroValue` | `optional.ofNonZeroValue(V) -> optional_type(V)` | the empty optional for a zero value ("Zero values"), else as `optional_of` | — | 1; an object 1 + f, f the number of its fields |
| `optional_none` | `optional.none() -> optional_type(V)` | the one static empty value, no allocation | — | 1 |
| `optional_value` | `optional_type(V).value() -> V` | the held value | `OptionalHasNoValue` (3016) `optional value is empty`, at the call | 1 |
| `optional_hasValue` | `optional_type(V).hasValue() -> bool` | `HasValue` | — | 1 |
| `optional_or_optional` | `optional_type(V).or(optional_type(V)) -> optional_type(V)` | special form ("Special forms") | — | 1 |
| `optional_orValue_value` | `optional_type(V).orValue(V) -> V` | special form | — | 1 |
| `select_optional_field` | `_?._(dyn, string) -> optional_type(V)` | special form: a node of the planner | as a selection: `EvalNoSuchField`, `FieldSelectionNotSupported` | as a selection: 1; a map 1 + ⌈L/10⌉ |
| `list_optindex_optional_int` | `_[?_](list(V), int) -> optional_type(V)` | "Indexes" | an element that is an error value is the result; a key that is no index, "Indexes" | 1 |
| `optional_list_optindex_optional_int` | `_[?_](optional_type(list(V)), int) -> optional_type(V)` | "Indexes" | as above | 1 |
| `map_optindex_optional_value` | `_[?_](map(K, V), K) -> optional_type(V)` | "Indexes" | `DuplicateMapKey` (3010) for a JSON object | 1; a string key of length L 1 + ⌈L/10⌉; the rows of a JSON object |
| `optional_map_optindex_optional_value` | `_[?_](optional_type(map(K, V)), K) -> optional_type(V)` | "Indexes" | as above | as above |
| `optional_list_index_int` | `_[_](optional_type(list(V)), int) -> optional_type(V)` | the code of `optional_list_optindex_optional_int` | as above | 1 |
| `optional_map_index_value` | `_[_](optional_type(map(K, V)), K) -> optional_type(V)` | the code of `optional_map_optindex_optional_value` | as above | as above |
| `list_first` | `list(V).first() -> optional_type(V)` | empty for an empty list, else `optional.of(list[0])` | an element that is an error value is the result | 1 |
| `list_last` | `list(V).last() -> optional_type(V)` | the last element | as above | 1 |
| `optional_unwrap` | `optional.unwrap(list(optional_type(V))) -> list(V)` | the values of the optionals that have one, in order, in a new list | an element that is not an optional: `EvalNoMatchingOverload` | 1 + n, n the size of the list |
| `optional_unwrapOpt` | `list(optional_type(V)).unwrapOpt() -> list(V)` | the same code | as above | 1 + n |

The units are those of the cost page, charged by `FixedCost` and `ArgumentCost` before the implementation runs. The `ArgumentCost` of the index overloads is the function of `index_list` and `index_map` ([library](library.md#tables)): the length of a string key and the rows of a JSON object or array. For an empty optional operand it is 1, and for a non-empty one the function of the held list or map. A test compares each binding with the cost page.

### Zero values

`optional.ofNonZeroValue(v)` is the empty optional exactly when `v` is a zero value of its kind: `null`; `false`; the `int` 0 and the `uint` 0; the `double` 0.0 and -0.0 (a NaN is not zero); the empty string; the empty `bytes`; the empty list; the empty map; the `duration` of 0 nanoseconds; the `timestamp` 0001-01-01T00:00:00Z; an object none of whose fields is set, which for a protobuf message is the empty message. Anything else — a `type` value, an optional, a value of an abstract type — is not zero. The list is the `Zeroer` of cel-go v0.32.0: `Bool`, `Bytes`, `Double`, `Duration`, `Int`, `Null`, `String`, `Timestamp`, `Uint`, the lists, the maps and the protobuf messages. Its zero `timestamp` is Go's `time.Time{}`, which is the year 1 and not the epoch, in spite of its comment; the differential run checks it ([eng-difftest](eng-difftest.md#profile-of-the-set-optional)).

The test of an object reads `HasField` of each field of its description, in the order of the description, and stops at the first one that is set. With a cost limit the call charges the units for the f fields it may read before the first read, so the row is 1 + f for an object and 1 for every other value.

### Special forms

`optional_or_optional`, `optional_orValue_value` and `select_optional_field` have no implementation, as `conditional` has: the planner evaluates them. It plans a call as the form when the candidates of the call — the list that type checking recorded, or, without it, the list of the index for the function name, the style and the number of arguments — hold the binding with that identifier. A call that has an overload of the application among its candidates is still the form: cel-go decorates every call of the two overloads, whatever else the name stands for. The nodes are in [evaluation](evaluation.md#optional-values-and-the-optional-syntax). A call of `or` or `orValue` in an environment without the set has no candidate and is `EvalNoMatchingOverload`, as the call of any unknown function.

### Indexes

The six overloads of indexes are ordinary overloads with implementations, so the planner makes them `CallPlanNode`s, and a `dyn` operand reaches them by dispatch ([evaluation](evaluation.md#optional-values-and-the-optional-syntax)). Type checking reads the declared parameters: the index of a list is an `int`, the key of a map is the key type, and `dyn` fits either, so `[1][?1u]` is a check error and `[1][?dyn(1u)]` is not. What a call does at run time is decided by the kinds of its values alone, as the optional qualifiers of cel-go decide it, and not by which overload of a pair type checking kept: `Matches` of the six reads the kind of the operand and never the kind of the key, and the implementation reads the key. The kept overload may be the one that does not fit the value, since the first trial binds a free type parameter: the type of `optional.none()` is `optional_type(V)` with `V` free, the overload over a list binds it to a list, and the overload over a map is then no candidate, so `optional.none()[dyn('a')]` has the one candidate over a list; it is `optional.none()` all the same.

The key is evaluated before the call, as every argument is, and then:

| Operand | Key | Result |
| :- | :- | :- |
| an empty optional | any value | the empty optional |
| a non-empty optional | — | the index of the held value by the rows below; a held value that is neither a list nor a map is `EvalNoMatchingOverload`, since an implementation checks the kinds it reads |
| a list | an `int` | `optional.of(l[k])` for an index inside the list, the empty optional for a negative index or one past the end: where `index_list` gives `IndexOutOfRange`, this gives none |
| a list | a `uint`, or a `double` with an integral value | the same for the integer it stands for, as `index_list` takes them (`[1, 2][?dyn(1u)]` and `[1, 2][?dyn(1.0)]` are `optional.of(2)`, `[1, 2][?dyn(2u)]` is none); what is no index at all stays an error as in `index_list`: a `double` that is not integral, NaN and the infinities are `InvalidArgument` (3008), and a `uint` or an integral `double` outside the range of `int64` is `IndexOutOfRange` (3003), not none, since cel-go refuses both |
| a list | a value of any other kind | `EvalNoMatchingOverload` (`no such overload: _[?_](list, string)`); cel-go fails too, so no list is indexed by a `string`, a `bool`, `bytes`, `null`, a list, a map or a `type` |
| a map | any value | `optional.of(m[k])` when the key lookup of the map keys in [values](values.md) finds the key, so `{1u: 1.0, 2: 2.0, 3u: 3.0}[?3.0]` is `optional.of(3.0)`; otherwise none, where `index_map` gives `NoSuchKey`; a key of a kind that no map holds — `bytes`, a list, a map, `null`, a `type`, a time value — finds nothing and gives none |

On a JSON object the lookup counts the matches: none is the empty optional, one is the value, two or more are `DuplicateMapKey`. So `optional.none()[0]` is `optional.none()` and `optional.of(['a'])[0]` is `optional.of('a')`; with keys of the type `dyn`, `optional.none()[dyn('a')]`, `optional.none()[?dyn(b'a')]` and `{1: 'x'}[?dyn(1.0)]` are `optional.none()`, `optional.none()` and `optional.of('x')`, `[1, 2][?dyn('a')]` is `EvalNoMatchingOverload`, and the differences from cel-go at the end of this page list what the table leaves to the registry.

## Macros

`optMap` and `optFlatMap` are described in [extension-macros](extension-macros.md#optional-and-block): the expansion, the hidden name `@target`, the error, the order in which the nodes are created, and the cost.

## The syntax and `has()`

The rules of the checker are in [checking](checking.md#optional-syntax-and-optional-types); the nodes, the order of evaluation, the costs and the errors are in [evaluation](evaluation.md#optional-values-and-the-optional-syntax).

**`has()` over an optional.** `has(a.?b)` is not a presence test: the parser reads `a.?b` as the call `_?._(a, 'b')`, so the argument of `has` is not a selection, and the macro refuses it with `InvalidMacroCall` (1006) `invalid call of macro 'has': argument must be a field selection`, the error that every argument of `has` that is not a selection gets ([syntax](syntax.md#macros)). `has(a.?b.c)` is a presence test of `c` on an operand of the type `optional_type(T)`: it is `bool` at check; at evaluation it is false for an empty optional, else the presence of `c` in the held value — the key in a map, a set field of an object — and `FieldSelectionNotSupported` (3025) for a held value that is neither. The same holds for an operand that is an optional by its value and not by its type. `has({'foo': optional.none()}.foo.bar)` is false, and `has({'foo': optional.of({'bar': 1})}.foo.bar)` is true, where cel-go v0.32.0 fails with `no such key: bar` in both, since its attribute unwraps an optional only at its base and reads the one a qualifier gives as a value; this is the record `has(optional_type) we-value` of the registry of known differences, a `cel-go-bug`.

## Conversions to and from .NET

By the rows of the type mapping table and [values](values.md#optional). The CEL type of `CelOptional<X>` is `optional_type(E)`, `E` the type of `X`, so `AddVariable<CelOptional<long>>("x")` declares `optional_type(int)` and needs the set. A value converts into the optional of the converted held value, and `default` is the empty optional. A result of the type `optional_type(E)` converts to `CelOptional<X>`, `X` by the result rows; the held value is converted by its own row at evaluation, not lazily, and a refusal is `ResultConversion` (3015) with the place of the root. When the held value is a collection, the rule of the lazy view and of the copy is that of the list and map rows, applied to it: `CelOptional<IReadOnlyList<long>>` over `optional_type(list(int))` holds a lazy view, and `CelOptional<IReadOnlyList<int>>` an immutable copy made at evaluation. The copy is not charged. `T = CelValue` gives the value, `T = object` a `CelOptional<object?>`.

## Public documentation

The pages of `docs/` that the set changes. The group "The values and the functions" writes them all except the sentences of `docs/extensions.md` about the syntax and `has()`, which the group "The syntax and the generalization of types" adds to the same section; each group writes the `CHANGELOG.md` line for what it makes visible.

| Page | Gets |
| :- | :- |
| `docs/extensions.md` | the set `optional` 2: the name, the version, the 18 overloads with their identifiers and the version of each, `optMap` and `optFlatMap`; the type `optional_type(T)`; the syntax with each form, its result and its error without the set (`SyntaxNotEnabled`), the optional chains, `has()` over an optional with the difference from cel-go, `optional.ofNonZeroValue` and its zero values; one example for each form. The page says that the 37 cases of `block_ext` use test macros that an internal set provides, and publishes nothing of `block` |
| `docs/cost.md` | the rows of the overloads, of the macros and of the literals with optional positions, with the examples of [extension-macros](extension-macros.md#optional-and-block) |
| `docs/type-mapping.md` | the rows `optional_type(E)` ↔ `CelOptional<X>`: input, result, the expected type, `none` against `of(null)`, the rule of the lazy view and the copy for the held value |
| `docs/error-codes.md` | `SyntaxNotEnabled` (1007) with the forms of the optional syntax, `OptionalHasNoValue` (3016), and the case of an optional position that holds no optional under `EvalNoMatchingOverload` |

## Differences from cel-go

- `has()` over an optional operand, above.
- The six overloads of indexes have implementations and `select_optional_field` a node, where cel-go plans them as qualifiers of an attribute; the outcomes are those of its optional qualifiers (`interpreter/attributes.go`: `applyQualifiers`, `newQualifier`, `refQualify`), except the three that follow.
- The key of an index of a map that is `bytes`, a list, a map, `null` or a `type` finds nothing and gives the empty optional, as the language definition has it: a map key is an `int`, a `uint`, a `bool` or a `string` (https://github.com/google/cel-spec/blob/8bef98f1caadc2036fe63d5516cf1df65dc4b741/doc/langdef.md#aggregate-values), so nothing else is a key, as `dyn(b'a') in {'a': 1}` is `false`. cel-go fails with `invalid qualifier type: types.Bytes` and the like: it makes a qualifier of the key, and only a `string`, `int`, `uint`, `bool` or `double` value makes one, whatever the map holds, even when it is empty. The registry has 25 records of class `cel-go-bug` for the kinds `bytes`, `list`, `map`, `null_type` and `type`: `map_optindex_optional_value(map, <kind>)`, and `optional_map_optindex_optional_value` and `optional_map_index_value` each with `optional_type(map)` and with `optional_type` as the first type. A kind that the profile does not build, a time value, is the same difference.
- The key of an index of an empty optional is evaluated, as every argument of a function is (the language definition: the default evaluation of function arguments is strict, https://github.com/google/cel-spec/blob/8bef98f1caadc2036fe63d5516cf1df65dc4b741/doc/langdef.md#functions), so `optional.none()[dyn(1) / 0]` is `DivisionByZero`. cel-go applies the qualifiers to a non-empty optional only and never reads the key of an empty one: it returns the empty optional whatever the key would give, an error or an unbound variable. The registry has `_[?_](optional_type, error) we-error` and `_[_](optional_type, error) we-error`, class `cel-go-bug`.
- An operand that is neither a list, a map nor an optional, or an optional that holds no list and no map — only a `dyn` expression can be one: `dyn(1)[?0]`, `dyn(optional.of(1))[0]`, `optional.of(dyn(1))[?0]` — is `EvalNoMatchingOverload`, as a call that no overload fits is (`Functions` of the language definition, as for `or` below). cel-go gives the empty optional there, since the qualifier of such an index tests the presence of an element in a value that has none. The profile builds none; a run that finds one adds the signature to the registry, class `cel-go-bug`.
- A static `null` is assignable to `optional_type(T)`, as the legacy rule of cel-go says, and a `null` value is not an optional: `[null, optional.of(1)][0]` has the static type `optional_type(int)` and the value `null`, which converts to no `CelOptional<X>`.
- `or` whose argument is not an optional — only a `dyn` value or a `null` can be one — is `EvalNoMatchingOverload` with the text `no such overload: or(optional_type, int)`, when the receiver is empty and the argument is evaluated. The overload declares an optional result, and the language definition says that a call that no overload fits is the error `no_matching_overload` (`Functions`, and `Extension Functions` for a function of a set). cel-go returns the argument, whatever its kind: `optional.none().or(dyn(1))` is `1` there. The registry has the signatures `optional_or_optional(optional_type, <kind>) we-error`, class `cel-go-bug`. `orValue` takes an argument of any kind, as in cel-go.
- Nested blocks need no entry: a slot that reads itself reads the slot of the same number of the enclosing block, as in cel-go ([evaluation](evaluation.md#blocks)).
- `[?1]` gives one error, where cel-go gives two ([checking](checking.md#optional-syntax-and-optional-types)).
- An optional element, entry or field whose value is not an optional, which only a `dyn` value can be, is `EvalNoMatchingOverload` with the function `?` ([evaluation](evaluation.md#optional-values-and-the-optional-syntax)); cel-go has its own text for it.

Related pages: [extensions](extensions.md), [extension-macros](extension-macros.md), [values](values.md), [types](types.md), [checking](checking.md), [evaluation](evaluation.md), [library](library.md), [configuration](configuration.md), [conformance](conformance.md).
