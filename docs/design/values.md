# Values: the value model and the value rules

`Great.Parts.Cel.Values` holds the public value model — `CelValue`, `CelValueKind`, `CelList`, `CelMap` and the types that later parts implement — and, in rule files, the value rules that evaluation relies on. This page covers every kind except time, `optional` and `decimal`, which arrive with their parts under the same rules; object values, type descriptions and their sources are in [objects](objects.md).

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
| `src/Great.Parts.Cel/Values/ConversionRules.Collections.cs` | the collections of the application: element and key conversion, the key lookup back to .NET, the order of enumeration | yes |
| `src/Great.Parts.Cel/Values/JsonRules.cs` | `JsonElement`: the kinds, numbers, the counting key lookup and the check of repeated names | yes |
| `src/Great.Parts.Cel/Values/ApplicationListView.cs`, `ApplicationMapView.cs`, `JsonArrayList.cs`, `JsonObjectMap.cs` | the adapters; they delegate every decision to the two rule files above | no |
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
| `Object` | the instance, or a pair of a description and the instance ([objects](objects.md)) | the object type of its description |
| `Error` | the `CelError` | — |

**Runtime type** — `ValueTypeRules.TypeOf(value)`, the value of the function `type` and the type names of evaluation messages: parameters are erased, as the language definition says for type values, so `type([1]) == list`. A type value holds the full `CelType` (`int`, `list(dyn)` for the identifier `list`), but CEL compares type values only by kind and `Name`. The text of a message names a runtime type without parameters, as `ValueTypeNames` writes it — `list`, `map`, `type`, `null_type` — so `no such field 'missing' in map`; an object value is named by the full name of its description, an inaccessible bare instance by its .NET type ([objects](objects.md)), resolved when the error value is created.

**Fits a type** — `ValueTypeRules.Fits(value, type, meter)`, at any depth: `dyn` and type parameters take any value; a primitive type takes a value of its kind; `wrapper(P)` takes `null` and a value of `P`; `null_type` takes `null`; `type(…)` takes a type value, whatever type it denotes, since type values compare by kind and `Name` only; an object type takes `null` and an object value of that description ([objects](objects.md), "Fits"); an error value fits nothing. `list(E)` takes a list whose every element fits `E`, and `map(K, V)` a map whose every key fits `K` and every value fits `V`; the check descends only into a parameter that is neither `dyn` nor a type parameter, so `list(dyn)` and `map(string, dyn)` cost the top level and the keys only. Before it descends into a list of n elements or a map of n entries it charges n units to the meter (the cost table of the documentation, row "check against a declared type"); with no meter nothing is counted. On a mismatch it returns the type that the messages of 3011, 3015 and 3028 name as the actual type: at the top level the name of the type of the value (`string`, `list`); deeper the checked type with the parameter at the path of the first element, key or value that does not fit, in the order of enumeration, replaced by the name of the type of that element — `list(string)` for a list with a string checked against `list(int)`, `map(string, dyn)` for `{'a': 1}` against `map(int, dyn)`, `map(string, list(string))` for `{'a': ['b']}` against `map(string, list(int))`. A caller may let `null` also fit the positions that it names, as the facade does before a lazy view; the type that a mismatch returns is then written without that addition.

Evaluation uses it where a value comes from outside the expression or leaves it: bindings against the declared type of a variable (`BindingTypeMismatch`, 3011); the arguments of application functions against the declared parameter types, and their results against the declared result type, type parameters as `dyn` (`EvalNoMatchingOverload`; `ImplementationResultMismatch`, 3028); the arguments that a static type does not cover (see [evaluation](evaluation.md), "Calls"); and the value of the root before a lazy view, against the expected type of `T`, which names what the view reads, with `null` also fitting the positions that the view gives as `string`, `byte[]`, `CelList`, `CelMap` or `CelType` ([facade](facade.md), "Result conversion"). **A static type is no guarantee of the value.** Gradual type checking lets a `dyn` value into a position whose type a type parameter, bound by another argument, gives, and nothing generalizes the parameter ([types](types.md), "Assignability", rule 3): `[1] + dyn(['a'])` has the type `list(int)` and the value `[1, 'a']`, and `([1] + dyn(['a']))[1]` the type `int` and the value `'a'`, as in cel-go v0.32.0. So no code relies on a static type for the kind of a value it reads: library implementations check the kinds they read ([library](library.md)), application code gets checked arguments, and the result conversion checks a collection before it returns a lazy view. A view that the conversions create from a typed .NET collection records the CEL type its .NET type gives, such as `list(int)` for an adapter of `IReadOnlyList<long>`; when that type is assignable to the checked one without `dyn`, the view already fits, and the check does not descend into it and charges nothing. Other views — those of `CelList.CreateView` and `CelMap.CreateView`, whose converters return any value, and JSON — are checked like the lists of this part, reading the elements; an error element met there is the result of the check, with the position of the node that checks, not the mismatch code.

## Lists and maps

`ValueArrayList` is a `CelList` over an array that only it holds: list literals, `CelList.Create`, concatenation and the results of `map` and `filter` fill an array once, and nothing changes it afterwards. `EntryTableMap` is a `CelMap` over an array of entries in the order of insertion; enumeration walks the array, so the order never depends on hash codes. Keys compare by `EqualityRules`. A map of n entries takes at most 64 · (1 + 2n) bytes, the bound of its cost ([evaluation](evaluation.md), "Cost"), and finds a key in constant time: the empty map is one instance for the process; a map of up to 8 entries has no index and compares the key with each entry; a larger map has an index of positions with open addressing by `KeyHashRules`, not a `Dictionary`, whose entry alone takes 48 bytes. The hash of an integral key has a random seed for the process, as that of a string has, so that keys chosen to collide do not make a lookup linear. A key of a kind that cannot be a map key is refused first, then a key equal to one already present: a map literal turns the refusals into evaluation errors (see [evaluation](evaluation.md)), `CelMap.Create` into `ArgumentException`. `CelList.Create` and `CelMap.Create` also throw `ArgumentException` for an error value among the elements or values — an error is a result, not a value of a CEL type, as for `CelList.CreateSorted` — so no list or map of the library contains an error value: literals, concatenation, `map` and `filter` are strict and never put one in.

## Collections of the application

The collection rows of the type mapping table, by `ConversionRules.Collections.cs`. Each adapter is immutable and serves any number of threads at once; it reflects its source, and a source changed during an evaluation gives an undefined result.

| API | Count, index, lookup | Order | Errors |
| :- | :- | :- | :- |
| `CelList.CreateView(source, converter)` | `source.Count`; an element is `converter(source[i])` at each read, in O(1); nothing is copied | the order of `source` | an error value returned, or an exception of `source` or of the converter other than `OutOfMemoryException`, which becomes `ApplicationException` with the callee `a list of the application`, is the element |
| `CelList.CreateCopy(source, converter)` | converts and copies now, in the order of enumeration | that order | as for the view, kept as elements |
| `CelList.CreateSorted(source, converter)` | converts and copies now, then sorts by `OrderingRules`, strings by code points | ascending | `ArgumentException` for `null`, an error value, two kinds, a NaN or two elements equal in CEL; exceptions pass to the caller |
| `CelMap.CreateView(source, converter, keepSourceOrder)` | `source.Count`; a lookup converts the CEL key back to `TKey` and makes one lookup in `source`; a value is `converter(value)` at each read | `keepSourceOrder`, or `source` a `Dictionary`, `OrderedDictionary`, `SortedDictionary`, `SortedList` or `ImmutableSortedDictionary`: the order of `source`; otherwise ascending CEL keys, sorted at each enumeration | values as for the list view; a key of an enumeration over `ulong` above `long.MaxValue` is `ConversionRange` when an operation meets it |

**Keys** to CEL: `string` as is; `Guid` as the string of format `D`, lower case; signed integers and enumerations as `int`; unsigned integers as `uint`; `bool`. Any other `TKey` is `ArgumentException` of `CreateView`. **Keys back** to `TKey`, for a lookup, `in` and `has`: a CEL key of another kind than the one `TKey` gives, or out of the range of `TKey`, finds nothing, without an error; the numeric keys meet on one number line as in "Map keys", so the key `1` of a dictionary over `int` is found by `1`, `1u` and `1.0`. A `Guid` key is found only by the canonical string — 36 characters, format `D`, lower case — exactly the string the enumeration gives: `Guid.TryParseExact` alone would also take upper case, so `{…}` braces, upper case and the formats `N`, `B`, `P` find nothing, and a map of `Guid` keys behaves as a map of its canonical strings.

**Error elements.** An element or a value of a collection of the application may be an error value, unlike a collection of the library. An operation that reads it — an index, a key lookup, a comprehension over it, `in`, equality, the check against a declared type, the result conversion — gets it as the value read: an index or a lookup returns it, a comprehension puts it into the iteration variable, equality and `in` return it. An error value that application data gives has no position of its own; the node that read it gives it its position. When several element errors meet in the check against a declared type or in the result conversion, the first one in the order of the check walk is returned (a list — by index, a map — in the order of iteration). `size` reads no element.

**Input rows of objects and collections** — the conversions of bindings, of `AddVariable<T>` and of the delegate forms of `AddFunction`, by the static type:

| .NET | CEL type | Value |
| :- | :- | :- |
| `T[]`, `IReadOnlyList<T>`, `IList<T>`, `List<T>`, `ImmutableArray<T>`, `ImmutableList<T>`, `ReadOnlyCollection<T>` | `list(E)`, `T` → `E` by these rows | a typed view, lazy |
| `IReadOnlySet<T>`, `ISet<T>` | `list(E)` | sorted as by `CreateSorted` at the conversion; elements that `CreateSorted` refuses are `ArgumentException` of `CelBindings.Add`, and in the result of an application function `InvalidArgument` (3008) at the call ([evaluation](evaluation.md), "Application functions") |
| other `IReadOnlyCollection<T>` | `list(E)` | copied as by `CreateCopy` at the conversion |
| `IReadOnlyDictionary<K, V>`, `IDictionary<K, V>`, `Dictionary<K, V>` | `map(K', V')`, keys as above | a typed map view, lazy; another key type is `TypeNotDeclared` at `Build` and `ArgumentException` of `CelBindings.Add`. Bound to a variable of an object type `T` whose carrier class the source is an instance of — `IReadOnlyDictionary<string, object?>` for a type of `ForDictionary` — the view is its source, a carrier of `T`, by the place rule ([objects](objects.md)) |
| `JsonElement` | `dyn` | "`JsonElement`" below; `default(JsonElement)` is `ArgumentException` of `CelBindings.Add` |
| a class, record, struct or interface outside `System` | the object type that a type source describes for it by the type index ([objects](objects.md)), resolved at `Build`; none is `TypeNotDeclared` | a bare instance, a struct boxed once at the conversion; the carrier of a type described at run time too, which the place rule pairs at the first read of a variable of that type |

A typed collection nests one step deep, as the result rows do: `IReadOnlyList<IReadOnlyList<long>>` is outside the rows. A view of these rows keeps its source and records that the rows made it, which a view of `CelList.CreateView` or `CelMap.CreateView` does not: the place rule looks through the first only. The `object` row rejects no instance: a boxed `CelValue`, `CelList` or `CelMap` is taken as it is, and any other instance outside the rows, a .NET collection included, is a bare object value. `CelBindings` knows no environment, so the node that reads the value decides with the type index, by the steps of [objects](objects.md), "Reading a value of the application": a carrier of the declared object type of the place; else a described instance, though enumerable; else a .NET collection — `System.Collections.IEnumerable` except `string`, `byte[]`, `CelList` and `CelMap` — is `TypeNotAccessible` (3012) with its .NET type at the identifier, the selection or the call; else a bare object described lazily. An element or a value of a collection of the application takes none of these steps and is described lazily. Objects in a `dyn` place are described by their .NET type ([objects](objects.md)).

## `JsonElement` — `JsonRules`

The JSON data conversion of the language definition, read lazily: the conversion looks at the kind of the element it reads, and only there.

| `JsonValueKind` | Value |
| :- | :- |
| `Null`, `True`, `False` | `null`, `bool` |
| `Number` | `double` by `TryGetDouble`, integers included; a number that is not a finite `double` is `ConversionRange` `{text} is out of the range of double`, `{text}` the number as written in the document |
| `String` | a new `string` at each read |
| `Array` | a `CelList` adapter of the element: count, index, enumeration in document order |
| `Object` | a `CelMap` adapter of the element, `map(string, dyn)`, enumeration in document order |
| `Undefined` (`default(JsonElement)`) | only from the application, never inside a document: a field value or the result of an application function is `InvalidArgument` (3008) `invalid argument to '{name}': undefined JSON element`, `{name}` `<type>.<field>` or the overload identifier, at the selection or the call |

**Repeated names.** A key lookup — an index, a selection, `has` — compares the key ordinally with every property name of the object, escape sequences decoded, and counts the matches: 0 is no key, 1 the value, 2 or more `DuplicateMapKey` (3010) with that key. `size`, enumeration, `in` and equality check every name for a repeat, by hashing with the random seed of `KeyHashRules`, before their result; a repeat is 3010 with the first name, in document order, that repeats an earlier one. The key of the text of 3010 is written as a string literal ([evaluation](evaluation.md)). A key that is not a `string` finds nothing. A JSON object equals a map by the rule of maps, with this lookup.

An `ObjectDisposedException` of an element whose document the application disposed is `ApplicationException` with the callee `a JSON element`. `JsonElement` is never a result: a JSON value converts by the `list`, `map` and primitive result rows.

## Equality — `EqualityRules`

CEL equality, `==` of the library and `CelValue.CelEquals`, by the language definition, "Equality", and the cases of cel-spec:

- `int`, `uint` and `double` compare on one number line: two numbers are equal when neither is less than the other by the ordering below. So `1 == 1u == 1.0`, and an `int` and a `double` compare as the `double` and the `int` rounded to the nearest `double` — the cel-spec cases `comparisons/…_lossy_double` require it. A NaN equals nothing, itself included.
- Values of different non-numeric kinds are not equal, without an error; `null` equals only `null`.
- `bool` by value; strings by their UTF-16 code units, which is equality of code points; bytes by content.
- Type values by kind and `Name`.
- Lists: the same count and the elements pairwise equal. Maps: the same count, and each key of the left map is found in the right one by the key lookup below with an equal value; a JSON object checks its names for repeats first and is searched by its counting lookup ("`JsonElement`"). An error element of a collection of the application is the result when the comparison reaches it.
- Objects: by their descriptions and fields ([objects](objects.md), "Equality of objects").
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
| `object` | `dyn` | by the row of its runtime type; a boxed `CelValue`, `CelList` or `CelMap` as it is; a `JsonElement` by its section; another instance, a .NET collection included, as a bare object value, which the reading node decides by the steps of [objects](objects.md), "Reading a value of the application"; `null` is the `null` value |

A `null` reference of every reference type of these rows is the `null` value, whatever the static type. It fits none of `string`, `bytes`, `list` and `map` ("Fits a type"), so a `null` `CelList` bound to a variable of type `list(dyn)` is `BindingTypeMismatch` with `null_type` when the variable is read, and a typed delegate that returns a `null` `string`, `byte[]`, `CelList` or `CelMap` is `ImplementationResultMismatch` ([evaluation](evaluation.md), "Application functions"). `CelBindings.Add` does not reject it.

A value that the conversion refuses becomes an error value where it is read — a binding when the expression reads the variable, a result of an application function at the call — never an exception of `CelBindings.Add`, except the conditions documented on the method. The collection, object and `JsonElement` rows are in "Collections of the application" and "`JsonElement`" below. Time and `decimal` come with their parts; until then `ConversionRules` throws `NotImplementedException` for them, and `AddVariable<T>` and `AddFunction` report them as `TypeNotDeclared` (see [declarations](declarations.md)). The kinds of .NET types that are never read — the table of types not read of the type mapping page, such as `char`, `Int128` and the classes of `System` without a row — are `TypeNotDeclared` as well.

**CEL to .NET** — `ConversionRules.Result.cs`: the result rows of the type mapping table, which the facade describes with the expected type (see [facade](facade.md)), and the same rows for the parameters of delegate implementations: a refusal there is `ArgumentConversion` (3024) with the function, the overload identifier and the parameter. That is a refusal by the value only, such as an `int` outside `Int32`: the CEL type of a parameter is the one that the expected-type table derives from its .NET type, and a parameter whose type is not the declared one — `Guid`, `short`, `float`, an enumeration, `CelList` for `list(int)`, a typed collection nested deeper than one step ([facade](facade.md), "Expected type") — is `ImplementationSignatureMismatch` at `Build` ([declarations](declarations.md)). So `IReadOnlyList<long>` and `IReadOnlyList<int>` take `list(int)`, by the pairs of the list row. A declared `wrapper(string)` or `wrapper(bytes)` also takes `string` or `byte[]`, to which the `null` value passes as a `null` reference.

## Members implemented in this part

`CelValue`: `Kind`, `Null`, the factories and `TryGet…` methods of every kind on this page, `FromType` and `TryGetType`, `FromObject` and `TryGetObject` ([objects](objects.md)), `CelEquals`, and .NET equality — the same kind and content, NaN equal to itself, strings ordinal, bytes by content, types by `CelType.Equals`, lists, maps and errors by reference, objects by the instance and the description of a pair; evaluation does not use it, so it is no rule file. `CelList`: `Count`, the indexer, enumeration, `Create`, `CreateView`, `CreateCopy`, `CreateSorted`. `CelMap`: `Count`, `TryGetValue` by the key lookup above, enumeration, `Create`, `CreateView`. The members of time, `optional` and `decimal` keep throwing `NotImplementedException` until their part.

Related pages: [objects](objects.md), [types](types.md), [evaluation](evaluation.md), [library](library.md), [checking](checking.md), [core](core.md), [facade](facade.md).
