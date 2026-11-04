# Standard library

This page lists what the standard library `CelStandardLibrary.V1` declares and how each function behaves: the type identifiers, the macros, and the functions and operators with their overloads. An environment names the version it uses in `new CelEnvironmentBuilder(CelStandardLibrary.V1)`.

Not in this version of the page: the functions `timestamp` and `duration` with their conversions, arithmetic and receivers, the function `matches`, and the extension sets, which have their own page. They are added to the library in a later release; until then a call to them is a type checking error.

The sources are the language definition of CEL (`doc/langdef.md` of cel-spec), cited by section title, and the CEL implementation in Go, cel-go v0.32.0, which is the reference where the language definition is silent. The costs of the overloads are on the page [Cost](cost.md), the error codes on the page [Error codes](error-codes.md), and the limits on the page [Limits](limits.md).

## Version 1

- The set of declarations of version 1 never changes. A new function of the standard library comes with a new version; a fix of behavior applies to every version.
- An application can add an overload to a function of the standard library when it does not overlap one of the library. An application does not overload operators: the name of an application function is an identifier or a qualified name, and a name such as `_+_`, `_[_]`, `@in` or `@not_strictly_false` is the build error `InvalidFunctionName` (4015).
- An overload of the application that coincides with or overlaps an overload of the library after the type parameters are erased, a variable or a type of the application named like a type identifier below, and a function of the application with the name and form of a macro below are the build error `StandardLibraryConflict` (4006), which names both sources.
- The tables of version 1 are built once per process and are immutable; an environment merges them with the declarations of the application when it is built.

## How to read the tables

- **Identifier** is the overload identifier of cel-go, letter for letter. An application registers the implementation of its own overloads by their identifiers; the identifiers of the library are the same as in cel-go, so the lists of overloads of the two libraries compare without exceptions.
- **Signature** is written as in the language definition: `(int, int) -> int` is a global call, `string.contains(string) -> bool` is a call on a receiver. `A` and `B` are type parameters.
- **Function** is the name of the function in the syntax tree. Operators have the names `_+_`, `_<_`, `@in`, `_[_]` and so on. The names of operators are not part of the public API; they are listed to compare the lists.
- **Errors** are the members of `CelErrorCode` with their numbers. `NoMatchingOverload` of type checking (2001) and `EvalNoMatchingOverload` of evaluation (3000) are different members. The error `CostLimitExceeded` (3013) and cancellation can occur in any operation and are not repeated.
- **Type checking errors common to all overloads.** A call for which the static types leave no overload is `NoMatchingOverload` (2001); an unknown name is `UndeclaredReference` (2000). When an operand has the static type `dyn`, type checking keeps all overloads that fit as candidates and evaluation chooses among them by the types of the values; when none fits, the error is `EvalNoMatchingOverload` (3000).

## Type identifiers

An identifier evaluates to a value of the type `type`; its static type is `type(T)`. A variable or a type of the application with the name of an identifier is `StandardLibraryConflict`.

| Identifier | Value |
| :- | :- |
| `bool`, `bytes`, `double`, `int`, `uint`, `string` | the type of the same name |
| `list` | the type `list`; the parameter is erased, so `type([1]) == list` |
| `map` | the type `map`; the parameters are erased |
| `null_type` | the type `null_type` |
| `type` | the type `type` |

`dyn` is not an identifier: the name `dyn` is only the function `dyn` below, as in cel-go and in the test data of cel-spec. The names `timestamp` and `duration` are functions, not type identifiers. The wrappers and the well-known types of protobuf are not part of the standard library; their names come from type providers.

## Macros

A macro is expanded when the text is parsed into a comprehension over its range, so the macros are part of the syntax, not calls. A call that a macro cannot expand, such as `has(x)` without a field selection or `all(1, true)` without an identifier, is the parse error `InvalidMacroCall` (1006). The cost of a macro is on the page [Cost](cost.md#macros).

| Macro | Form | Signature | Semantics | Evaluation errors |
| :- | :- | :- | :- | :- |
| `has` | global, 1 argument | `has(e.f) -> bool` | on a map, whether the key `f` is present | selection on a value that is not a map: `FieldSelectionNotSupported` (3025) |
| `all` | receiver, 2 arguments | `list(A).all(A, bool) -> bool`; `map(A, B).all(A, bool) -> bool` | as `&&`: `false` absorbs the errors of other elements | the error of the predicate when the result is not defined; a range that is not a list or a map under `dyn`: `EvalInvalidComprehensionRange` (3026) |
| `exists` | receiver, 2 arguments | `list(A).exists(A, bool) -> bool`; `map(A, B).exists(A, bool) -> bool` | as `\|\|`: `true` absorbs the errors | the same |
| `exists_one` | receiver, 2 arguments | `list(A).exists_one(A, bool) -> bool`; `map(A, B).exists_one(A, bool) -> bool` | exactly one element gives `true`; no short circuit | any error of the predicate |
| `map` | receiver, 2 arguments | `list(A).map(A, T) -> list(T)`; `map(A, B).map(A, T) -> list(T)` | transforms each element, or each key of a map | any error of the transformation |
| `map` | receiver, 3 arguments | `list(A).map(A, bool, T) -> list(T)`; `map(A, B).map(A, bool, T) -> list(T)` | filters, then transforms | any error |
| `filter` | receiver, 2 arguments | `list(A).filter(A, bool) -> list(A)`; `map(A, B).filter(A, bool) -> list(A)` | the elements, or keys, for which the predicate is `true` | any error of the predicate |

The expansion is as in cel-go. The condition of the loop of `all` is `@not_strictly_false(accu)` and that of `exists` is `@not_strictly_false(!accu)`: the internal function `@not_strictly_false` returns a `bool` as it is and gives `true` for an error, so the loop goes on. The name cannot be written in a text, because `@` is not part of an identifier. The macro `existsOne` with two arguments is not part of the standard library, as in cel-go.

## Functions and operators

The position of an error is the call; for `_[_]` it is the `[` of the index operation.

### Logic

The overloads are not strict: the error of an operand does not stop the evaluation.

| Identifier | Function | Signature | Semantics | Errors |
| :- | :- | :- | :- | :- |
| `conditional` | `_?_:_` | `(bool, A, A) -> A` | only the chosen branch is evaluated | a condition that is not a `bool` under `dyn`: `EvalNoMatchingOverload` |
| `logical_and` | `_&&_` | `(bool, bool) -> bool` | commutative; `false` absorbs the error of the other operand | the error of an operand when the result is not defined |
| `logical_or` | `_\|\|_` | `(bool, bool) -> bool` | commutative; `true` absorbs the error | the same |
| `logical_not` | `!_` | `(bool) -> bool` | negation | none |
| `not_strictly_false` | `@not_strictly_false` | `(bool) -> bool` | the condition of the loop of `all` and `exists`, see Macros | none |

An operand of `&&` or `||` that is not a `bool` is the type checking error `TypeMismatch` (2002).

### Equality

Type checking requires equal types of the operands, except with `dyn`; `type(T1) == type(T2)` is always allowed. Evaluation compares values of different kinds without an error: `int`, `uint` and `double` lie on one number line, so `1 == 1.0` and `1u == 1`; values of other different types are not equal; a NaN is equal to nothing; lists are compared element by element and maps by keys and values.

| Identifier | Function | Signature | Semantics | Errors |
| :- | :- | :- | :- | :- |
| `equals` | `_==_` | `(A, A) -> bool` | equality of the language definition | none |
| `not_equals` | `_!=_` | `(A, A) -> bool` | `!(e1 == e2)` | none |

### Arithmetic

An overflow of `int` or `uint` is an error. `double` follows IEEE 754 and gives no errors: a division by zero gives an infinity or a NaN. There is no mixed arithmetic: `1 + 1u` is a type checking error.

| Identifier | Function | Signature | Semantics | Errors |
| :- | :- | :- | :- | :- |
| `add_int64` | `_+_` | `(int, int) -> int` | sum | `Overflow` (3006) |
| `add_uint64` | `_+_` | `(uint, uint) -> uint` | sum | `Overflow` |
| `add_double` | `_+_` | `(double, double) -> double` | IEEE 754 | none |
| `add_string` | `_+_` | `(string, string) -> string` | concatenation | none |
| `add_bytes` | `_+_` | `(bytes, bytes) -> bytes` | concatenation | none |
| `add_list` | `_+_` | `(list(A), list(A)) -> list(A)` | concatenation | none |
| `subtract_int64` | `_-_` | `(int, int) -> int` | difference | `Overflow` |
| `subtract_uint64` | `_-_` | `(uint, uint) -> uint` | difference | a negative result: `Overflow` |
| `subtract_double` | `_-_` | `(double, double) -> double` | IEEE 754 | none |
| `multiply_int64` | `_*_` | `(int, int) -> int` | product | `Overflow` |
| `multiply_uint64` | `_*_` | `(uint, uint) -> uint` | product | `Overflow` |
| `multiply_double` | `_*_` | `(double, double) -> double` | IEEE 754 | none |
| `divide_int64` | `_/_` | `(int, int) -> int` | integer division, truncated toward zero | divisor 0: `DivisionByZero` (3004); `-2^63 / -1`: `Overflow` |
| `divide_uint64` | `_/_` | `(uint, uint) -> uint` | integer division | divisor 0: `DivisionByZero` |
| `divide_double` | `_/_` | `(double, double) -> double` | IEEE 754 | none |
| `modulo_int64` | `_%_` | `(int, int) -> int` | remainder with the sign of the dividend | divisor 0: `ModulusByZero` (3005); `-2^63 % -1`: `Overflow` |
| `modulo_uint64` | `_%_` | `(uint, uint) -> uint` | remainder | divisor 0: `ModulusByZero` |
| `negate_int64` | `-_` | `(int) -> int` | change of sign | `-(-2^63)`: `Overflow` |
| `negate_double` | `-_` | `(double) -> double` | change of sign | none |

### Ordering

Ordering is defined for `bool`, `int`, `uint`, `double`, `string` and `bytes`. Strings are ordered by code points, not by UTF-16 code units, and `bytes` by bytes; `e1 <= e2` is `(e1 < e2) || (e1 == e2)`. A comparison with a NaN is `false`. The ordering of lists, maps and `null` has no overload: with `dyn` the error is `EvalNoMatchingOverload`.

The operators are `<`, `<=`, `>` and `>=`. Their identifiers are the prefix `less_`, `less_equals_`, `greater_` or `greater_equals_` and a suffix; the signature of each is `(T, T) -> bool`:

| Suffix | `T` | Candidate in type checking |
| :- | :- | :- |
| `bool`, `int64`, `uint64`, `double`, `string`, `bytes` | `bool`, `int`, `uint`, `double`, `string`, `bytes` | always |
| `int64_double`, `int64_uint64`, `uint64_double`, `uint64_int64`, `double_int64`, `double_uint64` | the two numeric types in the order of the suffix | only when an operand has the type `dyn` |

The 24 overloads across numeric types follow the language definition: "Comparisons require strict type equality at type-check time" and numeric comparisons across types are allowed at run time. So `1 < 1.5` is a type checking error, and `-1 < dyn(1u)` is `true`. The environment of cel-go also filters out these overloads unless it enables `CrossTypeNumericComparisons`, which the environments of the conformance suite do not.

### Index and `in`

| Identifier | Function | Signature | Semantics | Errors |
| :- | :- | :- | :- | :- |
| `index_list` | `_[_]` | `(list(A), int) -> A` | the element at the index, from zero. With `dyn` an index of the type `uint` and a `double` with an integral value are accepted | an index outside `[0, size)`: `IndexOutOfRange` (3003); a `double` with a fractional part, a NaN or an infinity: `InvalidArgument` (3008) |
| `index_map` | `_[_]` | `(map(A, B), A) -> B` | the value of the key. Numeric keys are compared by value: `{1: 'a'}[dyn(1.0)]` is `'a'` | no such key: `NoSuchKey` (3002) |
| `in_list` | `@in` | `(A, list(A)) -> bool` | whether the value is in the list, by the equality above | a right operand that is neither a list nor a map under `dyn`: `EvalNoMatchingOverload` |
| `in_map` | `@in` | `(A, map(A, B)) -> bool` | whether the key is in the map, by the equality of numbers | the same |

When an operand of an index is `dyn`, evaluation chooses `index_list` or `index_map` by the kind of the value, even if type checking kept one candidate: with `x` of the type `dyn` bound to `[7]`, `x[0u]` is `7`, though with a `uint` key type checking keeps only `index_map`; `[7][0u]` is a compilation error.

### `size`

The size of a string counts Unicode code points, a surrogate pair counting once; the size of `bytes` counts bytes; the size of a list or a map is the number of its elements or pairs. There are no errors.

| Identifier | Function | Signature |
| :- | :- | :- |
| `size_bytes`, `bytes_size` | `size` | `(bytes) -> int`, `bytes.size() -> int` |
| `size_list`, `list_size` | `size` | `(list(A)) -> int`, `list(A).size() -> int` |
| `size_map`, `map_size` | `size` | `(map(A, B)) -> int`, `map(A, B).size() -> int` |
| `size_string`, `string_size` | `size` | `(string) -> int`, `string.size() -> int` |

### Strings

| Identifier | Function | Signature | Semantics | Errors |
| :- | :- | :- | :- | :- |
| `contains_string` | `contains` | `string.contains(string) -> bool` | whether the receiver has the argument as a substring | none |
| `ends_with_string` | `endsWith` | `string.endsWith(string) -> bool` | whether the receiver ends with the argument | none |
| `starts_with_string` | `startsWith` | `string.startsWith(string) -> bool` | whether the receiver starts with the argument | none |

The search for a substring takes time linear in the lengths of the string and of the substring in the worst case, so the cost on the page [Cost](cost.md) holds for every input.

### Type, `dyn` and conversions

Numbers and strings are parsed and written by the code of the library, without the culture of the process.

| Identifier | Function | Signature | Semantics | Errors |
| :- | :- | :- | :- | :- |
| `type` | `type` | `(A) -> type(A)` | the type of the value; the parameters of types are erased | none |
| `to_dyn` | `dyn` | `(A) -> dyn` | the value itself; for type checking the type is `dyn` | none |
| `bool_to_bool`, `bytes_to_bytes`, `double_to_double`, `int64_to_int64`, `string_to_string`, `uint64_to_uint64` | `bool`, `bytes`, `double`, `int`, `string`, `uint` | `(T) -> T` | the identity | none |
| `string_to_bool` | `bool` | `(string) -> bool` | exactly twelve strings: `true` from `1`, `t`, `T`, `true`, `TRUE`, `True`, and `false` from `0`, `f`, `F`, `false`, `FALSE`, `False` | another string, such as `TrUe`, `tRUE` or `yes`: `InvalidArgument` |
| `string_to_bytes` | `bytes` | `(string) -> bytes` | the UTF-8 of the string | none |
| `int64_to_double`, `uint64_to_double` | `double` | `(int) -> double`, `(uint) -> double` | the nearest `double` | none |
| `string_to_double` | `double` | `(string) -> double` | the forms of Go `strconv.ParseFloat(s, 64)`, the whole string, as listed below | a string of another form: `InvalidArgument`; a value from half a step of the last digit above the largest `double` (`'1e400'`): `ConversionRange` (3007) |
| `double_to_int64` | `int` | `(double) -> int` | truncation toward zero | a NaN, an infinity and a value outside the open interval (−2⁶³, 2⁶³): `ConversionRange` |
| `uint64_to_int64` | `int` | `(uint) -> int` | the same value | above 2⁶³ − 1: `ConversionRange` |
| `string_to_int64` | `int` | `(string) -> int` | decimal: `[+-]?` and digits, the whole string; `'+5'` is `5`, `'-0'` is `0`. No `_`, spaces or prefixes: `'1_0'`, `' 5'` and `'0x10'` are errors | another form: `InvalidArgument`; outside `int`: `ConversionRange` |
| `double_to_uint64` | `uint` | `(double) -> uint` | truncation toward zero; the sign is tested before the truncation, so `uint(-0.0)` and `uint(0.5)` are `0u` | a NaN, an infinity, a negative value, `uint(-0.5)` included, and a value from 2⁶⁴: `ConversionRange` |
| `int64_to_uint64` | `uint` | `(int) -> uint` | the same value | a negative value: `ConversionRange` |
| `string_to_uint64` | `uint` | `(string) -> uint` | decimal digits without a sign, the whole string; `'+5'` and `'-0'` are errors; no `_`, spaces or prefixes | another form, a sign included: `InvalidArgument`; outside `uint`: `ConversionRange` |
| `bool_to_string` | `string` | `(bool) -> string` | `"true"` or `"false"` | none |
| `bytes_to_string` | `string` | `(bytes) -> string` | the strict decoding of UTF-8 | invalid UTF-8: `InvalidArgument` |
| `int64_to_string` | `string` | `(int) -> string` | decimal notation | none |
| `uint64_to_string` | `string` | `(uint) -> string` | decimal notation without a suffix: `string(9876u)` is `"9876"` | none |
| `double_to_string` | `string` | `(double) -> string` | the format of cel-go, Go `%g`: the shortest digits that read back to the same `double`; `string(-4.5e-3)` is `"-0.0045"`, `string(1e6)` is `"1e+06"`, `-0.0` is `"-0"`, and the infinities and the NaN are `"+Inf"`, `"-Inf"` and `"NaN"` | none |

The language definition shows `string(123u)` as `"123u"`; that contradicts its own signature, the test data of cel-spec and cel-go, so the result is `"123"`.

`string_to_double` takes, as a whole string and without spaces, commas, the prefixes `0b` and `0o` or digits outside ASCII:

1. a decimal form: an optional sign, digits with an optional point, or a point and digits, and an optional exponent `e` or `E` with an optional sign: `'1'`, `'5.'`, `'.5'`, `'+1.5'`, `'1E+2'`;
2. a hexadecimal form: an optional sign, `0x` or `0X`, hexadecimal digits with an optional point, and the binary exponent `p` or `P` with an optional sign, which is required: `'0x1p-2'` is `0.25`, `'0X1.8P1'` is `3`, and `'0x10'` is an error;
3. an underscore only between two digits, or between the prefix `0x` and a digit, in the mantissa and in the exponent: `'1_000.5'` and `'0x_1p0'` are accepted, `'_1'`, `'1_'`, `'1__0'`, `'1_.5'` and `'1._5'` are not;
4. `inf`, `infinity` with or without a sign, and `nan` without a sign, in any case: `'+Inf'`, `'-infinity'` and `'NaN'` are accepted, `'-nan'` and `'infinit'` are not.

The result is the nearest `double`, with ties to even; an exponent that underflows (`'1e-400'`) gives `0` or a subnormal number without an error.

## Errors of the standard library

| Stage | Members of `CelErrorCode` |
| :- | :- |
| Parse | `InvalidMacroCall` |
| Check | `UndeclaredReference`, `NoMatchingOverload`, `TypeMismatch`, `InvalidComprehensionRange` |
| Evaluate | `EvalNoMatchingOverload`, `NoSuchKey`, `IndexOutOfRange`, `DivisionByZero`, `ModulusByZero`, `Overflow`, `ConversionRange`, `InvalidArgument`, `Unbound`, `CostLimitExceeded`, `FieldSelectionNotSupported`, `EvalInvalidComprehensionRange` |
| Build | `StandardLibraryConflict`, `InvalidFunctionName` |

The text and the position of each error are on the page [Error codes](error-codes.md).
