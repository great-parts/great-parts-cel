# Values: the value model and the value rules

`Great.Parts.Cel.Values` holds the public value model — `CelValue`, `CelValueKind`, `CelList`, `CelMap` and the types that later parts implement — and, in rule files, the value rules that evaluation relies on. This page covers what the walking skeleton needs: values of the kinds of literals, lists and maps built by literals, the error value, and the rules for map keys and for the conversion of values between .NET and CEL.

| File | Content | Core |
| :- | :- | :- |
| `src/Great.Parts.Cel/Values/CelValue.cs` | the structure, its layout, factories and `TryGet…` methods | no |
| `src/Great.Parts.Cel/Values/KindTag.cs` | the static kind tags of the 24-byte layout | no |
| `src/Great.Parts.Cel/Values/ValueArrayList.cs` | `CelList` over an array of values | no |
| `src/Great.Parts.Cel/Values/EntryTableMap.cs` | `CelMap` over a table of entries in the order of insertion | no |
| `src/Great.Parts.Cel/Values/ValueTypeNames.cs` | the names of the CEL types of value kinds in the text of error messages | no |
| `src/Great.Parts.Cel/Values/EqualityRules.cs` | CEL equality of map keys | yes |
| `src/Great.Parts.Cel/Values/KeyHashRules.cs` | which kinds can be map keys; their hash, consistent with equality | yes |
| `src/Great.Parts.Cel/Values/ConversionRules.cs` | .NET values of simple types to CEL values | yes |
| `src/Great.Parts.Cel/Values/ConversionRules.Result.cs` | CEL values to the requested .NET type | yes |

The rule files follow the naming and the content rules of [core](core.md): rule logic is only in them, and public members that expose a rule delegate to them.

## Layout of `CelValue`

`CelValue` is a `readonly struct` of a reference and 16 bytes of payload. Numbers, `bool`, `null` and, later, timestamps, durations and decimals live in the payload without allocation; strings, bytes, lists, maps, types and errors are references to immutable objects. The layout is not part of the public contract, and there are two variants of it. A measurement decides between them before the first code that creates values, the parser of literals; without a gain from the tag the library takes the explicit kind.

| | Kind tag, 24 bytes | Explicit kind, 32 bytes |
| :- | :- | :- |
| fields | `object? _ref`, `ulong _p0`, `ulong _p1` | the same, and `CelValueKind _kind` |
| kind of a payload value | `_ref` is a static `KindTag` object, one per kind | `_kind` |
| kind of a reference value | the type of `_ref`: `string`, `CelList`, `CelMap`, `CelError`, a byte array or a memory manager | `_kind` |
| `null` | `_ref` is `null` | `_kind` is `Null` |
| `Kind` | a test of the tag, then type tests of the reference | one field read |

What changes between them is only the private part of `CelValue.cs`: the fields, the private constructor of each kind and the getter of `Kind`; `KindTag.cs` exists only with the tag. The payload is the same in both. No code outside `CelValue.cs` reads the fields: the library, the rules and the evaluator read values through `Kind` and the `TryGet…` methods. So the choice does not touch any other file, and `default(CelValue)` is `null` in both. A test checks the size of the chosen layout.

## Kinds of literals

| Literal | `CelValueKind` | Storage |
| :- | :- | :- |
| `null` | `Null` | `default` |
| `true`, `false` | `Bool` | payload 0 or 1 |
| integer | `Int` | payload: the `long` |
| unsigned integer | `UInt` | payload: the `ulong` |
| floating | `Double` | payload: the bits of the `double` |
| string | `String` | the `string` |
| bytes | `Bytes` | the array or the memory manager under the `ReadOnlyMemory<byte>`; payload: the offset and the length |
| list literal | `List` | a `ValueArrayList` |
| map literal | `Map` | an `EntryTableMap` |

A bytes value is rebuilt as `ReadOnlyMemory<byte>` from its reference, offset and length, without copying. An error inside evaluation is a value of the kind `Error` whose reference is the `CelError`.

## Lists and maps of literals

`ValueArrayList` is a `CelList` over an array that only it holds: a list literal and `CelList.Create` fill the array once, and nothing changes it afterwards.

`EntryTableMap` is a `CelMap` over an array of entries in the order of insertion and an index from keys to positions. Enumeration walks the array, so the order of a map literal is the order of its text and never depends on hash codes. The index uses `KeyHashRules` and `EqualityRules`. A key of a kind that cannot be a map key is refused first; then a key that is equal by CEL equality to a key already present is refused, so `{1: 'a', 1.0: 'b'}` fails on the kind of `1.0`. A map literal turns these refusals into evaluation errors (see [evaluation](evaluation.md)); `CelMap.Create` turns them into `ArgumentException`, as its documentation says. `TryGetValue` finds a key by the same rules.

## Rules that the walking skeleton needs

- **Equality of keys** — `EqualityRules.cs`. Keys are `int`, `uint`, `bool` and `string`. `int`, `uint` and `double` compare on one number line, so `1` and `1u` are one key, and a lookup with `1.0` finds it; values of different non-numeric kinds are not equal, without an error; strings compare by their UTF-16 code units, which is equality of code points. The rest of CEL equality — lists, maps, bytes, NaN — and `CelValue.CelEquals` arrive with the equality operator.
- **Hash of keys** — `KeyHashRules.cs`. The hash of an `int`, a `uint` and a `double` with the same integral value is the same; a `double` without an integral value finds no key. The file also says which kinds can be keys.
- **Conversion into CEL** — `ConversionRules.cs`: the input rows of the type mapping table for the simple types that the facade lists for `CelBindings.Add<T>` and `AddVariable<T>` (see [facade](facade.md)).
- **Conversion out of CEL** — `ConversionRules.Result.cs`: the result rows for the simple `T` of the facade, with the error `ResultConversion` for a value that a row does not convert.

The other rule files — ordering, assignability with type parameters, the rules of time and `decimal` — come with the parts that need them, under the same naming.

## Members implemented in the walking skeleton

`CelValue`: `Kind`, `Null`, the factories and `TryGet…` methods of `bool`, `int`, `uint`, `double`, `string` and `bytes`, `FromList`, `FromMap`, `TryGetList`, `TryGetMap` and `TryGetError`. `CelList`: `Count`, the indexer, enumeration and `Create`. `CelMap`: `Count`, `TryGetValue`, enumeration and `Create`. .NET equality of `CelValue` — `Equals(CelValue)`, `Equals(object)`, `GetHashCode`, `==` and `!=` — as its documentation states: the same kind and content, NaN equal to itself, strings by ordinal comparison, bytes by content, lists, maps and errors by reference. It lives in `CelValue.cs`; evaluation does not use it, so it is no rule file. The other members keep throwing `NotImplementedException` until their part arrives: views over application data, time, types, optionals and objects, `decimal`, and CEL equality.

Related pages: [evaluation](evaluation.md), [library](library.md), [checking](checking.md), [core](core.md), [facade](facade.md).
