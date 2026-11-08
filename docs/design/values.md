# Values: the value model and the value rules

`Great.Parts.Cel.Values` holds the public value model — `CelValue`, `CelValueKind`, `CelList`, `CelMap` and the types that later parts implement — and, in rule files, the value rules that evaluation relies on. This page covers every kind except time, `optional`, objects and `decimal`, which arrive with their parts under the same rules.

| File | Content | Core |
| :- | :- | :- |
| `src/Great.Parts.Cel/Values/CelValue.cs` | the structure, factories, `TryGet…` methods, `CelEquals`, .NET equality | no |
| `src/Great.Parts.Cel/Values/ValueArrayList.cs` | `CelList` over an array of values | no |
| `src/Great.Parts.Cel/Values/EntryTableMap.cs` | `CelMap` over a table of entries in the order of insertion | no |
| `src/Great.Parts.Cel/Values/ValueTypeNames.cs` | the names of the CEL types of value kinds in the text of error messages | no |
| `src/Great.Parts.Cel/Values/EqualityRules.cs` | CEL equality | yes |
| `src/Great.Parts.Cel/Values/OrderingRules.cs` | CEL ordering | yes |
| `src/Great.Parts.Cel/Values/KeyHashRules.cs` | which kinds are map keys; their hash; the lookup of a key of another numeric kind | yes |
| `src/Great.Parts.Cel/Values/ValueTypeRules.cs` | the runtime type of a value; whether a value fits a declared type, at any depth | yes |
| `src/Great.Parts.Cel/Values/ConversionRules.cs` | .NET values to CEL values; the CEL type of a .NET type | yes |
| `src/Great.Parts.Cel/Values/ConversionRules.Result.cs` | CEL values to a requested .NET type | yes |
| `src/Great.Parts.Cel/Values/DoubleTextRules.cs` | the shortest digits of a double that read back to the same double | yes |

The rule files follow the naming and the content rules of [core](core.md): rule logic is only in them, and public members that expose a rule delegate to them.

## Kinds

`CelValue` is a `readonly struct` of 32 bytes: a reference, 16 bytes of payload and an explicit `CelValueKind` (the layout that the measurement of the kind tag chose; it is not part of the public contract, and no code outside `CelValue.cs` reads the fields).

| Kind | Storage | Runtime type |
| :- | :- | :- |
| `Null` | `default` | `null_type` |
| `Bool`, `Int`, `UInt`, `Double` | payload | `bool`, `int`, `uint`, `double` |
| `String` | the `string` | `string` |
| `Bytes` | the array or memory manager under a `ReadOnlyMemory<byte>`; payload: offset and length | `bytes` |
| `List` | a `CelList` | `list(dyn)` |
| `Map` | a `CelMap` | `map(dyn, dyn)` |
| `Type` | the `CelType` it denotes | `type(dyn)` |
| `Error` | the `CelError` | — |

**Runtime type** — `ValueTypeRules.TypeOf(value)`, the value of the function `type` and the type names of evaluation messages: parameters are erased, as the language definition says for type values, so `type([1]) == list`. A type value holds the full `CelType` (`int`, `list(dyn)` for the identifier `list`), but CEL compares type values only by kind and `Name`. The text of a message names a runtime type without parameters, as `ValueTypeNames` writes it — `list`, `map`, `type`, `null_type` — so `no such field 'missing' in map`.

**Fits a type** — `ValueTypeRules.Fits(value, type, meter)`, at any depth: `dyn` and type parameters take any value; a primitive type takes a value of its kind; `wrapper(P)` takes `null` and a value of `P`; `null_type` takes `null`; `type(…)` takes a type value, whatever type it denotes, since type values compare by kind and `Name` only; an error value fits nothing. `list(E)` takes a list whose every element fits `E`, and `map(K, V)` a map whose every key fits `K` and every value fits `V`; the check descends only into a parameter that is neither `dyn` nor a type parameter, so `list(dyn)` and `map(string, dyn)` cost the top level and the keys only. Before it descends into a list of n elements or a map of n entries it charges n units to the meter (the cost table of the documentation, row "check against a declared type"); with no meter nothing is counted. On a mismatch it returns the type that the messages of 3011, 3015 and 3028 name as the actual type: at the top level the name of the type of the value (`string`, `list`); deeper the checked type with the parameter at the path of the first element, key or value that does not fit, in the order of enumeration, replaced by the name of the type of that element — `list(string)` for a list with a string checked against `list(int)`, `map(string, dyn)` for `{'a': 1}` against `map(int, dyn)`, `map(string, list(string))` for `{'a': ['b']}` against `map(string, list(int))`. A caller may let `null` also fit the positions that it names, as the facade does before a lazy view; the type that a mismatch returns is then written without that addition.

Evaluation uses it where a value comes from outside the expression or leaves it: bindings against the declared type of a variable (`BindingTypeMismatch`, 3011); the arguments of application functions against the declared parameter types, and their results against the declared result type, type parameters as `dyn` (`EvalNoMatchingOverload`; `ImplementationResultMismatch`, 3028); the arguments that a static type does not cover (see [evaluation](evaluation.md), "Calls"); and the value of the root before a lazy view, against the expected type of `T`, which names what the view reads, with `null` also fitting the positions that the view gives as `string`, `byte[]`, `CelList`, `CelMap` or `CelType` ([facade](facade.md), "Result conversion"). **A static type is no guarantee of the value.** Gradual type checking lets a `dyn` value into a position whose type a type parameter, bound by another argument, gives, and nothing generalizes the parameter ([types](types.md), "Assignability", rule 3): `[1] + dyn(['a'])` has the type `list(int)` and the value `[1, 'a']`, and `([1] + dyn(['a']))[1]` the type `int` and the value `'a'`, as in cel-go v0.32.0. So no code relies on a static type for the kind of a value it reads: library implementations check the kinds they read ([library](library.md)), application code gets checked arguments, and the result conversion checks a collection before it returns a lazy view. Later parts add views over collections of the application: a view whose CEL type comes from its .NET type, such as an adapter of `IReadOnlyList<long>` for `list(int)`, already fits its type, and the check does not descend into it and charges nothing; other views are checked like the lists of this part.

## Lists and maps

`ValueArrayList` is a `CelList` over an array that only it holds: list literals, `CelList.Create`, concatenation and the results of `map` and `filter` fill an array once, and nothing changes it afterwards. `EntryTableMap` is a `CelMap` over an array of entries in the order of insertion; enumeration walks the array, so the order never depends on hash codes. Keys compare by `EqualityRules`. A map of n entries takes at most 64 · (1 + 2n) bytes, the bound of its cost ([evaluation](evaluation.md), "Cost"), and finds a key in constant time: the empty map is one instance for the process; a map of up to 8 entries has no index and compares the key with each entry; a larger map has an index of positions with open addressing by `KeyHashRules`, not a `Dictionary`, whose entry alone takes 48 bytes. The hash of an integral key has a random seed for the process, as that of a string has, so that keys chosen to collide do not make a lookup linear. A key of a kind that cannot be a map key is refused first, then a key equal to one already present: a map literal turns the refusals into evaluation errors (see [evaluation](evaluation.md)), `CelMap.Create` into `ArgumentException`. `CelList.Create` and `CelMap.Create` also throw `ArgumentException` for an error value among the elements or values — an error is a result, not a value of a CEL type, as for `CelList.CreateSorted` — so no list or map of this part contains an error value: literals, concatenation, `map` and `filter` are strict and never put one in. The views of later parts over collections of the application get their rule for error elements with their part.

Views over collections of the application (`CelList.CreateView`, `CreateCopy`, `CreateSorted`, `CelMap.CreateView`) are the collection rows of the type mapping table and come with application data.

## Equality — `EqualityRules`

CEL equality, `==` of the library and `CelValue.CelEquals`, by the language definition, "Equality", and the cases of cel-spec:

- `int`, `uint` and `double` compare on one number line: two numbers are equal when neither is less than the other by the ordering below. So `1 == 1u == 1.0`, and an `int` and a `double` compare as the `double` and the `int` rounded to the nearest `double` — the cel-spec cases `comparisons/…_lossy_double` require it. A NaN equals nothing, itself included.
- Values of different non-numeric kinds are not equal, without an error; `null` equals only `null`.
- `bool` by value; strings by their UTF-16 code units, which is equality of code points; bytes by content.
- Type values by kind and `Name`.
- Lists: the same count and the elements pairwise equal. Maps: the same count, and each key of the left map is found in the right one by the key lookup below with an equal value.
- An error value as an operand is the result. Nested lists and maps check the stack before each level; `false` is `InsufficientStack` (`insufficient execution stack to continue comparing values`) at the call.

## Ordering — `OrderingRules`

The result is less, equal, greater or unordered; the library turns it into the `bool` of `<`, `<=`, `>`, `>=`, and unordered into `false`, as the standard library page says. Types that have no ordering — lists, maps, `null`, types — never reach it: the library has no overload for them.

- `bool`: `false < true`. `int`, `uint`, `double` among themselves as numbers. `string` by code points. `bytes` by unsigned byte values, a prefix first.
- Across numeric kinds, as cel-go v0.32.0 `common/types/compare.go` and the cel-spec cases `comparisons/…_big_…` require: `int` with `uint` exactly; `int` with `double` — a `double` below −2⁶³ is less, above 2⁶³ greater, otherwise the `int` rounded to `double` is compared; `uint` with `double` — a negative `double` is less, above 2⁶⁴ greater, otherwise the `uint` rounded to `double` is compared.
- A NaN on either side is unordered.
- **Strings by code points.** Compare UTF-16 code units up to the first difference; when both differing units are at least U+D800, first map a unit at U+E000 or above to itself minus U+0800 and a surrogate to itself plus U+2000 (the code point order fix-up of the Unicode Standard, section 5.17). `string.CompareOrdinal` alone orders U+E000–U+FFFF after supplementary characters, against the language definition and cel-go.

## Map keys — `KeyHashRules`

Keys are `int`, `uint`, `bool` and `string`. An `int` and a `uint` with the same value are one key and have one hash; strings hash by ordinal comparison. A lookup — index, `in`, `has` on a map, the equality of maps — with:

- an `int` finds an `int` key, or a `uint` key when it is not negative; a `uint` the other way round;
- a `double` converts first, without loss, to `int` when it has an integral value inside the open range of `int` (the limits page: `double` to `int`, bounds excluded), and looks that up, else to `uint` when it has an integral value in [0, 2⁶⁴); otherwise it finds nothing, so `{1: 'a'}[dyn(1.0)]` is `'a'` and `{1: 'a'}[dyn(1.5)]` has no key;
- any other kind finds nothing.

## Conversions — `ConversionRules`

**.NET to CEL** — the input rows of the type mapping table for values, without time and `decimal`. The same rows give the CEL type that `AddVariable<T>` and the delegate forms of `AddFunction` infer from a .NET type, as the rules of that table for bindings and application functions say.

| .NET | CEL type | Value |
| :- | :- | :- |
| `long`, `int`, `short`, `sbyte`, `nint` | `int` | exact |
| `ulong`, `uint`, `ushort`, `byte`, `nuint` | `uint` | exact |
| `double`, `float`, `Half` | `double` | exact widening |
| `bool` | `bool` | — |
| `string` | `string` | the same instance; `null` is the `null` value |
| `Guid` | `string` | format `D`, lower case |
| an enumeration, `[Flags]` included | `int` | the underlying value; above the range of `int` (an enumeration on `ulong`) the value is the evaluation error `ConversionRange` |
| `byte[]`, `ReadOnlyMemory<byte>` | `bytes` | without copying; a `null` array is the `null` value |
| `Nullable<X>`, `X` a value type above | `wrapper` of the CEL type of `X` | `null` or the value of `X` |
| `CelValue` | `dyn` | as it is |
| `CelList`, `CelMap` | `list(dyn)`, `map(dyn, dyn)` | as they are; a `null` reference is the `null` value |
| `object` | `dyn` | by the row of its runtime type; a boxed `CelValue`, `CelList` or `CelMap` as it is; `null` is the `null` value |

A `null` reference of every reference type of these rows is the `null` value, whatever the static type. It fits none of `string`, `bytes`, `list` and `map` ("Fits a type"), so a `null` `CelList` bound to a variable of type `list(dyn)` is `BindingTypeMismatch` with `null_type` when the variable is read, and a typed delegate that returns a `null` `string`, `byte[]`, `CelList` or `CelMap` is `ImplementationResultMismatch` ([evaluation](evaluation.md), "Application functions"). `CelBindings.Add` does not reject it.

A value that the conversion refuses becomes an error value where it is read — a binding when the expression reads the variable, a result of an application function at the call — never an exception of `CelBindings.Add`, except the conditions documented on the method. A .NET type outside the rows — objects, collections, `JsonElement`, time, `decimal` — comes with its part; until then `ConversionRules` throws `NotImplementedException` for it, and `AddVariable<T>` and `AddFunction` report it as `TypeNotDeclared` (see [declarations](declarations.md)).

**CEL to .NET** — `ConversionRules.Result.cs`: the result rows of the type mapping table, which the facade describes with the expected type (see [facade](facade.md)), and the same rows for the parameters of delegate implementations: a refusal there is `ArgumentConversion` (3024) with the function, the overload identifier and the parameter. That is a refusal by the value only, such as an `int` outside `Int32`: the CEL type of a parameter is the one that the expected-type table derives from its .NET type, and a parameter whose type is not the declared one — `Guid`, `short`, `float`, an enumeration, `CelList` for `list(int)`, a typed collection nested deeper than one step ([facade](facade.md), "Expected type") — is `ImplementationSignatureMismatch` at `Build` ([declarations](declarations.md)). So `IReadOnlyList<long>` and `IReadOnlyList<int>` take `list(int)`, by the pairs of the list row. A declared `wrapper(string)` or `wrapper(bytes)` also takes `string` or `byte[]`, to which the `null` value passes as a `null` reference.

## Members implemented in this part

`CelValue`: `Kind`, `Null`, the factories and `TryGet…` methods of every kind on this page, `FromType` and `TryGetType`, `CelEquals`, and .NET equality — the same kind and content, NaN equal to itself, strings ordinal, bytes by content, types by `CelType.Equals`, lists, maps and errors by reference; evaluation does not use it, so it is no rule file. `CelList`: `Count`, the indexer, enumeration, `Create`. `CelMap`: `Count`, `TryGetValue` by the key lookup above, enumeration, `Create`. The members of time, `optional`, objects, `decimal` and the views keep throwing `NotImplementedException` until their part.

Related pages: [types](types.md), [evaluation](evaluation.md), [library](library.md), [checking](checking.md), [core](core.md), [facade](facade.md).
