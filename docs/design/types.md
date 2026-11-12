# Types: CEL types and their rules

`Great.Parts.Cel.Types` holds the public type model — `CelType` and `CelTypeKind` — and, in rule files, the two rules that type checking relies on: assignability of types and substitution of type parameters. Type checking itself is in [checking](checking.md); values and the rule that a value fits a type are in [values](values.md).

| File | Content | Core |
| :- | :- | :- |
| `src/Great.Parts.Cel/Types/CelType.cs` | the model: factories, `Kind`, `Name`, `Parameters`, equality, text form | no |
| `src/Great.Parts.Cel/Types/CelTypeKind.cs` | the families of types | no |
| `src/Great.Parts.Cel/Types/AssignabilityRules.cs` | assignability, the most general of two types, the join of two types, erasure | yes |
| `src/Great.Parts.Cel/Types/SubstitutionRules.cs` | the substitution of type parameters: binding, lookup, applying it to a type | yes |

The rule files follow the naming and the content rules of [core](core.md).

## The model

| Type | Factory | `Kind` | `Name` | `Parameters` | Text form |
| :- | :- | :- | :- | :- | :- |
| `dyn`, `null_type`, `bool`, `int`, `uint`, `double`, `string`, `bytes` | static properties | the kind | the CEL name | — | the name |
| timestamp, duration | static properties `Timestamp`, `Duration` | `Timestamp`, `Duration` | `google.protobuf.Timestamp`, `google.protobuf.Duration` | — | `timestamp`, `duration`: the short names, as the messages of errors and the signatures of the standard library page write them |
| list | `ListOf(E)` | `List` | `list` | `E` | `list(E)` |
| map | `MapOf(K, V)` | `Map` | `map` | `K`, `V` | `map(K, V)` |
| wrapper | `WrapperOf(P)` | `Wrapper` | `google.protobuf.BoolValue`, `BytesValue`, `DoubleValue`, `Int64Value`, `UInt64Value`, `StringValue` | `P` | `wrapper(P)` |
| type of type values | `TypeOf(T)` | `Type` | `type` | `T` | `type(T)` |
| type parameter | `TypeParameter(name)` | `TypeParameter` | the name | — | the name |
| object type | `ObjectNamed(fullName)` | `Object` | the full name | — | the full name |

`WrapperOf` takes `bool`, `bytes`, `double`, `int`, `uint` and `string`; any other type is `ArgumentException`. `TypeOf(Dyn)` is the type of any type value; the identifier `type` has the static type `type(type(dyn))`. `ObjectNamed` only creates the reference: the environment rejects it as `TypeNotDeclared` while no type source describes it (see [declarations](declarations.md)). The text form is what error messages show; it is the notation of the language definition, and cel-go writes wrappers the same way.

`OptionalOf` arrives with the `optional` set and `Decimal` with `decimal`; until then they throw `NotImplementedException`. `Any` and the names of protobuf are on the two sections below.

Inside type checking two more forms exist and never leave it:

- **`error`** — a node with an error; `CelType?` with `null`, as in [checking](checking.md).
- **Free type variables** — type parameters named `_var0`, `_var1`, … that the checker creates per checked expression: the type of an empty list or map literal, and one fresh variable for each type parameter of an overload at each call. The names of the parameters of a declaration therefore never meet each other or the checker's variables. A checked expression replaces every type parameter that is still free by `dyn`, so its type map and its result type have none.

## Substitution

A substitution maps type parameters to types; it belongs to one checked expression. `SubstitutionRules`:

- **find** — the type bound to a parameter, if any;
- **bind** `P := T` — allowed only when `P` does not occur in `T` after the substitution is applied (the occurs check), so that no binding refers to itself;
- **apply** to a type — replaces each bound parameter by its binding, recursively, through list, map, wrapper and `type(…)` parameters; with the option *free to `dyn`* also replaces the parameters that remain free.

A trial against a substitution works on a copy: a failed trial leaves the substitution as it was, a successful one replaces it with the copy. Overload candidates, joins and assertions of the checker are all trials.

## Assignability

`IsAssignable(from, to, substitution, deducible)` tells whether a value of the type `from` can stand where the type `to` is expected, and may bind type parameters. `deducible` is the set of variables that a trial inside a type value may bind (rule 6): the fresh variables of the overload being tried when the checker tries a candidate, and empty everywhere else. The order of the checks is that of cel-go v0.32.0 `checker/types.go`, `internalIsAssignable`; type values and the most general type follow the language definition (gradual type checking, "Type-Checking Type Values") and the cases of `type_deduction.textproto`, not cel-go. The checks, in order:

1. `to` is a type parameter: when it is bound to `B`, the result is that of `from` against `B`, and on success the binding becomes `MostGeneral(from, B)` (below) if the occurs check allows; when it is free, it is bound to `from` if the occurs check allows, otherwise the rules below decide.
2. `from` is a type parameter: the same with the roles swapped, except that when the occurs check fails the answer is no.
3. Either is `dyn` or `error`: yes.
4. Either is `null_type`: yes when the other is `null_type`, a wrapper, an object type, `timestamp`, `duration`, or a type that the later parts add as nullable — `google.protobuf.Any`, `optional_type(T)` and `decimal`: cel-go takes every abstract (opaque) type as nullable, `isLegacyNullable` of `checker/types.go` in v0.32.0, together with object types, `Any`, `timestamp` and `duration`. This is the static rule, as in cel-go; at run time a `null` value fits neither `timestamp` nor `duration` ([values](values.md), "Fits a type"). `list`, `map`, `type` and the primitive types do not take `null`.
5. Primitive types: yes when they are equal. A wrapper and its primitive are assignable both ways: `wrapper(int)` takes `int`, and `int` takes `wrapper(int)`; a mismatch at evaluation is a value of the wrong kind, not a type error.
6. `type(A)` and `type(B)`: always yes — every type value is compatible where a type value is expected (language definition, "Type-Checking Type Values"). The parameters are tried too, `A` against `B` by these rules, as a trial that may bind only the variables in `deducible`. Any other type variable — of an empty literal, of an iteration variable, of an earlier call — is neither bound nor bound again in that trial: a bound one stands for its binding, a free one matches only itself, `dyn`, `error` and a free deducible variable, which rule 1 or 2 binds to it. When the trial succeeds its bindings are kept; when it fails nothing is bound, and the answer is still yes. In the join of a literal and in the check against the binding of a bound variable (rules 1 and 2) nothing is deducible, so a trial there binds nothing.

   So the type parameters of an overload are deduced through type values (language definition: `type(T)` is there "to support type parameter deduction across functions accepting or returning type values"): with the overload `(dyn, type(T)) -> T`, `cast('hello', int)` is `int`; with `(dyn, type(list(T))) -> T`, `first_elem_type('data', type([1]))` is `int`; with `(type(type(T))) -> type(T)`, `unwrap_type(type(int))` is `type(int)`. And a comparison of type values never narrows the type of an operand ("`type(x) == int` is valid even if `x` is statically `uint`"): in `[].exists(x, type(x) == int && x == 'a')` the variable `x` has the free variable `_var0` of the empty range and `type(x)` is `type(_var0)`; `_==_` binds its `A` to `type(_var0)`, then checks `type(int)` against that binding, which binds nothing, so `A` becomes `type(dyn)` and `_var0` stays free until `x == 'a'` binds it to `string`. The expression compiles with the two parts in either order, as in cel-go v0.32.0.
7. `list` with `list`, `map` with `map`: the parameters pairwise, `from` into `to`, each by these rules with the same substitution.
8. Object types: equal full names. Anything else: no.

**Most general.** `MostGeneral(a, b)` is the type kept where two types assignable to each other meet: a bound type parameter bound again (rule 1 — so the result of `_?_:_` and of `_+_` on lists) and the join of a literal (below). It builds the least upper bound of the two after the substitution; apart from rule 1 below, its result does not depend on the order of `a` and `b`:

1. Either is `dyn`: `dyn`. Otherwise either is a type parameter still free: that parameter, `a` when both are.
2. `type(A)` and `type(B)`: `type(A)` when `A` and `B` are equal, otherwise `type(dyn)` — whatever the order, and whether or not `A` or `B` holds free type parameters (language definition, "Type Generalization").
3. `list(A)` and `list(B)`: `list(MostGeneral(A, B))`; `map(K1, V1)` and `map(K2, V2)`: `map(MostGeneral(K1, K2), MostGeneral(V1, V2))`. A difference between type values deep inside is generalized where it is, and the rest is kept. The parameterized types of later parts (`optional_type(T)`, abstract types) follow the same rule, parameter by parameter.
4. A wrapper and its primitive: the wrapper. `null_type` and a type that takes `null` (rule 4 above): that type.
5. Otherwise the types are equal: `a`.

So `true ? type(1) : type(1u)` is `type(dyn)`: `_?_:_` binds its `A` to `type(int)`, then to `MostGeneral(type(uint), type(int))`. In `([] + [[type(1)]]) + [[type([])]]` the second `_+_` binds `A` to `list(type(int))`, then to `list(type(dyn))`, so the expression is `list(list(type(dyn)))`. `true ? w : 42` and `false ? w : null` with `w` of the type `wrapper(int)` are `wrapper(int)`. cel-go v0.32.0 keeps one of the two types (`mostGeneral`) and gives `type(uint)` and `list(list(type(int)))` for the two expressions with type values; static types are not part of the outcome that `eng/difftest` compares.

**Join.** The type of a list literal is `list(E)`, where `E` is the join of the element types from left to right; the same for the keys and the values of a map literal. The join of `E` so far and the next type `T` is `MostGeneral(E, T)` when `T` is assignable to `E`, otherwise `dyn`:

- `[1, 'a']` and `[1, null]` are `list(dyn)`; `[w, 1]` and `[1, w]` are `list(wrapper(int))`;
- `[int, bool, double]`, `[type([]), int]` and `[int, type([])]` are `list(type(dyn))`; `[[type(1)], [type(1u)]]` is `list(list(type(dyn)))`;
- `[type([]), type([1])]` is `list(type(dyn))`: the join binds nothing inside type values (rule 6), so the variable of `type([])` stays free, the two type values differ, and rule 2 of the most general type gives `type(dyn)`.

An empty list literal is `list(_varN)`, an empty map literal `map(_varN, _varM)`; the variables are bound by what the expression does with them, so `[] + [1]` is `list(int)`, and a free one becomes `dyn` at the end.

**Erasure.** Erasure replaces every type parameter by `dyn`. Two overload declarations overlap when they have the same call style and the same number of parameters and, after erasure, at each position one parameter type takes the other by rules 3–8 without a substitution (cel-go `SignatureOverlaps`; language definition, "Functions": "non-overlapping argument types, after erasure of all type variables"). So `(list(A))` overlaps `(list(int))`, `(dyn)` overlaps everything, `(int)` and `(wrapper(int))` overlap, `(list(int))` and `(list(string))` do not. The build errors that use it are in [declarations](declarations.md).

## `Any`

`CelType.Any` is the type `google.protobuf.Any`: `Kind` `Any`, `Name` `google.protobuf.Any`, no parameters, text form `any`, as the messages of cel-go write it. A value never has this type: an `Any` is unpacked to the message it holds when it is read ([protobuf-messages](protobuf-messages.md#any)), so there is no value kind `Any`, the runtime type of a value is that of the unpacked content, and `type(x)` of an expression of the static type `Any` is the type of that content. In the checker `Any` is `dyn` ([checking](checking.md#the-type-any-in-the-checker)); the rules of this page change in four places:

- **Assignability, rule 3.** Either side is `dyn`, `error` or `Any`: yes. This precedes the rule of `null`, so `null` is assignable to `Any` because everything is, and the clause of rule 4 that lists `Any` among the nullable types is kept only as the rule of cel-go that it copies.
- **Erasure.** Erasure leaves `Any` as it is; by rule 3 it takes every type and every type takes it, so `(Any)` overlaps `(int)` as `(dyn)` does.
- **Most general.** `Any` with `dyn` or with any other type is `dyn`; `Any` with `Any` is `Any`. A list literal `[x, 1]` with `x` of the type `Any` is `list(dyn)`.
- **Join** is `MostGeneral` as above, so a list or map literal with a member of the type `Any` has `dyn` elements.

A declaration that uses `Any` at any depth — a variable, a field, a parameter or a result of a function, a record `any` of a description in JSON — needs the type index of the environment to have an object type named `google.protobuf.Any`, which the protobuf package describes; otherwise `Build` reports `TypeNotDeclared` (4008) for the declaration, as for an object type that no source declares ([objects](objects.md#descriptions-of-well-known-types)).

## Well-known names

`CelType.ObjectNamed(fullName)` gives the CEL type that a name of protobuf stands for, when the name is one of thirteen. The rule is in `Types/WellKnownRules.cs`, which `ObjectNamed` calls; the table is the same as the one of the protobuf package for the fields of these messages ([protobuf](protobuf.md#the-known-types)):

| Full name | The type |
| :- | :- |
| `google.protobuf.Any` | `Any` |
| `google.protobuf.Value` | `dyn` |
| `google.protobuf.Struct` | `map(string, dyn)` |
| `google.protobuf.ListValue` | `list(dyn)` |
| `google.protobuf.BoolValue` | `wrapper(bool)` |
| `google.protobuf.BytesValue` | `wrapper(bytes)` |
| `google.protobuf.DoubleValue`, `google.protobuf.FloatValue` | `wrapper(double)` |
| `google.protobuf.Int32Value`, `google.protobuf.Int64Value` | `wrapper(int)` |
| `google.protobuf.UInt32Value`, `google.protobuf.UInt64Value` | `wrapper(uint)` |
| `google.protobuf.StringValue` | `wrapper(string)` |

Any other name, `google.protobuf.Timestamp` and `google.protobuf.Duration` among them, is the object type of that name as before; those two names are type identifiers of the standard library, and the types `timestamp` and `duration` come from `CelType.Timestamp` and `CelType.Duration`. The effect is the same wherever a name becomes a type: the record `"google.protobuf.Int32Value"` of a description in JSON, a `message_type` of the conformance runner, the `Type` of a `CelObjectType` that has such a name — its `Type` is the type of the table, not an object type, which is how the checker types the literal `google.protobuf.Int32Value{value: 1}` ([checking](checking.md#message-literals-of-constructible-types)). A declaration with such a name needs no source for it, except `Any`; a literal needs the description that the protobuf package makes.

## Implemented members

`CelType`: everything in the table above, `Equals`, `GetHashCode` and `ToString`; with `Any` and the rule of the well-known names the properties `CelType.Any` and `ObjectNamed` of this page. `CelTypeKind` has all its members already. A type parameter in the type of a variable declaration is erased to `dyn` when the environment is built (see [declarations](declarations.md)); only overload declarations bind type parameters.

Related pages: [checking](checking.md), [values](values.md), [declarations](declarations.md), [core](core.md).
